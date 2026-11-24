// Fronts & Rivals: the live defense. When you're within 300 m of a front during its attack window,
// the crew's squad drives in from a road ~400 m away, gets out and pushes for the door; you fight beside your guards.
//   - Timer bars bottom right: attackers left, front integrity (100 -> 0)
//   - Integrity drops 1/s per attacker within 20 m of the door while no guard of yours is alive within 25 m
//   - Win: every attacker dead or fled. Lose: integrity reaches 0.
//   - The leader is a real ped who flees below 30% squad strength. Kill them and they're gone for good.
//   - You die, leave beyond 500 m, or a mission starts: peds go and the rest is auto-resolved. You never lose
//     a front just because you died.
//   - Police (INI): 90 s in, a police incident is created on the attackers.
// A state machine stepped every 0.5 s from the Streamer job; no Wait loops.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI;
using LemonUI.TimerBars;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Attacks
{
    public sealed class LiveDefense
    {
        public sealed class Result
        {
            public bool Finished;          // fought to the end (else aborted: auto-resolve the rest)
            public bool DefendersWon;
            public int AttackersLost;
            public int AttackersLeft;
            public bool LeaderKilled;
            public bool LeaderEscaped;
            public Dictionary<int, int> KillsByGuard = new Dictionary<int, int>();
        }

        private enum Stage { Loading, Driving, Fighting, Over }

        private const float DoorRadius = 20f, GuardRadius = 25f, AbortRange = 500f, ArriveRange = 30f, LeaderGoneRange = 180f;
        private const int LoadTimeoutMs = 8000, StuckMs = 8000, MaxDriveMs = 90000, PoliceAfterMs = 90000;
        private const int FightTimeoutMs = 6 * 60000;   // a fight that runs this long ends and the rest is auto-resolved
        private const int PushEveryMs = 4000;           // re-send attackers who aren't near the door
        private const float PushRange = 45f;            // closer than this to the door: they fight on their own
        private const int LeaderFleeMaxMs = 30000;      // a fleeing leader still nearby after this long has got away

        private readonly Registry _registry;
        private readonly Streamer _streamer;
        private readonly ObjectPool _barsPool = new ObjectPool();
        private readonly TimerBarCollection _bars = new TimerBarCollection();
        private readonly TimerBar _attackersBar = new TimerBar("ATTACKERS", "0");
        private readonly TimerBarProgress _integrityBar = new TimerBarProgress("FRONT");

        private Stage _stage = Stage.Over;
        private AttackState _attack;
        private FrontDef _front;
        private CrewDef _crew;
        private CrewMember _leaderMember;
        private bool _police;
        private Vector3 _door;
        private Vector3 _spawn;
        private float _spawnHeading;

        private readonly List<Model> _models = new List<Model>();
        private int _loadStarted, _started, _lastIntegrityTick, _lastPush, _leaderFleeStart;
        private readonly Dictionary<Vehicle, int> _stuckSince = new Dictionary<Vehicle, int>();
        private readonly List<Blip> _pedBlips = new List<Blip>();
        private List<string> _convoy = new List<string>();
        private int _tier;
        private readonly List<Dressing> _dress = new List<Dressing>();

        /// <summary>Street heat now (Main supplies it): part of how hard a crew comes for you.</summary>
        public Func<int> Heat;
        private int _grudge;
        private bool _explosiveGiven;
        /// <summary>How much a crew hates you right now (Main sets it): angrier crews bring heavier guns.</summary>
        public Func<string, int> Grudge;
        private readonly List<Vehicle> _cars = new List<Vehicle>();
        private readonly List<Ped> _attackers = new List<Ped>();
        private readonly HashSet<Ped> _counted = new HashSet<Ped>();
        private Ped _leader;
        private bool _leaderFleeing;
        private int _incident;
        private float _integrity = 100f;
        private readonly Result _result = new Result();
        private readonly List<Blip> _blips = new List<Blip>();

        public LiveDefense(Registry registry, Streamer streamer)
        {
            _registry = registry;
            _streamer = streamer;
            _bars.Add(_attackersBar);
            _bars.Add(_integrityBar);
            _barsPool.Add(_bars);
            _bars.Visible = false;
        }

        public bool Active { get { return _stage != Stage.Over; } }
        public string FrontId { get { return _front != null ? _front.Id : null; } }

        /// <summary>Starts the fight. Returns false if it can't (no spawn road found, budget full): auto-resolve instead.</summary>
        public bool Start(AttackState attack, FrontDef front, CrewDef crew, CrewMember leader, bool police)
        {
            _attack = attack;
            _grudge = Grudge != null ? Grudge(crew.Id) : 0;
            _explosiveGiven = false;
            _front = front;
            _crew = crew;
            _leaderMember = leader;
            _police = police;
            _door = new Vector3(front.Door.X, front.Door.Y, front.Door.Z);
            ClearLeftovers();
            ResetResult();
            if (!FindSpawn()) { Log.Write("LiveDefense: no road spawn found near " + front.Id); return false; }

            // Request models: one car model per car, ped models for the squad.
            _models.Clear();
            // The crew's own rides: Ironside on bikes (two up), the others in their signature cars.
            _tier = CrewDef.TierFor(_grudge, Heat != null ? Heat() : 0); // weak / normal / powerful rides by revenge intensity
            _convoy = crew.ConvoyPlan(attack.Squad, _tier);
            Log.Write("LiveDefense: " + crew.Id + " convoy tier " + _tier + ": " + string.Join(", ", _convoy.ToArray()));
            foreach (string vehicle in _convoy) _models.Add(new Model(vehicle));
            _dress.Clear();
            for (int i = 0; i < attack.Squad; i++)
            {
                // Everyone in the crew's colour, the leader too.
                Dressing dr = Wardrobe.ForCrew(crew.Id, front.Number * 53 + i * 7 + attack.Squad, i == 0 && leader != null); // the leader dressed a step up
                _dress.Add(dr);
                _models.Add(new Model(dr != null ? Wardrobe.ModelName(dr) : (i == 0 && leader != null && leader.Model != null ? leader.Model : crew.Models[i % crew.Models.Count])));
            }
            foreach (Model m in _models) if (m.IsValid) m.Request();
            _loadStarted = Game.GameTime;
            _stage = Stage.Loading;
            _streamer.LiveFrontId = front.Id;
            Log.Write("LiveDefense: " + crew.Name + " x" + attack.Squad + " heading for " + front.Id);
            return true;
        }

        /// <summary>Deletes whatever is left of the previous fight (bodies, survivors, wrecks).</summary>
        private void ClearLeftovers()
        {
            foreach (Ped p in _attackers) if (p != null && p.Exists()) _registry.Despawn(p);
            foreach (Vehicle v in _cars) if (v != null && v.Exists()) _registry.Despawn(v);
            _attackers.Clear();
            _cars.Clear();
            ReleaseModels();
        }

        private void ReleaseModels()
        {
            foreach (Model m in _models) m.MarkAsNoLongerNeeded();
            _models.Clear();
        }

        private void ResetResult()
        {
            _result.Finished = false; _result.DefendersWon = false; _result.AttackersLost = 0; _result.AttackersLeft = 0;
            _result.LeaderKilled = false; _result.LeaderEscaped = false; _result.KillsByGuard.Clear();
            _cars.Clear(); _attackers.Clear(); _counted.Clear(); _leader = null; _leaderFleeing = false; _incident = 0; _integrity = 100f;
            _stuckSince.Clear(); _lastPush = 0; _leaderFleeStart = 0;
        }

        /// <summary>A road ~400 m from the door, on the side away from you, so the convoy arrives unseen.</summary>
        private bool FindSpawn()
        {
            if (_front.ConvoySpawns.Count > 0)
            {
                Spot s = _front.ConvoySpawns[new Random().Next(_front.ConvoySpawns.Count)];
                _spawn = new Vector3(s.X, s.Y, s.Z);
                _spawnHeading = s.H;
                return true;
            }
            Vector3 player = Game.Player.Character.Position;
            Vector3 away = _door - player;
            away.Z = 0;
            if (away.Length() < 1f) away = new Vector3(1f, 0f, 0f);
            away.Normalize();
            for (int attempt = 0; attempt < 6; attempt++)
            {
                double ang = attempt * Math.PI / 3.0;
                var dir = new Vector3((float)(away.X * Math.Cos(ang) - away.Y * Math.Sin(ang)), (float)(away.X * Math.Sin(ang) + away.Y * Math.Cos(ang)), 0f);
                Vector3 probe = _door + dir * 400f;
                Vector3 road = GTA.World.GetNextPositionOnStreet(probe, true);
                float d = road.DistanceTo(_door);
                if (road != Vector3.Zero && d > 300f && d < 520f && road.DistanceTo(player) > 200f)
                {
                    _spawn = road;
                    Vector3 to = _door - road;
                    _spawnHeading = (float)(-Math.Atan2(to.X, to.Y) * 180.0 / Math.PI);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every 0.5 s while active. Returns a result when the fight is over, otherwise null.</summary>
        public Result Update()
        {
            if (_stage == Stage.Over) return null;
            Ped player = Game.Player.Character;
            if (player.IsDead || player.Position.DistanceTo(_door) > AbortRange) return Abort("player " + (player.IsDead ? "died" : "left"));

            if (_stage == Stage.Loading)
            {
                bool all = true;
                foreach (Model m in _models) if (m.IsValid && !m.IsLoaded) all = false;
                if (!all && Game.GameTime - _loadStarted < LoadTimeoutMs) return null;
                SpawnConvoy();
                if (_attackers.Count == 0) return Abort("convoy failed to spawn");
                _stage = Stage.Driving;
                _started = Game.GameTime;
                _lastIntegrityTick = Game.GameTime;
                _bars.Visible = true;
                return null;
            }

            CountCasualties();
            UpdateBlips();
            int alive = AliveAttackers();
            _attackersBar.Info = alive.ToString();
            _integrityBar.Progress = _integrity;

            if (_stage == Stage.Driving) UpdateDriving();
            PushToDoor();
            UpdatePedBlips();
            if (Game.GameTime - _started > FightTimeoutMs) return Abort("ran too long");
            if (_police && _incident == 0 && Game.GameTime - _started > PoliceAfterMs) CallPolice();
            UpdateLeader(alive);
            UpdateIntegrity();

            if (alive == 0) return Finish(true);
            if (_integrity <= 0f) return Finish(false);
            return null;
        }

        private void SpawnConvoy()
        {
            int squad = _attack.Squad;
            int cars = _convoy.Count;
            int carModel = 0, pedModel = cars;
            int placed = 0;
            for (int c = 0; c < cars && placed < squad; c++)
            {
                if (!_registry.CanSpawnVehicle || !_registry.CanSpawnPed) break;
                Model vm = _models[carModel + c];
                if (!vm.IsLoaded) continue;
                Vector3 pos = _spawn + new Vector3(0f, 0f, 0.5f) - HeadingVector(_spawnHeading) * (9f * c);
                Vehicle v = GTA.World.CreateVehicle(vm, pos, _spawnHeading);
                if (v == null) continue;
                _registry.Track(v, Markers.MakePlate(_front.Number, _crew.Id));
                Function.Call(Hash.SET_VEHICLE_COLOURS, v, _crew.Paint, _crew.Paint);
                CrewColor paint = CrewColors.Get(CrewColors.ForCrew(_crew.Id)); // exactly the crew's colour, like its clothes
                v.Mods.CustomPrimaryColor = System.Drawing.Color.FromArgb(paint.R, paint.G, paint.B);
                v.Mods.CustomSecondaryColor = System.Drawing.Color.FromArgb(paint.R / 3, paint.G / 3, paint.B / 3);
                // Tougher than a street car: full health (1000 is the documented max) and no blowing up from a single explosion.
                v.MaxHealth = 2000; v.Health = 2000;
                v.BodyHealth = 1000f; v.EngineHealth = 1000f; v.PetrolTankHealth = 1000f; Function.Call(Hash.SET_VEHICLE_EXPLODES_ON_HIGH_EXPLOSION_DAMAGE, v, false);
                v.PlaceOnGround();
                _cars.Add(v);
                int seats = Math.Min(_crew.SeatsOf(_convoy[c]), v.PassengerCapacity + 1);
                for (int seat = 0; seat < seats && placed < squad; seat++)
                {
                    if (!_registry.CanSpawnPed) break;
                    Model pm = _models[pedModel + placed];
                    if (!pm.IsLoaded) pm = _models[pedModel];
                    Ped ped = GTA.World.CreatePed(pm, pos + new Vector3(0, 0, 2f), _spawnHeading);
                    if (ped == null) continue;
                    if (placed < _dress.Count && pm == _models[pedModel + placed]) Wardrobe.Apply(ped, _dress[placed], placed > 0 || _leaderMember == null); // masks up, except the leader: you see who's leading
                    ped.RelationshipGroup = Factions.CrewGroup(_crew.Id);
                    _registry.Track(ped);
                    ped.SetIntoVehicle(v, seat == 0 ? VehicleSeat.Driver : (VehicleSeat)(seat - 1));
                    // Bigger fronts and angrier crews bring heavier guns; the leader carries the best and a full vest;
                    // now and then one of them brings the crew's explosive (one per squad at most).
                    string gun = placed == 0 ? _crew.LeaderWeapon() : _crew.WeaponFor(_front.Number * 31 + placed, _front.Tier, _grudge);
                    ped.Weapons.Give(Streamer.CrewWeapon(gun), 300, true, true);
                    if (placed > 0 && !_explosiveGiven)
                    {
                        string boom = _crew.ExplosiveFor(_front.Number * 131 + placed + _attack.Squad);
                        if (boom != null)
                        {
                            _explosiveGiven = true;
                            bool launcher = boom == "rpg" || boom == "grenadelauncher";
                            ped.Weapons.Give(Streamer.CrewWeapon(boom), launcher ? 3 : 2, false, true);
                            Log.Write("LiveDefense: one of the " + _crew.Name + " brought a " + boom);
                        }
                    }
                    ped.Accuracy = 20 + (int)(_crew.SoldierPower);
                    ped.Armor = placed == 0 ? 100 : (int)(_crew.SoldierPower * 2);
                    ped.MaxHealth = FrontsAndRivals.Rules.Stats.CrewHealth; // twice an ordinary ped, like everyone else's
                    ped.Health = ped.MaxHealth;
                    ped.BlockPermanentEvents = false;
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 0, true);   // CA_USE_COVER
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped, 5, true);   // CA_ALWAYS_FIGHT (citizenfx: CA_ALWAYS_FIGHT = 5)
                    Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped, 0, false);
                    _attackers.Add(ped);
                    if (placed == 0) _leader = ped;
                    placed++;
                }
                Ped driver = v.Driver;
                if (driver != null) driver.Task.DriveTo(v, _door, ArriveRange * 0.5f, 22f, DrivingStyle.Rushed);
            }
            ReleaseModels();
            Log.Write("LiveDefense: convoy of " + _cars.Count + " cars, " + _attackers.Count + " attackers");
        }

        private static Vector3 HeadingVector(float heading)
        {
            double r = heading * Math.PI / 180.0;
            return new Vector3((float)-Math.Sin(r), (float)Math.Cos(r), 0f);
        }

        /// <summary>Cars that arrive, stall for 8 s or take over 90 s unload; everyone fights.</summary>
        private void UpdateDriving()
        {
            bool allOut = true;
            foreach (Vehicle v in _cars)
            {
                if (v == null || !v.Exists()) continue;
                bool occupied = false;
                foreach (Ped p in _attackers) if (p.Exists() && !p.IsDead && p.CurrentVehicle == v) occupied = true;
                if (!occupied) continue;
                allOut = false;
                bool arrived = v.Position.DistanceTo(_door) < ArriveRange;
                bool slow = v.Speed < 1.5f;
                int since;
                if (!slow) _stuckSince.Remove(v);
                else if (!_stuckSince.TryGetValue(v, out since)) _stuckSince[v] = Game.GameTime;
                bool stuck = _stuckSince.TryGetValue(v, out since) && Game.GameTime - since > StuckMs;
                if (arrived || stuck || Game.GameTime - _started > MaxDriveMs) Unload(v);
            }
            if (allOut) _stage = Stage.Fighting;
        }

        private void Unload(Vehicle v)
        {
            foreach (Ped p in _attackers)
            {
                if (!p.Exists() || p.IsDead || p.CurrentVehicle != v) continue;
                p.Task.ClearAll();
                p.Task.LeaveVehicle(v, false);
                p.AlwaysKeepTask = true;
            }
        }

        /// <summary>Attackers on foot head for the door; once close, they fight whoever they hate (you and your guards).</summary>
        private void PushToDoor()
        {
            if (Game.GameTime - _lastPush < PushEveryMs) return;
            _lastPush = Game.GameTime;
            foreach (Ped p in _attackers)
            {
                if (p == null || !p.Exists() || p.IsDead || p == _leader && _leaderFleeing) continue;
                if (p.IsInVehicle()) continue;
                if (p.Position.DistanceTo(_door) > PushRange)
                {
                    if (!p.IsInCombat) p.Task.GoTo(_door, -1);
                }
                else if (!p.IsInCombat)
                {
                    p.Task.FightAgainstHatedTargets(80f);
                }
            }
        }

        /// <summary>Small red blips on attackers on foot within 200 m, so nobody has to hunt blind.</summary>
        private void UpdatePedBlips()
        {
            for (int i = _pedBlips.Count; i < _attackers.Count; i++) _pedBlips.Add(null);
            for (int i = 0; i < _attackers.Count; i++)
            {
                Ped p = _attackers[i];
                bool show = p != null && p.Exists() && !p.IsDead && !p.IsInVehicle() && p.Position.DistanceTo(_door) < 200f;
                if (show && _pedBlips[i] == null)
                {
                    Blip b = p.AddBlip();
                    b.Color = (BlipColor)_crew.BlipColor; // the crew's own colour, like its fronts on the map
                    b.Scale = 0.7f;
                    b.Name = _crew.Name;
                    _pedBlips[i] = b;
                }
                else if (!show && _pedBlips[i] != null) { if (_pedBlips[i].Exists()) _pedBlips[i].Delete(); _pedBlips[i] = null; }
            }
        }

        private int AliveAttackers()
        {
            int n = 0;
            foreach (Ped p in _attackers) if (p != null && p.Exists() && !p.IsDead) n++;
            return n;
        }

        /// <summary>Count each death once: who killed whom, and the leader.</summary>
        private void CountCasualties()
        {
            foreach (Ped p in _attackers)
            {
                if (p == null || _counted.Contains(p)) continue;
                if (!p.Exists())
                {
                    _counted.Add(p);
                    if (p == _leader && _leaderFleeing) _result.LeaderEscaped = true; // deleted while fleeing = got away
                    continue;
                }
                if (!p.IsDead) continue;
                _counted.Add(p);
                _result.AttackersLost++;
                if (p == _leader) _result.LeaderKilled = true;
                Ped killer = p.Killer as Ped;
                GuardState g = _streamer.GuardForPed(killer);
                if (g != null)
                {
                    int k;
                    _result.KillsByGuard.TryGetValue(g.Id, out k);
                    _result.KillsByGuard[g.Id] = k + 1;
                }
            }
        }

        /// <summary>Below 30% strength the leader runs. Out of range while fleeing = escaped.</summary>
        private void UpdateLeader(int alive)
        {
            if (_leader == null || !_leader.Exists() || _leader.IsDead) return;
            if (!_leaderFleeing && alive <= Math.Max(1, (int)Math.Floor(_attack.Squad * 0.3)) && _attack.Squad > 1)
            {
                _leaderFleeing = true;
                _leaderFleeStart = Game.GameTime;
                _leader.Task.ClearAll();
                _leader.Task.FleeFrom(Game.Player.Character, -1);
                Log.Write("LiveDefense: leader is running");
            }
            if (_leaderFleeing && (_leader.Position.DistanceTo(_door) > LeaderGoneRange || Game.GameTime - _leaderFleeStart > LeaderFleeMaxMs))
            {
                _result.LeaderEscaped = true;
                _counted.Add(_leader);
                _registry.Despawn(_leader);
            }
        }

        private void UpdateIntegrity()
        {
            int now = Game.GameTime;
            if (now - _lastIntegrityTick < 1000) return;
            float seconds = (now - _lastIntegrityTick) / 1000f;
            _lastIntegrityTick = now;
            int near = 0;
            foreach (Ped p in _attackers) if (p.Exists() && !p.IsDead && p.Position.DistanceTo(_door) < DoorRadius) near++;
            if (near == 0) return;
            foreach (KeyValuePair<GuardState, Ped> kv in _streamer.GuardPedsAt(_front.Id))
                if (kv.Value.Position.DistanceTo(_door) < GuardRadius) return; // a guard is holding the door
            _integrity = Math.Max(0f, _integrity - near * seconds);
        }

        private void CallPolice()
        {
            foreach (Ped p in _attackers)
            {
                if (!p.Exists() || p.IsDead) continue;
                var outId = new OutputArgument();
                // dispatch service 1 = police (community notes in NativeDB; confirm in game)
                if (Function.Call<bool>(Hash.CREATE_INCIDENT_WITH_ENTITY, 1, p, 3, 0f, outId, 0, 0)) _incident = outId.GetResult<int>();
                Log.Write("LiveDefense: police incident " + _incident);
                return;
            }
        }

        private void UpdateBlips()
        {
            // Red car blips once the convoy is within sight of your guards (120 m).
            for (int i = _blips.Count; i < _cars.Count; i++) _blips.Add(null);
            for (int i = 0; i < _cars.Count; i++)
            {
                Vehicle v = _cars[i];
                bool show = v != null && v.Exists() && v.Position.DistanceTo(_door) < 120f && v.IsDriveable;
                if (show && _blips[i] == null)
                {
                    Blip b = v.AddBlip();
                    b.Sprite = BlipSprite.PersonalVehicleCar;
                    b.Color = BlipColor.Red;
                    b.Name = _crew.Name;
                    _blips[i] = b;
                }
                else if (!show && _blips[i] != null) { if (_blips[i].Exists()) _blips[i].Delete(); _blips[i] = null; }
            }
        }

        private Result Finish(bool won)
        {
            _result.Finished = true;
            _result.DefendersWon = won;
            _result.AttackersLeft = AliveAttackers();
            if (!_result.LeaderKilled && won && _leaderMember != null) _result.LeaderEscaped = true;
            Log.Write("LiveDefense: over, " + (won ? "held" : "lost") + ", attackers lost " + _result.AttackersLost);
            End(won);
            return _result;
        }

        /// <summary>Stops early: the remaining squad is auto-resolved by the Director.</summary>
        public Result Abort(string why)
        {
            if (_stage == Stage.Over) return null;
            CountCasualties();
            _result.Finished = false;
            // Nobody spawned yet (still loading, or the convoy failed): the whole squad is still out there.
            _result.AttackersLeft = _attackers.Count == 0 ? _attack.Squad : AliveAttackers();
            Log.Write("LiveDefense: aborted (" + why + "), " + _result.AttackersLeft + " attackers left to auto-resolve");
            End(false);
            return _result;
        }

        private void End(bool won)
        {
            _stage = Stage.Over;
            _bars.Visible = false;
            ReleaseModels();
            foreach (Blip b in _blips) if (b != null && b.Exists()) b.Delete();
            _blips.Clear();
            foreach (Blip b in _pedBlips) if (b != null && b.Exists()) b.Delete();
            _pedBlips.Clear();
            if (_incident != 0) { Function.Call(Hash.DELETE_INCIDENT, _incident); _incident = 0; }
            // Survivors leave; bodies and wrecks are cleaned up as the front streams out (registry owns them all).
            foreach (Ped p in _attackers)
            {
                if (p == null || !p.Exists()) continue;
                if (p.IsDead) continue;
                p.Task.ClearAll();
                p.Task.FleeFrom(_door, -1);
            }
            _streamer.SetFighting(_front.Id, false);
            _streamer.LiveFrontId = null;
            _cleanupAfter = Game.GameTime + 60000;
        }

        private int _cleanupAfter;

        /// <summary>Every 0.5 s: removes the fight's leftovers once they're off screen or a minute has passed.</summary>
        public void Cleanup()
        {
            if (_stage != Stage.Over) return;
            if (_attackers.Count == 0 && _cars.Count == 0) return;
            bool late = Game.GameTime > _cleanupAfter;
            for (int i = _attackers.Count - 1; i >= 0; i--)
            {
                Ped p = _attackers[i];
                if (p == null || !p.Exists()) { _attackers.RemoveAt(i); continue; }
                if (late || !p.IsOnScreen) { _registry.Despawn(p); _attackers.RemoveAt(i); }
            }
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                Vehicle v = _cars[i];
                if (v == null || !v.Exists()) { _cars.RemoveAt(i); continue; }
                if (late || !v.IsOnScreen) { _registry.Despawn(v); _cars.RemoveAt(i); }
            }
        }

        /// <summary>Every tick: draw the timer bars.</summary>
        public void Draw()
        {
            if (_bars.Visible) UI.FightHud.Draw("ATTACKERS " + _attackersBar.Info + "      FRONT " + (int)System.Math.Round(_integrityBar.Progress) + "%");
        }

        /// <summary>The bars sit where GTA writes the vehicle name, area name, vehicle class and street name: hide those
        /// while a bar is up, as the game's own missions do, so the text never lands on top of each other.</summary>
        public static void HideCornerText()
        {
            foreach (int c in new[] { 6, 7, 8, 9 }) GTA.Native.Function.Call(GTA.Native.Hash.HIDE_HUD_COMPONENT_THIS_FRAME, c);
        }

        /// <summary>A mission started or the script stopped: drop everything (the registry deletes the entities).</summary>
        public void ForceClear()
        {
            _bars.Visible = false;
            ReleaseModels();
            foreach (Blip b in _pedBlips) if (b != null && b.Exists()) b.Delete();
            _pedBlips.Clear();
            foreach (Blip b in _blips) if (b != null && b.Exists()) b.Delete();
            _blips.Clear();
            if (_incident != 0) { Function.Call(Hash.DELETE_INCIDENT, _incident); _incident = 0; }
            _attackers.Clear();
            _cars.Clear();
            _streamer.LiveFrontId = null;
            _stage = Stage.Over;
        }
    }
}
