// Fronts & Rivals: automatic guard posts. Pure planning (no game calls, unit-tested):
//   - where to probe around a front's door
//   - which probed spots to keep (near the door, spread out, same floor)
//   - which way a guard faces (outward, away from the door) and what idle he plays
// AutoPostPlacer does the game side: it asks the navmesh for a safe spot near each probe.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Spawn
{
    public static class AutoPosts
    {
        public const int Wanted = 4;            // posts per front
        public const float MinSpacing = 3.5f;   // metres between two guards
        public const float MinFromDoor = 1.5f;  // don't stand in the doorway
        public const float MaxFromDoor = 14f;   // stay with the building
        public const float MaxHeightDiff = 3f;  // same floor as the door (no rooftops, no basements)
        public const float RootHeight = 1.0f;   // navmesh gives the ground; a ped's position is about 1 m up

        /// <summary>Idles handed out in order: two stand guard, one smokes, one is on the phone.</summary>
        public static readonly string[] Idles =
        {
            StreamRules.GuardStand, "WORLD_HUMAN_SMOKING", StreamRules.GuardStand, "WORLD_HUMAN_STAND_MOBILE"
        };

        /// <summary>Probe points: an inner ring of 6 at 5 m, then an outer ring of 6 at 9 m (offset 30 degrees),
        /// starting in front of the door so the first guards end up out front.</summary>
        public static List<float[]> Probes(Spot door)
        {
            var list = new List<float[]>(12);
            AddRing(list, door, 5f, 0f);
            AddRing(list, door, 9f, 30f);
            return list;
        }

        private static void AddRing(List<float[]> list, Spot door, float radius, float offsetDeg)
        {
            for (int i = 0; i < 6; i++)
            {
                double deg = door.H + offsetDeg + i * 60.0;
                double rad = deg * Math.PI / 180.0;
                // GTA heading: 0 = north (+Y), 90 = west (-X).
                float x = door.X - (float)Math.Sin(rad) * radius;
                float y = door.Y + (float)Math.Cos(rad) * radius;
                list.Add(new[] { x, y, door.Z });
            }
        }

        /// <summary>Is this navmesh spot usable as a post, given the ones already chosen?</summary>
        public static bool Accept(Spot door, IList<PostDef> chosen, float x, float y, float groundZ)
        {
            float dx = x - door.X, dy = y - door.Y;
            float flat = (float)Math.Sqrt(dx * dx + dy * dy);
            if (flat < MinFromDoor || flat > MaxFromDoor) return false;
            if (Math.Abs(groundZ + RootHeight - door.Z) > MaxHeightDiff) return false;
            foreach (PostDef p in chosen)
            {
                float px = p.X - x, py = p.Y - y;
                if (px * px + py * py < MinSpacing * MinSpacing) return false;
            }
            return true;
        }

        /// <summary>GTA heading (degrees) for a guard at (x, y) facing away from the door.</summary>
        public static float FacingAway(Spot door, float x, float y)
        {
            float dx = x - door.X, dy = y - door.Y;
            if (Math.Abs(dx) < 0.01f && Math.Abs(dy) < 0.01f) return door.H;
            double h = -Math.Atan2(dx, dy) * 180.0 / Math.PI;
            if (h < 0) h += 360.0;
            return (float)h;
        }

        public static PostDef MakePost(Spot door, int index, float x, float y, float groundZ)
        {
            return new PostDef
            {
                X = x, Y = y, Z = groundZ + RootHeight,
                H = FacingAway(door, x, y),
                Scenario = Idles[index % Idles.Length]
            };
        }
    }
}
