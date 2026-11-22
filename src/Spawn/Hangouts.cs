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
            public Watch Watch;            // calm, watching you, warning you, or fighting (WatchRules)
            public int WarnStart;          // game time the current warning began
            public bool LastWarned;
            public int LastWatchAt;        // a long gap between looks restarts a warning's fuse
            public int RequestedAt;
            public Group Rival;            // a street fight: the other crew's group across the road
            public StreetCrime Crime;
            public int Stage, StageAt;     // where the crime is up to, and when the current step started
            public Ped Victim;
            public Vehicle Taken;          // a carjacked car (not ours: never deleted)
            public readonly List<Ped> Cops = new List<Ped>();
            public Vehicle CopCar;
            public int PoliceAt;           // a street fight: when the cops get there (0 = not coming)
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
        private int _nextSpawn, _nextCrime;
        private double _fps;
        private static readonly Model CopModel = new Model("s_m_y_cop_01"), CopCarModel = new Model("police");

        /// <summary>INI Attacks.PoliceRespond (Main supplies it).</summary>
        public Func<bool> PoliceOn = () => true;

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
                try
                {
                if (!g.Spawned)
                {
                    TrySpawn(g, e);
                    if (!g.Spawned && Game.GameTime - g.RequestedAt > 15000) { Release(g); _groups.RemoveAt(i); }
                    else if (g.Spawned && g.Peds.Count == 0) { Remove(g); _groups.RemoveAt(i); } // nobody made it out: free the slot
                    continue;
                }
                if (g.Where().DistanceTo(p) > HangoutRules.DespawnRange) { Remove(g); _groups.RemoveAt(i); continue; }
                if (g.Rival != null && !g.Alerted && g.Spawned && g.Rival.Spawned) StartClash(g, e);
                try
                {
                    if (g.Crime != StreetCrime.None) RunCrime(g, me);
                    if (g.PoliceAt > 0 && Game.GameTime >= g.PoliceAt) SendPolice(g);
                    if (g.Rival != null && g.CopCar != null && g.CopCar.Exists() && g.Cops.Count > 0 && g.CopCar.Position.DistanceTo(g.Spot) < 30f && g.Cops[0].IsInVehicle())
                        foreach (Ped cop in g.Cops) if (Alive(cop)) { cop.Task.LeaveVehicle(g.CopCar, false); Ped target = FirstAlive(g) ?? FirstAlive(g.Rival); if (target != null) cop.Task.FightAgainst(target); }
                }
                catch (Exception ex) { Log.Error("Street crime", ex); g.Crime = StreetCrime.None; g.PoliceAt = 0; }
                if (g.Mine) TendMine(g, me); else Tend(g, e, me);
                }
                catch (Exception ex)
                {
                    // One bad group never takes the whole system down: it's cleared and the rest carry on.
                    Log.Error("Hangout group", ex);
                    try { Remove(g); } catch { }
                    if (i < _groups.Count) _groups.RemoveAt(i);
                }
            }

            int heat = e.State.Heat;
            double fps = Game.FPS;
            if (fps > 0 && fps < 300) _fps = _fps <= 0 ? fps : _fps * 0.8 + fps * 0.2; // smoothed over a few seconds
            if (_groups.Count >= HangoutRules.MaxGroups(heat, _fps) || Game.GameTime < _nextSpawn) return;
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
                bool car = kind == HangoutKind.Cruise || (kind == HangoutKind.HangOut && HangoutRules.WithCar(_rng.Next()));
                string carModel = car && _registry.CanSpawnVehicle ? PickCar(e, owner) : null;
                if (kind == HangoutKind.Cruise && carModel == null) kind = HangoutKind.Walk; // no car to cruise in: walk instead
                // The crime is picked once the group's car is settled, so a chase never loses its car.
                StreetCrime crime = StreetCrime.None;
                if (!mine && e.InTheGame() && Game.GameTime >= _nextCrime && !AnyCrime())
                {
                    crime = HangoutRules.CrimeFor(kind, _rng.Next());
                    if ((crime == StreetCrime.Chase) && !PoliceOn()) crime = StreetCrime.None;
                    if (crime == StreetCrime.Mugging || crime == StreetCrime.Carjack || crime == StreetCrime.Shooting) size = 1;
                    if (crime == StreetCrime.Kidnap) size = Math.Max(2, size);
                    if (crime != StreetCrime.None) _nextCrime = Game.GameTime + HangoutRules.CrimeCooldownMs;
                }
                if (!HangoutRules.Fits(_registry.PedCount, size, Registry.MaxPeds)) return;
                var g = new Group { Crew = owner, Kind = kind, Spot = spot, Heading = (float)(_rng.NextDouble() * 360.0), Size = size, Seed = _rng.Next(), RequestedAt = Game.GameTime, Crime = crime };
                if (crime == StreetCrime.Chase) { CopModel.Request(); CopCarModel.Request(); }
                g.CarModel = carModel;
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
            if (PoliceOn() && HangoutRules.PoliceCome(_rng.Next()))
            {
                // A shootout in the street draws the police, like it would in GTA.
                g.PoliceAt = Game.GameTime + HangoutRules.PoliceDelayMs;
                CopModel.Request(); CopCarModel.Request();
            }
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
                    v.MaxHealth = 2000; v.Health = 2000; v.BodyHealth = 1000f; v.EngineHealth = 1000f; v.PetrolTankHealth = 1000f; Function.Call(Hash.SET_VEHICLE_EXPLODES_ON_HIGH_EXPLOSION_DAMAGE, v, false);
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
                if (g.Kind == HangoutKind.Cruise && g.Car == null) { g.Kind = HangoutKind.Walk; g.Crime = StreetCrime.None; } // the car didn't spawn: walk instead
                if (g.Kind == HangoutKind.Cruise && k >= seats) break;
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
                        // You on foot, your car, or your own people (guards, bodyguards, your crew on the street).
                        Entity killer = ped.Killer;
                        Ped killerPed = killer as Ped;
                        bool byYou = killer == me || (killer != null && (killer == me.CurrentVehicle || killer == me.LastVehicle))
                                     || (killerPed != null && killerPed.RelationshipGroup == Factions.Guards);
                        if (cs != null && (byYou || g.Rival != null)) cs.Manpower = Math.Max(0, cs.Manpower - 1);
                        if (cs != null && byYou && e.InTheGame()) Nemesis.StreetKill(e.State, cs); // angrier crew, hotter streets (capped daily)
                    }
                }
                else if (ped.Health < ped.MaxHealth) hurt = true;
            }
            if (g.Alerted) return;
            bool inTheGame = e.InTheGame();
            float near = Math.Min(dist, WatchFx.Nearest(g.Peds));
            // Busy crews (in a street fight, mid-crime or cruising) skip the warning: only shots or a hurt one set them off.
            if (g.Rival != null || g.Crime != StreetCrime.None || g.Kind == HangoutKind.Cruise)
            {
                if (StreamRules.CrewProvoked(inTheGame, false, WatchFx.HeardShot(near), hurt)) AlertGroup(g);
                return;
            }
            // Everyone else on the street: notice, warn, then fight, like a front's soldiers but on a shorter fuse.
            bool aiming = false, seen = false;
            bool aimingNow = WatchFx.Aiming();
            float scale = WatchFx.SightScale();
            foreach (Ped ped in g.Peds)
                if (aimingNow && Alive(ped) && WatchFx.AimedAt(ped)) { aiming = true; break; }
            if (near <= WatchRules.WatchRange(HangoutRules.AlertOnFoot * scale) + 5f)
                foreach (Ped ped in g.Peds)
                    if (Alive(ped) && WatchFx.Sees(ped, g.Watch == Watch.Calm)) { seen = true; break; }
            int gt = Game.GameTime;
            if (g.Watch == Watch.Warning && gt - g.LastWatchAt > 3000) { g.WarnStart = gt; g.LastWarned = false; }
            g.LastWatchAt = gt;
            CrewState angry = Nemesis.Crew(e.State, g.Crew);
            int limit = WatchRules.StreetWarnMs(g.Crew, WatchRules.Temper(e.State.Heat, angry != null ? angry.Grudge : 0));
            int warned = g.Watch == Watch.Warning ? gt - g.WarnStart : 0;
            Watch next = WatchRules.Next(g.Watch, near, me.IsOnFoot, aiming, WatchFx.HeardShot(near), hurt, inTheGame,
                                         warned, limit, HangoutRules.AlertOnFoot, seen, scale, WatchFx.Nearest(g.Peds));
            if (next == Watch.Hostile) { g.Watch = Watch.Hostile; AlertGroup(g); return; }
            if (next != g.Watch) { g.Watch = next; EnterWatch(g, gt); return; }
            if (next == Watch.Warning && inTheGame && !g.LastWarned && WatchRules.LastWarningDue(warned, limit))
            {
                g.LastWarned = true;
                WatchFx.Say(_content, g.Crew, "lastWarning");
            }
        }

        /// <summary>Re-tasks a street crew when its mood changes: eyes on you, guns up, or back to hanging out.</summary>
        private void EnterWatch(Group g, int gt)
        {
            if (g.Watch == Watch.Warning)
            {
                g.WarnStart = gt;
                g.LastWarned = false;
                WatchFx.Say(_content, g.Crew, "warn" + g.Crew);
            }
            int k = 0;
            foreach (Ped ped in g.Peds)
            {
                int index = k++;
                if (!Alive(ped)) continue;
                ped.Task.ClearAll();
                if (g.Watch == Watch.Warning) { WatchFx.Raise(ped); WatchFx.Blip(_content, ped, g.Crew); continue; }
                WatchFx.DropBlip(ped);
                if (g.Watch == Watch.Watching) WatchFx.Eye(ped);
                else
                {
                    ped.Task.ClearLookAt();
                    if (g.Kind == HangoutKind.Walk) ped.Task.WanderAround(g.Spot, 60f);
                    else ped.Task.StartScenario(HangoutRules.Scenario(index + g.Seed), ped.Position, ped.Heading);
                }
            }
        }

        /// <summary>The whole group turns on you.</summary>
        private void AlertGroup(Group g)
        {
            g.Alerted = true;
            g.Watch = Watch.Hostile;
            foreach (Ped ped in g.Peds)
            {
                if (ped == null || !ped.Exists() || ped.IsDead) continue;
                ped.BlockPermanentEvents = false;
                ped.Task.ClearAll();
                ped.Task.FightAgainstHatedTargets(80f);
                WatchFx.Blip(_content, ped, g.Crew);
            }
        }

        private bool AnyCrime() { foreach (Group g in _groups) if (g.Crime != StreetCrime.None) return true; return false; }

        // ---------------- street crime: only ever the crews' own people doing it ----------------

        private void RunCrime(Group g, Ped me)
        {
            Ped crook = null;
            foreach (Ped p in g.Peds) if (p != null && p.Exists() && !p.IsDead) { crook = p; break; }
            if (crook == null || g.Alerted) { g.Crime = StreetCrime.None; return; } // stopped, or the fight's with you now
            int now = Game.GameTime;
            switch (g.Crime)
            {
                case StreetCrime.Mugging:
                case StreetCrime.Shooting:
                    if (g.Stage == 0)
                    {
                        g.Victim = Civilian(crook.Position, 45f);
                        if (g.Victim == null) { g.Crime = StreetCrime.None; return; }
                        crook.Task.ClearAll();
                        // Walk up behind them (TASK_GOTO_ENTITY_OFFSET_XY: duration, radius, x, y, speed, flags).
                        Function.Call(Hash.TASK_GOTO_ENTITY_OFFSET_XY, crook, g.Victim, 30000, 0.5f, 0f, -1.5f, 1.0f, 0);
                        g.Stage = 1; g.StageAt = now;
                    }
                    else if (g.Stage == 1)
                    {
                        if (!Alive(g.Victim) || now - g.StageAt > 30000) { EndCrime(g, crook); return; }
                        if (crook.Position.DistanceTo(g.Victim.Position) < (g.Crime == StreetCrime.Shooting ? 12f : 3f))
                        {
                            crook.BlockPermanentEvents = false;
                            if (g.Crime == StreetCrime.Shooting) crook.Task.FightAgainst(g.Victim);
                            else { crook.Task.AimAt(g.Victim, 6000); g.Victim.Task.HandsUp(6000); }
                            g.Stage = 2; g.StageAt = now;
                        }
                    }
                    else if (now - g.StageAt > 7000)
                    {
                        if (Alive(g.Victim)) g.Victim.Task.FleeFrom(crook, 15000);
                        EndCrime(g, crook);
                    }
                    break;

                case StreetCrime.Carjack:
                    if (g.Stage == 0)
                    {
                        g.Taken = CivilianCar(crook.Position, 55f);
                        if (g.Taken == null) { g.Crime = StreetCrime.None; return; }
                        crook.BlockPermanentEvents = false;
                        Function.Call(Hash.TASK_ENTER_VEHICLE, crook, g.Taken, 20000, -1, 2.0f, 8, 0); // run up and drag the driver out
                        g.Stage = 1; g.StageAt = now;
                    }
                    else if (g.Stage == 1)
                    {
                        if (g.Taken == null || !g.Taken.Exists() || now - g.StageAt > 25000) { EndCrime(g, crook); return; }
                        if (crook.IsInVehicle(g.Taken)) { crook.Task.CruiseWithVehicle(g.Taken, 30f, DrivingStyle.Rushed); g.Stage = 2; }
                    }
                    break;

                case StreetCrime.Chase:
                    if (g.Stage == 0 && g.Car != null && g.Car.Exists() && CopModel.IsLoaded && CopCarModel.IsLoaded && _registry.CanSpawnVehicle && HangoutRules.Fits(_registry.PedCount, 2, Registry.MaxPeds))
                    {
                        Ped driver = g.Car.GetPedOnSeat(VehicleSeat.Driver);
                        if (driver == null) { g.Crime = StreetCrime.None; return; }
                        Vector3 behind = GTA.World.GetNextPositionOnStreet(g.Car.Position - g.Car.ForwardVector * 30f, true);
                        Vehicle cop = SpawnCopCar(g, behind, g.Car.Heading);
                        if (cop == null) { g.Crime = StreetCrime.None; return; }
                        foreach (Ped p in g.Peds) if (p != null && p.Exists()) p.BlockPermanentEvents = false;
                        driver.Task.CruiseWithVehicle(g.Car, 35f, DrivingStyle.Rushed); // floor it
                        Ped copDriver = cop.GetPedOnSeat(VehicleSeat.Driver);
                        if (copDriver != null) Function.Call(Hash.TASK_VEHICLE_CHASE, copDriver, driver);
                        g.Stage = 1;
                        Log.Write("Hangouts: police chasing a " + g.Crew + " car");
                    }
                    else if (g.Stage == 0 && now - g.RequestedAt > 20000) g.Crime = StreetCrime.None;
                    break;

                case StreetCrime.Kidnap:
                    if (g.Car == null || !g.Car.Exists()) { g.Crime = StreetCrime.None; return; }
                    Ped grabber = g.Peds.Count > 1 && Alive(g.Peds[1]) ? g.Peds[1] : null;
                    Ped wheel = g.Car.GetPedOnSeat(VehicleSeat.Driver);
                    if (grabber == null || wheel == null) { g.Crime = StreetCrime.None; return; }
                    if (g.Stage == 0)
                    {
                        g.Victim = Civilian(g.Car.Position, 60f);
                        if (g.Victim == null) { g.Crime = StreetCrime.None; return; }
                        wheel.Task.DriveTo(g.Car, g.Victim.Position, 8f, 12f, DrivingStyle.Normal);
                        g.Stage = 1; g.StageAt = now;
                    }
                    else if (g.Stage == 1)
                    {
                        if (!Alive(g.Victim) || now - g.StageAt > 40000) { EndCrime(g, wheel); return; }
                        if (g.Car.Position.DistanceTo(g.Victim.Position) < 12f)
                        {
                            grabber.BlockPermanentEvents = false;
                            grabber.Task.LeaveVehicle(g.Car, false);
                            g.Stage = 2; g.StageAt = now;
                        }
                    }
                    else if (g.Stage == 2)
                    {
                        if (!Alive(g.Victim) || now - g.StageAt > 25000) { EndCrime(g, wheel); return; }
                        if (!grabber.IsInVehicle())
                        {
                            if (grabber.Position.DistanceTo(g.Victim.Position) > 2.5f) grabber.Task.GoTo(g.Victim, Vector3.Zero, 10000);
                            else
                            {
                                grabber.Task.AimAt(g.Victim, 4000);
                                Function.Call(Hash.TASK_ENTER_VEHICLE, g.Victim, g.Car, 10000, 2, 2.0f, 1, 0); // into the back, at gunpoint
                                Function.Call(Hash.TASK_ENTER_VEHICLE, grabber, g.Car, 10000, 1, 2.0f, 1, 0);
                                g.Stage = 3; g.StageAt = now;
                            }
                        }
                    }
                    else if (g.Stage == 3)
                    {
                        bool done = g.Victim.IsInVehicle(g.Car) && grabber.IsInVehicle(g.Car);
                        if (done || now - g.StageAt > 15000)
                        {
                            wheel.Task.CruiseWithVehicle(g.Car, 30f, DrivingStyle.Rushed);
                            Log.Write("Hangouts: " + g.Crew + " kidnapping " + (done ? "done" : "abandoned"));
                            g.Crime = StreetCrime.None;
                        }
                    }
                    break;
            }
        }

        private void EndCrime(Group g, Ped crook)
        {
            if (crook != null && crook.Exists() && !crook.IsDead && !crook.IsInVehicle()) crook.Task.WanderAround(crook.Position, 80f);
            g.Crime = StreetCrime.None;
        }

        private static bool Alive(Ped p) { return p != null && p.Exists() && !p.IsDead; }

        private static Ped FirstAlive(Group g) { if (g == null) return null; foreach (Ped p in g.Peds) if (Alive(p)) return p; return null; }

        /// <summary>An ordinary pedestrian nearby: not you, not anyone of ours, not someone a mission owns.</summary>
        private Ped Civilian(Vector3 at, float radius)
        {
            Ped best = null;
            float bestD = radius;
            foreach (Ped p in GTA.World.GetNearbyPeds(at, radius))
            {
                if (p == null || !p.Exists() || p.IsDead || p.IsPlayer || !p.IsHuman || p.IsInVehicle() || _registry.Owns(p)) continue;
                if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, p)) continue;
                float d = p.Position.DistanceTo(at);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>A passing car with an ordinary driver: never yours, never ours, never a mission's.</summary>
        private Vehicle CivilianCar(Vector3 at, float radius)
        {
            Vehicle mine = Game.Player.Character.CurrentVehicle;
            foreach (Vehicle v in GTA.World.GetNearbyVehicles(at, radius))
            {
                if (v == null || !v.Exists() || v == mine || v.IsDead) continue;
                if (Function.Call<bool>(Hash.IS_ENTITY_A_MISSION_ENTITY, v)) continue;
                Ped d = v.GetPedOnSeat(VehicleSeat.Driver);
                if (d == null || !d.Exists() || d.IsPlayer || _registry.Owns(d)) continue;
                if (v.PassengerCapacity < 1) continue; // cars, not bikes or buses
                return v;
            }
            return null;
        }

        private Vehicle SpawnCopCar(Group g, Vector3 at, float heading)
        {
            if (at == Vector3.Zero || !_registry.CanSpawnVehicle) return null;
            Vehicle v = GTA.World.CreateVehicle(CopCarModel, at, heading);
            if (v == null) return null;
            _registry.Track(v, Markers.MakePlate(0, "PD"));
            v.IsSirenActive = true;
            g.CopCar = v;
            for (int i = 0; i < 2 && _registry.CanSpawnPed; i++)
            {
                Ped cop = GTA.World.CreatePed(CopModel, at + new Vector3(0f, 0f, 2f), heading);
                if (cop == null) continue;
                // Real police (the game's COP group): shoot them and the game gives you a wanted level, like any cop.
                _registry.TrackKeepGroup(cop);
                cop.SetIntoVehicle(v, i == 0 ? VehicleSeat.Driver : VehicleSeat.Passenger);
                cop.Weapons.Give(WeaponHash.Pistol, 200, true, true);
                g.Cops.Add(cop);
            }
            return v;
        }

        // A street fight's police response: a squad car rolls in, siren going, and the cops go for whoever's still fighting.
        private void SendPolice(Group g)
        {
            g.PoliceAt = 0;
            if (!CopModel.IsLoaded || !CopCarModel.IsLoaded || !HangoutRules.Fits(_registry.PedCount, 2, Registry.MaxPeds)) return;
            double a = _rng.NextDouble() * Math.PI * 2.0;
            Vector3 from = GTA.World.GetNextPositionOnStreet(g.Spot + new Vector3((float)Math.Cos(a) * 110f, (float)Math.Sin(a) * 110f, 0f), true);
            Vehicle car = SpawnCopCar(g, from, 0f);
            if (car == null) return;
            Ped driver = car.GetPedOnSeat(VehicleSeat.Driver);
            if (driver != null) driver.Task.DriveTo(car, g.Spot, 20f, 25f, DrivingStyle.Rushed);
            g.StageAt = Game.GameTime;
            Log.Write("Hangouts: police responding to a street fight");
        }

        private void Remove(Group g)
        {
            foreach (Ped cop in g.Cops) if (cop != null && cop.Exists()) _registry.Despawn(cop);
            if (g.CopCar != null && g.CopCar.Exists()) _registry.Despawn(g.CopCar);
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
