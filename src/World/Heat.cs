// Fronts & Rivals: street heat. Taking fronts makes noise; the noise dies down overnight. The hotter the streets,
// the more often the crews hit you and the harder their fronts fight back, so steamrolling the city in one sitting
// gets steadily harder, and laying low for a night or two calms things down again. Pure: no game calls, unit-tested.
using System;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Heat
    {
        public const int Max = 100;
        public const int PerTake = 25;      // every front you take, in person or with a raid
        public const int NightlyCool = 15;  // every midnight

        public static void Add(EmpireState s, int amount)
        {
            if (s == null) return;
            s.Heat = Math.Max(0, Math.Min(Max, s.Heat + amount));
        }

        public static void Nightly(EmpireState s) { Add(s, -NightlyCool); }

        static double Share(int heat) { return Math.Max(0, Math.Min(Max, heat)) / (double)Max; }

        /// <summary>Where the streets sit for a steady player taking a front every day or two: the attack pace is unchanged.</summary>
        public const int Neutral = 40;

        /// <summary>Attack cooldown multiplier: x1.25 (calmer) on cool streets, x1.0 at 40, x0.65 at full heat.</summary>
        public static double CooldownFactor(int heat)
        {
            int h = Math.Max(0, Math.Min(Max, heat));
            return h <= Neutral ? 1.0 + 0.25 * (Neutral - h) / Neutral : 1.0 - 0.35 * (h - Neutral) / (double)(Max - Neutral);
        }

        /// <summary>Crew fronts on alert: up to x1.8 harder to raid at full heat.</summary>
        public static double CrewDefenseFactor(int heat) { return 1.0 + 0.8 * Share(heat); }

        /// <summary>Extra armour on a crew front's soldiers in a live fight (on top of the front's own).</summary>
        public static int ExtraArmor(int heat) { return (int)Math.Round(50 * Share(heat)); }

        /// <summary>Accuracy for a crew front's soldiers in a live fight: 25 on cool streets, up to 45 at full heat.</summary>
        public static int CrewAccuracy(int heat) { return 25 + (int)Math.Round(20 * Share(heat)); }

        /// <summary>Counts like grudge for how many heavy guns a crew front's soldiers carry.</summary>
        public static int ExtraGrudge(int heat) { return (int)Math.Round(60 * Share(heat)); }

        /// <summary>Blazing streets (70+): crews send one more attacker.</summary>
        public static int SquadBonus(int heat) { return heat >= 70 ? 1 : 0; }

        public static string Label(int heat)
        {
            if (heat < 15) return "Cool";
            if (heat < 40) return "Warm";
            if (heat < 70) return "Hot";
            return "Blazing";
        }
    }
}
