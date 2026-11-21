// Fronts & Rivals: crew hangouts in the world (rules in HangoutRules). Groups of two or three of a crew's people hang
// out on the streets of their turf, sometimes round one of their cars. They're calm until provoked (the same rules
// as a front's soldiers), then fight. Every one you kill costs their crew a soldier. Every entity goes through the
// Registry, so the ped budget and cleanup cover them; models load without blocking the game thread.
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
    public sealed class Hangouts
    {
        private sealed class Group
        {
            public string Crew;
            public Vector3 Spot;
            public float Heading;
            public int Size;
            public string CarModel;
            public readonly List<Model> Models = new List<Model>();
            public readonly List<Ped> Peds = new List<Ped>();
            public readonly HashSet<int> Counted = new HashSet<int>();
            public Vehicle Car;
            public bool Spawned, Alerted;
            public int RequestedAt;
        }

        private readonly Registry _registry;
        private readonly ContentDb _content;
        private readonly Func<Empire> _empire;
        private readonly List<Group> _groups = new List<Group>();
        private readonly Random _rng = new Random();
        private int _nextSpawn;

        public Hangouts(Registry registry, ContentDb content, Func<Empire> empire)
        {
            _registry = registry;
            _content = content;
            _empire = empire;
        }

        /// <summary>The world was cleared (mission, reload): every entity is already gone.</summary>
        public void Forget() { _groups.Clear(); }

        /// <summary>About once a second while the game is open.</summary>
        public void Update()
        {
            Empire e = _empire();
            if (e == null) return;
            Ped me = Game.Player.Character;
            if (me == null || !me.Exists() || me.IsDead) return;
            Vector3 p = me.Position;

            for (int i = _groups.Count - 1; i >= 0; i--)
            {
                Group g = _groups[i];
                if (!g.Spawned) { TrySpawn(g, e); if (!g.Spawned && Game.GameTime - g.RequestedAt > 15000) _groups.RemoveAt(i); continue; }
                if (g.Spot.DistanceTo(p) > HangoutRules.DespawnRange) { Remove(g); _groups.RemoveAt(i); continue; }
                Tend(g, e, me);
            }

            if (_groups.Count >= HangoutRules.MaxGroups || Game.GameTime < _nextSpawn) return;
            _nextSpawn = Game.GameTime + HangoutRules.RespawnMs;
            Plan(e, p);
        }

        // Pick the nearest crew front, then a sidewalk spot on its streets that's out of your sight.
        private void Plan(Empire e, Vector3 p)
        {
            FrontDef near = null;
            float best = HangoutRules.TurfRange;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet || !Owners.IsCrew(e.OwnerOf(def.Id))) continue;
                float d = def.Door.DistanceTo(p.X, p.Y, p.Z);
                if (d < best) { best = d; near = def; }
            }
            if (near == null) return;
            string crewId = e.OwnerOf(near.Id);
            CrewDef crew = _content.Crew(crewId);
            CrewState cs = Nemesis.Crew(e.State, crewId);
            if (crew == null || cs == null || cs.Manpower < 3 || crew.Models == null || crew.Models.Count == 0) return;
            int size = HangoutRules.GroupSize(_rng.Next());
            if (!HangoutRules.Fits(_registry.PedCount, size, Registry.MaxPeds)) return;

            var door = new Vector3(near.Door.X, near.Door.Y, near.Door.Z);
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double a = _rng.NextDouble() * Math.PI * 2.0;
                float r = HangoutRules.FrontMin + (float)_rng.NextDouble() * (HangoutRules.FrontMax - HangoutRules.FrontMin);
                Vector3 want = door + new Vector3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, 0f);
                Vector3 spot = GTA.World.GetNextPositionOnSidewalk(want);
                if (spot == Vector3.Zero) continue;
                float fromMe = spot.DistanceTo(p);
                if (fromMe < HangoutRules.SpawnMin || fromMe > HangoutRules.SpawnMax) continue;
                bool crowded = false;
                foreach (Group other in _groups) if (other.Spot.DistanceTo(spot) < 50f) crowded = true;
                if (crowded) continue;

                var g = new Group { Crew = crewId, Spot = spot, Heading = (float)(_rng.NextDouble() * 360.0), Size = size, RequestedAt = Game.GameTime };
                for (int k = 0; k < size; k++) g.Models.Add(new Model(crew.Models[_rng.Next(crew.Models.Count)]));
                if (HangoutRules.WithCar(_rng.Next()) && _registry.CanSpawnVehicle)
                {
                    List<string> rides = crew.RidesFor(Math.Min(1, CrewDef.TierFor(cs.Grudge, e.State.Heat)));
                    if (rides != null && rides.Count > 0) { g.CarModel = rides[_rng.Next(rides.Count)]; g.Models.Add(new Model(g.CarModel)); }
                }
                foreach (Model m in g.Models) if (m.IsValid) m.Request();
                _groups.Add(g);
                return;
            }
        }

        private void TrySpawn(Group g, Empire e)
        {
            foreach (Model m in g.Models) if (m.IsValid && !m.IsLoaded) return;
            CrewDef crew = _content.Crew(g.Crew);
            CrewState cs = Nemesis.Crew(e.State, g.Crew);
            if (crew == null || !HangoutRules.Fits(_registry.PedCount, g.Size, Registry.MaxPeds)) return;
            g.Spawned = true;

            if (g.CarModel != null && _registry.CanSpawnVehicle)
            {
                Model cm = g.Models[g.Models.Count - 1];
                Vehicle v = GTA.World.CreateVehicle(cm, g.Spot + new Vector3(3.5f, 0f, 0.5f), g.Heading);
                if (v != null)
                {
                    _registry.Track(v, Markers.MakePlate(0, crew.Id));
                    CrewColor paint = CrewColors.Get(CrewColors.ForCrew(crew.Id));
                    v.Mods.CustomPrimaryColor = System.Drawing.Color.FromArgb(paint.R, paint.G, paint.B);
                    v.Mods.CustomSecondaryColor = System.Drawing.Color.FromArgb(paint.R / 3, paint.G / 3, paint.B / 3);
                    v.MaxHealth = 2000; v.Health = 2000; v.BodyHealth = 2000f; v.EngineHealth = 2000f; v.PetrolTankHealth = 2000f;
                    v.PlaceOnGround();
                    v.IsEngineRunning = false;
                    g.Car = v;
                }
            }

            int grudge = cs != null ? cs.Grudge : 0;
            for (int k = 0; k < g.Size; k++)
            {
                if (!_registry.CanSpawnPed) break;
                double a = (k / (double)g.Size) * Math.PI * 2.0;
                Vector3 at = g.Spot + new Vector3((float)Math.Cos(a) * 1.3f, (float)Math.Sin(a) * 1.3f, 0f);
                float face = (float)(Math.Atan2(-(g.Spot.X - at.X), g.Spot.Y - at.Y) * 180.0 / Math.PI); // face each other
                Ped ped = GTA.World.CreatePed(g.Models[k], at, face);
                if (ped == null) continue;
                ped.RelationshipGroup = Factions.CrewGroup(crew.Id);
                _registry.Track(ped);
                ped.Weapons.Give(Streamer.CrewWeapon(crew.WeaponFor(Markers.Hash(crew.Id) + k * 17 + g.Heading.GetHashCode(), 0, grudge)), 250, false, true);
                ped.Accuracy = CrewDef.FrontAccuracy(0);
                ped.MaxHealth = Rules.Stats.CrewHealth;
                ped.Health = ped.MaxHealth;
                ped.BlockPermanentEvents = true; // calm until provoked
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
                ped.Task.StartScenario(HangoutRules.Scenario(k + (int)g.Heading), at, face);
                g.Peds.Add(ped);
            }
            Log.Write("Hangouts: " + g.Peds.Count + " " + crew.Id + (g.Car != null ? " with a " + g.CarModel : "") + " near " + g.Spot);
        }

        private void Tend(Group g, Empire e, Ped me)
        {
            float dist = g.Spot.DistanceTo(me.Position);
            bool hurt = false;
            foreach (Ped ped in g.Peds)
            {
                if (ped == null || !ped.Exists()) continue;
                if (ped.IsDead)
                {
                    hurt = true;
                    if (g.Counted.Add(ped.Handle))
                    {
                        // A soldier down: their crew is one short, and they won't forget who did it.
                        CrewState cs = Nemesis.Crew(e.State, g.Crew);
                        if (cs != null && ped.Killer == me) { cs.Manpower = Math.Max(0, cs.Manpower - 1); cs.LastContactDay = e.State.Day; }
                    }
                }
                else if (ped.Health < ped.MaxHealth) hurt = true;
            }
            if (g.Alerted) return;
            bool provoked = StreamRules.CrewProvoked(e.InTheGame(), me.IsOnFoot && dist < HangoutRules.AlertOnFoot, me.IsShooting && dist < HangoutRules.AlertShots, hurt);
            if (!provoked) return;
            g.Alerted = true;
            CrewDef crew = _content.Crew(g.Crew);
            foreach (Ped ped in g.Peds)
            {
                if (ped == null || !ped.Exists() || ped.IsDead) continue;
                ped.BlockPermanentEvents = false;
                ped.Task.ClearAll();
                ped.Task.FightAgainstHatedTargets(80f);
                try
                {
                    if (ped.AttachedBlip == null || !ped.AttachedBlip.Exists())
                    {
                        Blip b = ped.AddBlip();
                        b.Color = crew != null ? (BlipColor)crew.BlipColor : BlipColor.Red;
                        b.Scale = 0.7f;
                        b.Name = crew != null ? crew.Name : "Crew";
                    }
                }
                catch (Exception ex) { Log.Error("Hangout blip", ex); }
            }
        }

        private void Remove(Group g)
        {
            foreach (Ped ped in g.Peds) if (ped != null && ped.Exists()) _registry.Despawn(ped);
            if (g.Car != null && g.Car.Exists()) _registry.Despawn(g.Car);
            foreach (Model m in g.Models) m.MarkAsNoLongerNeeded();
        }
    }
}
