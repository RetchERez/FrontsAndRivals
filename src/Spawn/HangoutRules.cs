// Fronts & Rivals: crews on their streets. Pure rules, no game calls, unit-tested.
// Like rival gangs in Saints Row, a crew's people turn up anywhere on its turf: a few hanging out (sometimes round
// one of their cars), one or two walking the block, or a car full of them cruising. Shoot them and their crew is a
// soldier short. How many are out depends on street heat: quiet streets have a couple of groups, hot ones more.
using System;

namespace FrontsAndRivals.Spawn
{
    public enum HangoutKind { HangOut, Walk, Cruise }

    public static class HangoutRules
    {
        public const float TurfRange = 600f;       // a crew front this close to a spot makes it that crew's turf
        public const float SpawnMin = 70f, SpawnMax = 150f; // from you: out of sight when they appear
        public const float DespawnRange = 280f;    // gone once you're well away
        public const float AlertOnFoot = 25f, AlertShots = 70f;
        public const int CarChancePct = 35;        // a group hanging out round one of their cars

        public static readonly string[] Scenarios =
        {
            "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_HANG_OUT_STREET", "WORLD_HUMAN_LEANING",
            "WORLD_HUMAN_STAND_MOBILE", "WORLD_HUMAN_SMOKING_POT"
        };

        /// <summary>Groups out at once: 2 on cool streets, 3 once it's warm (40+), never more, so the ped budget always
        /// has room for a real fight at a front.</summary>
        public static int MaxGroups(int heat) { return heat >= 40 ? 3 : 2; }

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
