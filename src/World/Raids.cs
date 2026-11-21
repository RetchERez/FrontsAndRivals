// Fronts & Rivals: raids. Send your guards to take a rival front without going yourself.
// The mirror of the crews' attacks on you, decided by the same guard rules. Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Raids
    {
        public const long RaidMs = 3 * Attacks.Minute; // about how long until word comes back
        public const int MaxGuards = 4;
        public const double DugInBonus = 1.8;          // the crew fights hard at its own door: challenging, but a good crew has the edge

        /// <summary>How many crew soldiers hold this front tonight (its size, fewer if weakened, never more than the crew has).</summary>
        public static int Soldiers(FrontDef def, FrontState f, CrewState crew, int day)
        {
            if (def == null || crew == null) return 0;
            int n = FrontDef.CrewSoldiers(def);
            if (f != null && f.WeakUntilDay >= day) n = Math.Max(1, n - 1 - def.Tier); // thinned out
            return Math.Min(n, Math.Max(0, crew.Manpower));
        }

        /// <summary>A front's defense against a raid, with street heat (the car wash's melee kids ignore it and count 60%).</summary>
        public static double FrontDefense(CrewDef crew, FrontDef def, int soldiers, int heat)
        {
            if (def != null && def.Starter) return CrewDefense(crew, soldiers) * StarterFactor;
            return CrewDefense(crew, soldiers) * Heat.CrewDefenseFactor(heat);
        }

        public const double StarterFactor = 0.6;

        public static double CrewDefense(CrewDef crew, int soldiers)
        {
            return crew == null ? 0 : soldiers * crew.SoldierPower * DugInBonus;
        }

        /// <summary>Your raiding party's strength: the same formula as guarding, without a front's upgrades.</summary>
        public static double TeamPower(IList<GuardRecord> team, int hour)
        {
            return DefenseRating.Compute(team, new FrontUpgrades(), hour);
        }

        public static double WinChance(double teamPower, double crewDefense) { return Resolver.WinChance(teamPower, crewDefense); }

        public sealed class Outcome
        {
            public bool Won;
            public string FrontId, CrewId;
            public List<GuardState> Dead = new List<GuardState>();
            public List<GuardState> Injured = new List<GuardState>();
            public int SoldiersLost;
        }

        /// <summary>Settles a raid. A win hands you the front (with the usual grace period); a loss angers the crew.
        /// Survivors gain 2 XP and skills either way.</summary>
        public static Outcome Resolve(EmpireState s, ContentDb content, Empire empire, Random rng, int hour, long nowMs,
                                      bool permanentDeath, long recoveryMs)
        {
            RaidState raid = s.Raid;
            var o = new Outcome();
            if (raid == null) return o;
            s.Raid = null;
            o.FrontId = raid.Front;
            o.CrewId = raid.Crew;
            FrontDef def = content.Front(raid.Front);
            FrontState fs = empire.Find(raid.Front);
            CrewDef crewDef = content.Crew(raid.Crew);
            CrewState crew = Nemesis.Crew(s, raid.Crew);
            if (def == null || fs == null || crewDef == null || crew == null) return o;

            var team = new List<GuardState>();
            var records = new List<GuardRecord>();
            foreach (int id in raid.GuardIds)
            {
                GuardState g = empire.Guard(id);
                if (g == null || g.Status != GuardStatus.Active) continue;
                team.Add(g);
                records.Add(Empire.Record(g));
            }
            // Someone else took it meanwhile, or nobody could go: the crew comes home.
            if (team.Count == 0 || fs.Owner != raid.Crew) return o;

            int soldiers = Soldiers(def, fs, crew, s.Day);
            double power = TeamPower(records, hour), defense = FrontDefense(crewDef, def, soldiers, s.Heat) * Difficulty.Strength(s.Difficulty);
            o.Won = soldiers == 0 || rng.NextDouble() < WinChance(power, defense);
            double r = power + defense <= 0 ? 0.5 : defense / (power + defense);
            string where = "the raid on the " + def.ShortName;

            foreach (GuardState g in team)
            {
                double injury = (o.Won ? 0.5 : 0.8) * r * Stats.InjuryFactor(g.Stamina);
                double death = (o.Won ? 0.12 : 0.3) * r * Stats.DeathFactor(g.Stealth);
                if (g.Trait == (int)Trait.Hothead) death *= 2.0;
                if (g.Vest) { injury *= DefenseRating.VestInjury; death *= DefenseRating.VestDeath; }
                double roll = rng.NextDouble();
                if (roll < death)
                {
                    empire.GuardDown(g, where, permanentDeath, nowMs, recoveryMs);
                    if (permanentDeath) { o.Dead.Add(g); s.Memorial.Add(g.FullName + ", " + Economy.RankName((Rank)g.Rank) + ", fell in " + where); }
                    else o.Injured.Add(g);
                }
                else if (roll < death + injury) { empire.GuardDown(g, where, false, nowMs, recoveryMs); o.Injured.Add(g); }
                else
                {
                    g.Xp += 2;
                    Stats.Add(g, Stats.Defense(o.Won));
                    Attacks.Promote(g);
                }
            }

            o.SoldiersLost = o.Won ? soldiers : Math.Max(1, (int)Math.Round(soldiers * 0.4));
            crew.Manpower = Math.Max(0, crew.Manpower - o.SoldiersLost);
            crew.LastContactDay = s.Day;
            if (o.Won)
            {
                Attacks.TakeFront(s, empire, fs, raid.Crew);
                Attacks.GracePeriod(s, def.Id, nowMs);
            }
            else
            {
                Nemesis.AddGrudge(crew, Nemesis.GrudgeRepelled);
                crew.LastMove = "Beat off your raid on the " + def.ShortName;
            }
            return o;
        }
    }
}
