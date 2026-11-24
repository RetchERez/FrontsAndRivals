// Fronts & Rivals: decides whether the mod may act this tick, and why not.
// The mod only runs in true free roam (FreeRoamRules): never in a mission, side job, minigame, heist step, friend
// activity, Director Mode, or as anyone but Michael, Franklin or Trevor.
using GTA;
using GTA.Native;

namespace FrontsAndRivals.Core
{
    public enum PauseReason
    {
        None,             // open: the mod may act
        Online,           // GTA Online session: the mod switches itself off for good
        Mission,          // story or strand mission
        RandomEvent,
        CharacterSwitch,
        Cutscene,
        FadedOut,
        PlayerNotPlaying, // dead, arrested, or no ped
        NoControl,
        Minigame,         // golf, tennis, darts and the rest
        SideJob,          // a mission, job or activity script running without the mission flag
        DirectorMode,
        NotProtagonist    // an animal (peyote) or anyone who isn't Michael, Franklin or Trevor
    }

    public static class PauseGate
    {
        private static int _scriptsCheckedAt = -100000;
        private static string _runningScript;
        private static int[] _hashes;
        private static int[] _protagonists;

        /// <summary>The game script that last paused the mod (for the log).</summary>
        public static string LastScript { get { return _runningScript; } }

        /// <summary>Checked every tick; all calls are cheap reads (the script list is checked twice a second).</summary>
        public static PauseReason Check()
        {
            // Rockstar's guidelines: never in multiplayer.
            if (Function.Call<bool>(Hash.NETWORK_IS_SESSION_STARTED)) return PauseReason.Online;

            if (Game.IsMissionActive) return PauseReason.Mission;
            if (Game.IsRandomEventActive) return PauseReason.RandomEvent;
            if (Function.Call<bool>(Hash.IS_MINIGAME_IN_PROGRESS)) return PauseReason.Minigame;
            if (Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS)) return PauseReason.CharacterSwitch;
            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE)) return PauseReason.Cutscene;
            if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)) return PauseReason.FadedOut;

            Player player = Game.Player;
            if (!Function.Call<bool>(Hash.IS_PLAYER_PLAYING, player)) return PauseReason.PlayerNotPlaying;
            if (!IsProtagonist(player.Character)) return PauseReason.NotProtagonist;
            if (SideJobRunning()) return _runningScript == "director_mode" ? PauseReason.DirectorMode : PauseReason.SideJob;
            if (!player.CanControlCharacter) return PauseReason.NoControl;

            return PauseReason.None;
        }

        private static bool IsProtagonist(Ped me)
        {
            if (me == null) return false;
            if (_protagonists == null)
            {
                _protagonists = new int[FreeRoamRules.Protagonists.Length];
                for (int i = 0; i < _protagonists.Length; i++) _protagonists[i] = Game.GenerateHash(FreeRoamRules.Protagonists[i]);
            }
            int model = me.Model.Hash;
            foreach (int h in _protagonists) if (h == model) return true;
            return false;
        }

        /// <summary>Any blocking game script running (FreeRoamRules.Blocking). Checked every 500 ms; between checks the
        /// last answer stands.</summary>
        private static bool SideJobRunning()
        {
            int now = Game.GameTime;
            if (now - _scriptsCheckedAt < 500) return _runningScript != null;
            _scriptsCheckedAt = now;
            if (_hashes == null)
            {
                _hashes = new int[FreeRoamRules.Blocking.Length];
                for (int i = 0; i < _hashes.Length; i++) _hashes[i] = Game.GenerateHash(FreeRoamRules.Blocking[i]);
            }
            for (int i = 0; i < _hashes.Length; i++)
                if (Function.Call<int>(Hash.GET_NUMBER_OF_THREADS_RUNNING_THE_SCRIPT_WITH_THIS_HASH, _hashes[i]) > 0)
                {
                    _runningScript = FreeRoamRules.Blocking[i];
                    return true;
                }
            _runningScript = null;
            return false;
        }

        /// <summary>Reasons that must leave the world empty of mod entities: missions, jobs and activities never see
        /// our people, and a character switch streams everything out around the old character.</summary>
        public static bool ClearsWorld(PauseReason r)
        {
            return r == PauseReason.Online || r == PauseReason.Mission || r == PauseReason.RandomEvent
                || r == PauseReason.CharacterSwitch || r == PauseReason.Minigame || r == PauseReason.SideJob
                || r == PauseReason.DirectorMode || r == PauseReason.NotProtagonist;
        }
    }
}
