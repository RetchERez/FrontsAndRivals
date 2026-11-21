using System;
// Fronts & Rivals: one look for every menu (the Office and the dev menus).
// The game's own Interaction Menu banner, titled in GTA's menu font, keyboard and controller only.
using System.Drawing;
using GTA;
using LemonUI.Elements;
using LemonUI.Menus;
using LemonUI.Scaleform;

namespace FrontsAndRivals.UI
{
    public static class MenuStyle
    {
        public const float BannerHeight = 107f;

        /// <summary>Banner texture, Chalet title, no mouse, no camera spin.</summary>
        public static void Banner(NativeMenu m, float width)
        {
            m.Banner = new ScaledTexture(PointF.Empty, new SizeF(width, BannerHeight), "commonmenu", "interaction_bgd");
            m.BannerText.Font = GTA.UI.Font.ChaletLondon;
            m.Alignment = GTA.UI.Alignment.Left;   // top-left like the Interaction Menu
            m.SafeZoneAware = true;                // inside the game's safe zone, whatever the screen and setting
            FitTitle(m);
            m.RotateCamera = false;
            m.MouseBehavior = MenuMouseBehavior.Disabled; // like the Interaction Menu
        }

        /// <summary>GTA's banner font is big: shrink the title by its measured width so it always sits inside the banner
        /// with a margin, never past the edge.</summary>
        public static void FitTitle(NativeMenu m)
        {
            try
            {
                m.BannerText.Scale = 0.85f;
                float room = m.Width * 0.88f;
                float w = m.BannerText.Width;
                if (w > room && w > 0f) m.BannerText.Scale = Math.Max(0.45f, 0.85f * room / w);
            }
            catch { m.BannerText.Scale = 0.75f; }
        }

        /// <summary>A plain one-panel menu: the banner plus bottom-right control hints, which show
        /// the pad's buttons on a controller and the keys on keyboard.</summary>
        public static void Apply(NativeMenu m, bool hasLists)
        {
            Banner(m, m.Width);
            m.Buttons.Clear();
            m.Buttons.Add(new InstructionalButton("Select", Control.FrontendAccept));
            m.Buttons.Add(new InstructionalButton("Back", Control.FrontendCancel));
            if (hasLists) m.Buttons.Add(new InstructionalButton("Change", Control.FrontendRight));
        }
    }
}
