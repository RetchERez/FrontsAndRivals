// Fronts & Rivals: the Streamer. Every 0.5 s it checks the distance to each scouted front:
//   under 250 m  -> its guards spawn at their posts with idle scenarios (nearest front first)
//   over 300 m   -> they're deleted (the 50 m gap stops flicker)
// Your fronts get your guards. Rival-held fronts get the crew's own soldiers, who watch you, warn you off, then fight (WatchRules);
// kill them all and the front can be claimed at its door (FrontAssault).
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
        public delegate void GuardDownHandler(GuardState guard, Ped ped, FrontDef front);
        public delegate void CrewGuardDownHandler(FrontDef front, string crewId, Ped ped, bool lastOne, bool byYou);

        private const float DoorReachedDistance = 2.5f;
        private const int LeaveTimeoutMs = 20000;
        private const float LeftoverScanRadius = 150f;

        private sealed class Slot
        {
            public int GuardId;          // your guard's id, or -1 for a crew soldier, 0 for none
            public int Weapon = -1;      // the Rules.Weapon this ped was handed
            public Ped Ped;
            public Ped Body;             // a dead body, removed when the front streams out
            public bool Pending;
            public Model PendingModel;
            public int PendingGuardId;
            public int PendingSince;
            public long RespawnAt;
            public bool Leaving;
            public long LeaveStart;
            public bool Killed;          // crew soldier killed this visit: stays dead until the front streams out
            public int PatrolAt = -1;    // patrol point this person is heading to or standing at
            public bool PatrolWalking;
            public int PatrolSince, PatrolNext; // game time
            public string Colour;        // the crew colour this guard was dressed in
        }

        private sealed class StreamedFront
        {
            public FrontDef Def;
            public string Owner;         // Owners.Player or a crew id
            public bool Streamed;
            public float Distance;
            public readonly LayLowTimer LayLow = new LayLowTimer();
            public readonly Slot[] Slots = new Slot[FrontDef.MaxCrew]; // guards use the first posts; a crew can hold more
            public bool CrewSpawnedThisVisit;
            public bool ClearedReported;
            public bool Alerted;          // crew soldiers have noticed you and are fighting
            public Watch Watch;           // calm, watching you, warning you, or fighting (WatchRules)
            public int WarnStart;         // game time the current warning began
            public bool LastWarned;       // "last warning" shown for this warning
            public int LastWatchAt;       // game time of the last WatchTick (a long gap restarts a warning's fuse)
            public int CrewCount = -1;    // soldiers this visit, fixed when the front streams in
            public List<Vector3> Patrol;  // walkable points around the building, found once per visit
            public bool PatrolTried;
            public bool BackupCalled;     // a car kill this visit: backup is on its way
            public int BackupAt;          // game time the backup car is sent (0 = not pending)
            public readonly List<Ped> Backup = new List<Ped>();
            public readonly HashSet<int> BackupCounted = new HashSet<int>();
            public Vehicle BackupCar;
            public Model BackupCarModel;
            public bool BackupOut;        // they've got out and joined the fight
            public bool KilledByYou;      // you (on foot, in a car) or your people dropped at least one soldier this visit

            public StreamedFront(FrontDef def, string owner)
            {
                Def = def;
                Owner = owner;
                for (int i = 0; i < Slots.Length; i++) Slots[i] = new Slot();
            }

            public bool IsCrew { get { return Owner != Owners.Player; } }
        }

        private readonly Registry _registry;
        private readonly ContentDb _content;
        private readonly Func<long> _activeClock;
        private readonly GuardDownHandler _onGuardDown;
        private readonly List<StreamedFront> _fronts = new List<StreamedFront>();
        private readonly List<StreamedFront> _order = new List<StreamedFront>();
        private Empire _empire;
        private bool _showRivals = true;

        public int StreamIns, StreamOuts, Leftovers, Vanished;

        /// <summary>A front kept streamed no matter the distance (a live defense is running there).</summary>
        public string LiveFrontId;
        /// <summary>Raised when a rival crew's soldier at one of their fronts dies.</summary>
        public CrewGuardDownHandler OnCrewGuardDown;

        public Streamer(Registry registry, ContentDb content, Func<long> activeClock, GuardDownHandler onGuardDown)
        {
            _registry = registry;
            _content = content;
            _activeClock = activeClock;
            _onGuardDown = onGuardDown;
        }

        public void Attach(Empire empire, bool showRivalFronts)
        {
            _empire = empire;
            _showRivals = showRivalFronts;
            Rebuild();
        }

        /// <summary>Re-reads ownership. Fronts that changed hands are streamed out and come back with the new owner's people.</summary>
        public void Rebuild()
        {
            if (_empire == null) return;
            for (int i = _fronts.Count - 1; i >= 0; i--)
            {
                StreamedFront f = _fronts[i];
                FrontDef def = _content.Front(f.Def.Id);
                string owner = def != null ? _empire.OwnerOf(def.Id) : null;
                if (def == null || !def.IsScouted || def != f.Def || owner != f.Owner || !Wanted(owner))
                {
                    StreamOut(f, false);
                    _fronts.RemoveAt(i);
                }
            }
            foreach (FrontDef def in _content.Fronts)
            {
                string owner = _empire.OwnerOf(def.Id);
                if (!def.IsScouted || !Wanted(owner)) continue;
                bool known = false;
                for (int i = 0; i < _fronts.Count; i++) if (_fronts[i].Def == def) { known = true; break; }
                if (!known) _fronts.Add(new StreamedFront(def, owner));
            }
        }

        private static bool Wanted(string owner)
        {
            // Your fronts and crew fronts stream; fronts for sale stand empty.
            return owner == Owners.Player || owner == Owners.CrownKings || owner == Owners.Ironside || owner == Owners.Velvet;
        }

        /// <summary>Your living guard peds at a front, for the live defense.</summary>
        public List<KeyValuePair<GuardState, Ped>> GuardPedsAt(string frontId)
        {
            var list = new List<KeyValuePair<GuardState, Ped>>();
            foreach (StreamedFront f in _fronts)
            {
                if (f.Def.Id != frontId || f.IsCrew) continue;
                foreach (Slot s in f.Slots)
                {
                    if (s.Ped == null || !s.Ped.Exists() || s.Ped.IsDead || s.GuardId <= 0) continue;
                    GuardState g = _empire.Guard(s.GuardId);
                    if (g != null) list.Add(new KeyValuePair<GuardState, Ped>(g, s.Ped));
                }
            }
            return list;
        }

        /// <summary>The guard a ped belongs to, or null (kill credit in live defenses).</summary>
        public GuardState GuardForPed(Ped ped)
        {
            if (ped == null) return null;
            foreach (StreamedFront f in _fronts)
                foreach (Slot s in f.Slots)
                    if (s.Ped == ped && s.GuardId > 0) return _empire.Guard(s.GuardId);
            return null;
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
                bool live = f.Def.Id == LiveFrontId;
                bool hidden = !live && !f.IsCrew && f.LayLow.Update(now, wanted, f.Distance < StreamRules.SpawnRange, grey);
                bool want = live || (StreamRules.ShouldStream(f.Streamed, f.Distance) && !hidden);

                if (want && !f.Streamed)
                {
                    f.Streamed = true;
                    f.CrewSpawnedThisVisit = false;
                    f.ClearedReported = false;
                    f.Alerted = false;
                    f.Watch = Watch.Calm;
                    f.CrewCount = -1;
                    f.Patrol = null;
                    f.PatrolTried = false;
                    f.BackupCalled = false;
                    f.BackupAt = 0;
                    f.BackupOut = false;
                    f.Backup.Clear();
                    f.BackupCounted.Clear();
                    f.BackupCar = null;
                    f.KilledByYou = false;
                    for (int i = 0; i < f.Slots.Length; i++) f.Slots[i].PatrolAt = -1;
                    StreamIns++;
                }
                else if (!want && f.Streamed)
                {
                    StreamOut(f, hidden && f.Distance < StreamRules.DespawnRange);
                }

                if (f.Streamed)
                {
                    if (f.IsCrew) UpdateCrewSlots(f, hour);
                    else UpdateSlots(f, now, hour);
                    try { PatrolTick(f); } catch (Exception ex) { Log.Error("Patrol " + f.Def.Id, ex); }
                }
                UpdateLeaving(f, now);
            }
        }

        // ---------------- patrols ----------------

        /// <summary>Every second person at a front walks a loop around the building (sides and back where there's
        /// ground to walk), stopping at each point to stand guard, smoke or take a call, while the rest hold the door.
        /// Stops the moment there's a fight; the usual calm-down puts everyone back afterwards.</summary>
        private void PatrolTick(StreamedFront f)
        {
            if (f.Def.Id == LiveFrontId || (f.IsCrew && (f.Alerted || f.Watch != Watch.Calm)) || f.Distance > StreamRules.SpawnRange) return;
            if (f.Patrol == null)
            {
                if (f.PatrolTried) return;
                f.PatrolTried = true;
                f.Patrol = PatrolPoints(f.Def);
                Log.Write("Patrol: " + f.Def.Id + " has " + f.Patrol.Count + " points");
            }
            if (f.Patrol.Count < 2) return;
            int count = 0;
            for (int i = 0; i < f.Slots.Length; i++) if (f.Slots[i].Ped != null) count = i + 1;
            int gt = Game.GameTime;
            for (int i = 0; i < count; i++)
            {
                Slot s = f.Slots[i];
                Ped p = s.Ped;
                if (!StreamRules.Patrols(i, count) || p == null || !p.Exists() || p.IsDead || p.IsInCombat || p.IsInVehicle()) continue;
                int pick = Markers.Hash(f.Def.Id) + i * 31;
                if (s.PatrolAt < 0)
                {
                    s.PatrolAt = (i * 3) % f.Patrol.Count;
                    WalkTo(s, p, f.Patrol[s.PatrolAt], gt);
                    continue;
                }
                Vector3 target = f.Patrol[s.PatrolAt];
                if (s.PatrolWalking)
                {
                    if (p.Position.DistanceTo(target) < 1.8f || gt - s.PatrolSince > 45000)
                    {
                        s.PatrolWalking = false;
                        float heading = (float)(Math.Atan2(-(f.Def.Door.X - target.X), f.Def.Door.Y - target.Y) * 180.0 / Math.PI); // face back towards the front
                        p.Task.StartScenario(StreamRules.PatrolScenario(pick + s.PatrolAt), p.Position, heading);
                        s.PatrolNext = gt + StreamRules.PatrolDwellMs(pick + s.PatrolAt);
                    }
                }
                else if (gt >= s.PatrolNext)
                {
                    s.PatrolAt = (s.PatrolAt + 1) % f.Patrol.Count;
                    WalkTo(s, p, f.Patrol[s.PatrolAt], gt);
                }
            }
        }

        private static void WalkTo(Slot s, Ped p, Vector3 point, int gt)
        {
            p.Task.ClearAll();
            p.Task.GoTo(point, -1); // a walk, along the navmesh
            s.PatrolWalking = true;
            s.PatrolSince = gt;
        }

        /// <summary>Walkable points 10-28 m from the door all the way round (the sides and back of the building where the
        /// navmesh reaches), off the road, at about the door's height, at least 6 m apart, sorted into a loop.</summary>
        private static List<Vector3> PatrolPoints(FrontDef def)
        {
            var door = new Vector3(def.Door.X, def.Door.Y, def.Door.Z);
            var found = new List<Vector3>();
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? 12f : 22f;
                for (int k = 0; k < 8; k++)
                {
                    double a = k * Math.PI / 4.0 + ring * Math.PI / 8.0;
                    var want = door + new Vector3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, 0f);
                    Vector3 c = GTA.World.GetSafeCoordForPed(want, false, 4 | 8); // never inside a building or in water
                    if (c == Vector3.Zero) continue;
                    float d = c.DistanceTo(door);
                    if (c.DistanceTo(want) > 8f || Math.Abs(c.Z - door.Z) > 4f || d < 6f || d > 30f) continue;
                    if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD, c.X, c.Y, c.Z, 0)) continue; // never patrol the street
                    bool close = false;
                    foreach (Vector3 q in found) if (q.DistanceTo(c) < 6f) { close = true; break; }
                    if (!close) found.Add(c);
                }
            }
            var xs = new float[found.Count];
            var ys = new float[found.Count];
            for (int i = 0; i < found.Count; i++) { xs[i] = found[i].X - door.X; ys[i] = found[i].Y - door.Y; }
            var loop = new List<Vector3>();
            foreach (int i in StreamRules.LoopOrder(xs, ys)) loop.Add(found[i]);
            return loop;
        }

        // ---------------- your fronts ----------------

        private void UpdateSlots(StreamedFront f, long now, int hour)
        {
            for (int post = 0; post < f.Def.PlacedPosts; post++)
            {
                Slot slot = f.Slots[post];
                GuardState g = _empire.AtPost(f.Def.Id, post);
                int wantId = (g != null && g.Status == GuardStatus.Active && !g.Escort && !_empire.OnRaid(g)) ? g.Id : 0; // with you, or out raiding

                if (slot.Ped != null)
                {
                    if (!slot.Ped.Exists())
                    {
                        Log.Write("Streamer: guard " + slot.GuardId + " at " + f.Def.Id + " vanished, respawning in 10 s");
                        slot.Ped = null;
                        slot.RespawnAt = now + StreamRules.RespawnDelayMs;
                        Vanished++;
                    }
                    else if (slot.Ped.IsDead)
                    {
                        GuardState dead = _empire.Guard(slot.GuardId);
                        if (slot.Body != null) _registry.Despawn(slot.Body);
                        slot.Body = slot.Ped;
                        slot.Ped = null;
                        slot.GuardId = 0;
                        if (dead != null && dead.Status == GuardStatus.Active && _onGuardDown != null) _onGuardDown(dead, slot.Body, f.Def);
                        continue;
                    }
                    else if (slot.GuardId != wantId)
                    {
                        _registry.Despawn(slot.Ped);
                        slot.Ped = null;
                    }
                    else if (g != null && f.Def.Id != LiveFrontId && !slot.Ped.IsInCombat
                             && (slot.Ped.Health < slot.Ped.MaxHealth || slot.Ped.Armor < (g.Vest ? DefenseRating.VestArmor : 0)))
                    {
                        Restore(slot.Ped, g); // calm again: back to full, vest topped up
                    }
                    else if (g != null && Wardrobe.Enabled && slot.Colour != Wardrobe.PlayerColour() + "/" + Wardrobe.PlayerStyle())
                    {
                        Wardrobe.Apply(slot.Ped, Wardrobe.ForGuard(g)); // new crew colour or style: changes on the spot
                        slot.Colour = Wardrobe.PlayerColour() + "/" + Wardrobe.PlayerStyle();
                    }
                    else if (g != null && slot.Weapon != g.Weapon)
                    {
                        // A new gun was bought in the Office: hand it over on the spot, no respawn.
                        slot.Ped.Weapons.RemoveAll();
                        slot.Ped.Weapons.Give(WeaponFor(g.Weapon), 250, f.Def.Id == LiveFrontId, true);
                        slot.Weapon = g.Weapon;
                    }
                }

                if (slot.Ped != null || wantId == 0) { CancelPending(slot); continue; }
                if (now < slot.RespawnAt) continue;
                Dressing gd = Wardrobe.ForGuard(g);
                Load(slot, gd != null ? Wardrobe.ModelName(gd) : g.Model, wantId, now, () =>
                {
                    SpawnGuard(slot, g, f.Def.Posts[post], hour, f.Def.Id == LiveFrontId);
                    if (slot.Ped != null && gd != null) { Wardrobe.Apply(slot.Ped, gd); slot.Colour = Wardrobe.PlayerColour() + "/" + Wardrobe.PlayerStyle(); }
                });
            }
        }

        private void SpawnGuard(Slot slot, GuardState g, PostDef post, int hour, bool fighting)
        {
            Ped ped = GTA.World.CreatePed(slot.PendingModel, new Vector3(post.X, post.Y, post.Z), post.H);
            if (ped == null) return;
            ped.RelationshipGroup = Factions.Guards;
            _registry.Track(ped);
            slot.Ped = ped;
            slot.GuardId = g.Id;
            ped.Armor = g.Vest ? DefenseRating.VestArmor : 0; // body armour bought in the Office
            ped.Weapons.Give(WeaponFor(g.Weapon), 250, fighting, true);
            slot.Weapon = g.Weapon;
            // Skills show in a fight: Shooting is their aim, Strength how much they can take.
            ped.Accuracy = Rules.Stats.Accuracy(g.Shooting);
            ped.MaxHealth = Rules.Stats.Health(g.Strength);
            ped.Health = ped.MaxHealth;
            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, false);
            Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
            if (fighting)
            {
                ped.BlockPermanentEvents = false;
                ped.Task.FightAgainstHatedTargets(60f);
                return;
            }
            // Idle guards ignore stray events so a bump or a car horn doesn't send them running.
            ped.BlockPermanentEvents = true;
            string scenario = StreamRules.GuardScenario(post.Scenario, (Trait)g.Trait, hour);
            Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, ped, scenario, post.X, post.Y, post.Z, post.H, -1, false, true);
        }

        public static WeaponHash WeaponFor(int weapon)
        {
            switch ((Rules.Weapon)weapon)
            {
                case Rules.Weapon.Knife: return WeaponHash.Knife;
                case Rules.Weapon.Pistol: return WeaponHash.Pistol;
                case Rules.Weapon.Shotgun: return WeaponHash.PumpShotgun;
                case Rules.Weapon.Smg: return WeaponHash.SMG;
                default: return WeaponHash.CarbineRifle;
            }
        }

        // ---------------- rival crews' fronts ----------------

        /// <summary>A crew front holds min(posts, 2 + tier) soldiers while the crew has manpower; killed ones stay dead
        /// until you leave. Weakened fronts (after a failed attack on you) hold one fewer.</summary>
        private void UpdateCrewSlots(StreamedFront f, int hour)
        {
            CrewDef crew = _content.Crew(f.Owner);
            CrewState cs = Nemesis.Crew(_empire.State, f.Owner);
            if (crew == null || cs == null) return;
            // Fixed for the whole visit, so kills lowering manpower never stop the front's soldiers being tracked.
            if (f.CrewCount < 0)
            {
                FrontState fs = _empire.Find(f.Def.Id);
                int c = f.Def.Posts != null && f.Def.Posts.Count > 0 ? Raids.Soldiers(f.Def, fs, cs, _empire.State.Day) : 0;
                f.CrewCount = Math.Min(c, FrontDef.MaxCrew); // Raids.Soldiers already counts the crew's people, above the skeleton guard
            }
            int count = f.CrewCount;
            if (count == 0) return; // only a front with no posts: every crew keeps a skeleton guard, so never a free front

            int alive = 0, killed = 0;
            for (int post = 0; post < count; post++)
            {
                Slot slot = f.Slots[post];
                if (slot.Ped != null)
                {
                    if (!slot.Ped.Exists()) { slot.Ped = null; continue; } // deleted elsewhere: no credit, respawns next visit
                    if (slot.Ped.IsDead)
                    {
                        Ped body = slot.Ped;
                        bool byCar = false;
                        try { byCar = StreamRules.VehicleKill((int)body.CauseOfDeath, body.Killer is Vehicle); } catch { }
                        WatchFx.DropBlip(body); // no crew dot left on a body
                        if (ByYou(body)) f.KilledByYou = true;
                        if (slot.Body != null) _registry.Despawn(slot.Body);
                        slot.Body = body;
                        slot.Ped = null;
                        if (OnCrewGuardDown != null) OnCrewGuardDown(f.Def, f.Owner, slot.Body, false, false);
                        slot.Killed = true;
                        if (byCar)
                        {
                            // A car kill counts, but the whole front opens up on you and calls for backup (once a visit).
                            if (!f.Alerted) Alert(f, count);
                            if (!f.BackupCalled) CallBackup(f, crew, cs);
                        }
                        continue;
                    }
                    alive++;
                    continue;
                }
                if (slot.Killed) { killed++; continue; }
                int pick = Markers.Hash(f.Def.Id) + post * 7;
                Dressing dress = Wardrobe.ForCrew(f.Owner, pick); // in the crew's colour
                string model = dress != null ? Wardrobe.ModelName(dress) : crew.Models[Math.Abs(f.Def.Id.GetHashCode() + post * 7) % crew.Models.Count];
                int tier = f.Def.Tier, grudge = cs.Grudge + Heat.ExtraGrudge(_empire.State.Heat); // on alert: heavier guns
                int index = post;
                bool starter = f.Def.Starter;
                StreamedFront front = f;
                Load(slot, model, -1, 0, () =>
                {
                    SpawnCrewGuard(slot, crew, CrewSpot(front.Def, index), hour, pick, tier, grudge, starter);
                    if (slot.Ped != null) Wardrobe.Apply(slot.Ped, dress);
                    if (front.Alerted && slot.Ped != null) { slot.Ped.BlockPermanentEvents = false; slot.Ped.Task.FightAgainstHatedTargets(60f); }
                    else if (front.Watch == Watch.Warning && slot.Ped != null) { WatchFx.Raise(slot.Ped); WatchFx.Blip(_content, slot.Ped, front.Owner); } // joins the warning
                    else if (front.Watch == Watch.Watching && slot.Ped != null) WatchFx.Eye(slot.Ped); // joins in keeping an eye on you
                });
            }
            if (alive > 0 || killed > 0) f.CrewSpawnedThisVisit = true;
            if (!f.Alerted && alive > 0)
            {
                try { WatchTick(f, count); }
                catch (Exception ex) { Log.Error("Watch " + f.Def.Id, ex); if (FallbackProvoked(f, count)) Alert(f, count); }
            }
            try { UpdateBackup(f, crew, cs); } catch (Exception ex) { Log.Error("Backup " + f.Def.Id, ex); f.BackupAt = 0; }
            if (f.CrewSpawnedThisVisit && alive == 0 && killed > 0 && !f.ClearedReported && BackupDone(f))
            {
                // Wait for a soldier who's still on the way in, but not for one who can't appear (a model that won't
                // load, or the people budget full): after 10 s they don't count, so the front can always be cleared.
                bool anyPending = false;
                for (int post = 0; post < count; post++)
                {
                    Slot s = f.Slots[post];
                    if (s.Pending && Game.GameTime - s.PendingSince < StuckSpawnMs) anyPending = true;
                }
                if (!anyPending)
                {
                    f.ClearedReported = true;
                    for (int post = 0; post < count; post++) CancelPending(f.Slots[post]);
                    if (OnCrewGuardDown != null) OnCrewGuardDown(f.Def, f.Owner, null, true, f.KilledByYou);
                }
            }
        }

        private const int StuckSpawnMs = 10000;

        /// <summary>Killed by you on foot, by your car, or by one of your own people.</summary>
        private static bool ByYou(Ped body)
        {
            try
            {
                Ped me = Game.Player.Character;
                Entity killer = body.Killer;
                Ped killerPed = killer as Ped;
                return killer == me || (killer != null && (killer == me.CurrentVehicle || killer == me.LastVehicle))
                       || (killerPed != null && killerPed.RelationshipGroup == Factions.Guards);
            }
            catch { return false; }
        }

        // ---------------- backup: a crew car answering the call after a car kill ----------------

        private void CallBackup(StreamedFront f, CrewDef crew, CrewState cs)
        {
            f.BackupCalled = true;
            if (crew == null || cs == null || cs.Manpower < StreamRules.BackupSize) return; // nobody left to send
            List<string> rides = crew.RidesFor(CrewDef.TierFor(cs.Grudge, _empire.State.Heat));
            if (rides == null || rides.Count == 0) return;
            f.BackupCarModel = new Model(rides[0]);
            f.BackupCarModel.Request();
            if (crew.Models != null) foreach (string m in crew.Models) new Model(m).Request(); // fallback looks, if outfits are missing
            f.BackupAt = Game.GameTime + StreamRules.BackupDelayMs;
            GTA.UI.Screen.ShowSubtitle("They've called for backup.", 4000);
            Log.Write("Streamer: " + f.Def.Id + " called for backup after a car kill");
        }

        private void UpdateBackup(StreamedFront f, CrewDef crew, CrewState cs)
        {
            var door = new Vector3(f.Def.Door.X, f.Def.Door.Y, f.Def.Door.Z);
            if (f.BackupAt > 0 && Game.GameTime >= f.BackupAt)
            {
                // Keep trying for up to 20 s (a model loading, the ped budget full, no road spot yet); only then give up.
                bool late = Game.GameTime - f.BackupAt > 20000;
                if (!f.BackupCarModel.IsLoaded || !_registry.CanSpawnVehicle || _registry.PedCount + StreamRules.BackupSize > Registry.MaxPeds)
                {
                    if (late) f.BackupAt = 0;
                    return;
                }
                double a = (Markers.Hash(f.Def.Id) + Game.GameTime / 1000) % 360 * Math.PI / 180.0; // a different side each try
                Vector3 from = GTA.World.GetNextPositionOnStreet(door + new Vector3((float)Math.Cos(a) * 90f, (float)Math.Sin(a) * 90f, 0f), true);
                if (from == Vector3.Zero) { if (late) f.BackupAt = 0; return; }
                Vehicle car = GTA.World.CreateVehicle(f.BackupCarModel, from, 0f);
                if (car == null) { if (late) f.BackupAt = 0; return; }
                f.BackupAt = 0;
                _registry.Track(car, Markers.MakePlate(f.Def.Number, crew.Id));
                CrewColor paint = CrewColors.Get(CrewColors.ForCrew(crew.Id));
                car.Mods.CustomPrimaryColor = System.Drawing.Color.FromArgb(paint.R, paint.G, paint.B);
                car.Mods.CustomSecondaryColor = System.Drawing.Color.FromArgb(paint.R / 3, paint.G / 3, paint.B / 3);
                car.MaxHealth = 2000; car.Health = 2000; car.BodyHealth = 1000f; car.EngineHealth = 1000f; car.PetrolTankHealth = 1000f; Function.Call(Hash.SET_VEHICLE_EXPLODES_ON_HIGH_EXPLOSION_DAMAGE, car, false);
                car.PlaceOnGround();
                f.BackupCar = car;
                int seats = Math.Min(StreamRules.BackupSize, car.PassengerCapacity + 1);
                for (int i = 0; i < seats; i++)
                {
                    int pick = Markers.Hash(f.Def.Id) + 500 + i * 7;
                    Dressing dress = Wardrobe.ForCrew(crew.Id, pick);
                    Model m = dress != null ? Wardrobe.ModelOf(dress) : new Model(crew.Models[i % crew.Models.Count]);
                    if (!m.IsLoaded) { m.Request(); continue; } // the two character models are kept loaded, so this is rare
                    Ped ped = GTA.World.CreatePed(m, from + new Vector3(0f, 0f, 2f), 0f);
                    if (ped == null) continue;
                    ped.RelationshipGroup = Factions.CrewGroup(crew.Id);
                    _registry.Track(ped);
                    Wardrobe.Apply(ped, dress);
                    ped.SetIntoVehicle(car, i == 0 ? VehicleSeat.Driver : VehicleSeat.Passenger);
                    ped.Weapons.Give(CrewWeapon(crew.WeaponFor(pick, f.Def.Tier, cs != null ? cs.Grudge : 0)), 250, true, true);
                    ped.Accuracy = CrewDef.FrontAccuracy(f.Def.Tier) + Heat.CrewAccuracy(_empire.State.Heat) - 25;
                    ped.Armor = Math.Min(100, CrewDef.FrontArmor(f.Def.Tier) + Heat.ExtraArmor(_empire.State.Heat));
                    ped.MaxHealth = Rules.Stats.CrewHealth;
                    ped.Health = ped.MaxHealth;
                    ped.BlockPermanentEvents = true; // drive in first, then fight
                    if (i == 0) ped.Task.DriveTo(car, door, 15f, 25f, DrivingStyle.Rushed);
                    f.Backup.Add(ped);
                }
                if (f.Backup.Count == 0) { _registry.Despawn(car); f.BackupCar = null; Log.Write("Streamer: backup for " + f.Def.Id + " couldn't load its people"); }
                else Log.Write("Streamer: backup for " + f.Def.Id + " on its way (" + f.Backup.Count + ")");
            }
            // Pulled up (or stuck, or under fire): everybody out and into the fight.
            if (!f.BackupOut && f.Backup.Count > 0)
            {
                bool arrived = f.BackupCar == null || !f.BackupCar.Exists() || f.BackupCar.Position.DistanceTo(door) < 25f || f.BackupCar.IsDead;
                bool hit = false;
                foreach (Ped p in f.Backup) if (p != null && p.Exists() && (p.IsDead || p.Health < p.MaxHealth)) hit = true;
                if (arrived || hit)
                {
                    f.BackupOut = true;
                    foreach (Ped p in f.Backup)
                    {
                        if (p == null || !p.Exists() || p.IsDead) continue;
                        p.BlockPermanentEvents = false;
                        p.Task.ClearAll();
                        p.Task.FightAgainstHatedTargets(80f);
                    }
                }
            }
            // Backup soldiers who die cost the crew a soldier, like the front's own.
            foreach (Ped p in f.Backup)
                if (p != null && p.Exists() && p.IsDead && f.BackupCounted.Add(p.Handle))
                {
                    WatchFx.DropBlip(p);
                    if (ByYou(p)) f.KilledByYou = true;
                    if (OnCrewGuardDown != null) OnCrewGuardDown(f.Def, f.Owner, p, false, false);
                }
        }

        /// <summary>The front only counts as cleared once any backup that was called is down too.</summary>
        private static bool BackupDone(StreamedFront f)
        {
            if (f.BackupAt > 0) return false;
            foreach (Ped p in f.Backup) if (p != null && p.Exists() && !p.IsDead) return false;
            return true;
        }

        /// <summary>Where crew soldier number i stands: the scouted posts first, then extra soldiers a step to the side of a
        /// post (never through a wall, always on the ground), so a big front's crew stands in twos.</summary>
        private static PostDef CrewSpot(FrontDef def, int i)
        {
            int n = def.Posts.Count;
            PostDef p = def.Posts[i % n];
            if (i < n) return p;
            double h = p.H * Math.PI / 180.0;
            var post = new Vector3(p.X, p.Y, p.Z + 0.5f);
            var side = new Vector3((float)Math.Cos(h), (float)Math.Sin(h), 0f);
            var back = new Vector3((float)Math.Sin(h), -(float)Math.Cos(h), 0f);
            // Soldier n+1 stands to one side of post 1, 2n+1 to the other, 3n+1 further out, and so on: never two on one spot.
            int ring = i / n;
            float sign = ring % 2 == 1 ? 1f : -1f;
            float step = 1.6f * ((ring + 1) / 2);
            Vector3[] tries = { post + side * (step * sign), post - side * (step * sign), post + back * (1.2f * ((ring + 1) / 2)) };
            foreach (Vector3 t in tries)
            {
                RaycastResult r = GTA.World.Raycast(post, t, IntersectFlags.Map, null);
                if (r.DidHit) continue;
                float g = GTA.World.GetGroundHeight(new Vector3(t.X, t.Y, p.Z + 1.5f));
                if (g <= 0f || Math.Abs(g - p.Z) > 1.2f) continue;
                return new PostDef { X = t.X, Y = t.Y, Z = g, H = p.H, Scenario = i % 2 == 0 ? "WORLD_HUMAN_SMOKING" : p.Scenario };
            }
            return p == null ? null : new PostDef { X = p.X + 0.6f * sign, Y = p.Y, Z = p.Z, H = p.H, Scenario = "WORLD_HUMAN_SMOKING" };
        }

        /// <summary>INI Rivals.AlertRange: on foot this close to a rival front's door, its soldiers raise their guns and warn
        /// you off (they start watching at twice this).</summary>
        public float AlertOnFootRange = 25f;

        /// <summary>Every half second at a rival front that isn't fighting yet: notice, warn, then fight (WatchRules).
        /// Driving past never bothers them. Before your first front they warn but only fight if you hurt one.</summary>
        private void WatchTick(StreamedFront f, int count)
        {
            Ped player = Game.Player.Character;
            bool hurt = false, aiming = false, seen = false;
            bool aimingNow = WatchFx.Aiming();
            float scale = WatchFx.SightScale();
            var soldiers = new List<Ped>();
            for (int i = 0; i < count; i++)
            {
                Slot s = f.Slots[i];
                if (s.Killed) { hurt = true; continue; }
                if (s.Ped == null || !s.Ped.Exists() || s.Ped.IsDead) continue;
                soldiers.Add(s.Ped);
                if (s.Ped.Health < s.Ped.MaxHealth) hurt = true;
                if (aimingNow && !aiming && WatchFx.AimedAt(s.Ped)) aiming = true;
            }
            // Distance to the nearest of the door and the soldiers: walking up behind a patroller counts.
            float nearestSoldier = WatchFx.Nearest(soldiers);
            float distance = Math.Min(f.Distance, nearestSoldier);
            if (distance <= WatchRules.WatchRange(AlertOnFootRange * scale) + 5f)
                foreach (Ped p in soldiers) if (WatchFx.Sees(p, f.Watch == Watch.Calm)) { seen = true; break; }
            bool inTheGame = _empire != null && _empire.OwnedCount() > 0;
            int gt = Game.GameTime;
            // Ticks stopped for a while (a fade, a death, a paused stretch): a warning's fuse starts again rather than going off.
            if (f.Watch == Watch.Warning && gt - f.LastWatchAt > 3000) { f.WarnStart = gt; f.LastWarned = false; }
            f.LastWatchAt = gt;
            CrewState angry = _empire != null ? Nemesis.Crew(_empire.State, f.Owner) : null;
            int limit = WatchRules.WarnMs(f.Owner, f.Def.Tier, WatchRules.Temper(_empire != null ? _empire.State.Heat : 0, angry != null ? angry.Grudge : 0));
            int warned = f.Watch == Watch.Warning ? gt - f.WarnStart : 0;
            Watch next = WatchRules.Next(f.Watch, distance, !player.IsInVehicle(), aiming, WatchFx.HeardShot(distance),
                                         hurt, inTheGame, warned, limit, AlertOnFootRange, seen, scale, nearestSoldier);
            if (next == Watch.Hostile)
            {
                f.Watch = Watch.Hostile;
                Alert(f, count);
                return;
            }
            if (next != f.Watch)
            {
                Watch was = f.Watch;
                f.Watch = next;
                EnterWatch(f, count, was, next, gt);
                return;
            }
            if (next == Watch.Warning && inTheGame && !f.LastWarned && WatchRules.LastWarningDue(warned, limit))
            {
                f.LastWarned = true;
                WatchFx.Say(_content, f.Owner, "lastWarning");
            }
        }

        /// <summary>If the watch logic ever fails: the old simple rule (walk up on foot, shoot nearby or hurt one).</summary>
        private bool FallbackProvoked(StreamedFront f, int count)
        {
            try
            {
                bool hurt = false;
                for (int i = 0; i < count; i++)
                {
                    Slot s = f.Slots[i];
                    if (s.Killed || (s.Ped != null && s.Ped.Exists() && !s.Ped.IsDead && s.Ped.Health < s.Ped.MaxHealth)) hurt = true;
                }
                Ped player = Game.Player.Character;
                bool inTheGame = _empire != null && _empire.OwnedCount() > 0;
                return StreamRules.CrewProvoked(inTheGame, !player.IsInVehicle() && f.Distance < AlertOnFootRange,
                                                player.IsShooting && f.Distance < WatchRules.ShotHeard, hurt);
            }
            catch { return false; }
        }

        /// <summary>Re-tasks the soldiers when the front's mood changes (never every tick, so tasks aren't restarted).</summary>
        private void EnterWatch(StreamedFront f, int count, Watch was, Watch now, int gt)
        {
            int hour = GTA.World.CurrentTimeOfDay.Hours;
            if (now == Watch.Warning)
            {
                f.WarnStart = gt;
                f.LastWarned = false;
                WatchFx.Say(_content, f.Owner, "warn" + f.Owner);
            }
            for (int i = 0; i < count; i++)
            {
                Slot s = f.Slots[i];
                Ped p = s.Ped;
                if (p == null || !p.Exists() || p.IsDead) continue;
                p.Task.ClearAll();
                s.PatrolWalking = false;
                s.PatrolAt = -1; // patrols pick up again from scratch once they're calm
                if (now == Watch.Warning)
                {
                    WatchFx.Raise(p);
                    WatchFx.Blip(_content, p, f.Owner);
                    continue;
                }
                WatchFx.DropBlip(p);
                if (now == Watch.Watching) WatchFx.Eye(p);
                else
                {
                    // Calm again: the door-holders walk back to their post's idle (no teleport); patrollers set off on their
                    // loop again on the next patrol tick.
                    p.Task.ClearLookAt();
                    if (StreamRules.Patrols(i, count)) continue;
                    PostDef post = CrewSpot(f.Def, i);
                    if (post != null)
                    {
                        string scenario = StreamRules.GuardScenario(post.Scenario, Trait.None, hour);
                        Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, p, scenario, post.X, post.Y, post.Z, post.H, -1, false, false);
                    }
                }
            }
            Log.Write("Streamer: crew at " + f.Def.Id + " " + was + " -> " + now);
        }

        private void Alert(StreamedFront f, int count)
        {
            f.Alerted = true;
            for (int i = 0; i < count; i++)
            {
                Slot s = f.Slots[i];
                if (s.Ped == null || !s.Ped.Exists() || s.Ped.IsDead) continue;
                s.Ped.Task.ClearAll();
                s.Ped.BlockPermanentEvents = false;
                s.Ped.Task.FightAgainstHatedTargets(60f);
                WatchFx.Blip(_content, s.Ped, f.Owner); // a small blip in the crew's colour on every soldier in the fight
            }
            Log.Write("Streamer: crew at " + f.Def.Id + " alerted");
        }

        private void SpawnCrewGuard(Slot slot, CrewDef crew, PostDef post, int hour, int pick, int tier, int grudge, bool starter)
        {
            Ped ped = GTA.World.CreatePed(slot.PendingModel, new Vector3(post.X, post.Y, post.Z), post.H);
            if (ped == null) return;
            ped.RelationshipGroup = Factions.CrewGroup(crew.Id);
            _registry.Track(ped);
            slot.Ped = ped;
            slot.GuardId = -1;
            int heat = _empire.State.Heat;
            if (starter)
            {
                // The car wash: pistols and melee, no armour, street heat or not. The easy way in.
                ped.Weapons.Give(CrewWeapon(CrewDef.StarterWeapon(pick)), 250, false, true);
                ped.Accuracy = 15;
                ped.Armor = 0;
            }
            else
            {
                ped.Weapons.Give(CrewWeapon(crew.WeaponFor(pick, tier, grudge)), 250, false, true);
                ped.Accuracy = CrewDef.FrontAccuracy(tier) + Heat.CrewAccuracy(heat) - 25; // by size, sharper on hot streets
                ped.Armor = Math.Min(100, CrewDef.FrontArmor(tier) + Heat.ExtraArmor(heat)); // bigger fronts are better kitted out, more so on hot streets
            }
            ped.MaxHealth = Rules.Stats.CrewHealth; // twice an ordinary ped: fights last long enough to be fun
            ped.Health = ped.MaxHealth;
            Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
            // Calm until alerted (see ShouldAlert): idle at the post, ignoring the world.
            ped.BlockPermanentEvents = true;
            string scenario = StreamRules.GuardScenario(post.Scenario, Trait.None, hour);
            Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, ped, scenario, post.X, post.Y, post.Z, post.H, -1, false, true);
        }

        public static WeaponHash CrewWeapon(string name)
        {
            WeaponHash w;
            return Enum.TryParse(name, true, out w) ? w : WeaponHash.Pistol;
        }

        // ---------------- shared ----------------

        private void Load(Slot slot, string modelName, int forId, long now, Action spawn)
        {
            if (!slot.Pending)
            {
                var model = new Model(modelName);
                if (!model.IsValid || !model.IsPed)
                {
                    Log.Write("Streamer: bad ped model '" + modelName + "', retrying in 10 s");
                    slot.RespawnAt = now + StreamRules.RespawnDelayMs;
                    return;
                }
                model.Request();
                slot.Pending = true;
                slot.PendingModel = model;
                slot.PendingGuardId = forId;
                slot.PendingSince = Game.GameTime;
            }
            else if (slot.PendingGuardId != forId)
            {
                CancelPending(slot);
            }
            else if (slot.PendingModel.IsLoaded)
            {
                if (!_registry.CanSpawnPed) return;
                try { spawn(); }
                finally { CancelPending(slot); }
            }
            else if (Game.GameTime - slot.PendingSince > StreamRules.ModelTimeoutMs)
            {
                Log.Write("Streamer: model " + modelName + " didn't load in 5 s, retrying in 10 s");
                CancelPending(slot);
                slot.RespawnAt = now + StreamRules.RespawnDelayMs;
            }
        }

        private void StreamOut(StreamedFront f, bool layLow)
        {
            Vector3 door = new Vector3(f.Def.Door.X, f.Def.Door.Y, f.Def.Door.Z);
            long now = _activeClock();
            bool anyLeaving = false;
            for (int i = 0; i < f.Slots.Length; i++)
            {
                Slot s = f.Slots[i];
                CancelPending(s);
                s.Killed = false;
                if (i == 0)
                {
                    foreach (Ped p in f.Backup) if (p != null && p.Exists()) _registry.Despawn(p);
                    f.Backup.Clear();
                    if (f.BackupCar != null && f.BackupCar.Exists()) _registry.Despawn(f.BackupCar);
                    f.BackupCar = null;
                    f.BackupAt = 0;
                }
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

        /// <summary>The drive-by test: after a front streams out, no FR_ ped may remain near it.</summary>
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
                    s.Ped = null; s.Body = null; s.GuardId = 0; s.Leaving = false; s.RespawnAt = 0; s.Killed = false;
                }
                f.Streamed = false;
            }
        }

        /// <summary>Streams every front out now (dev tools, content reload).</summary>
        public void DespawnAll()
        {
            foreach (StreamedFront f in _fronts) StreamOut(f, false);
        }

        /// <summary>Re-tasks a front's idle guards for a fight (live defense start) or back to idle (fight over).</summary>
        /// <summary>Full health, and a full vest for anyone who owns one. Vests never wear out: they're the guard's for good.</summary>
        public static void Restore(Ped ped, GuardState g)
        {
            if (ped == null || !ped.Exists() || ped.IsDead) return;
            ped.MaxHealth = Rules.Stats.Health(g.Strength); // same as when they spawned
            ped.Health = ped.MaxHealth;
            ped.Armor = g.Vest ? DefenseRating.VestArmor : 0;
        }

        public void SetFighting(string frontId, bool fighting)
        {
            int hour = GTA.World.CurrentTimeOfDay.Hours;
            foreach (StreamedFront f in _fronts)
            {
                if (f.Def.Id != frontId || f.IsCrew) continue;
                for (int i = 0; i < f.Slots.Length && i < f.Def.Posts.Count; i++)
                {
                    Slot s = f.Slots[i];
                    if (s.Ped == null || !s.Ped.Exists() || s.Ped.IsDead) continue;
                    s.Ped.Task.ClearAll();
                    if (fighting)
                    {
                        s.Ped.BlockPermanentEvents = false;
                        s.Ped.Task.FightAgainstHatedTargets(60f);
                    }
                    else
                    {
                        GuardState g = _empire.Guard(s.GuardId);
                        PostDef post = f.Def.Posts[i];
                        if (g != null) Restore(s.Ped, g); // the fight's over: patched up, vest back to full
                        s.Ped.BlockPermanentEvents = true;
                        string scenario = StreamRules.GuardScenario(post.Scenario, g != null ? (Trait)g.Trait : Trait.None, hour);
                        Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, s.Ped, scenario, post.X, post.Y, post.Z, post.H, -1, false, false);
                    }
                }
            }
        }

        public int StreamedCount
        {
            get { int n = 0; foreach (StreamedFront f in _fronts) if (f.Streamed) n++; return n; }
        }
    }
}
