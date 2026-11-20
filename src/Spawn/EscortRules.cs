// Fronts & Rivals: bodyguards. Pure rules, no game calls, unit-tested.
// Up to two of your guards can leave their post to follow you around the city and fight at your side.
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

        /// <summary>Can this guard be called? Healthy, not already with you, and fewer than two bodyguards out.</summary>
        public static bool CanCall(bool active, bool alreadyEscort, int escortsNow)
        {
            return active && !alreadyEscort && escortsNow < Max;
        }
    }
}
