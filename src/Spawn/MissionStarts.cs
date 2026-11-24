// Fronts & Rivals: where missions start. Reads the game's own map blips (story mission letters, Strangers and Freaks,
// family and strand missions, heists: FreeRoamRules.MissionStartSprites) so no street crew, street crime or live fight
// starts within 120 m of one. A mission then always begins on a clean street, and the mod never stands in its way.
// The blip list is read every 5 seconds (cheap: a handful of blips).
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Core;

namespace FrontsAndRivals.Spawn
{
    public static class MissionStarts
    {
        private static readonly List<Vector3> _spots = new List<Vector3>();
        private static int _readAt = -100000;

        /// <summary>A mission starts within MissionStartRange of this spot.</summary>
        public static bool Near(Vector3 at)
        {
            Refresh();
            float r2 = FreeRoamRules.MissionStartRange * FreeRoamRules.MissionStartRange;
            foreach (Vector3 s in _spots)
            {
                float dx = s.X - at.X, dy = s.Y - at.Y;
                if (dx * dx + dy * dy < r2) return true;
            }
            return false;
        }

        public static int Count { get { Refresh(); return _spots.Count; } }

        private static void Refresh()
        {
            int now = Game.GameTime;
            if (now - _readAt < 5000) return;
            _readAt = now;
            _spots.Clear();
            try
            {
                foreach (int sprite in FreeRoamRules.MissionStartSprites)
                {
                    int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, sprite);
                    int guard = 0;
                    while (blip != 0 && guard++ < 64)
                    {
                        if (Function.Call<bool>(Hash.DOES_BLIP_EXIST, blip))
                            _spots.Add(Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip));
                        blip = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprite);
                    }
                }
            }
            catch (System.Exception ex) { Log.Error("Mission starts", ex); }
        }

        /// <summary>Dev tools: every blip on the map with its sprite and place, so mission-start sprites can be checked
        /// against a real save (the Michael and Trevor mission letters aren't in the public reference).</summary>
        public static List<string> DumpAll(int maxSprite)
        {
            var lines = new List<string>();
            for (int sprite = 0; sprite <= maxSprite; sprite++)
            {
                int blip = Function.Call<int>(Hash.GET_FIRST_BLIP_INFO_ID, sprite);
                int guard = 0;
                while (blip != 0 && guard++ < 64)
                {
                    Vector3 c = Function.Call<Vector3>(Hash.GET_BLIP_INFO_ID_COORD, blip);
                    lines.Add("sprite " + sprite + (FreeRoamRules.IsMissionStartSprite(sprite) ? " (mission start)" : "") + " at "
                              + c.X.ToString("0") + ", " + c.Y.ToString("0") + ", " + c.Z.ToString("0"));
                    blip = Function.Call<int>(Hash.GET_NEXT_BLIP_INFO_ID, sprite);
                }
            }
            return lines;
        }
    }
}
