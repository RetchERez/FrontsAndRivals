// Fronts & Rivals: staff at your fronts, so a business you own looks open.
// Only at the one front you're standing near, only with plenty of ped budget left, and they leave the moment
// guards, crews or a fight need the slots. Every staff ped is FR_STAFF (neutral) and tracked by the Registry.
// Models load asynchronously: no Wait, one step per call.
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
    public sealed class Workers
    {
        private const int FlagPavement = 1, FlagNotInterior = 4, FlagNotWater = 8;

        private readonly Registry _registry;
        private readonly ContentDb _content;
        private readonly Func<Empire> _empire;
        private readonly List<Ped> _peds = new List<Ped>();
        private readonly List<Ped> _bodies = new List<Ped>(); // killed staff, removed when you leave

        private FrontDef _front;
        private Model _pending;
        private bool _hasPending;
        private int _requestedAt;
        private int _spawnedThisVisit;

        public Workers(Registry registry, ContentDb content, Func<Empire> empire)
        {
            _registry = registry;
            _content = content;
            _empire = empire;
        }

        public int Count { get { return _peds.Count; } }

        /// <summary>Every second with the gate open.</summary>
        public void Update()
        {
            Empire e = _empire();
            if (e == null) { Clear(); return; }
            Vector3 p = Game.Player.Character.Position;

            FrontDef target = PickFront(e, p);
            if (target != _front) { Clear(); _front = target; _spawnedThisVisit = 0; }
            if (_front == null) return;
            _peds.RemoveAll(x => x == null || !x.Exists());
            for (int i = _peds.Count - 1; i >= 0; i--)
                if (_peds[i].IsDead) { _bodies.Add(_peds[i]); _peds.RemoveAt(i); }

            // Any live fight needs the budget, and nobody sweeps up while their front is under attack.
            AttackState a = e.State.Attack;
            if (a != null && (a.Phase == AttackPhase.Live || (a.Front == _front.Id && a.Phase != AttackPhase.Warning))) { Clear(); return; }

            int others = _registry.PedCount - _peds.Count - _bodies.Count;
            if (WorkerRules.MustYield(others, _peds.Count, Registry.MaxPeds)) { DespawnOne(); return; }

            int hour = GTA.World.CurrentTimeOfDay.Hours;
            int want = WorkerRules.Count(_front.Tier, hour);
            while (_peds.Count > want) DespawnOne(); // the day crew goes home at night
            // Staff who were killed or scared off don't keep coming back during the same visit.
            if (_peds.Count >= want || _spawnedThisVisit >= want + 2 || !WorkerRules.Fits(others, _peds.Count, Registry.MaxPeds))
            {
                CancelPending();
                return;
            }

            if (!_hasPending)
            {
                string[] models = WorkerRules.Models(_front.Id);
                int pick = (Math.Abs(_front.Id.GetHashCode()) + _spawnedThisVisit) % models.Length;
                _pending = new Model(models[pick]);
                if (!_pending.IsValid || !_pending.IsPed) { Log.Write("Workers: bad model " + models[pick]); _spawnedThisVisit++; return; }
                _pending.Request();
                _hasPending = true;
                _requestedAt = Game.GameTime;
                return;
            }
            if (!_pending.IsLoaded)
            {
                if (Game.GameTime - _requestedAt > StreamRules.ModelTimeoutMs) CancelPending();
                return;
            }
            try { Spawn(_peds.Count, hour); }
            finally { CancelPending(); }
        }

        private FrontDef PickFront(Empire e, Vector3 p)
        {
            if (_front != null && e.PlayerOwns(_front.Id) && _front.Door.DistanceTo(p.X, p.Y, p.Z) < WorkerRules.OutRange) return _front;
            FrontDef best = null;
            float bestD = WorkerRules.InRange;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet || !e.PlayerOwns(def.Id)) continue;
                float d = def.Door.DistanceTo(p.X, p.Y, p.Z);
                if (d < bestD) { bestD = d; best = def; }
            }
            return best;
        }

        private void Spawn(int index, int hour)
        {
            Vector3 door = new Vector3(_front.Door.X, _front.Door.Y, _front.Door.Z);
            Vector3 spot;
            if (!FindSpot(door, index, out spot)) { _spawnedThisVisit++; return; }
            Vector3 outward = spot - door;
            outward.Z = 0f;
            float heading = outward.Length() > 0.1f ? outward.ToHeading() : _front.Door.H;

            Ped ped = GTA.World.CreatePed(_pending, spot, heading);
            _spawnedThisVisit++;
            if (ped == null) return;
            ped.RelationshipGroup = Factions.Staff; // neutral to everyone; must be set before Track
            _registry.Track(ped);
            _peds.Add(ped);
            ped.BlockPermanentEvents = false; // gunfire sends them running, like any shopkeeper
            Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, false);
            string scenario = WorkerRules.Scenario(_front.Id, index, hour);
            Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, ped, scenario, spot.X, spot.Y, spot.Z, heading, -1, false, true);
        }

        /// <summary>A walkable outdoor spot beside the door, off the road and clear of every guard post.</summary>
        private bool FindSpot(Vector3 door, int index, out Vector3 spot)
        {
            spot = Vector3.Zero;
            // "Out" is towards the guard posts (they're placed in sight of the door), otherwise towards you.
            Vector3 dir = Vector3.Zero;
            foreach (PostDef post in _front.Posts) dir += new Vector3(post.X, post.Y, post.Z) - door;
            if (dir.Length() < 0.5f) dir = Game.Player.Character.Position - door;
            dir.Z = 0f;
            if (dir.Length() < 0.1f) dir = new Vector3(0f, 1f, 0f);
            dir.Normalize();

            for (int attempt = 0; attempt < 6; attempt++)
            {
                float[] off = WorkerRules.Offset(index + attempt);
                float rad = off[0] * (float)Math.PI / 180f;
                var turned = new Vector3(dir.X * (float)Math.Cos(rad) - dir.Y * (float)Math.Sin(rad),
                                         dir.X * (float)Math.Sin(rad) + dir.Y * (float)Math.Cos(rad), 0f);
                Vector3 probe = door + turned * off[1];
                var outPos = new OutputArgument();
                if (!Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, probe.X, probe.Y, probe.Z, true, outPos, FlagPavement | FlagNotInterior | FlagNotWater)) continue;
                Vector3 s = outPos.GetResult<Vector3>();
                if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD, s.X, s.Y, s.Z, 0)) continue;
                float d = s.DistanceTo(door);
                if (d < 1.5f || d > 7f || Math.Abs(s.Z - door.Z) > 2f) continue;
                bool clear = true;
                foreach (PostDef post in _front.Posts) if (s.DistanceTo(new Vector3(post.X, post.Y, post.Z)) < 2f) { clear = false; break; }
                foreach (Ped other in _peds) if (other.Exists() && other.Position.DistanceTo(s) < 1.5f) { clear = false; break; }
                if (!clear) continue;
                spot = s;
                return true;
            }
            return false;
        }

        private void DespawnOne()
        {
            if (_peds.Count == 0) return;
            Ped last = _peds[_peds.Count - 1];
            _peds.RemoveAt(_peds.Count - 1);
            _registry.Despawn(last);
        }

        private void CancelPending()
        {
            if (_hasPending) _pending.MarkAsNoLongerNeeded();
            _hasPending = false;
        }

        /// <summary>Removes every staff ped (leaving the front, a mission, a fight).</summary>
        public void Clear()
        {
            CancelPending();
            while (_peds.Count > 0) DespawnOne();
            foreach (Ped b in _bodies) _registry.Despawn(b);
            _bodies.Clear();
        }

        /// <summary>After Registry.DeleteAll: the peds are gone already, forget them.</summary>
        public void Forget()
        {
            CancelPending();
            _peds.Clear();
            _bodies.Clear();
            _front = null;
        }
    }
}
