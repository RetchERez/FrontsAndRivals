// Fronts & Rivals: attack planning and results. Pure: no game calls, unit-tested.
// Design doc "Attacks": one attack every 40-60 minutes on Normal, always with a warning, always a fair fight.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Attacks
    {
        public const long Minute = 60000;
        public const long GameHourMs = 2 * Minute;                 // 1 in-game hour = 2 real minutes
        public const long FirstAttackDelayMs = 10 * Minute;        // never in the first 10 minutes after loading
        public const long AfterLossDelayMs = 30 * Minute;          // never within 30 minutes of losing a front
        public const long SameFrontDelayMs = 2 * 24 * GameHourMs;  // never the same front twice within 2 in-game days
        public const long WindowMs = 3 * Minute;                   // the attack window lasts 3 real minutes
        public const double PlanChancePerMinute = 0.25;
        public const float LiveRange = 300f;                       // inside this during the window: live defense
        public const float OnTheWayRange = 1000f;                  // this close when the window closes: the crew waits for you
        public const long OnTheWayMs = 90000;                      // ...once, for 90 more seconds

        /// <summary>Cooldown in ms for a frequency name, or -1 when attacks are off.</summary>
        public static long CooldownMs(string frequency)
        {
            switch ((frequency ?? "Normal").ToLowerInvariant())
            {
                case "off": return -1;
                case "low": return 75 * Minute;
                case "high": return 25 * Minute;
                default: return 45 * Minute;
            }
        }

        // ---- Empire-size pressure: the bigger you get, the more the crews come for you ----
        // Up to 3 fronts: the base timing. Each front past 3 makes attacks about 4% more frequent, down to 55% of the
        // cooldown (45 min on Normal becomes ~35 at 10 fronts, ~30 at 15, 25 at 23+). Squads grow by one at 6, 12 and 20 fronts.
        public const int PressureFreeFronts = 3;
        public const double PressureMinFactor = 0.55;
        public const long PressureFloorMs = 20 * Minute; // pressure never makes attacks closer than this

        /// <summary>Multiplier on the attack cooldown for the number of fronts you hold (1.0 = no change).</summary>
        public static double PressureCooldownFactor(int held)
        {
            if (held <= PressureFreeFronts) return 1.0;
            return Math.Max(PressureMinFactor, 1.0 / (1.0 + 0.04 * (held - PressureFreeFronts)));
        }

        /// <summary>Extra attackers per squad for the number of fronts you hold (still capped at 9).</summary>
        public static int PressureSquadBonus(int held)
        {
            return held >= 20 ? 3 : held >= 12 ? 2 : held >= 6 ? 1 : 0;
        }

        /// <summary>How the street sees you, for the Office.</summary>
        public static string PressureLabel(int held)
        {
            if (held == 0) return "Unknown";
            if (held <= PressureFreeFronts) return "Noticed";
            if (held < 6) return "Rising";
            if (held < 12) return "Wanted";
            if (held < 20) return "Hunted";
            return "Marked";
        }

        public static int HeldCount(EmpireState s)
        {
            int n = 0;
            foreach (FrontState f in s.Fronts) if (f.Owner == Owners.Player) n++;
            return n;
        }

        /// <summary>Can the Director plan an attack right now? (The 25% roll per minute comes after this.)</summary>
        public static bool CanPlan(EmpireState s, long nowMs, long sinceLoadMs, string frequency)
        {
            return CanPlan(s, nowMs, sinceLoadMs, frequency, 0, (int)(FirstAttackDelayMs / Minute));
        }

        /// <param name="cooldownMinutes">INI CooldownMinutes; 0 = use the frequency's cooldown. Off still means off.</param>
        /// <param name="firstDelayMinutes">INI FirstAttackDelayMinutes.</param>
        public static bool CanPlan(EmpireState s, long nowMs, long sinceLoadMs, string frequency, int cooldownMinutes, int firstDelayMinutes)
        {
            return CanPlan(s, nowMs, sinceLoadMs, frequency, cooldownMinutes, firstDelayMinutes, false);
        }

        /// <param name="pressure">Empire-size pressure on: the Frequency cooldown shrinks as you hold more fronts.
        /// An exact CooldownMinutes from the INI is always kept as set.</param>
        public static bool CanPlan(EmpireState s, long nowMs, long sinceLoadMs, string frequency, int cooldownMinutes, int firstDelayMinutes, bool pressure)
        {
            long cooldown = CooldownMs(frequency);
            if (cooldown >= 0 && cooldownMinutes > 0) cooldown = cooldownMinutes * Minute;
            else if (cooldown > 0 && pressure)
                cooldown = Math.Min(cooldown, Math.Max(PressureFloorMs, (long)(cooldown * PressureCooldownFactor(HeldCount(s)))));
            if (cooldown < 0 || s.Attack != null) return false;
            if (sinceLoadMs < firstDelayMinutes * Minute) return false;
            if (nowMs - s.LastAttackMs < cooldown) return false;
            if (nowMs - s.LastLossMs < AfterLossDelayMs) return false;
            foreach (FrontState f in s.Fronts) if (f.Owner == Owners.Player) return true;
            return false;
        }

        /// <summary>Pick the attacking crew by weight w = aggression + grudge/25. Quiet or leaderless crews sit out.</summary>
        public static CrewState PickCrew(EmpireState s, ContentDb content, Random rng)
        {
            var pool = new List<CrewState>();
            var weights = new List<double>();
            foreach (CrewState c in s.Crews)
            {
                CrewDef def = content.Crew(c.Id);
                if (def == null || Nemesis.IsQuiet(c, s.Day) || c.Manpower < 2) continue;
                if (c.Boss == null && c.Lieutenants().Count == 0) continue;
                pool.Add(c);
                weights.Add(def.Aggression + c.Grudge / 25.0);
            }
            return Weighted(pool, weights, rng);
        }

        /// <summary>Distance factor: 1.0 near the crew's home, down to 0.6 for far fronts.</summary>
        public static double DistanceFactor(Spot home, Spot door)
        {
            if (home == null || !home.IsSet || door == null || !door.IsSet) return 1.0;
            float dx = home.X - door.X, dy = home.Y - door.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= 1000) return 1.0;
            if (d >= 4000) return 0.6;
            return 1.0 - 0.4 * (d - 1000) / 3000.0;
        }

        /// <summary>Choose the target front. s = income/1000 x (A/D) x b x d (b = 1.5 if taken from this crew).
        /// Velvet always hits the weakest front; a Tactician leader the best income-to-defense ratio.</summary>
        public static FrontState PickFront(EmpireState s, ContentDb content, Empire empire, CrewState crew, CrewMember leader,
                                           double estimatedAttack, int hour, long nowMs, Random rng)
        {
            CrewDef def = content.Crew(crew.Id);
            var pool = new List<FrontState>();
            var scores = new List<double>();
            FrontState weakest = null, bestRatio = null;
            double weakestD = double.MaxValue, bestR = -1;
            foreach (FrontState f in s.Fronts)
            {
                if (f.Owner != Owners.Player) continue;
                FrontDef fd = content.Front(f.Id);
                if (fd == null) continue;
                long last;
                if (s.FrontAttackedMs.TryGetValue(f.Id, out last) && nowMs - last < SameFrontDelayMs) continue;
                double d = Math.Max(1.0, empire.Defense(f.Id, hour));
                double b = f.TakenFrom == crew.Id ? 1.5 : 1.0;
                double score = fd.Income / 1000.0 * (estimatedAttack / d) * b * DistanceFactor(def != null ? def.Home : null, fd.Door);
                pool.Add(f);
                scores.Add(score);
                if (d < weakestD) { weakestD = d; weakest = f; }
                double ratio = fd.Income / d;
                if (ratio > bestR) { bestR = ratio; bestRatio = f; }
            }
            if (pool.Count == 0) return null;
            if (leader != null && leader.LeaderTrait == LeaderTrait.Tactician) return bestRatio;
            if (crew.Id == Owners.Velvet) return weakest;
            return Weighted(pool, scores, rng);
        }

        /// <summary>Hours of warning: the INI value + Cameras + a Lookout guard, minus 1 for the Velvet Syndicate. Never under 1.</summary>
        public static int WarningHours(int baseHours, bool cameras, bool lookout, string crewId)
        {
            int h = baseHours + (cameras ? 1 : 0) + (lookout ? 1 : 0) - (crewId == Owners.Velvet ? 1 : 0);
            return Math.Max(1, h);
        }

        /// <summary>Plans a complete attack, or returns null if nobody can attack anything right now.</summary>
        /// <param name="squadBonus">Extra attackers from empire-size pressure (PressureSquadBonus).</param>
        public static AttackState Plan(EmpireState s, ContentDb content, Empire empire, Random rng, int warningBaseHours, int hour, long nowMs,
                                       int squadBonus = 0)
        {
            CrewState crew = PickCrew(s, content, rng);
            if (crew == null) return null;
            CrewDef def = content.Crew(crew.Id);
            CrewMember leader = Nemesis.PickLeader(crew, rng);
            if (leader == null) return null;

            // Estimate strength against a mid-tier front to score targets, then size the real squad for the chosen one.
            int estSquad = Resolver.SquadSize(1, crew.Grudge, crew.Manpower, squadBonus);
            double estA = Resolver.AttackStrength(estSquad, def.SoldierPower, Nemesis.LeaderBonus(leader), leader.LeaderTrait == LeaderTrait.Ruthless, crew.Grudge);
            FrontState target = PickFront(s, content, empire, crew, leader, estA, hour, nowMs, rng);
            if (target == null) return null;
            FrontDef fd = content.Front(target.Id);

            int squad = Resolver.SquadSize(fd.Tier, crew.Grudge, crew.Manpower, squadBonus);
            if (squad < 1) return null;
            double a = Resolver.AttackStrength(squad, def.SoldierPower, Nemesis.LeaderBonus(leader), leader.LeaderTrait == LeaderTrait.Ruthless, crew.Grudge);
            bool lookout = false;
            foreach (GuardState g in s.Guards)
                if (g.Front == target.Id && g.Status == GuardStatus.Active && g.Trait == (int)Trait.Lookout) lookout = true;
            int hours = WarningHours(warningBaseHours, target.Cameras, lookout, crew.Id);

            return new AttackState
            {
                Crew = crew.Id,
                Front = target.Id,
                Leader = leader.Id,
                Squad = squad,
                Strength = a,
                Phase = AttackPhase.Warning,
                WarnEnds = nowMs + hours * GameHourMs,
                WindowEnds = nowMs + hours * GameHourMs + WindowMs
            };
        }

        public sealed class Outcome
        {
            public bool DefendersWon;
            public bool FrontLost;
            public bool FrontDamaged;
            public int AttackersLost;
            public List<GuardState> Dead = new List<GuardState>();
            public List<GuardState> Injured = new List<GuardState>();
            public bool LeaderDied;
            public bool LeaderEscaped;
            public List<Nemesis.Event> Events = new List<Nemesis.Event>();
        }

        /// <summary>Applies a result (auto-resolved or the remains of a live defense) to the empire.
        /// liveResult: pass the defenders' win/loss from a live fight, or null to roll the auto-resolver.</summary>
        public static Outcome Resolve(EmpireState s, ContentDb content, Empire empire, AttackState attack, Random rng,
                                      int hour, long nowMs, bool permanentDeath, bool protectedMode, bool? liveResult,
                                      int liveAttackersLost, long recoveryMs)
        {
            var o = new Outcome();
            CrewState crew = Nemesis.Crew(s, attack.Crew);
            CrewDef def = content.Crew(attack.Crew);
            FrontState front = empire.Find(attack.Front);
            FrontDef fd = content.Front(attack.Front);
            CrewMember leader = crew != null ? crew.Member(attack.Leader) : null;
            string frontName = fd != null ? fd.ShortName : attack.Front;
            string crewName = def != null ? def.Name : attack.Crew;

            var posted = new List<GuardState>();
            var records = new List<GuardRecord>();
            bool medic = false;
            foreach (GuardState g in s.Guards)
            {
                if (g.Front != attack.Front || g.Status == GuardStatus.Dead) continue;
                posted.Add(g);
                records.Add(new GuardRecord { Name = g.First, Rank = (Rank)g.Rank, Weapon = (Weapon)g.Weapon, Trait = (Trait)g.Trait, Injured = g.Status == GuardStatus.Injured });
                if (g.Trait == (int)Trait.Medic && g.Status == GuardStatus.Active) medic = true;
            }
            double defense = empire.Defense(attack.Front, hour);
            Resolver.Result r = Resolver.Resolve(rng, records, defense, attack.Strength, attack.Squad, medic);
            o.DefendersWon = liveResult ?? r.DefendersWon;
            o.AttackersLost = liveResult.HasValue ? liveAttackersLost : r.AttackersLost;

            // Guard casualties (a live fight already applied its own deaths in the world; roll the rest for auto-resolve only).
            if (!liveResult.HasValue)
            {
                for (int i = 0; i < posted.Count; i++)
                {
                    GuardState g = posted[i];
                    Resolver.GuardOutcome go = r.Guards[i];
                    if (go.Dead)
                    {
                        if (permanentDeath && !protectedMode) o.Dead.Add(g); else o.Injured.Add(g);
                    }
                    else if (go.Injured) o.Injured.Add(g);
                }
                foreach (GuardState g in o.Dead)
                {
                    empire.GuardDown(g, "the " + frontName, true, nowMs, recoveryMs);
                    s.Memorial.Add(g.FullName + ", " + Economy.RankName((Rank)g.Rank) + ", fell at the " + frontName);
                }
                foreach (GuardState g in o.Injured) empire.GuardDown(g, "the " + frontName, false, nowMs, recoveryMs);
            }
            foreach (GuardState g in posted)
                if (g.Status == GuardStatus.Active) { g.Xp++; g.Defenses += o.DefendersWon ? 1 : 0; Promote(g); }

            if (crew != null)
            {
                crew.Manpower = Math.Max(0, crew.Manpower - o.AttackersLost);
                crew.LastContactDay = s.Day;
            }

            if (o.DefendersWon)
            {
                if (crew != null) Nemesis.AddGrudge(crew, Nemesis.GrudgeRepelled);
                if (leader != null && leader.Alive)
                {
                    if (!liveResult.HasValue)
                    {
                        o.LeaderDied = Nemesis.LeaderLost(leader, frontName, rng, nowMs);
                        o.LeaderEscaped = !o.LeaderDied;
                    }
                }
                if (o.LeaderDied && crew != null) o.Events.AddRange(Nemesis.OnMemberDied(s, content, crew, leader, frontName, rng, s.Day));
                else if (o.LeaderEscaped) o.Events.Add(Nemesis.Ev("leaderEscaped", "leader", leader.DisplayName, "front", frontName));
                if (crew != null) crew.LastMove = "Hit your " + frontName + " and lost";
                // Counter-strike: their nearest front is thin for a day.
                FrontState theirs = NearestCrewFront(s, content, attack.Crew, fd);
                if (theirs != null)
                {
                    theirs.WeakUntilDay = s.Day + 1;
                    o.Events.Add(Nemesis.Ev("counterStrike", "crew", crewName, "front", content.Front(theirs.Id).ShortName));
                }
            }
            else
            {
                if (leader != null && leader.Alive) Nemesis.LeaderWon(leader, frontName);
                if (crew != null) crew.LastMove = "Took your " + frontName;
                if (protectedMode)
                {
                    front.Damaged = true;
                    o.FrontDamaged = true;
                }
                else
                {
                    LoseFront(s, empire, front, attack.Crew);
                    o.FrontLost = true;
                    s.LastLossMs = nowMs;
                }
            }

            s.LastAttackMs = nowMs;
            s.FrontAttackedMs[attack.Front] = nowMs;
            s.Attack = null;
            return o;
        }

        /// <summary>A lost front becomes the crew's. Its upgrades stay with the building, so retaking it restores them
        /// (the design doc left half-or-all open; all keeps losses from turning into chores).</summary>
        public static void LoseFront(EmpireState s, Empire empire, FrontState front, string crewId)
        {
            empire.SetOwner(front.Id, crewId);
            front.TakenFrom = null;
            front.Damaged = false;
        }

        /// <summary>The player claims a crew front at its door.</summary>
        public static void TakeFront(EmpireState s, Empire empire, FrontState front, string fromCrew)
        {
            empire.SetOwner(front.Id, Owners.Player);
            front.TakenFrom = fromCrew;
            front.WeakUntilDay = -1;
            CrewState crew = Nemesis.Crew(s, fromCrew);
            if (crew != null) { Nemesis.AddGrudge(crew, Nemesis.GrudgeTakeFront); crew.LastContactDay = s.Day; crew.LastMove = "Lost a front to you"; }
        }

        /// <summary>XP thresholds 3 / 8 / 15 raise the rank.</summary>
        public static void Promote(GuardState g)
        {
            while (true)
            {
                int need = Economy.NextRankXp((Rank)g.Rank);
                if (need < 0 || g.Xp < need) return;
                g.Rank++;
            }
        }

        private static FrontState NearestCrewFront(EmpireState s, ContentDb content, string crewId, FrontDef from)
        {
            FrontState best = null;
            double bestD = double.MaxValue;
            foreach (FrontState f in s.Fronts)
            {
                if (f.Owner != crewId) continue;
                FrontDef fd = content.Front(f.Id);
                if (fd == null || from == null) continue;
                double d = fd.Door.DistanceTo(from.Door.X, from.Door.Y, from.Door.Z);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        private static T Weighted<T>(List<T> items, List<double> weights, Random rng) where T : class
        {
            if (items.Count == 0) return null;
            double total = 0;
            foreach (double w in weights) total += Math.Max(0, w);
            if (total <= 0) return items[rng.Next(items.Count)];
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < items.Count; i++)
            {
                roll -= Math.Max(0, weights[i]);
                if (roll <= 0) return items[i];
            }
            return items[items.Count - 1];
        }
    }
}
