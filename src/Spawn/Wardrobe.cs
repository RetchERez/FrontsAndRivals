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

        /// <summary>Set by Main so the one-off outfit check can track its two test characters.</summary>
        public static Registry Registry;
        private static bool _checked;

        /// <summary>Keeps both character models loaded (cheap, called with the streamer), and the first time both are
        /// ready, checks every clothing piece against this game version.</summary>
        public static void Keep()
        {
            if (!Enabled) return;
            if (!_m.IsLoaded) _m.Request();
            if (!_f.IsLoaded) _f.Request();
            if (!_checked && _m.IsLoaded && _f.IsLoaded && Registry != null)
            {
                _checked = true;
                try { SelfCheck(); } catch (Exception ex) { Log.Error("Outfit check", ex); }
            }
        }

        // Every piece is asked about on a real character of each model (IS_PED_COMPONENT_VARIATION_VALID), so a piece
        // this version of the game doesn't have (the clothing numbers come from the Legacy edition's data) is dropped
        // before anyone wears it. The two test characters are hidden, frozen and removed straight away.
        private static void SelfCheck()
        {
            Ped me = Game.Player.Character;
            if (me == null || !me.Exists()) { _checked = false; return; }
            var at = me.Position + new GTA.Math.Vector3(0f, 0f, -3f);
            Ped man = GTA.World.CreatePed(_m, at), woman = GTA.World.CreatePed(_f, at);
            try
            {
                if (man == null || woman == null) { _checked = false; return; }
                foreach (Ped p in new[] { man, woman })
                {
                    p.RelationshipGroup = Factions.Staff;
                    Registry.Track(p);
                    p.IsVisible = false; p.IsPositionFrozen = true; p.IsInvincible = true; p.IsCollisionEnabled = false;
                }
                int asked = 0;
                int removed = OutfitRules.Prune(Data, (female, c, d, t) =>
                {
                    asked++;
                    return Function.Call<bool>(Hash.IS_PED_COMPONENT_VARIATION_VALID, female ? woman : man, c, d, t);
                });
                removed += OutfitRules.PruneExtras(Data, (female, c, d, t) =>
                {
                    asked++;
                    return Function.Call<bool>(Hash.IS_PED_COMPONENT_VARIATION_VALID, female ? woman : man, c, d, t);
                }, (female, prop, d, t) =>
                {
                    asked++;
                    return PropValid(female ? woman : man, prop, d, t);
                });
                Log.Write("Outfit check: " + asked + " clothing pieces checked against this game, " + removed + " dropped"
                          + (removed > 0 ? " (those looks or colours simply aren't used)" : ""));
            }
            finally
            {
                if (man != null && man.Exists()) Registry.Despawn(man);
                if (woman != null && woman.Exists()) Registry.Despawn(woman);
            }
        }

        /// <summary>A rival crew member: their crew's style and colour.</summary>
        public static Dressing ForCrew(string crewId, int seed) { return ForCrew(crewId, seed, false); }

        /// <param name="lead">A boss or lieutenant: dressed a step up (gold chain, watch, shades).</param>
        public static Dressing ForCrew(string crewId, int seed, bool lead) { return Enabled ? OutfitRules.Choose(Data, crewId, CrewColors.ForCrew(crewId), seed, null, lead) : null; }

        /// <summary>One of your guards: your colour, the guard's own gender, the same look every time.</summary>
        public static Dressing ForGuard(GuardState g)
        {
            if (!Enabled || g == null) return null;
            bool female = g.Model != null && g.Model.StartsWith("a_f_", StringComparison.OrdinalIgnoreCase);
            return OutfitRules.Choose(Data, CrewColors.StyleKey(PlayerStyle()), PlayerColour(), g.Id * 977 + 13, female);
        }

        public static string ModelName(Dressing d) { return d.Female ? Female : Male; }

        public static Model ModelOf(Dressing d) { return d.Female ? _f : _m; }

        public static void Apply(Ped ped, Dressing d) { Apply(ped, d, false); }

        /// <param name="attack">They're hitting one of your fronts: masks go on.</param>
        public static void Apply(Ped ped, Dressing d, bool attack)
        {
            if (ped == null || d == null || !ped.Exists()) return;
            try
            {
                if (!Whole(ped, d.Look, d.Colour))
                {
                    // A top without its matching arms (or the other way round) shows as floating hands. Swap in a look
                    // that's whole on this ped, or keep the model's own default clothes.
                    OutfitLook other = OutfitRules.FindWhole(Data, d.Female, d.Colour, d.Look, l => Whole(ped, l, d.Colour));
                    string was = PartsKey(d.Look);
                    if (_warned.Add("look " + was)) Log.Write("Wardrobe: look " + was + " isn't whole in this game version, " + (other != null ? "using " + PartsKey(other) : "using default clothes"));
                    if (other == null) { Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped); return; }
                    d.Look = other;
                }
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, ped);
                Function.Call(Hash.SET_PED_HEAD_BLEND_DATA, ped, d.Mother, d.Father, 0, d.Mother, d.Father, 0, d.ShapeMix, d.SkinMix, 0f, false);
                Set(ped, 2, d.Hair, 0);
                Function.Call(Hash.SET_PED_HAIR_TINT, ped, d.HairTint, d.HairTint);
                Faces(ped, d);
                if (d.Under != null) Set(ped, 8, d.Under.Drawable, d.Under.Texture);
                if (d.Legs != null) Set(ped, 4, d.Legs.Drawable, d.Legs.Texture);
                if (d.Shoes != null) Set(ped, 6, d.Shoes.Drawable, d.Shoes.Texture);
                foreach (OutfitPart p in d.Look.Parts) Set(ped, p.Component, p.Drawable, p.TextureFor(d.Colour));
                Function.Call(Hash.CLEAR_ALL_PED_PROPS, ped);
                if (d.Extras != null)
                    foreach (WornExtra x in d.Extras)
                    {
                        if (x.AttackOnly && !attack) continue;
                        if (!x.IsProp) { Set(ped, x.Index, x.Drawable, x.Texture); continue; }
                        if (PropValid(ped, x.Index, x.Drawable, x.Texture))
                            Function.Call(Hash.SET_PED_PROP_INDEX, ped, x.Index, x.Drawable, x.Texture, true);
                    }
            }
            catch (Exception ex) { Log.Error("Wardrobe", ex); }
        }

        /// <summary>Every piece of the look is valid on this ped in this colour.</summary>
        private static bool Whole(Ped ped, OutfitLook look, string colour)
        {
            if (look == null || look.Parts == null) return false;
            foreach (OutfitPart p in look.Parts)
                if (!Function.Call<bool>(Hash.IS_PED_COMPONENT_VARIATION_VALID, ped, p.Component, p.Drawable, p.TextureFor(colour))) return false;
            return true;
        }

        private static string PartsKey(OutfitLook l)
        {
            var sb = new System.Text.StringBuilder(l.Gender);
            foreach (OutfitPart p in l.Parts) sb.Append(' ').Append(p.Component).Append('/').Append(p.Drawable);
            return sb.ToString();
        }

        /// <summary>Beard, eyebrows and age on the face (GTA Online's head overlays: 1 facial hair, 2 eyebrows, 3 ageing),
        /// hair-coloured. NativeDB: SET_PED_HEAD_OVERLAY(ped, overlay, index, opacity); 255 = none.
        /// SET_PED_HEAD_OVERLAY_TINT(ped, overlay, colourType 1 = hair, colour, second colour).</summary>
        private static void Faces(Ped ped, Dressing d)
        {
            Overlay(ped, 1, d.Beard, d.BeardOpacity, d.HairTint);
            Overlay(ped, 2, d.Brows, d.BrowsOpacity, d.HairTint);
            Overlay(ped, 3, d.Ageing > 0f ? d.AgeingStyle : -1, d.Ageing, -1);
        }

        private static void Overlay(Ped ped, int overlay, int index, float opacity, int tint)
        {
            int count = Function.Call<int>(Hash.GET_PED_HEAD_OVERLAY_NUM, overlay);
            if (index < 0 || index >= count) { Function.Call(Hash.SET_PED_HEAD_OVERLAY, ped, overlay, 255, 0f); return; }
            Function.Call(Hash.SET_PED_HEAD_OVERLAY, ped, overlay, index, opacity);
            if (tint >= 0) Function.Call(Hash.SET_PED_HEAD_OVERLAY_TINT, ped, overlay, 1, tint, tint);
        }

        /// <summary>A prop (hat, glasses, watch) this ped's model really has: the drawable and texture are within the
        /// game's own counts for it.</summary>
        private static bool PropValid(Ped ped, int prop, int drawable, int texture)
        {
            int drawables = Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_DRAWABLE_VARIATIONS, ped, prop);
            if (drawable < 0 || drawable >= drawables) return false;
            int textures = Function.Call<int>(Hash.GET_NUMBER_OF_PED_PROP_TEXTURE_VARIATIONS, ped, prop, drawable);
            return texture >= 0 && texture < textures;
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
