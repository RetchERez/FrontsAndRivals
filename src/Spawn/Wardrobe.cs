// Fronts & Rivals: dressing crew members and guards in their colours (the picking rules are Data.OutfitRules).
// Everyone is a GTA Online-style character (mp_m/mp_f_freemode_01): a face, hair, and clothes where a major piece is
// always in the crew's colour. Every clothing piece is checked with IS_PED_COMPONENT_VARIATION_VALID first, so a piece
// that doesn't exist in this game version is skipped instead of showing broken. If the outfit data is missing, the
// old ambient models are used instead.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Spawn
{
    public static class Wardrobe
    {
        public const string Male = "mp_m_freemode_01", Female = "mp_f_freemode_01";
        private static readonly Model _m = new Model(Male), _f = new Model(Female);
        private static readonly HashSet<string> _warned = new HashSet<string>();

        public static OutfitData Data;

        /// <summary>Your crew's colour now (Main supplies it).</summary>
        public static Func<string> PlayerColour = () => CrewColors.DefaultPlayer;

        /// <summary>Your crew's style now (Main supplies it).</summary>
        public static Func<string> PlayerStyle = () => CrewColors.DefaultStyle;

        public static bool Enabled { get { return Data != null && Data.Styles != null; } }

        /// <summary>Keeps both character models loaded (cheap, called with the streamer).</summary>
        public static void Keep()
        {
            if (!Enabled) return;
            if (!_m.IsLoaded) _m.Request();
            if (!_f.IsLoaded) _f.Request();
        }

        /// <summary>A rival crew member: their crew's style and colour.</summary>
        public static Dressing ForCrew(string crewId, int seed) { return Enabled ? OutfitRules.Choose(Data, crewId, CrewColors.ForCrew(crewId), seed, null) : null; }

        /// <summary>One of your guards: your colour, the guard's own gender, the same look every time.</summary>
        public static Dressing ForGuard(GuardState g)
        {
            if (!Enabled || g == null) return null;
            bool female = g.Model != null && g.Model.StartsWith("a_f_", StringComparison.OrdinalIgnoreCase);
            return OutfitRules.Choose(Data, CrewColors.StyleKey(PlayerStyle()), PlayerColour(), g.Id * 977 + 13, female);
        }

        public static string ModelName(Dressing d) { return d.Female ? Female : Male; }

        public static Model ModelOf(Dressing d) { return d.Female ? _f : _m; }

        public static void Apply(Ped ped, Dressing d)
        {
            if (ped == null || d == null || !ped.Exists()) return;
            try
            {
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped);
                Function.Call(Hash.SET_PED_HEAD_BLEND_DATA, ped, d.Mother, d.Father, 0, d.Mother, d.Father, 0, d.ShapeMix, d.SkinMix, 0f, false);
                Set(ped, 2, d.Hair, 0);
                Function.Call(Hash.SET_PED_HAIR_TINT, ped, d.HairTint, d.HairTint);
                if (d.Under != null) Set(ped, 8, d.Under.Drawable, d.Under.Texture);
                if (d.Legs != null) Set(ped, 4, d.Legs.Drawable, d.Legs.Texture);
                if (d.Shoes != null) Set(ped, 6, d.Shoes.Drawable, d.Shoes.Texture);
                foreach (OutfitPart p in d.Look.Parts) Set(ped, p.Component, p.Drawable, p.TextureFor(d.Colour));
                Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped);
            }
            catch (Exception ex) { Log.Error("Wardrobe", ex); }
        }

        private static void Set(Ped ped, int component, int drawable, int texture)
        {
            if (Function.Call<bool>(Hash.IS_PED_COMPONENT_VARIATION_VALID, ped, component, drawable, texture))
            {
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, ped, component, drawable, texture, 0);
                return;
            }
            string key = component + "/" + drawable + "/" + texture;
            if (_warned.Add(key)) Log.Write("Wardrobe: clothing " + key + " isn't valid in this game version, skipped");
        }
    }
}
