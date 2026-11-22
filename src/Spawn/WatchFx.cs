// Fronts & Rivals: the game side of WatchRules, shared by front soldiers (Streamer) and street crews (Hangouts):
// what the player is doing that a crew could notice, and how the crew shows it (guns up, blips, a warning line).
using System;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.UI;

namespace FrontsAndRivals.Spawn
{
    public static class WatchFx
    {
        /// <summary>How far a crew can pick you out right now: crouched in stealth mode (better with the character's own
        /// Stealth skill) and at night they notice you later.</summary>
        public static float SightScale()
        {
            Ped me = Game.Player.Character;
            bool stealth = false;
            int skill = 0;
            try
            {
                stealth = me.IsInStealthMode;
                if (stealth) skill = StealthSkill(me);
            }
            catch (Exception ex) { Log.Error("Stealth read", ex); }
            int hour = GTA.World.CurrentTimeOfDay.Hours;
            return WatchRules.SightScale(stealth, skill, WatchRules.IsNight(hour));
        }

        /// <summary>The story character's Stealth stat (0-100), 0 for anyone else.</summary>
        private static int StealthSkill(Ped me)
        {
            int slot = me.Model == new Model(PedHash.Michael) ? 0 : me.Model == new Model(PedHash.Franklin) ? 1 : me.Model == new Model(PedHash.Trevor) ? 2 : -1;
            if (slot < 0) return 0;
            var value = new OutputArgument();
            if (!Function.Call<bool>(Hash.STAT_GET_INT, Game.GenerateHash("SP" + slot + "_STEALTH_ABILITY"), value, -1)) return 0;
            return Math.Max(0, Math.Min(100, value.GetResult<int>()));
        }

        /// <summary>Is the player aiming at this person (lock-on or free aim)?</summary>
        public static bool AimedAt(Ped p)
        {
            Player me = Game.Player;
            return me.IsTargeting(p) || Function.Call<bool>(Hash.IS_PLAYER_FREE_AIMING_AT_ENTITY, me, p);
        }

        /// <summary>A shot they'd hear: any gunfire within 80 m, a silenced one only within 20 m.</summary>
        public static bool HeardShot(float distance)
        {
            Ped me = Game.Player.Character;
            if (!me.IsShooting) return false;
            bool silenced = false;
            try { silenced = Function.Call<bool>(Hash.IS_PED_CURRENT_WEAPON_SILENCED, me); } catch { }
            return WatchRules.HeardShot(true, silenced, distance);
        }

        /// <summary>Can this person see you? Calm people only notice what's in front of them.</summary>
        public static bool Sees(Ped p, bool calm)
        {
            Ped me = Game.Player.Character;
            return calm ? Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY_IN_FRONT, p, me)
                        : Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, p, me, 17);
        }

        /// <summary>Guns up at the player (anyone with only a bat or a knife squares up instead).</summary>
        public static void Raise(Ped p)
        {
            Ped me = Game.Player.Character;
            if (Function.Call<bool>(Hash.IS_PED_ARMED, p, 4)) p.Task.AimAt(me, -1);
            else p.Task.TurnTo(me, -1);
        }

        /// <summary>Turns to keep an eye on the player until re-tasked.</summary>
        public static void Eye(Ped p)
        {
            Ped me = Game.Player.Character;
            p.Task.TurnTo(me, -1);
            p.Task.LookAt(me, -1);
        }

        /// <summary>A line in the subtitle bar, the crew's name in its colour (text only: no reused game voices).
        /// The first warning in a save also brings up a help box explaining it.</summary>
        public static void Say(ContentDb content, string crewId, string moment)
        {
            try
            {
                CrewDef cd = content.Crew(crewId);
                CrewColor c = CrewColors.Get(CrewColors.ForCrew(crewId));
                string line = content.Lines.Pick(new Random(Game.GameTime), moment, null);
                if (string.IsNullOrEmpty(line)) return;
                GTA.UI.Screen.ShowSubtitle((c != null ? c.Text : "") + (cd != null ? cd.Name : "Crew") + ":~s~ " + line, 4000);
                if (moment.StartsWith("warn", StringComparison.Ordinal)) Tips.Show(Tip.Warned);
            }
            catch (Exception ex) { Log.Error("Warning line", ex); }
        }

        public static void Blip(ContentDb content, Ped p, string crewId)
        {
            try
            {
                CrewDef cd = content.Crew(crewId);
                if (p.AttachedBlip == null || !p.AttachedBlip.Exists())
                {
                    Blip b = p.AddBlip();
                    b.Color = cd != null ? (BlipColor)cd.BlipColor : BlipColor.Red;
                    b.Scale = 0.7f;
                    b.Name = cd != null ? cd.Name : "Crew";
                }
            }
            catch (Exception ex) { Log.Error("Crew blip", ex); }
        }

        public static void DropBlip(Ped p)
        {
            try { if (p.AttachedBlip != null && p.AttachedBlip.Exists()) p.AttachedBlip.Delete(); } catch { }
        }

        /// <summary>The player's distance to the nearest living person in the list (float.MaxValue if none).</summary>
        public static float Nearest(System.Collections.Generic.IEnumerable<Ped> peds)
        {
            Vector3 me = Game.Player.Character.Position;
            float best = float.MaxValue;
            foreach (Ped p in peds)
            {
                if (p == null || !p.Exists() || p.IsDead) continue;
                float d = p.Position.DistanceTo(me);
                if (d < best) best = d;
            }
            return best;
        }
    }
}
