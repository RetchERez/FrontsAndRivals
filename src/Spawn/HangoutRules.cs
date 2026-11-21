// Fronts & Rivals: crew hangouts. Pure rules, no game calls, unit-tested.
// Small groups of a crew's people loiter on the streets of their own turf, like rival gangs in Saints Row: two or
// three of them smoking, drinking and talking, sometimes round one of their cars. Shoot them and their crew is a
// soldier short.
using System;

namespace FrontsAndRivals.Spawn
{
    public static class HangoutRules
    {
        public const int MaxGroups = 2;            // at once, so the ped budget always has room for a real fight
        public const float TurfRange = 450f;       // a crew front this close makes the streets around you its turf
        public const float SpawnMin = 70f, SpawnMax = 140f; // from you: out of sight when they appear
        public const float FrontMin = 40f, FrontMax = 120f; // from the crew's front: their streets, not their door
        public const float DespawnRange = 260f;    // gone once you're well away
        public const float AlertOnFoot = 25f, AlertShots = 70f;
        public const int RespawnMs = 20000;        // between new groups
        public const int CarChancePct = 35;

        public static readonly string[] Scenarios =
        {
            "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_DRINKING", "WORLD_HUMAN_HANG_OUT_STREET", "WORLD_HUMAN_LEANING",
            "WORLD_HUMAN_STAND_MOBILE", "WORLD_HUMAN_SMOKING_POT"
        };

        /// <summary>2 or 3 to a group (about 60/40).</summary>
        public static int GroupSize(int roll) { return Math.Abs(roll) % 100 < 60 ? 2 : 3; }

        public static bool WithCar(int roll) { return Math.Abs(roll) % 100 < CarChancePct; }

        public static string Scenario(int pick) { return Scenarios[(int)((uint)(pick * 2654435761u) % (uint)Scenarios.Length)]; }

        /// <summary>Room for a group of this size without eating into the slots a live fight at a front needs.</summary>
        public static bool Fits(int pedCount, int size, int maxPeds) { return pedCount + size <= maxPeds - EscortRules.Reserve; }
    }
}
