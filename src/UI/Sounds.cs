// Fronts & Rivals: a few of the game's own frontend sounds for moments that matter. All from GTA V's always-loaded
// HUD sound sets (names checked against the DurtyFree soundNames.json dump), played with PLAY_SOUND_FRONTEND.
// INI [Sound] Effects / Office > Settings > Sound effects turns them off.
using System;
using GTA.Native;

namespace FrontsAndRivals.UI
{
    public enum Cue { AttackWarning, AttackStarted, Purchase, MidnightTake, FrontTaken, FrontDefended, FrontLost, NemesisKilled }

    public static class Sounds
    {
        /// <summary>Asked every time a cue plays, so the Office toggle works immediately.</summary>
        public static Func<bool> Enabled = () => true;

        public static void Play(Cue cue)
        {
            if (!Enabled()) return;
            string name, set;
            switch (cue)
            {
                case Cue.AttackWarning: name = "TIMER_STOP"; set = "HUD_MINI_GAME_SOUNDSET"; break;
                case Cue.AttackStarted: name = "5_SEC_WARNING"; set = "HUD_MINI_GAME_SOUNDSET"; break;
                case Cue.Purchase: name = "PURCHASE"; set = "HUD_LIQUOR_STORE_SOUNDSET"; break;
                case Cue.MidnightTake: name = "ROBBERY_MONEY_TOTAL"; set = "HUD_FRONTEND_CUSTOM_SOUNDSET"; break;
                case Cue.FrontTaken: name = "PROPERTY_PURCHASE"; set = "HUD_AWARDS"; break;
                case Cue.FrontDefended: name = "MP_WAVE_COMPLETE"; set = "HUD_FRONTEND_DEFAULT_SOUNDSET"; break;
                case Cue.FrontLost: name = "LOSER"; set = "HUD_AWARDS"; break;
                default: name = "RANK_UP"; set = "HUD_AWARDS"; break;
            }
            try
            {
                // NativeDB: soundId (-1 = fire and forget), audioName, audioRef, p3
                Function.Call(Hash.PLAY_SOUND_FRONTEND, -1, name, set, true);
            }
            catch { }
        }
    }
}
