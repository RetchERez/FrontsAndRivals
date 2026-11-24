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
        [DataMember(Name = "fallback")] public bool Fallback;   // t0 stands in for colours the piece doesn't come in

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

    /// <summary>An extra a style can add on top of a look: a hat, glasses, a chain or tie, a watch, or a mask for attacks.</summary>
    [DataContract]
    public sealed class ExtraPiece
    {
        [DataMember(Name = "slot")] public string Slot;        // hat, glasses, watch, neck, mask
        [DataMember(Name = "chance")] public double Chance;
        [DataMember(Name = "only")] public string Only;        // the under-layer the look must have (a tie needs a shirt)
        [DataMember(Name = "when")] public string When;        // "attack": only on people hitting your fronts
        [DataMember(Name = "m")] public List<OutfitPart> Male;
        [DataMember(Name = "f")] public List<OutfitPart> Female;
    }

    /// <summary>One extra a person actually wears.</summary>
    public sealed class WornExtra
    {
        public string Slot;
        public int Drawable, Texture;
        public bool AttackOnly;
        public bool IsProp { get { return Slot == "hat" || Slot == "glasses" || Slot == "watch"; } }
        /// <summary>The game's prop index (hat 0, glasses 1, watch 6) or component (neck 7, mask 1).</summary>
        public int Index { get { switch (Slot) { case "hat": return 0; case "glasses": return 1; case "watch": return 6; case "mask": return 1; default: return 7; } } }
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
        [DataMember(Name = "extras")] public Dictionary<string, List<ExtraPiece>> Extras;

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
        public List<WornExtra> Extras = new List<WornExtra>();
    }

    public static class OutfitRules
    {
        /// <summary>Drops every clothing piece the game said isn't valid (valid(model, component, drawable, texture)):
        /// a colour a piece doesn't come in, a neutral piece that doesn't exist, a look left with no colour at all.
        /// Returns how many pieces were removed. Pure: the check itself is passed in.</summary>
        public static int Prune(OutfitData data, Func<bool, int, int, int, bool> valid)
        {
            if (data == null || data.Styles == null) return 0;
            int removed = 0;
            foreach (List<OutfitLook> looks in data.Styles.Values)
            {
                for (int i = looks.Count - 1; i >= 0; i--)
                {
                    OutfitLook l = looks[i];
                    bool female = l.Gender == "f", broken = false;
                    foreach (OutfitPart p in l.Parts)
                    {
                        if (p.Coloured)
                        {
                            foreach (string colour in new List<string>(p.Textures.Keys))
                                if (!valid(female, p.Component, p.Drawable, p.Textures[colour])) { p.Textures.Remove(colour); removed++; }
                            if (p.Textures.Count == 0) broken = true;
                        }
                        else if (!valid(female, p.Component, p.Drawable, p.FixedTexture)) { broken = true; removed++; }
                    }
                    if (broken) looks.RemoveAt(i);
                }
            }
            if (data.Neutral != null)
                foreach (KeyValuePair<string, OutfitNeutral> kv in data.Neutral)
                {
                    bool female = kv.Key == "f";
                    foreach (KeyValuePair<string, List<OutfitPiece>> legs in kv.Value.Legs) removed += legs.Value.RemoveAll(x => !valid(female, 4, x.Drawable, x.Texture));
                    foreach (KeyValuePair<string, List<OutfitPiece>> shoes in kv.Value.Shoes) removed += shoes.Value.RemoveAll(x => !valid(female, 6, x.Drawable, x.Texture));
                    foreach (string k in new List<string>(kv.Value.Under.Keys))
                        if (!valid(female, 8, kv.Value.Under[k].Drawable, kv.Value.Under[k].Texture)) { kv.Value.Under.Remove(k); removed++; }
                }
            if (data.Hair != null)
                foreach (KeyValuePair<string, List<int>> h in data.Hair) { bool female = h.Key == "f"; removed += h.Value.RemoveAll(d => !valid(female, 2, d, 0)); }
            return removed;
        }

        /// <summary>The same check for the extras: components (chains, ties, masks) with valid(), props (hats, glasses,
        /// watches) with validProp(female, propIndex, drawable, texture). A colour a piece doesn't come in is dropped;
        /// a piece left with no colour at all is dropped. Returns how many were removed.</summary>
        public static int PruneExtras(OutfitData data, Func<bool, int, int, int, bool> valid, Func<bool, int, int, int, bool> validProp)
        {
            if (data == null || data.Extras == null) return 0;
            int removed = 0;
            foreach (List<ExtraPiece> pieces in data.Extras.Values)
                foreach (ExtraPiece x in pieces)
                    foreach (bool female in new[] { false, true })
                    {
                        List<OutfitPart> list = female ? x.Female : x.Male;
                        if (list == null) continue;
                        var probe = new WornExtra { Slot = x.Slot };
                        Func<int, int, bool> ok = (d, t) => probe.IsProp ? validProp(female, probe.Index, d, t) : valid(female, probe.Index, d, t);
                        for (int i = list.Count - 1; i >= 0; i--)
                        {
                            OutfitPart p = list[i];
                            if (p.Coloured)
                            {
                                foreach (string colour in new List<string>(p.Textures.Keys))
                                    if (!ok(p.Drawable, p.Textures[colour])) { p.Textures.Remove(colour); removed++; }
                                if (p.Fallback && !ok(p.Drawable, p.FixedTexture)) p.Fallback = false;
                                if (p.Textures.Count == 0) { list.RemoveAt(i); removed++; }
                            }
                            else if (!ok(p.Drawable, p.FixedTexture)) { list.RemoveAt(i); removed++; }
                        }
                    }
            return removed;
        }

        /// <summary>The extras a person wears on top of their look: the style's own (each by its chance), with a leader's
        /// pieces replacing the style's in the same slot. A coloured piece only goes on when it comes in the colour (or has
        /// a stand-in colour, like black masks). Decided from the same number as the look, so it never changes.</summary>
        public static List<WornExtra> ChooseExtras(OutfitData data, string style, string colour, OutfitLook look, bool female, int seed, bool lead)
        {
            var worn = new List<WornExtra>();
            if (data == null || data.Extras == null) return worn;
            var pieces = new List<ExtraPiece>();
            List<ExtraPiece> list;
            if (lead && data.Extras.TryGetValue("lead", out list)) pieces.AddRange(list);
            if (data.Extras.TryGetValue(style, out list))
                foreach (ExtraPiece x in list) if (!pieces.Exists(p => p.Slot == x.Slot)) pieces.Add(x);
            int salt = 101;
            foreach (ExtraPiece x in pieces)
            {
                salt += 7;
                if (x.Only != null && (look == null || look.Under != x.Only)) continue;
                if (Pick(seed, salt, 1000) >= (int)Math.Round(x.Chance * 1000)) continue;
                List<OutfitPart> options = female ? x.Female : x.Male;
                if (options == null || options.Count == 0) continue;
                OutfitPart p = options[Pick(seed, salt + 3, options.Count)];
                int tex;
                if (!p.Coloured) tex = p.FixedTexture;
                else if (p.Textures.TryGetValue(colour, out tex)) { }
                else if (p.Fallback) tex = p.FixedTexture;
                else continue; // never in someone else's colour
                worn.Add(new WornExtra { Slot = x.Slot, Drawable = p.Drawable, Texture = tex, AttackOnly = x.When == "attack" });
            }
            return worn;
        }

        /// <summary>Another look of the same gender, in the colour, that passes the check (any style: the same colour
        /// matters more than the style). Null if there's none.</summary>
        public static OutfitLook FindWhole(OutfitData data, bool female, string colour, OutfitLook not, Func<OutfitLook, bool> whole)
        {
            if (data == null || data.Styles == null) return null;
            string g = female ? "f" : "m";
            foreach (List<OutfitLook> looks in data.Styles.Values)
                foreach (OutfitLook l in looks)
                    if (l != not && l.Gender == g && l.Wears(colour) && whole(l)) return l;
            return null;
        }

        private static uint Mix(int n) { return unchecked((uint)n * 2654435761u); }
        private static int Pick(int seed, int salt, int count) { return count <= 0 ? 0 : (int)(Mix(seed * 31 + salt) % (uint)count); }

        /// <summary>The style a group dresses in: a crew id (CK street, IR bikers, VS suits) or "player".</summary>
        public static Dressing Choose(OutfitData data, string style, string colour, int seed) { return Choose(data, style, colour, seed, null); }

        /// <param name="female">Force the gender (your guards keep the gender their name has); null picks by the style's mix.</param>
        public static Dressing Choose(OutfitData data, string style, string colour, int seed, bool? female0) { return Choose(data, style, colour, seed, female0, false); }

        /// <param name="lead">A boss or lieutenant: the leader's extras (gold chain, watch, shades) on top.</param>
        public static Dressing Choose(OutfitData data, string style, string colour, int seed, bool? female0, bool lead)
        {
            if (data == null || data.Styles == null) return null;
            List<OutfitLook> looks;
            if (!data.Styles.TryGetValue(style, out looks) || looks == null || looks.Count == 0) return null;
            double fc = 0.3, given;
            if (data.FemaleChance != null && data.FemaleChance.TryGetValue(style, out given)) fc = given;
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
            d.Extras = ChooseExtras(data, style, colour, look, female, seed, lead);
            return d;
        }
    }
}
