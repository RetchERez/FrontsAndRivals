// Fronts & Rivals: guard skills, named like the player's own (pause menu > Stats > Skills).
// Pure rules, no game calls, unit-tested.
//
//   Shooting  aim. Most of a guard's power, and their accuracy in a live fight.
//   Strength  hits harder, takes more punishment: some power, and extra health in a live fight.
//   Stamina   keeps going: fewer injuries, and quicker back on duty after one.
//   Stealth   stays out of the line of fire: fewer deaths.
//
// Every skill is 0-100. 50 is average; a fresh recruit is usually 20-45 with one or two strong spots from their trait.
// Skills grow from the work itself, like the player's do: days on duty, fights survived, kills, promotions.
using System;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public enum Skill { Shooting, Strength, Stamina, Stealth }

    public static class Stats
    {
        public const int Max = 100;
        public const int Average = 50;

        // ---- what skills do ----

        /// <summary>Power multiplier from Shooting (60%) and Strength (40%): x0.8 at 0, x1.0 at 50, x1.2 at 100.</summary>
        public static double PowerFactor(int shooting, int strength)
        {
            double s = (0.6 * Clamp(shooting) + 0.4 * Clamp(strength)) / Max;
            return 0.8 + 0.4 * s;
        }

        /// <summary>Injury chance multiplier from Stamina: x1.3 at 0, x1.0 at 50, x0.7 at 100.</summary>
        public static double InjuryFactor(int stamina) { return 1.3 - 0.6 * Clamp(stamina) / Max; }

        /// <summary>Death chance multiplier from Stealth: x1.3 at 0, x1.0 at 50, x0.7 at 100.</summary>
        public static double DeathFactor(int stealth) { return 1.3 - 0.6 * Clamp(stealth) / Max; }

        /// <summary>Time off after an injury, from Stamina: x1.25 at 0, x1.0 at 50, x0.75 at 100.</summary>
        public static double RecoveryFactor(int stamina) { return 1.25 - 0.5 * Clamp(stamina) / Max; }

        /// <summary>Ped accuracy in a live fight (the game's 0-100 scale): 15 at Shooting 0, 65 at 100.</summary>
        public static int Accuracy(int shooting) { return 15 + Clamp(shooting) / 2; }

        /// <summary>Ped health in a live fight: 200 is a normal ped; Strength adds up to 100.</summary>
        /// <summary>A guard's health in a live fight: twice an ordinary ped's (GTA counts health above 100), plus Strength.
        /// 300 at Strength 0 up to 500 at 100.</summary>
        public static int Health(int strength) { return 300 + 2 * Clamp(strength); }

        /// <summary>Crew soldiers at fronts and in attacks: twice an ordinary ped's health too, so fights last.</summary>
        public const int CrewHealth = 300;

        // ---- where skills start ----

        /// <summary>A new recruit: 20-45 in everything, and their trait shows.</summary>
        public static int[] Seed(Trait trait, Random rng)
        {
            var v = new int[4];
            // Every recruit is rolled fresh, so no two games have the same people: 15-55 in each skill, and now and then
            // (1 in 8) a standout with one skill 20 higher, then the trait. Saved with the empire, so a run keeps its people.
            for (int i = 0; i < 4; i++) v[i] = 15 + rng.Next(41);
            if (rng.Next(8) == 0) v[rng.Next(4)] += 20;
            switch (trait)
            {
                case Trait.Sharpshooter: v[0] += 25; break;
                case Trait.Tough: v[1] += 15; v[2] += 15; break;
                case Trait.Hothead: v[1] += 20; v[3] -= 10; break;
                case Trait.NightOwl: v[2] += 10; v[3] += 10; break;
                case Trait.Medic: v[2] += 20; break;
                case Trait.Loyal: v[2] += 10; break;
                case Trait.Nervous: v[0] -= 10; v[1] -= 5; v[3] += 10; break;
                default: for (int i = 0; i < 4; i++) v[i] += 5; break;
            }
            for (int i = 0; i < 4; i++) v[i] = Clamp(v[i]);
            return v;
        }

        /// <summary>Saves from before skills existed: a steady, rank-based line, no randomness.</summary>
        public static int[] FromRank(Rank rank, int id)
        {
            int b = 32 + 12 * (int)rank;
            return new[] { Clamp(b + id % 7), Clamp(b + id % 5), Clamp(b + id % 3), Clamp(b + id % 4) };
        }

        /// <summary>The average of all four, for the Office and the hire price.</summary>
        public static int Overall(int shooting, int strength, int stamina, int stealth)
        {
            return (Clamp(shooting) + Clamp(strength) + Clamp(stamina) + Clamp(stealth)) / 4;
        }

        // ---- how skills grow (the Gains are added and capped at 100 by the caller's Add) ----

        /// <summary>A midnight spent posted at a front: walking the block, standing the cold.</summary>
        public static readonly int[] DayOnDuty = { 0, 0, 1, 1 };

        /// <summary>A defense they were at, win or lose. A win teaches more.</summary>
        public static int[] Defense(bool won) { return won ? new[] { 3, 2, 1, 1 } : new[] { 2, 1, 1, 1 }; }

        /// <summary>Each kill in a live fight sharpens their aim.</summary>
        public static int[] Kills(int n) { return new[] { Math.Max(0, n), 0, 0, 0 }; }

        /// <summary>A promotion: everyone notices, a little across the board.</summary>
        public static readonly int[] Promotion = { 3, 3, 3, 3 };

        /// <summary>Adds a gain (Shooting, Strength, Stamina, Stealth), capped at 100.</summary>
        public static void Add(GuardState g, int[] gain)
        {
            if (g == null || gain == null) return;
            g.Shooting = Clamp(g.Shooting + gain[0]);
            g.Strength = Clamp(g.Strength + gain[1]);
            g.Stamina = Clamp(g.Stamina + gain[2]);
            g.Stealth = Clamp(g.Stealth + gain[3]);
        }

        public static void Set(GuardState g, int[] v)
        {
            g.Shooting = Clamp(v[0]); g.Strength = Clamp(v[1]); g.Stamina = Clamp(v[2]); g.Stealth = Clamp(v[3]);
        }

        public static int Overall(GuardState g) { return Overall(g.Shooting, g.Strength, g.Stamina, g.Stealth); }

        public static bool HasSkills(GuardState g) { return g.Shooting + g.Strength + g.Stamina + g.Stealth > 0; }

        public static int Clamp(int v) { return v < 0 ? 0 : v > Max ? Max : v; }

        public static string Name(Skill s) { return s.ToString(); }
    }
}
