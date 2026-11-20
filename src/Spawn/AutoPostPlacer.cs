// Fronts & Rivals: places a front's guard posts automatically.
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

        /// <summary>Why probes were rejected on the last Place call (for the log).</summary>
        public string LastReport { get; private set; }

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

                int n = Place(def, false);
                if (n > 0)
                {
                    filled++;
                    Log.Write("AutoPosts: " + def.Id + " got " + n + " posts automatically (" + LastReport + ")");
                }
                else
                {
                    _nextTry[def.Id] = nowMs + RetryMs; // navmesh not loaded yet, or nothing usable; try again later
                    Log.Write("AutoPosts: no usable spots around " + def.Id + " yet (" + LastReport + "), retrying in 30 s");
                }
            }
            return filled;
        }

        /// <summary>Works out posts for a front. Replaces the current posts when it finds at least as many
        /// (or always, with replace=true). Returns how many posts the front has afterwards.</summary>
        public int Place(FrontDef def, bool replace)
        {
            var chosen = new List<PostDef>();
            var reasons = new int[4]; // 0 no navmesh spot, 1 on a road, 2 too near/far/high, 3 too close to another post
            // First pass: pavement only (guards on the forecourt). Second pass: any outdoor ground.
            Fill(def, chosen, FlagPavement | FlagNotInterior | FlagNotWater, reasons);
            if (chosen.Count < AutoPosts.Wanted) Fill(def, chosen, FlagNotInterior | FlagNotWater, reasons);
            LastReport = chosen.Count + " placed; rejected: " + reasons[0] + " no ground, " + reasons[1] + " road, "
                       + reasons[2] + " distance/height, " + reasons[3] + " spacing";
            if (chosen.Count == 0) return def.Posts.Count;
            if (replace || chosen.Count >= def.Posts.Count)
            {
                def.Posts.Clear();
                def.Posts.AddRange(chosen);
            }
            return def.Posts.Count;
        }

        private static void Fill(FrontDef def, List<PostDef> chosen, int flags, int[] reasons)
        {
            foreach (float[] probe in AutoPosts.Probes(def.Door))
            {
                if (chosen.Count >= AutoPosts.Wanted) return;
                var outPos = new OutputArgument();
                bool ok = Function.Call<bool>(Hash.GET_SAFE_COORD_FOR_PED, probe[0], probe[1], probe[2], true, outPos, flags);
                if (!ok) { reasons[0]++; continue; }
                Vector3 spot = outPos.GetResult<Vector3>();
                if (Function.Call<bool>(Hash.IS_POINT_ON_ROAD, spot.X, spot.Y, spot.Z, 0)) { reasons[1]++; continue; }
                if (!AutoPosts.Accept(def.Door, new List<PostDef>(), spot.X, spot.Y, spot.Z)) { reasons[2]++; continue; }
                if (!AutoPosts.Accept(def.Door, chosen, spot.X, spot.Y, spot.Z)) { reasons[3]++; continue; }
                chosen.Add(AutoPosts.MakePost(def.Door, chosen.Count, spot.X, spot.Y, spot.Z));
            }
        }

        /// <summary>Forget retry timers (dev tools: "Re-place posts").</summary>
        public void ResetTimer(string frontId) { _nextTry.Remove(frontId); }
    }
}
