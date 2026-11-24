// Fronts & Rivals dev tools: "Map Colour Check". Puts every candidate map-icon colour for the crews (the greens, oranges
// and blues in the game's own blip palette) on the map in rows around you, each named with its number, so the exact
// match for each crew's uniform can be picked from a screenshot of the pause map.
using System.Collections.Generic;
using GTA;
using GTA.Math;

namespace FrontsAndRivals.Dev
{
    public sealed class BlipColourCheck
    {
        private static readonly int[][] Rows =
        {
            new[] { 2, 25, 52, 69, 11, 24 },                       // greens
            new[] { 17, 47, 51, 81, 44, 46 },                      // oranges
            new[] { 3, 38, 54, 57, 63, 67, 68, 74, 77, 78, 83 },   // blues
            new[] { 1, 49, 59, 75, 76, 79 },                       // reds
        };
        private static readonly string[] RowNames = { "green", "orange", "blue", "red" };
        private readonly List<Blip> _blips = new List<Blip>();

        public void Show()
        {
            Clear();
            Vector3 me = Game.Player.Character.Position;
            for (int r = 0; r < Rows.Length; r++)
                for (int k = 0; k < Rows[r].Length; k++)
                {
                    Blip b = World.CreateBlip(me + new Vector3(-250f + k * 50f, 150f + r * 60f, 0f));
                    b.Sprite = BlipSprite.Standard;
                    b.Color = (BlipColor)Rows[r][k];
                    b.Scale = 1.2f;
                    b.Name = RowNames[r] + " " + Rows[r][k] + " (" + (BlipColor)Rows[r][k] + ")";
                    b.IsShortRange = false;
                    _blips.Add(b);
                }
            DevTools.Notify("Map colours placed north of you: rows of greens, oranges, blues and reds, left to right by number. Open the map, zoom in, hover each to see its number, and take a screenshot.");
        }

        public void Clear()
        {
            foreach (Blip b in _blips) if (b != null && b.Exists()) b.Delete();
            _blips.Clear();
        }
    }
}
