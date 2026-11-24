// Fronts & Rivals: one line of fight information at the top centre of the screen, where GTA puts a mission's
// objective and timers: "ATTACKERS 3   FRONT 85%" in a live defense, "CAR WASH HOLDS 3:42" while your people hold.
// Nothing in the bottom-right corner, so the game's own area and vehicle names and the menus' button hints stay clear.
using System.Drawing;

namespace FrontsAndRivals.UI
{
    public static class FightHud
    {
        private static GTA.UI.TextElement _line;

        /// <summary>Draw this frame. Called every frame while there's something to show.</summary>
        public static void Draw(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_line == null)
                _line = new GTA.UI.TextElement("", new PointF(640f, 38f), 0.5f, Color.White, GTA.UI.Font.ChaletLondon, GTA.UI.Alignment.Center, true, true);
            _line.Caption = text;
            _line.Draw();
        }
    }
}
