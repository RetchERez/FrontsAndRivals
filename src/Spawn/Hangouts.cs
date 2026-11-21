// Fronts & Rivals: crews on their streets, like Saints Row (rules in HangoutRules). Anywhere on a crew's turf - the
// streets whose nearest front it owns - a few of its people hang out (sometimes round one of their cars), one or two
// walk the block, or a car full of them cruises past. Around your own fronts it's your crew, in your colour, and
// friendly. Rival crews are calm until provoked (the same rules as a front's soldiers), then fight; every one you
// kill costs their crew a soldier. Everything goes through the Registry (ped budget, cleanup) and models load
// without blocking the game thread.
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
            public string Crew;            // a crew id, or Owners.Player for your own people
            public HangoutKind Kind;
            public Vector3 Spot;
            public float Heading;
            public int Size, Seed;
            public string CarModel;
            public readonly List<Model> Models = new List<Model>();
            public readonly List<Dressing> Dress = new List<Dressing>();
            public readonly List<Ped> Peds = new List<Ped>();
            public readonly HashSet<int> Counted = new HashSet<int>();
            public Vehicle Car;
            public bool Spawned, Alerted;
            public int RequestedAt;
            public Group Rival;            // a street fight: the other crew's group across the road
            public bool Mine { get { return Crew == Owners.Player; } }

            public Vector3 Where()
            {
                if (Car != null && Car.Exists()) return Car.Position;
                foreach (Ped p in Peds) if (p != null && p.Exists() && !p.IsDead) return p.Position;
                return Spot;
            }
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
                if (!g.Spawned)
                {
                    TrySpawn(g, e);
                    if (!g.Spawned && Game.GameTime - g.RequestedAt > 15000) { Release(g); _groups.RemoveAt(i); }
                    continue;
                }
                if (g.Where().DistanceTo(p) > HangoutRules.DespawnRange) { Remove(g); _groups.RemoveAt(i); continue; }
                if (g.Rival != null && !g.Alerted && g.Spawned && g.Rival.Spawned) StartClash(g, e);
                if (g.Mine) TendMine(g, me); else Tend(g, e, me);
            }

            int heat = e.State.Heat;
            if (_groups.Count >= HangoutRules.MaxGroups(heat) || Game.GameTime < _nextSpawn) return;
            _nextSpawn = Game.GameTime + HangoutRules.RespawnMs(heat);
            try { Plan(e, p); } catch (Exception ex) { Log.Error("Hangouts plan", ex); }
        }

        // A spot out of your sight, then whoever owns the nearest front owns that street.
        private void Plan(Empire e, Vector3 p)
        {
            HangoutKind kind = HangoutRules.Kind(_rng.Next());
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double a = _rng.NextDouble() * Math.PI * 2.0;
                float r = HangoutRules.SpawnMin + (float)_rng.NextDouble() * (HangoutRules.SpawnMax - HangoutRules.SpawnMin);
                Vector3 want = p + new Vector3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, 0f);
                Vector3 spot = kind == HangoutKind.Cruise ? GTA.World.GetNextPositionOnStreet(want, true) : GTA.World.GetNextPositionOnSidewalk(want);
                if (spot == Vector3.Zero) continue;
                float fromMe = spot.DistanceTo(p);
                if (fromMe < HangoutRules.SpawnMin * 0.8f || fromMe > HangoutRules.SpawnMax * 1.3f) continue;
                bool crowded = false;
                foreach (Group other in _groups) if (other.Where().DistanceTo(spot) < 50f) crowded = true;
                if (crowded) continue;

                string owner = TurfOwner(e, spot);
                if (owner == null) return; // nobody's turf out here
                if (NearOtherSide(e, spot, owner)) continue; // never right on top of the other side's front
                bool mine = owner == Owners.Player;
                CrewDef crew = mine ? null : _content.Crew(owner);
                if (!mine)
                {
                    CrewState cs = Nemesis.Crew(e.State, owner);
                    if (crew == null || cs == null || cs.Manpower < 3) return;
                }

                int size = HangoutRules.GroupSize(kind, _rng.Next());
                if (!HangoutRules.Fits(_registry.PedCount, size, Registry.MaxPeds)) return;
                var g = new Group { Crew = owner, Kind = kind, Spot = spot, Heading = (float)(_rng.NextDouble() * 360.0), Size = size, Seed = _rng.Next(), RequestedAt = Game.GameTime };
                bool car = kind == HangoutKind.Cruise || (kind == HangoutKind.HangOut && HangoutRules.WithCar(_rng.Next()));
                if (car && _registry.CanSpawnVehicle) g.CarModel = PickCar(e, owner);
                if (kind == HangoutKind.Cruise && g.CarModel == null) g.Kind = HangoutKind.Walk;
                for (int k = 0; k < size; k++)
                {
                    Dressing d = mine ? PlayerDressing(g.Seed + k * 131) : Wardrobe.ForCrew(owner, g.Seed + k * 131);
                    g.Dress.Add(d);
                    g.Models.Add(new Model(d != null ? Wardrobe.ModelName(d) : (crew != null && crew.Models != null && crew.Models.Count > 0 ? crew.Models[_rng.Next(crew.Models.Count)] : "a_m_y_genstreet_01")));
                }
                if (g.CarModel != null) g.Models.Add(new Model(g.CarModel));
                foreach (Model m in g.Models) if (m.IsValid) m.Request();
                _groups.Add(g);
                if (!mine && kind == HangoutKind.HangOut) TryClash(e, g, spot);
                return;
            }
        }

        // Where two crews' turfs meet, now and then a group of the neighbours turns up across the road and it kicks off.
        private void TryClash(Empire e, Group a, Vector3 spot)
        {
            if (!HangoutRules.Clash(_rng.Next())) return;
            string other = null;
            float best = HangoutRules.TurfRange;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet) continue;
                string o = e.OwnerOf(def.Id);
                if (!Owners.IsCrew(o) || o == a.Crew) continue;
                float d = def.Door.DistanceTo(spot.X, spot.Y, spot.Z);
                if (d < best) { best = d; other = o; }
            }
            if (other == null) return;
            CrewState ocs = Nemesis.Crew(e.State, other);
            if (ocs == null || ocs.Manpower < 3) return;
            int size = HangoutRules.GroupSize(HangoutKind.HangOut, _rng.Next());
            if (!HangoutRules.Fits(_registry.PedCount, a.Size + size, Registry.MaxPeds)) return;
            double ang = _rng.NextDouble() * Math.PI * 2.0;
            Vector3 there = GTA.World.GetNextPositionOnSidewalk(spot + new Vector3((float)Math.Cos(ang) * HangoutRules.ClashGap, (float)Math.Sin(ang) * HangoutRules.ClashGap, 0f));
            if (there == Vector3.Zero || there.DistanceTo(spot) > 40f) return;
            var b = new Group { Crew = other, Kind = HangoutKind.HangOut, Spot = there, Heading = (float)(_rng.NextDouble() * 360.0), Size = size, Seed = _rng.Next(), RequestedAt = Game.GameTime };
            for (int k = 0; k < size; k++)
            {
                Dressing d = Wardrobe.ForCrew(other, b.Seed + k * 131);
                b.Dress.Add(d);
                CrewDef oc = _content.Crew(other);
                b.Models.Add(new Model(d != null ? Wardrobe.ModelName(d) : (oc != null && oc.Models.Count > 0 ? oc.Models[0] : "a_m_y_genstreet_01")));
            }
            foreach (Model m in b.Models) if (m.IsValid) m.Request();
            a.Rival = b; b.Rival = a;
            _groups.Add(b);
        }

        private void StartClash(Group g, Empire e)
        {
            foreach (Group side in new[] { g, g.Rival })
            {
                side.Alerted = true;
                CrewDef crew = _content.Crew(side.Crew);
                foreach (Ped ped in side.Peds)
                {
                    if (ped == null || !ped.Exists() || ped.IsDead) continue;
                    ped.BlockPermanentEvents = false;
                    ped.Task.ClearAll();
                    ped.Task.FightAgainstHatedTargets(60f);
                    try
                    {
                        if (ped.AttachedBlip == null || !ped.AttachedBlip.Exists())
                        {
                            Blip b = ped.AddBlip();
                            b.Color = crew != null ? (BlipColor)crew.BlipColor : BlipColor.Red;
                            b.Scale = 0.6f;
                            b.Name = crew != null ? crew.Name : "Crew";
                        }
                    }
                    catch (Exception ex) { Log.Error("Clash blip", ex); }
                }
            }
            Log.Write("Hangouts: street fight, " + g.Crew + " vs " + g.Rival.Crew);
        }

        private string TurfOwner(Empire e, Vector3 spot)
        {
            string owner = null;
            float best = HangoutRules.TurfRange;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet) continue;
                string o = e.OwnerOf(def.Id);
                if (o != Owners.Player && !Owners.IsCrew(o)) continue;
                float d = def.Door.DistanceTo(spot.X, spot.Y, spot.Z);
                if (d < best) { best = d; owner = o; }
            }
            return owner;
        }

        public const float OtherSideGap = 80f;

        /// <summary>A front of the other side this close: rivals don't loiter at your door, your people don't loiter at theirs.</summary>
        private bool NearOtherSide(Empire e, Vector3 spot, string owner)
        {
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet) continue;
                string o = e.OwnerOf(def.Id);
                bool other = owner == Owners.Player ? Owners.IsCrew(o) : o == Owners.Player;
                if (other && def.Door.DistanceTo(spot.X, spot.Y, spot.Z) < OtherSideGap) return true;
            }
            return false;
        }

        /// <summary>Your people stay calm, and back you up when you're fighting near them or one of them is hurt.</summary>
        private void TendMine(Group g, Ped me)
        {
            if (g.Alerted) return;
            bool hurt = false;
            foreach (Ped ped in g.Peds) if (ped != null && ped.Exists() && (ped.IsDead || ped.Health < ped.MaxHealth)) hurt = true;
            float dist = g.Where().DistanceTo(me.Position);
            if (!hurt && !((me.IsShooting || me.IsInCombat) && dist < HangoutRules.AlertShots)) return;
            g.Alerted = true;
            foreach (Ped ped in g.Peds)
            {
                if (ped == null || !ped.Exists() || ped.IsDead) continue;
                ped.BlockPermanentEvents = false;
                ped.Task.ClearAll();
                ped.Task.FightAgainstHatedTargets(80f);
            }
        }

        private string PickCar(Empire e, string owner)
        {
            if (owner == Owners.Player)
            {
                // Your people drive the everyday rides of whoever's turf they took it from: a plain sedan, painted yours.
                string[] own = { "washington", "schafter2", "primo2", "baller3" };
                return own[_rng.Next(own.Length)];
            }
            CrewDef crew = _content.Crew(owner);
            CrewState cs = Nemesis.Crew(e.State, owner);
            if (crew == null) return null;
            List<string> rides = crew.RidesFor(Math.Min(1, CrewDef.TierFor(cs != null ? cs.Grudge : 0, e.State.Heat)));
            return rides != null && rides.Count > 0 ? rides[_rng.Next(rides.Count)] : null;
        }

        private static Dressing PlayerDressing(int seed)
        {
            if (!Wardrobe.Enabled) return null;
            return OutfitRules.Choose(Wardrobe.Data, CrewColors.StyleKey(Wardrobe.PlayerStyle()), Wardrobe.PlayerColour(), seed, null);
        }

        private void TrySpawn(Group g, Empire e)
        {
            foreach (Model m in g.Models) if (m.IsValid && !m.IsLoaded) return;
            if (!HangoutRules.Fits(_registry.PedCount, g.Size, Registry.MaxPeds)) return;
            CrewDef crew = g.Mine ? null : _content.Crew(g.Crew);
            CrewState cs = g.Mine ? null : Nemesis.Crew(e.State, g.Crew);
            g.Spawned = true;
            string colourKey = g.Mine ? Wardrobe.PlayerColour() : CrewColors.ForCrew(g.Crew);

            if (g.CarModel != null && _registry.CanSpawnVehicle)
            {
                Model cm = g.Models[g.Models.Count - 1];
                Vector3 at = g.Kind == HangoutKind.Cruise ? g.Spot + new Vector3(0f, 0f, 0.5f) : g.Spot + new Vector3(3.5f, 0f, 0.5f);
                Vehicle v = GTA.World.CreateVehicle(cm, at, g.Heading);
                if (v != null)
                {
                    _registry.Track(v, Markers.MakePlate(0, g.Mine ? "FR" : g.Crew));
                    CrewColor paint = CrewColors.Get(colourKey);
                    v.Mods.CustomPrimaryColor = System.Drawing.Color.FromArgb(paint.R, paint.G, paint.B);
                    v.Mods.CustomSecondaryColor = System.Drawing.Color.FromArgb(paint.R / 3, paint.G / 3, paint.B / 3);
                    v.MaxHealth = 2000; v.Health = 2000; v.BodyHealth = 2000f; v.EngineHealth = 2000f; v.PetrolTankHealth = 2000f;
                    v.PlaceOnGround();
                    if (g.Kind != HangoutKind.Cruise) v.IsEngineRunning = false;
                    g.Car = v;
                }
            }

            int grudge = cs != null ? cs.Grudge : 0;
            int seats = g.Car != null ? Math.Max(1, g.Car.PassengerCapacity + 1) : 0;
            for (int k = 0; k < g.Size; k++)
            {
                if (!_registry.CanSpawnPed) break;
                if (g.Kind == HangoutKind.Cruise && (g.Car == null || k >= seats)) break;
                double a = (k / (double)g.Size) * Math.PI * 2.0;
                Vector3 at = g.Spot + new Vector3((float)Math.Cos(a) * 1.3f, (float)Math.Sin(a) * 1.3f, 0f);
                float face = (float)(Math.Atan2(-(g.Spot.X - at.X), g.Spot.Y - at.Y) * 180.0 / Math.PI); // face each other
                Ped ped = GTA.World.CreatePed(g.Models[k], at, face);
                if (ped == null) continue;
                ped.RelationshipGroup = g.Mine ? Factions.Guards : Factions.CrewGroup(g.Crew);
                _registry.Track(ped);
                if (k < g.Dress.Count) Wardrobe.Apply(ped, g.Dress[k]);
                string gun = crew != null ? crew.WeaponFor(g.Seed + k * 17, 0, grudge) : (k % 2 == 0 ? "pistol" : "microsmg");
                ped.Weapons.Give(Streamer.CrewWeapon(gun), 250, false, true);
                ped.Accuracy = CrewDef.FrontAccuracy(0);
                ped.MaxHealth = Rules.Stats.CrewHealth;
                ped.Health = ped.MaxHealth;
                if (g.Mine) Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, false);
                Function.Call(Hash.SET_PED_KEEP_TASK, ped, true);
                if (g.Kind == HangoutKind.Cruise)
                {
                    ped.SetIntoVehicle(g.Car, k == 0 ? VehicleSeat.Driver : (VehicleSeat)(k - 1));
                    if (k == 0) ped.Task.CruiseWithVehicle(g.Car, 12f, DrivingStyle.Normal);
                    ped.BlockPermanentEvents = true;
                }
                else if (g.Kind == HangoutKind.Walk)
                {
                    ped.BlockPermanentEvents = true;
                    ped.Task.WanderAround(g.Spot, 60f);
                }
                else
                {
                    ped.BlockPermanentEvents = true; // everyone calm until there's a fight (your people join yours)
                    ped.Task.StartScenario(HangoutRules.Scenario(k + g.Seed), at, face);
                }
                g.Peds.Add(ped);
            }
            Log.Write("Hangouts: " + g.Peds.Count + " " + (g.Mine ? "of yours" : g.Crew) + " " + g.Kind + (g.Car != null ? " with a " + g.CarModel : ""));
        }

        private void Tend(Group g, Empire e, Ped me)
        {
            float dist = g.Where().DistanceTo(me.Position);
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
                        if (cs != null && (ped.Killer == me || g.Rival != null)) { cs.Manpower = Math.Max(0, cs.Manpower - 1); if (ped.Killer == me) cs.LastContactDay = e.State.Day; }
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
            Release(g);
        }

        private static void Release(Group g)
        {
            foreach (Model m in g.Models)
                if (m.Hash != Game.GenerateHash(Wardrobe.Male) && m.Hash != Game.GenerateHash(Wardrobe.Female)) m.MarkAsNoLongerNeeded(); // the two character models stay loaded
        }
    }
}
