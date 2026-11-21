// Fronts & Rivals: crew colours. One table ties a colour name to its map blip (GTA.BlipColor numbers), its text
// colour code in menus, and the paint for crew vehicles. The rival crews have fixed colours (Dead Kings forest green,
// Hellfire MC orange, Vinewood Elite royal blue); your own crew wears whichever you pick in Office > Settings.
// Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;

namespace FrontsAndRivals.Data
{
    public sealed class CrewColor
    {
        public string Name;      // shown in Settings
        public string Key;       // saved, and used to pick clothing textures
        public int Blip;         // GTA.BlipColor value (SHVDN enum order)
        public string Text;      // GTA text colour code for menus
        public int R, G, B;      // vehicle paint
    }

    public static class CrewColors
    {
        // Blip numbers from SHVDN's BlipColor enum: Red 1, Green 2, Blue 3, White 0, Purple 50, Orange 51,
        // BlueLight 53, Grey 55, GreyDark 40, Pink 61, Yellow 66, and 14 is the closest teal (NetPlayer9 is a
        // player colour, so teal uses BlueLight with its own paint).
        public static readonly CrewColor[] All =
        {
            new CrewColor { Name = "White", Key = "white", Blip = 0, Text = "~s~", R = 235, G = 235, B = 235 },
            new CrewColor { Name = "Red", Key = "red", Blip = 1, Text = "~r~", R = 200, G = 20, B = 25 },
            new CrewColor { Name = "Orange", Key = "orange", Blip = 51, Text = "~o~", R = 235, G = 110, B = 20 },
            new CrewColor { Name = "Yellow", Key = "yellow", Blip = 66, Text = "~y~", R = 240, G = 200, B = 30 },
            new CrewColor { Name = "Green", Key = "green", Blip = 2, Text = "~g~", R = 20, G = 150, B = 60 },
            new CrewColor { Name = "Forest Green", Key = "forest", Blip = 52, Text = "~g~", R = 30, G = 95, B = 45 }, // GreenDark
            new CrewColor { Name = "Teal", Key = "teal", Blip = 53, Text = "~b~", R = 0, G = 140, B = 140 },
            new CrewColor { Name = "Light Blue", Key = "lightblue", Blip = 53, Text = "~b~", R = 90, G = 170, B = 235 },
            new CrewColor { Name = "Royal Blue", Key = "blue", Blip = 3, Text = "~b~", R = 30, G = 70, B = 200 },
            new CrewColor { Name = "Purple", Key = "purple", Blip = 50, Text = "~p~", R = 120, G = 40, B = 170 },
            new CrewColor { Name = "Pink", Key = "pink", Blip = 61, Text = "~q~", R = 230, G = 80, B = 160 },
            new CrewColor { Name = "Burgundy", Key = "burgundy", Blip = 76, Text = "~r~", R = 110, G = 20, B = 35 },
            new CrewColor { Name = "Gold", Key = "gold", Blip = 56, Text = "~y~", R = 205, G = 150, B = 30 },
            new CrewColor { Name = "Cyan", Key = "cyan", Blip = 57, Text = "~b~", R = 0, G = 190, B = 220 },
            new CrewColor { Name = "Lime", Key = "lime", Blip = 69, Text = "~g~", R = 140, G = 210, B = 30 },
            new CrewColor { Name = "Lavender", Key = "lavender", Blip = 50, Text = "~p~", R = 175, G = 140, B = 220 },
            new CrewColor { Name = "Bright Blue", Key = "brightblue", Blip = 3, Text = "~b~", R = 20, G = 110, B = 240 },
            new CrewColor { Name = "Black", Key = "black", Blip = 40, Text = "~s~", R = 15, G = 15, B = 15 },
            new CrewColor { Name = "Grey", Key = "grey", Blip = 55, Text = "~c~", R = 120, G = 120, B = 120 },
        };

        public const string DefaultPlayer = "white";

        /// <summary>Your crew's look, picked in Settings: every guard wears it in your colour.</summary>
        public static readonly string[] Styles = { "Street", "Smart", "Biker", "Mixed" };
        public const string DefaultStyle = "Street";
        public static string StyleKey(string style) { return "player_" + (Array.IndexOf(Styles, style) >= 0 ? style : DefaultStyle).ToLowerInvariant(); }

        /// <summary>The rival crews' colours (fixed).</summary>
        public static string ForCrew(string crewId)
        {
            switch (crewId)
            {
                case "CK": return "forest";  // Dead Kings: forest green, darker than the Families' Grove Street green
                case "IR": return "orange";  // Hellfire MC
                case "VS": return "blue";    // Vinewood Elite, royal blue
                default: return DefaultPlayer;
            }
        }

        public static CrewColor Get(string key)
        {
            foreach (CrewColor c in All) if (string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase)) return c;
            return All[0];
        }

        public static int IndexOf(string key)
        {
            for (int i = 0; i < All.Length; i++) if (string.Equals(All[i].Key, key, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        public static List<string> Names()
        {
            var l = new List<string>();
            foreach (CrewColor c in All) l.Add(c.Name);
            return l;
        }

        /// <summary>Colours a rival already wears: picking one for yourself is allowed, but the Office warns you.</summary>
        public static bool TakenByRival(string key) { return key == "forest" || key == "orange" || key == "blue"; }
    }
}
