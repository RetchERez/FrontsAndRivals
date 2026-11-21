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
            var reasons = new int[6]; // 0 no navmesh spot, 1 on a road, 2 too near/far/high, 3 too close to another post,
                                      // 4 a wall or fence between it and the door, 5 no solid ground or something overhead
            // First pass: pavement only (guards on the forecourt). Second pass: any outdoor ground.
            Fill(def, chosen, FlagPavement | FlagNotInterior | FlagNotWater, reasons);
            if (chosen.Count < AutoPosts.Wanted) Fill(def, chosen, FlagNotInterior | FlagNotWater, reasons);
            LastReport = chosen.Count + " placed; rejected: " + reasons[0] + " no walkable spot, " + reasons[1] + " road, "
                       + reasons[2] + " distance/height, " + reasons[3] + " spacing, " + reasons[4] + " out of sight of the door, "
                       + reasons[5] + " bad ground/overhead";
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
                if (!CanSeeDoor(def.Door, spot)) { reasons[4]++; continue; }
                if (!GoodFooting(spot)) { reasons[5]++; continue; }
                chosen.Add(AutoPosts.MakePost(def.Door, chosen.Count, spot.X, spot.Y, spot.Z));
            }
        }

        private const IntersectFlags Solid = IntersectFlags.Map | IntersectFlags.Objects | IntersectFlags.Glass;

        /// <summary>A guard must be able to see the door: no wall, fence or prop between them at chest height.
        /// The ray starts 1 m out from the door so the building's own wall doesn't count.</summary>
        private static bool CanSeeDoor(Spot door, Vector3 ground)
        {
            var doorPos = new Vector3(door.X, door.Y, door.Z);
            Vector3 flat = new Vector3(ground.X - door.X, ground.Y - door.Y, 0f);
            float len = flat.Length();
            if (len < 1.2f) return true;
            flat.Normalize();
            Vector3 from = doorPos + flat * 1.0f + new Vector3(0f, 0f, 0.5f);
            Vector3 to = ground + new Vector3(0f, 0f, 1.2f);
            RaycastResult r = GTA.World.Raycast(from, to, Solid, Game.Player.Character);
            return !r.DidHit || r.HitPosition.DistanceTo(to) < 0.5f;
        }

        /// <summary>Solid ground right under the spot (not floating over a drop), and 2 m of clear headroom
        /// (not tucked under stairs, an awning edge or a low overhang).</summary>
        private static bool GoodFooting(Vector3 ground)
        {
            float g = GTA.World.GetGroundHeight(new Vector3(ground.X, ground.Y, ground.Z + 1.5f));
            if (g == 0f || System.Math.Abs(g - ground.Z) > 1.0f) return false;
            RaycastResult up = GTA.World.Raycast(ground + new Vector3(0f, 0f, 0.3f), ground + new Vector3(0f, 0f, 2.2f), Solid, Game.Player.Character);
            return !up.DidHit;
        }

        /// <summary>Forget every retry timer (a new empire's clock starts again at zero).</summary>
        public void ResetAll() { _nextTry.Clear(); }

        /// <summary>Forget retry timers (dev tools: "Re-place posts").</summary>
        public void ResetTimer(string frontId) { _nextTry.Remove(frontId); }
    }
}
