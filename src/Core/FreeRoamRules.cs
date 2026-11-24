// Fronts & Rivals: what counts as "not free roam". The mod only runs in true free roam: never in a story mission, a
// Strangers & Freaks job, a side job, a minigame, a heist step or a friend activity, even the ones that don't set the
// game's mission flag. The names are the game's own script names (from the decompiled game scripts), so nothing is
// guessed; a script on this list running means "not free roam". Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;

namespace FrontsAndRivals.Core
{
    public static class FreeRoamRules
    {
        /// <summary>Scripts that only run while you're doing something: a mission, job, activity or minigame.
        /// Always-on controllers and launchers (blip_controller, launcher_golf, controller_taxi...) are never here,
        /// or the mod would never run; nor are the "ambient" heist-prep watchers, hunting's ambient script or the pier
        /// rides, which can run in plain free roam.</summary>
        public static readonly string[] Blocking = Build();

        private static string[] Build()
        {
            var list = new List<string>();
            // Story missions (they set the mission flag too; listed so a missed flag can't slip through)
            list.AddRange(new[] { "prologue1", "armenian1", "armenian2", "armenian3", "franklin0", "franklin1", "franklin2", "lamar1",
                "family1", "family2", "family3", "family4", "family5", "family6", "michael1", "michael2", "michael3", "michael4",
                "trevor1", "trevor2", "trevor3", "trevor4", "fbi1", "fbi2", "fbi3", "fbi4_intro", "fbi4", "fbi5a",
                "chinese1", "chinese2", "exile1", "exile2", "exile3", "martin1", "solomon1", "solomon2", "solomon3", "lester1",
                "carsteal1", "carsteal2", "carsteal3", "carsteal4", "finale_intro", "finale_heist1", "finale_heist2_intro",
                "finale_heist2a", "finale_heist2b", "finalea", "finaleb", "finalec1", "finalec2", "finale_choice", "finale_credits",
                "selling_short_1", "selling_short_2" });
            // Heists: setups, preps and the jobs themselves
            list.AddRange(new[] { "jewelry_setup1", "jewelry_prep1a", "jewelry_prep1b", "jewelry_prep2a", "jewelry_heist",
                "docks_setup", "docks_prep1", "docks_prep2b", "docks_heista", "docks_heistb",
                "rural_bank_setup", "rural_bank_prep1", "rural_bank_heist",
                "agency_prep1", "agency_heist1", "agency_heist2", "agency_heist3a", "agency_heist3b",
                "fbi4_prep1", "fbi4_prep2", "fbi4_prep3", "fbi4_prep4", "fbi4_prep5",
                "finale_heist_prepa", "finale_heist_prepb", "finale_heist_prepc", "finale_heist_prepd" });
            // Strangers and Freaks, and Lester's assassinations
            list.AddRange(new[] { "abigail1", "abigail2", "barry1", "barry2", "barry3", "barry3a", "barry3c", "barry4", "dreyfuss1",
                "epsilon1", "epsilon2", "epsilon3", "epsilon4", "epsilon5", "epsilon6", "epsilon7", "epsilon8",
                "extreme1", "extreme2", "extreme3", "extreme4", "fanatic1", "fanatic2", "fanatic3", "hao1", "hunting1", "hunting2",
                "josh1", "josh2", "josh3", "josh4", "maude1", "minute1", "minute2", "minute3", "mrsphilips1", "mrsphilips2",
                "nigel1", "nigel1a", "nigel1b", "nigel1c", "nigel1d", "nigel2", "nigel3", "omega1", "omega2",
                "paparazzo1", "paparazzo2", "paparazzo3", "paparazzo3a", "paparazzo3b", "paparazzo4",
                "tonya1", "tonya2", "tonya3", "tonya4", "tonya5", "thelastone",
                "bailbond1", "bailbond2", "bailbond3", "bailbond4",
                "assassin_bus", "assassin_construction", "assassin_hooker", "assassin_multi", "assassin_valet",
                "me_amanda1", "me_jimmy1", "me_tracey1" });
            // Side jobs: taxi, towing, trafficking, property missions, rampages
            list.AddRange(new[] { "taxiservice", "taxitutorial", "taxi_procedural", "taxi_clowncar", "taxi_cutyouin", "taxi_deadline",
                "taxi_followcar", "taxi_gotyounow", "taxi_gotyourback", "taxi_needexcitement", "taxi_takeiteasy", "taxi_taketobest",
                "towing", "traffick_air", "traffick_ground",
                "pm_defend", "pm_delivery", "pm_gang_attack", "pm_plane_promotion", "pm_recover_stolen",
                "rampage1", "rampage2", "rampage3", "rampage4", "rampage5" });
            // Minigames and activities
            list.AddRange(new[] { "golf", "golf_ai_foursome", "golf_ai_foursome_putting", "tennis", "tennis_family", "darts", "yoga",
                "triathlonsp", "offroad_races", "country_race", "stunt_plane_races", "pilot_school", "range_modern",
                "bj", "stripclub", "friendactivity" });
            // Director Mode (its script only runs while you're in it)
            list.Add("director_mode");
            return list.ToArray();
        }

        /// <summary>The three playable characters; anyone else (Director Mode actors, peyote animals) isn't free roam.</summary>
        public static readonly string[] Protagonists = { "player_zero", "player_one", "player_two" };

        /// <summary>Map blip sprites that mark where a story mission, a Strangers and Freaks job or a heist starts
        /// (docs.fivem.net blip reference). No street crews or attacks start within MissionStartRange of one, so a
        /// mission always begins on a clean street.</summary>
        public static readonly HashSet<int> MissionStartSprites = new HashSet<int>
        {
            212, 213, 214,                               // Franklin's mission letters
            66, 114, 115,                                // random characters (Strangers and Freaks)
            76, 77, 78, 79, 86, 88, 89, 108, 112, 120, 123, 124, 207, 208, 209, 210, 211, // family and strand missions
            59, 80, 96, 104, 105, 106, 107, 113, 118     // heists
        };

        public const float MissionStartRange = 120f;

        public static bool IsMissionStartSprite(int sprite) { return MissionStartSprites.Contains(sprite); }
    }
}
