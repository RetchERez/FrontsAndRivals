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
            m.BannerText.Scale = 0.85f;
            m.RotateCamera = false;
            m.MouseBehavior = MenuMouseBehavior.Disabled; // like the Interaction Menu
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
