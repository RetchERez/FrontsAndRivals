// Fronts & Rivals: the Streamer. Every 0.5 s it checks the distance to each scouted front:
//   under 250 m  -> its guards spawn at their posts with idle scenarios (nearest front first)
//   over 300 m   -> they're deleted (the 50 m gap stops flicker)
// Your fronts get your guards. Rival-held fronts get the crew's own soldiers, who fight you on sight;
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
        public delegate void CrewGuardDownHandler(FrontDef front, string crewId, Ped ped, bool lastOne);

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
            public int ReinforceAt;      // game time a replacement for a run-over soldier comes out
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
            public int CrewCount = -1;    // soldiers this visit, fixed when the front streams in
            public List<Vector3> Patrol;  // walkable points around the building, found once per visit
            public bool PatrolTried;
            public int Reinforcements;    // run-over soldiers replaced this visit

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
                    f.CrewCount = -1;
                    f.Patrol = null;
                    f.PatrolTried = false;
                    f.Reinforcements = 0;
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
            if (f.Def.Id == LiveFrontId || (f.IsCrew && f.Alerted) || f.Distance > StreamRules.SpawnRange) return;
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
                    Vector3 c = GTA.World.GetSafeCoordForPed(want, false, 0);
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
                f.CrewCount = Math.Min(Math.Min(c, FrontDef.MaxCrew), Math.Max(0, cs.Manpower));
            }
            int count = f.CrewCount;
            // An empty crew front (no soldiers to spare) can be claimed as soon as you walk up.
            if (count == 0 && !f.ClearedReported && f.Distance < 60f)
            {
                f.ClearedReported = true;
                if (OnCrewGuardDown != null) OnCrewGuardDown(f.Def, f.Owner, null, true);
                return;
            }

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
                        if (slot.Body != null) _registry.Despawn(slot.Body);
                        slot.Body = body;
                        slot.Ped = null;
                        if (OnCrewGuardDown != null) OnCrewGuardDown(f.Def, f.Owner, slot.Body, false);
                        if (byCar && f.Reinforcements < StreamRules.MaxReinforcements)
                        {
                            // Run over: it doesn't count. Another one comes out of the building, and they're all on you now.
                            f.Reinforcements++;
                            slot.ReinforceAt = Game.GameTime + 3000;
                            if (!f.Alerted) Alert(f, count);
                            continue;
                        }
                        slot.Killed = true;
                        continue;
                    }
                    alive++;
                    continue;
                }
                if (slot.Killed) { killed++; continue; }
                if (Game.GameTime < slot.ReinforceAt) continue;
                int pick = Markers.Hash(f.Def.Id) + post * 7;
                Dressing dress = Wardrobe.ForCrew(f.Owner, pick + f.Reinforcements * 101); // in the crew's colour
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
                });
            }
            if (alive > 0 || killed > 0) f.CrewSpawnedThisVisit = true;
            if (!f.Alerted && alive > 0 && ShouldAlert(f, count)) Alert(f, count);
            if (f.CrewSpawnedThisVisit && alive == 0 && killed > 0 && !f.ClearedReported)
            {
                bool anyPending = false;
                for (int post = 0; post < count; post++) if (f.Slots[post].Pending) anyPending = true;
                if (!anyPending)
                {
                    f.ClearedReported = true;
                    if (OnCrewGuardDown != null) OnCrewGuardDown(f.Def, f.Owner, null, true);
                }
            }
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
            float sign = (i / n) % 2 == 1 ? 1f : -1f;
            Vector3[] tries = { post + side * (1.6f * sign), post - side * (1.6f * sign), post + back * 1.2f };
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

        /// <summary>INI Rivals.AlertRange: walking this close to a rival front's door on foot sets its soldiers on you.</summary>
        public float AlertOnFootRange = 35f;
        private const float AlertShotRange = 80f;

        /// <summary>Crew soldiers stay calm while you drive past. Once you own a front they turn on you when you walk up
        /// to their door or shoot nearby; before that, only if you hurt or kill one of them.</summary>
        private bool ShouldAlert(StreamedFront f, int count)
        {
            Ped player = Game.Player.Character;
            bool hurt = false;
            for (int i = 0; i < count && !hurt; i++)
            {
                Slot s = f.Slots[i];
                if (s.Killed) hurt = true;
                else if (s.Ped != null && s.Ped.Exists() && !s.Ped.IsDead && s.Ped.Health < s.Ped.MaxHealth) hurt = true;
            }
            bool inTheGame = _empire != null && _empire.OwnedCount() > 0;
            return StreamRules.CrewProvoked(inTheGame, !player.IsInVehicle() && f.Distance < AlertOnFootRange,
                                            player.IsShooting && f.Distance < AlertShotRange, hurt);
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
                try
                {
                    // A small blip in the crew's colour (the same as its fronts on the map) on every soldier in the fight.
                    CrewDef cd = _content.Crew(f.Owner);
                    if (s.Ped.AttachedBlip == null || !s.Ped.AttachedBlip.Exists())
                    {
                        Blip b = s.Ped.AddBlip();
                        b.Color = cd != null ? (BlipColor)cd.BlipColor : BlipColor.Red;
                        b.Scale = 0.7f;
                        b.Name = cd != null ? cd.Name : "Crew";
                    }
                }
                catch (Exception ex) { Log.Error("Crew blip", ex); }
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
