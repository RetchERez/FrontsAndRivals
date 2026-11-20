// Fronts & Rivals: decides whether the mod may start anything new this frame.
using GTA;
using GTA.Native;

namespace FrontsAndRivals.Core
{
    public static class PauseGate
    {
        /// <summary>True when Fronts & Rivals may act. Checked every tick; all calls are cheap.</summary>
        public static bool CanAct()
        {
            // Never in GTA Online (Rockstar's guidelines: multiplayer modding is off-limits).
            if (Function.Call<bool>(Hash.NETWORK_IS_SESSION_STARTED)) return false;

            // Story or strand mission, or a random event, is running.
            if (Game.IsMissionActive || Game.IsRandomEventActive) return false;

            // Character switch, cutscene, or a game-driven fade.
            if (Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS)) return false;
            if (Function.Call<bool>(Hash.IS_CUTSCENE_ACTIVE)) return false;
            if (Function.Call<bool>(Hash.IS_SCREEN_FADED_OUT)) return false;

            // Player must exist, be alive, not arrested, and in control.
            Player player = Game.Player;
            if (!Function.Call<bool>(Hash.IS_PLAYER_PLAYING, player)) return false;
            if (!player.CanControlCharacter) return false;

            return true;
        }
    }
}
