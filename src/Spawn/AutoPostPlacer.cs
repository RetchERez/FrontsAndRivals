// Fronts & Rivals: places a front's guard posts automatically, the first time you come near it.
// For each probe point around the door it asks the game's navmesh for the nearest safe spot
// (GET_SAFE_COORD_FOR_PED, flags from NativeDB: 1 pavement only, 4 no interiors, 8 no water),
// throws away spots on a road (IS_POINT_ON_ROAD) and keeps the ones AutoPosts accepts.
// The navmesh only exists near the player, so this runs when you're within 100 m of the door.
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Spawn
{
    public sealed class AutoPostPlacer
    {
        public const float PlaceRange = 100f;
        private const int RetryMs = 30000;
        private const int FlagPavement = 1, FlagNotInterior = 4, FlagNotWater = 8;

        private readonly Dictionary<string, long> _nextTry = new Dictionary<string, long>();

        /// <summary>Fronts that have a door but no posts get posts once the player is close. Returns the fronts it filled.</summary>
        public int Update(ContentDb content, long nowMs)
        {
            Vector3 p = Game.Player.Character.Position;
            int filled = 0;
            foreach (FrontDef def in content.Fronts)
            {
                if (!def.Door.IsSet || def.Posts.Count > 0) continue;
                if (def.Door.DistanceTo(p.X, p.Y, p.Z) > PlaceRange) continue;
                long next;
                if (_nextTry.TryGetValue(def.Id, out next) && nowMs < next) continue;

                int n = Place(def);
                if (n > 0)
                {
                    filled++;
                    Log.Write("AutoPosts: " + def.Id + " got " + n + " posts automatically");
                }
                else
                {
                    _nextTry[def.Id] = nowMs + RetryMs; // navmesh not loaded yet, or nothing usable; try again later
                    Log.Write("AutoPosts: no usable spots around " + def.Id + " yet, retrying in 30 s");
                }
            }
            return filled;
        }

        /// <summary>Replaces the front's posts with automatic ones. Returns how many were placed.</summary>
        public int Place(FrontDef def)
        {
            var chosen = new List<PostDef>();
            // First pass: pavement only (looks like guards on the forecourt). Second pass: any outdoor ground.
            Fill(def, chosen, FlagPavement | FlagNotInterior | FlagNotWater);
            if (chosen.Count < AutoPosts.Wanted) Fill(def, chosen, FlagNotInterior | FlagNotWater);
            if (chosen.Count == 0) return 0;
            def.Posts.Clear();
            def.Posts.AddRange(chosen);
            return chosen.Count;
        }

        private static void Fill(FrontDef def, List<PostDef> chosen, int flags)
        {
            foreach (float[] probe in AutoPosts.Probes(def.Door))
            {
                if (chosen.Count >= AutoPosts.Wanted) return;
                var outPos = new OutputArgument();
                bool ok = Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, probe[0], probe[1], probe[2], true, outPos, flags);
                if (!ok) continue;
                Vector3 spot = outPos.GetResult<Vector3>();
                if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD, spot.X, spot.Y, spot.Z, 0)) continue;
                if (!AutoPosts.Accept(def.Door, chosen, spot.X, spot.Y, spot.Z)) continue;
                chosen.Add(AutoPosts.MakePost(def.Door, chosen.Count, spot.X, spot.Y, spot.Z));
            }
        }

        /// <summary>Forget retry timers (dev tools: "Re-place posts").</summary>
        public void ResetTimer(string frontId) { _nextTry.Remove(frontId); }
    }
}
