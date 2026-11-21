// Fronts & Rivals: the pure rules behind streaming fronts in and out. No game calls, unit-tested.
using FrontsAndRivals.Rules;

namespace FrontsAndRivals.Spawn
{
    public static class StreamRules
    {
        public const float SpawnRange = 250f;   // guards appear inside this
        public const float DespawnRange = 300f; // and go away past this; the 50 m gap stops flicker
        public const int RespawnDelayMs = 10000; // a guard deleted by another mod comes back after 10 s
        public const int ModelTimeoutMs = 5000;

        /// <summary>Do a crew front's soldiers turn on you? Until you own a front you're nobody to them: only hurting
        /// or killing one of them starts a fight. Once you're in the game, walking up or shooting nearby does too.</summary>
        public static bool CrewProvoked(bool inTheGame, bool onFootNear, bool shootingNear, bool hurtOne)
        {
            if (hurtOne) return true;
            return inTheGame && (onFootNear || shootingNear);
        }

        /// <summary>Hysteresis: stream in under 250 m, out over 300 m, otherwise keep the current state.</summary>
        public static bool ShouldStream(bool streamedNow, float distance)
        {
            if (distance < SpawnRange) return true;
            if (distance > DespawnRange) return false;
            return streamedNow;
        }

        public const string GuardStand = "WORLD_HUMAN_GUARD_STAND";
        public const string Torch = "WORLD_HUMAN_SECURITY_SHINE_TORCH";
        public const string Binoculars = "WORLD_HUMAN_BINOCULARS";

        /// <summary>Guard idles allowed at posts (all in the game's scenario list).</summary>
        public static readonly string[] PostScenarios =
        {
            GuardStand, "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_LEANING", "WORLD_HUMAN_STAND_MOBILE"
        };

        /// <summary>Lookouts use binoculars; at night everyone else shines a torch; otherwise the post's own idle.</summary>
        public static string GuardScenario(string postScenario, Trait trait, int hour)
        {
            if (trait == Trait.Lookout) return Binoculars;
            if (DefenseRating.IsNight(hour)) return Torch;
            return string.IsNullOrEmpty(postScenario) ? GuardStand : postScenario;
        }
    }

    /// <summary>Laying low: with stars within 250 m of a front, its guards go inside. They come back
    /// 60 s after the stars clear, and stay hidden while the stars are grey (police still searching).</summary>
    public sealed class LayLowTimer
    {
        public const int ReturnDelayMs = 60000;

        private bool _hidden;
        private long _clearSince = -1;

        public bool Hidden { get { return _hidden; } }

        /// <param name="nowMs">Free-roam clock.</param>
        /// <param name="wanted">Wanted level above 0.</param>
        /// <param name="near">The player is within 250 m of the front.</param>
        /// <param name="starsGrey">The police are still searching.</param>
        public bool Update(long nowMs, bool wanted, bool near, bool starsGrey)
        {
            if (wanted && near)
            {
                _hidden = true;
                _clearSince = -1;
            }
            else if (_hidden)
            {
                if (wanted || starsGrey) _clearSince = -1; // not clear yet, wherever the player is
                else if (_clearSince < 0) _clearSince = nowMs;
                else if (nowMs - _clearSince >= ReturnDelayMs) { _hidden = false; _clearSince = -1; }
            }
            return _hidden;
        }

        public void Reset() { _hidden = false; _clearSince = -1; }
    }
}
