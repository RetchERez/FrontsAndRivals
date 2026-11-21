// Fronts & Rivals: crew outfits. Every crew member (and every one of your guards) is a GTA Online-style character
// dressed so a major piece of clothing - a hoodie, jacket, vest, suit or trousers - is always in the crew's colour.
// The clothing numbers come from the game's own shop names (root-cause/v-clothingnames, v-besttorso), compiled into
// Content/outfits.json. This file is the data and the picking rules: pure, no game calls, unit-tested.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FrontsAndRivals.Data
{
    [DataContract]
    public sealed class OutfitPart
    {
        [DataMember(Name = "c")] public int Component;
        [DataMember(Name = "d")] public int Drawable;
        [DataMember(Name = "t")] public Dictionary<string, int> Textures; // by colour key, when this piece carries the colour
        [DataMember(Name = "t0")] public int FixedTexture;

        public bool Coloured { get { return Textures != null && Textures.Count > 0; } }
        public bool Has(string colour) { return Textures != null && Textures.ContainsKey(colour); }
        public int TextureFor(string colour) { int t; return Textures != null && Textures.TryGetValue(colour, out t) ? t : FixedTexture; }
    }

    [DataContract]
    public sealed class OutfitLook
    {
        [DataMember(Name = "g")] public string Gender;         // "m" or "f"
        [DataMember(Name = "parts")] public List<OutfitPart> Parts;
        [DataMember(Name = "under")] public string Under;      // none / plain / shirt
        [DataMember(Name = "legs")] public string Legs;        // street / biker / suit, or null when the trousers are coloured
        [DataMember(Name = "shoes")] public string Shoes;      // street / boots / formal

        /// <summary>A major piece of this look comes in the colour.</summary>
        public bool Wears(string colour)
        {
            if (Parts == null) return false;
            foreach (OutfitPart p in Parts) if (p.Has(colour)) return true;
            return false;
        }
    }

    [DataContract]
    public sealed class OutfitPiece { [DataMember(Name = "d")] public int Drawable; [DataMember(Name = "t")] public int Texture; }

    [DataContract]
    public sealed class OutfitNeutral
    {
        [DataMember(Name = "legs")] public Dictionary<string, List<OutfitPiece>> Legs;
        [DataMember(Name = "shoes")] public Dictionary<string, List<OutfitPiece>> Shoes;
        [DataMember(Name = "under")] public Dictionary<string, OutfitPiece> Under;
    }

    [DataContract]
    public sealed class OutfitData
    {
        [DataMember(Name = "styles")] public Dictionary<string, List<OutfitLook>> Styles;
        [DataMember(Name = "neutral")] public Dictionary<string, OutfitNeutral> Neutral;
        [DataMember(Name = "hair")] public Dictionary<string, List<int>> Hair;
        [DataMember(Name = "hairTints")] public List<int> HairTints;
        [DataMember(Name = "femaleChance")] public Dictionary<string, double> FemaleChance;

        public static OutfitData Load(string path)
        {
            if (!File.Exists(path)) return null;
            var ser = new DataContractJsonSerializer(typeof(OutfitData), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var s = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path))))
                return (OutfitData)ser.ReadObject(s);
        }
    }

    /// <summary>One person's whole look, decided from a stable number so the same guard always looks the same.</summary>
    public sealed class Dressing
    {
        public bool Female;
        public OutfitLook Look;
        public string Colour;
        public int Hair, HairTint, Mother, Father;
        public float ShapeMix, SkinMix;
        public OutfitPiece Legs, Shoes, Under;
    }

    public static class OutfitRules
    {
        private static uint Mix(int n) { return unchecked((uint)n * 2654435761u); }
        private static int Pick(int seed, int salt, int count) { return count <= 0 ? 0 : (int)(Mix(seed * 31 + salt) % (uint)count); }

        /// <summary>The style a group dresses in: a crew id (CK street, IR bikers, VS suits) or "player".</summary>
        public static Dressing Choose(OutfitData data, string style, string colour, int seed) { return Choose(data, style, colour, seed, null); }

        /// <param name="female">Force the gender (your guards keep the gender their name has); null picks by the style's mix.</param>
        public static Dressing Choose(OutfitData data, string style, string colour, int seed, bool? female0)
        {
            if (data == null || data.Styles == null) return null;
            List<OutfitLook> looks;
            if (!data.Styles.TryGetValue(style, out looks) || looks == null || looks.Count == 0) return null;
            double fc = 0.3;
            if (data.FemaleChance != null) data.FemaleChance.TryGetValue(style, out fc);
            bool female = female0.HasValue ? female0.Value : (Mix(seed * 7 + 3) % 1000) / 1000.0 < fc;
            string g = female ? "f" : "m";
            var fits = new List<OutfitLook>();
            foreach (OutfitLook l in looks) if (l.Gender == g && l.Wears(colour)) fits.Add(l);
            if (fits.Count == 0 && !female0.HasValue) { female = !female; g = female ? "f" : "m"; foreach (OutfitLook l in looks) if (l.Gender == g && l.Wears(colour)) fits.Add(l); }
            if (fits.Count == 0) return null; // nothing in this colour: never dress someone out of their colour
            OutfitLook look = fits[Pick(seed, 11, fits.Count)];

            var d = new Dressing { Female = female, Look = look, Colour = colour };
            OutfitNeutral n = null;
            if (data.Neutral != null) data.Neutral.TryGetValue(g, out n);
            if (n != null)
            {
                List<OutfitPiece> list;
                if (look.Legs != null && n.Legs != null && n.Legs.TryGetValue(look.Legs, out list) && list.Count > 0) d.Legs = list[Pick(seed, 17, list.Count)];
                if (look.Shoes != null && n.Shoes != null && n.Shoes.TryGetValue(look.Shoes, out list) && list.Count > 0) d.Shoes = list[Pick(seed, 19, list.Count)];
                OutfitPiece u;
                if (n.Under != null && n.Under.TryGetValue(look.Under ?? "none", out u)) d.Under = u;
            }
            List<int> hair = null;
            if (data.Hair != null) data.Hair.TryGetValue(g, out hair);
            d.Hair = hair != null && hair.Count > 0 ? hair[Pick(seed, 23, hair.Count)] : 0;
            d.HairTint = data.HairTints != null && data.HairTints.Count > 0 ? data.HairTints[Pick(seed, 29, data.HairTints.Count)] : 0;
            // Faces: a mother (21-41) and a father (0-20), mixed towards one side by gender.
            d.Mother = 21 + Pick(seed, 37, 21);
            d.Father = Pick(seed, 41, 21);
            d.ShapeMix = female ? 0.1f + Pick(seed, 43, 30) / 100f : 0.6f + Pick(seed, 47, 30) / 100f;
            d.SkinMix = Pick(seed, 53, 101) / 100f;
            return d;
        }
    }
}
