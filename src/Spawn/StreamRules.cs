using System;
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

        /// <summary>Every guard keeps watch: at night they shine a torch, by day the post's own idle.</summary>
        /// <summary>Who walks a patrol: every second person at a front (the first always holds the door), so with two or
        /// more there's always someone on the move around the building and someone at the entrance.</summary>
        /// <summary>Run over or rammed by a car. It counts like any kill, but the first one at a front makes them call for
        /// backup: a crew car with more soldiers turns up shortly after, so a front can't be cleared from the driver's seat.</summary>
        public static bool VehicleKill(int causeHash, bool killerIsVehicle)
        {
            return killerIsVehicle || causeHash == RunOverHash || causeHash == RammedHash;
        }

        public static readonly int RunOverHash = Markers.Hash("WEAPON_RUN_OVER_BY_CAR"), RammedHash = Markers.Hash("WEAPON_RAMMED_BY_CAR");
        public const int BackupDelayMs = 20000;    // the backup car arrives about this long after the call
        public const int BackupSize = 2;

        public static bool Patrols(int index, int count) { return count >= 2 && index % 2 == 1; }

        /// <summary>What a patroller does at each stop, and for how long: 12-25 seconds of standing guard, a smoke, a
        /// phone call or a lean, picked from a stable number so it doesn't flicker.</summary>
        public static readonly string[] PatrolStops = { GuardStand, "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_STAND_MOBILE", GuardStand, "WORLD_HUMAN_LEANING" };

        public static string PatrolScenario(int pick) { return PatrolStops[(int)((uint)(pick * 2654435761u) % (uint)PatrolStops.Length)]; }

        public static int PatrolDwellMs(int pick) { return 12000 + (int)((uint)(pick * 40503u) % 13000u); }

        /// <summary>Patrol points sorted into a loop around the door (by angle), so a patroller walks the building's
        /// sides and back in order instead of zig-zagging. xs/ys are offsets from the door.</summary>
        public static int[] LoopOrder(float[] xs, float[] ys)
        {
            var idx = new int[xs.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => Math.Atan2(ys[a], xs[a]).CompareTo(Math.Atan2(ys[b], xs[b])));
            return idx;
        }

        public static string GuardScenario(string postScenario, Trait trait, int hour)
        {
            if (DefenseRating.IsNight(hour)) return Torch;
            return string.IsNullOrEmpty(postScenario) ? GuardStand : postScenario;
        }
        // ---------------- morale in a live fight ----------------

        /// <summary>An attacker this badly hurt (health left, as a share of full) makes a run for it.</summary>
        public const double BreakHealth = 0.25;

        public static bool Hurt(int health, int maxHealth) { return maxHealth > 0 && health > 0 && health <= maxHealth * BreakHealth; }

        /// <summary>The rest break and run together: outnumbered two to one by you and your people, once they're out of
        /// their cars and in the fight, and only when they're down to a few.</summary>
        public static bool Rout(int defenders, int attackersFighting, bool inTheFight)
        {
            return inTheFight && attackersFighting > 0 && attackersFighting <= 3 && defenders >= attackersFighting * 2;
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
