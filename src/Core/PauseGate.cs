// Fronts & Rivals: decides whether the mod may act this tick, and why not.
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
        NoControl
    }

    public static class PauseGate
    {
        /// <summary>Checked every tick; all calls are cheap reads.</summary>
        public static PauseReason Check()
        {
            // Rockstar's guidelines: never in multiplayer.
            if (Function.Call<bool>(Hash.NETWORK_IS_SESSION_STARTED)) return PauseReason.Online;

            if (Game.IsMissionActive) return PauseReason.Mission;
            if (Game.IsRandomEventActive) return PauseReason.RandomEvent;
            if (Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS)) return PauseReason.CharacterSwitch;
            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE)) return PauseReason.Cutscene;
            if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)) return PauseReason.FadedOut;

            Player player = Game.Player;
            if (!Function.Call<bool>(Hash.IS_PLAYER_PLAYING, player)) return PauseReason.PlayerNotPlaying;
            if (!player.CanControlCharacter) return PauseReason.NoControl;

            return PauseReason.None;
        }

        /// <summary>Reasons that must leave the world empty of mod entities: missions never see our peds,
        /// and a character switch streams everything out around the old character.</summary>
        public static bool ClearsWorld(PauseReason r)
        {
            return r == PauseReason.Online || r == PauseReason.Mission
                || r == PauseReason.RandomEvent || r == PauseReason.CharacterSwitch;
        }
    }
}
