// Fronts & Rivals: the Streamer. Every 0.5 s it checks the distance to each front you own:
//   under 250 m  -> its guards spawn at their posts with idle scenarios (nearest front first)
//   over 300 m   -> they're deleted (the 50 m gap stops flicker)
// Models load without blocking: request, check on later updates, give up after 5 s.
// A guard deleted by another mod is "not spawned", never dead, and comes back 10 s later.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

namespace FrontsAndRivals.Spawn
{
    public sealed class Streamer
    {
        /// <summary>Raised when a streamed guard ped dies in the world.</summary>
        public delegate void GuardDownHandler(GuardState guard, Ped ped, FrontDef front);

        private const float DoorReachedDistance = 2.5f;
        private const int LeaveTimeoutMs = 20000;
        private const float LeftoverScanRadius = 150f;

        private sealed class Slot
        {
            public int GuardId;          // guard the ped belongs to (0 = none)
            public Ped Ped;
            public Ped Body;             // a dead guard's body, removed when the front streams out
            public bool Pending;
            public Model PendingModel;
            public int PendingGuardId;
            public int PendingSince;
            public long RespawnAt;
            public bool Leaving;         // walking to the door to go inside (laying low)
            public long LeaveStart;
        }

        private sealed class StreamedFront
        {
            public FrontDef Def;
            public bool Streamed;
            public float Distance;
            public readonly LayLowTimer LayLow = new LayLowTimer();
            public readonly Slot[] Slots = new Slot[FrontDef.MaxPosts];

            public StreamedFront(FrontDef def)
            {
                Def = def;
                for (int i = 0; i < Slots.Length; i++) Slots[i] = new Slot();
            }
        }

        private readonly Registry _registry;
        private readonly ContentDb _content;
        private readonly Func<long> _activeClock;
        private readonly GuardDownHandler _onGuardDown;
        private readonly List<StreamedFront> _fronts = new List<StreamedFront>();
        private readonly List<StreamedFront> _order = new List<StreamedFront>(); // reused for nearest-first sorting
        private Empire _empire;

        public int StreamIns, StreamOuts, Leftovers, Vanished;

        public Streamer(Registry registry, ContentDb content, Func<long> activeClock, GuardDownHandler onGuardDown)
        {
            _registry = registry;
            _content = content;
            _activeClock = activeClock;
            _onGuardDown = onGuardDown;
        }

        public void Attach(Empire empire)
        {
            _empire = empire;
            Rebuild();
        }

        /// <summary>Re-reads which fronts are owned and scouted. Fronts that drop out are streamed out first.</summary>
        public void Rebuild()
        {
            if (_empire == null) return;
            // Stream out anything that's no longer eligible, keep the rest as they are.
            for (int i = _fronts.Count - 1; i >= 0; i--)
            {
                StreamedFront f = _fronts[i];
                FrontDef def = _content.Front(f.Def.Id);
                if (def == null || !def.IsScouted || !_empire.PlayerOwns(def.Id) || def != f.Def)
                {
                    StreamOut(f, false);
                    _fronts.RemoveAt(i);
                }
            }
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.IsScouted || !_empire.PlayerOwns(def.Id)) continue;
                bool known = false;
                for (int i = 0; i < _fronts.Count; i++) if (_fronts[i].Def == def) { known = true; break; }
                if (!known) _fronts.Add(new StreamedFront(def));
            }
        }

        /// <summary>Every 0.5 s while the pause gate is open.</summary>
        public void Update()
        {
            if (_empire == null || _fronts.Count == 0) return;
            Player player = Game.Player;
            Vector3 p = player.Character.Position;
            bool wanted = player.WantedLevel > 0;
            bool grey = wanted && Function.Call<bool>(Hash.ARE_PLAYER_STARS_GREYED_OUT, player);
            long now = _activeClock();
            int hour = GTA.World.CurrentTimeOfDay.Hours;

            // Nearest front first, so the budget goes where you are.
            _order.Clear();
            foreach (StreamedFront f in _fronts)
            {
                f.Distance = f.Def.Door.DistanceTo(p.X, p.Y, p.Z);
                int at = 0;
                while (at < _order.Count && _order[at].Distance <= f.Distance) at++;
                _order.Insert(at, f);
            }

            foreach (StreamedFront f in _order)
            {
                bool hidden = f.LayLow.Update(now, wanted, f.Distance < StreamRules.SpawnRange, grey);
                bool want = StreamRules.ShouldStream(f.Streamed, f.Distance) && !hidden;

                if (want && !f.Streamed)
                {
                    f.Streamed = true;
                    StreamIns++;
                }
                else if (!want && f.Streamed)
                {
                    StreamOut(f, hidden && f.Distance < StreamRules.DespawnRange);
                }

                if (f.Streamed) UpdateSlots(f, now, hour);
                UpdateLeaving(f, now);
            }
        }

        private void UpdateSlots(StreamedFront f, long now, int hour)
        {
            for (int post = 0; post < f.Def.Posts.Count && post < FrontDef.MaxPosts; post++)
            {
                Slot slot = f.Slots[post];
                GuardState g = _empire.AtPost(f.Def.Id, post);
                int wantId = (g != null && g.Status == GuardStatus.Active) ? g.Id : 0;

                if (slot.Ped != null)
                {
                    if (!slot.Ped.Exists())
                    {
                        // Deleted by someone else: not dead, just not spawned. Back in 10 s.
                        Log.Write("Streamer: guard " + slot.GuardId + " at " + f.Def.Id + " vanished, respawning in 10 s");
                        slot.Ped = null;
                        slot.RespawnAt = now + StreamRules.RespawnDelayMs;
                        Vanished++;
                    }
                    else if (slot.Ped.IsDead)
                    {
                        GuardState dead = _empire.Guard(slot.GuardId);
                        if (slot.Body != null) _registry.Despawn(slot.Body);
                        slot.Body = slot.Ped; // stays until the front streams out
                        slot.Ped = null;
                        slot.GuardId = 0;
                        if (dead != null && dead.Status == GuardStatus.Active && _onGuardDown != null)
                            _onGuardDown(dead, slot.Body, f.Def);
                        continue; // the post is re-read next update
                    }
                    else if (slot.GuardId != wantId)
                    {
                        _registry.Despawn(slot.Ped); // reassigned, injured or sent to the reserve
                        slot.Ped = null;
                    }
                }

                if (slot.Ped != null || wantId == 0)
                {
                    CancelPending(slot);
                    continue;
                }
                if (now < slot.RespawnAt) continue;

                if (!slot.Pending)
                {
                    var model = new Model(g.Model);
                    if (!model.IsValid || !model.IsPed)
                    {
                        Log.Write("Streamer: bad ped model '" + g.Model + "' for guard " + g.Id + ", retrying in 10 s");
                        slot.RespawnAt = now + StreamRules.RespawnDelayMs;
                        continue;
                    }
                    model.Request();
                    slot.Pending = true;
                    slot.PendingModel = model;
                    slot.PendingGuardId = wantId;
                    slot.PendingSince = Game.GameTime;
                }
                else if (slot.PendingGuardId != wantId)
                {
                    CancelPending(slot);
                }
                else if (slot.PendingModel.IsLoaded)
                {
                    if (!_registry.CanSpawnPed) continue; // budget full, try next update
                    try { SpawnGuard(slot, g, f.Def.Posts[post], hour); }
                    finally { CancelPending(slot); }
                }
                else if (Game.GameTime - slot.PendingSince > StreamRules.ModelTimeoutMs)
                {
                    Log.Write("Streamer: model " + g.Model + " didn't load in 5 s, retrying in 10 s");
                    CancelPending(slot);
                    slot.RespawnAt = now + StreamRules.RespawnDelayMs;
                }
            }
        }

        private void SpawnGuard(Slot slot, GuardState g, PostDef post, int hour)
        {
            var pos = new Vector3(post.X, post.Y, post.Z);
            Ped ped = GTA.World.CreatePed(slot.PendingModel, pos, post.H);
            if (ped == null) return;

            ped.RelationshipGroup = Factions.Guards;
            _registry.Track(ped);
            slot.Ped = ped;
            slot.GuardId = g.Id;

            ped.Armor = 0;
            ped.Weapons.Give(WeaponFor(g.Weapon), 250, false, true);
            // Idle guards ignore stray events so a bump or a car horn doesn't send them running.
            // Switched off when an attack starts (milestone 4/5) so they can fight.
            ped.BlockPermanentEvents = true;
            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, false);
            Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);

            string scenario = StreamRules.GuardScenario(post.Scenario, (Trait)g.Trait, hour);
            // NativeDB: ped, scenario, x, y, z, heading, timeToLeave (<0 = idle forever), playIntro, teleport
            Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, ped, scenario, post.X, post.Y, post.Z, post.H, -1, false, true);
        }

        private static WeaponHash WeaponFor(int weapon)
        {
            switch ((Rules.Weapon)weapon)
            {
                case Rules.Weapon.Smg: return WeaponHash.SMG;
                case Rules.Weapon.Rifle: return WeaponHash.CarbineRifle;
                default: return WeaponHash.Pistol;
            }
        }

        /// <summary>Deletes a front's peds. When laying low, guards on screen walk to the door first.</summary>
        private void StreamOut(StreamedFront f, bool layLow)
        {
            Vector3 door = new Vector3(f.Def.Door.X, f.Def.Door.Y, f.Def.Door.Z);
            long now = _activeClock();
            bool anyLeaving = false;
            for (int i = 0; i < f.Slots.Length; i++)
            {
                Slot s = f.Slots[i];
                CancelPending(s);
                if (s.Body != null) { _registry.Despawn(s.Body); s.Body = null; }
                if (s.Ped == null) continue;
                if (layLow && s.Ped.Exists() && !s.Ped.IsDead && s.Ped.IsOnScreen)
                {
                    s.Ped.Task.ClearAll();
                    s.Ped.Task.GoTo(door, -1);
                    s.Leaving = true;
                    s.LeaveStart = now;
                    anyLeaving = true;
                }
                else
                {
                    _registry.Despawn(s.Ped);
                    s.Ped = null;
                    s.GuardId = 0;
                }
            }
            if (f.Streamed) StreamOuts++;
            f.Streamed = false;
            if (!anyLeaving) CheckLeftovers(door);
        }

        private void UpdateLeaving(StreamedFront f, long now)
        {
            Vector3 door = new Vector3(f.Def.Door.X, f.Def.Door.Y, f.Def.Door.Z);
            for (int i = 0; i < f.Slots.Length; i++)
            {
                Slot s = f.Slots[i];
                if (!s.Leaving) continue;
                bool done = s.Ped == null || !s.Ped.Exists() || s.Ped.IsDead || !s.Ped.IsOnScreen
                         || s.Ped.Position.DistanceTo(door) < DoorReachedDistance || now - s.LeaveStart > LeaveTimeoutMs;
                if (!done) continue;
                if (s.Ped != null) _registry.Despawn(s.Ped);
                s.Ped = null;
                s.GuardId = 0;
                s.Leaving = false;
            }
        }

        /// <summary>The 50-drive-by test: after a front streams out, no FR_ ped may remain near it.</summary>
        private void CheckLeftovers(Vector3 door)
        {
            foreach (Ped p in GTA.World.GetNearbyPeds(door, LeftoverScanRadius))
            {
                if (p == null || !p.Exists() || p.IsPlayer) continue;
                if (!Markers.IsModGroup(p.RelationshipGroup.Hash) || _registry.Owns(p)) continue;
                Leftovers++;
                Log.Write("Streamer: LEFTOVER FR_ ped " + p.Handle + " near " + door + ", deleting it");
                p.Delete();
            }
        }

        private static void CancelPending(Slot s)
        {
            if (!s.Pending) return;
            s.PendingModel.MarkAsNoLongerNeeded();
            s.Pending = false;
            s.PendingGuardId = 0;
        }

        /// <summary>After Registry.DeleteAll (mission start, switch): everything is gone, forget the handles.</summary>
        public void Reset()
        {
            foreach (StreamedFront f in _fronts)
            {
                for (int i = 0; i < f.Slots.Length; i++)
                {
                    Slot s = f.Slots[i];
                    CancelPending(s);
                    s.Ped = null; s.Body = null; s.GuardId = 0; s.Leaving = false; s.RespawnAt = 0;
                }
                f.Streamed = false;
            }
        }

        /// <summary>Streams every front out now (dev tools, content reload).</summary>
        public void DespawnAll()
        {
            foreach (StreamedFront f in _fronts) StreamOut(f, false);
        }

        public int StreamedCount
        {
            get { int n = 0; foreach (StreamedFront f in _fronts) if (f.Streamed) n++; return n; }
        }
    }
}
