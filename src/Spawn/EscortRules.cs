// Fronts & Rivals: bodyguards. Pure rules, no game calls, unit-tested.
// Bodyguards are earned: the first at 3 fronts, the second at 8, and only Soldiers or better (a Recruit isn't trusted
// with your back yet). They leave their post to follow you around the city and fight at your side.
// Their post stays theirs: send them back and they return to it.
namespace FrontsAndRivals.Spawn
{
    public static class EscortRules
    {
        public const int Max = 2;
        public const float LeftBehindRange = 200f;  // further than this (you drove off): they catch up, re-placed near you
        public const float BodyRange = 100f;        // a fallen bodyguard's body is cleared once you're this far away
        public const int Reserve = 10;              // ped slots always left for a live fight at a front

        /// <summary>Room for one more bodyguard ped without eating into the slots a live defense needs.</summary>
        public static bool Fits(int pedCount, int maxPeds) { return pedCount + 1 <= maxPeds - Reserve; }

        /// <summary>Fronts you must hold for the first and second bodyguard (the same steps as bigger fronts).</summary>
        public const int FirstAt = 3, SecondAt = 8;

        /// <summary>How many bodyguards you've earned: none before 3 fronts, one at 3, two at 8.</summary>
        public static int Slots(int frontsHeld) { return frontsHeld >= SecondAt ? 2 : frontsHeld >= FirstAt ? 1 : 0; }

        /// <summary>Fronts needed for the next bodyguard, or -1 when you have both.</summary>
        public static int NextAt(int frontsHeld) { return frontsHeld < FirstAt ? FirstAt : frontsHeld < SecondAt ? SecondAt : -1; }

        /// <summary>Rank 1 (Soldier) or better.</summary>
        public static bool Trusted(int rank) { return rank >= 1; }

        /// <summary>Can this guard be called? Healthy, a Soldier or better, not already with you, and a slot free.</summary>
        public static bool CanCall(bool active, bool alreadyEscort, int escortsNow, int frontsHeld, int rank)
        {
            return active && !alreadyEscort && Trusted(rank) && escortsNow < Slots(frontsHeld);
        }

        /// <summary>Old form: two slots, any rank (kept for the tests of the ped budget).</summary>
        public static bool CanCall(bool active, bool alreadyEscort, int escortsNow)
        {
            return active && !alreadyEscort && escortsNow < Max;
        }
    }
}
