// Fronts & Rivals: bodyguards in the world. Each guard marked as a bodyguard in the Office gets a ped that joins
// your own ped group, so GTA's own AI has them follow you on foot, get in your car and fight whoever attacks you.
// Every ped is FR_GUARDS and tracked by the Registry. Models load asynchronously, one at a time, no Wait.
// During missions the Scheduler pauses this and ClearWorld removes them; they come back when free roam does.
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
    public sealed class Escorts
    {
        private sealed class Entry
        {
            public int GuardId;
            public Ped Ped;
            public int CalmSince;
        }

        private readonly Registry _registry;
        private readonly Func<Empire> _empire;
        private readonly Action<GuardState, Ped> _onDown; // a bodyguard was killed
        private readonly List<Entry> _out = new List<Entry>();
        private readonly List<Ped> _bodies = new List<Ped>();

        private Model _pending;
        private readonly Dictionary<int, int> _badModelUntil = new Dictionary<int, int>(); // guard id -> retry time
        private int _pendingFor;
        private int _requestedAt;

        public Escorts(Registry registry, Func<Empire> empire, Action<GuardState, Ped> onDown)
        {
            _registry = registry;
            _empire = empire;
            _onDown = onDown;
        }

        /// <summary>Every 0.5 s with the gate open.</summary>
        public void Update()
        {
            Empire e = _empire();
            if (e == null) { Clear(); return; }
            Ped me = Game.Player.Character;
            if (me == null || !me.Exists() || me.IsDead) return; // wasted: wait for the respawn

            // Bodyguards who were sent back, fired or hurt elsewhere leave.
            for (int i = _out.Count - 1; i >= 0; i--)
            {
                Entry en = _out[i];
                GuardState g = e.Guard(en.GuardId);
                bool wanted = g != null && g.Escort && g.Status == GuardStatus.Active;
                if (en.Ped == null || !en.Ped.Exists()) { _out.RemoveAt(i); continue; }
                if (en.Ped.IsDead)
                {
                    if (en.Ped.AttachedBlip != null && en.Ped.AttachedBlip.Exists()) en.Ped.AttachedBlip.Delete();
                    _bodies.Add(en.Ped);
                    _out.RemoveAt(i);
                    if (g != null && g.Status == GuardStatus.Active && _onDown != null) _onDown(g, en.Ped);
                    continue;
                }
                if (!wanted) { Despawn(en.Ped); _out.RemoveAt(i); continue; }
                // Ten seconds out of a fight: patched up and the vest topped up, like the player between missions.
                if (en.Ped.IsInCombat) en.CalmSince = 0;
                else if (en.CalmSince == 0) en.CalmSince = Game.GameTime;
                else if (Game.GameTime - en.CalmSince > 10000 && g != null) Streamer.Restore(en.Ped, g);
                // Left far behind (a fast car, a flight): they catch up instead of running across the map.
                if (!me.IsInVehicle() && en.Ped.Position.DistanceTo(me.Position) > EscortRules.LeftBehindRange)
                {
                    Despawn(en.Ped);
                    _out.RemoveAt(i);
                }
            }
            for (int i = _bodies.Count - 1; i >= 0; i--)
            {
                Ped b = _bodies[i];
                if (b == null || !b.Exists()) { _bodies.RemoveAt(i); continue; }
                if (b.Position.DistanceTo(me.Position) > EscortRules.BodyRange) { _registry.Despawn(b); _bodies.RemoveAt(i); }
            }

            // Bring in whoever is missing, one at a time, once you're on foot or stopped.
            GuardState next = null;
            foreach (GuardState g in e.State.Guards)
            {
                int until;
                if (_badModelUntil.TryGetValue(g.Id, out until) && Game.GameTime < until) continue;
                if (g.Escort && g.Status == GuardStatus.Active && Find(g.Id) == null) { next = g; break; }
            }
            if (next == null) { CancelPending(); return; }
            if (me.IsInVehicle() && me.CurrentVehicle.Speed > 2f) return;
            if (!EscortRules.Fits(_registry.PedCount, Registry.MaxPeds)) return;

            if (_pendingFor != next.Id)
            {
                CancelPending();
                Dressing nd = Wardrobe.ForGuard(next);
                _pending = new Model(nd != null ? Wardrobe.ModelName(nd) : next.Model);
                if (!_pending.IsValid || !_pending.IsPed)
                {
                    Log.Write("Escorts: bad model " + next.Model + " for guard " + next.Id + ", retrying in a minute");
                    _badModelUntil[next.Id] = Game.GameTime + 60000;
                    return;
                }
                _pending.Request();
                _pendingFor = next.Id;
                _requestedAt = Game.GameTime;
                return;
            }
            if (!_pending.IsLoaded)
            {
                if (Game.GameTime - _requestedAt > StreamRules.ModelTimeoutMs) CancelPending();
                return;
            }
            try { Spawn(next, me); }
            finally { CancelPending(); }
        }

        private void Spawn(GuardState g, Ped me)
        {
            // A walkable spot a few metres behind you, so they walk up rather than appear in your face.
            Vector3 probe = me.Position - me.ForwardVector * 6f;
            var outPos = new OutputArgument();
            Vector3 at = Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, probe.X, probe.Y, probe.Z, true, outPos, 0)
                ? outPos.GetResult<Vector3>() : probe;
            if (at.DistanceTo(me.Position) > 25f) at = probe;

            Ped ped = GTA.World.CreatePed(_pending, at, me.Heading);
            if (ped == null) return;
            Wardrobe.Apply(ped, Wardrobe.ForGuard(g)); // in your crew's colour
            ped.RelationshipGroup = Factions.Guards; // before Track
            _registry.Track(ped);
            _out.Add(new Entry { GuardId = g.Id, Ped = ped });

            ped.Weapons.Give(Streamer.WeaponFor(g.Weapon), 500, true, true);
            ped.Accuracy = Stats.Accuracy(g.Shooting);
            ped.MaxHealth = Stats.Health(g.Strength);
            ped.Health = ped.MaxHealth;
            ped.Armor = g.Vest ? DefenseRating.VestArmor : 0; // body armour bought in the Office
            ped.BlockPermanentEvents = false;
            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, false);

            PedGroup group = me.PedGroup;
            if (group != null && group.Exists())
            {
                group.Add(ped, false);
                ped.NeverLeavesGroup = true;
            }
            else
            {
                Log.Write("Escorts: no player group, " + g.Id + " will only fight nearby");
                ped.Task.FightAgainstHatedTargets(60f);
            }

            Blip b = ped.AddBlip();
            b.Color = (BlipColor)CrewColors.Get(_empire() != null ? _empire().State.CrewColor ?? CrewColors.DefaultPlayer : CrewColors.DefaultPlayer).Blip; // your crew's colour
            b.Scale = 0.7f;
            b.Name = "Bodyguard";
            Log.Write("Escorts: " + g.FullName + " is with you");
        }

        private Ped Find(int guardId)
        {
            foreach (Entry en in _out) if (en.GuardId == guardId) return en.Ped;
            return null;
        }

        private void Despawn(Ped ped)
        {
            if (ped == null) return;
            if (ped.Exists() && ped.AttachedBlip != null && ped.AttachedBlip.Exists()) ped.AttachedBlip.Delete();
            _registry.Despawn(ped);
        }

        private void CancelPending()
        {
            if (_pendingFor != 0) _pending.MarkAsNoLongerNeeded();
            _pendingFor = 0;
        }

        public void Clear()
        {
            CancelPending();
            foreach (Entry en in _out) Despawn(en.Ped);
            _out.Clear();
            foreach (Ped b in _bodies) _registry.Despawn(b);
            _bodies.Clear();
        }

        /// <summary>After Registry.DeleteAll: the peds are gone already.</summary>
        public void Forget()
        {
            CancelPending();
            _out.Clear();
            _bodies.Clear();
        }
    }
}
