// Fronts & Rivals: crews on their streets. Pure rules, no game calls, unit-tested.
// Like rival gangs in Saints Row, a crew's people turn up anywhere on its turf: a few hanging out (sometimes round
// one of their cars), one or two walking the block, or a car full of them cruising. Shoot them and their crew is a
// soldier short. How many are out depends on street heat: quiet streets have a couple of groups, hot ones more.
using System;

namespace FrontsAndRivals.Spawn
{
    public enum HangoutKind { HangOut, Walk, Cruise }

    /// <summary>What a rival crew group gets up to on the street, now and then.</summary>
    public enum StreetCrime { None, Mugging, Carjack, Shooting, Chase, Kidnap }

    public static class HangoutRules
    {
        public const float TurfRange = 600f;       // a crew front this close to a spot makes it that crew's turf
        public const float DeepTurfRange = 250f;   // this close to one of their fronts, one more of their groups is about
        public const double DeepTurfMinFps = 40;   // ...unless the game is already struggling
        public const float SpawnMin = 70f, SpawnMax = 150f; // from you: out of sight when they appear
        public const float DespawnRange = 280f;    // gone once you're well away
        /// <summary>Street crews warn you off at 15 m on foot (they watch from 30 m). AlertShots: how near a fight has to be for
        /// your own people to join in (rival crews hear shots by WatchRules.ShotHeard).</summary>
        public const float AlertOnFoot = 15f, AlertShots = 70f;
        public const int CarChancePct = 35;        // a group hanging out round one of their cars
        public const int ClashChancePct = 15;      // on a border between two crews' turf: they run into each other and fight
        public const float ClashGap = 18f;         // how far apart the two sides start

        public static bool Clash(int roll) { return Math.Abs(roll) % 100 < ClashChancePct; }

        /// <summary>At most one street crime at a time, and three minutes between them, so the city feels rough without
        /// turning into a war zone.</summary>
        public const int CrimeCooldownMs = 180000;
        public const int PoliceChancePct = 40;     // cops show up at a street fight
        public const int PoliceDelayMs = 25000;    // after it kicks off

        /// <summary>A walking crew member: mugging 15%, carjacking 12%, a shooting 5%. A crew car: a police chase 18%,
        /// a kidnapping 8%. Otherwise nothing.</summary>
        public static StreetCrime CrimeFor(HangoutKind kind, int roll)
        {
            int r = Math.Abs(roll) % 100;
            if (kind == HangoutKind.Walk) return r < 15 ? StreetCrime.Mugging : r < 27 ? StreetCrime.Carjack : r < 32 ? StreetCrime.Shooting : StreetCrime.None;
            if (kind == HangoutKind.Cruise) return r < 18 ? StreetCrime.Chase : r < 26 ? StreetCrime.Kidnap : StreetCrime.None;
            return StreetCrime.None;
        }

        public static bool PoliceCome(int roll) { return Math.Abs(roll) % 100 < PoliceChancePct; }

        public static readonly string[] Scenarios =
        {
            "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_HANG_OUT_STREET", "WORLD_HUMAN_LEANING",
            "WORLD_HUMAN_STAND_MOBILE", "WORLD_HUMAN_SMOKING_POT"
        };

        /// <summary>Groups out at once: 2 on cool streets, 3 once it's warm (40+), never more, so the ped budget always
        /// has room for a real fight at a front.</summary>
        public static int MaxGroups(int heat) { return heat >= 40 ? 3 : 2; }

        /// <summary>The frame-rate safeguard: street groups are scenery, so when the game is struggling (smoothed FPS under
        /// 40) only one is out, and under 30 none, until it recovers.</summary>
        public static int MaxGroups(int heat, double fps)
        {
            if (fps > 0 && fps < 30) return 0;
            if (fps > 0 && fps < 40) return Math.Min(1, MaxGroups(heat));
            return MaxGroups(heat);
        }

        /// <summary>Time between new groups: 30 seconds on cool streets down to 15 at full heat.</summary>
        public static int RespawnMs(int heat) { return 30000 - 150 * Math.Max(0, Math.Min(100, heat)); }

        /// <summary>What kind of group: hanging out 45%, walking 30%, cruising 25%.</summary>
        public static HangoutKind Kind(int roll)
        {
            int r = Math.Abs(roll) % 100;
            return r < 45 ? HangoutKind.HangOut : r < 75 ? HangoutKind.Walk : HangoutKind.Cruise;
        }

        /// <summary>People in a group: hanging out 1-3 (mostly 2), walking 1-2, cruising 2-4 (trimmed to the seats).</summary>
        public static int GroupSize(HangoutKind kind, int roll)
        {
            int r = Math.Abs(roll) % 100;
            switch (kind)
            {
                case HangoutKind.Walk: return r < 50 ? 1 : 2;
                case HangoutKind.Cruise: return r < 40 ? 2 : r < 80 ? 3 : 4;
                default: return r < 20 ? 1 : r < 70 ? 2 : 3;
            }
        }

        /// <summary>Old form: a hanging-out group of 2 or 3.</summary>
        public static int GroupSize(int roll) { return Math.Abs(roll) % 100 < 60 ? 2 : 3; }

        public static bool WithCar(int roll) { return Math.Abs(roll) % 100 < CarChancePct; }

        public static string Scenario(int pick) { return Scenarios[(int)((uint)(pick * 2654435761u) % (uint)Scenarios.Length)]; }

        /// <summary>Room for a group of this size without eating into the slots a live fight at a front needs.</summary>
        public static bool Fits(int pedCount, int size, int maxPeds) { return pedCount + size <= maxPeds - EscortRules.Reserve; }
    }
}
