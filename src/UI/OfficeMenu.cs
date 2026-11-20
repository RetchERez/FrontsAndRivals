// Fronts & Rivals: the Office menu, built with LemonUI's Interaction Menu style.
// Two levels at most; bars instead of raw numbers.
using LemonUI;
using LemonUI.Menus;
using LemonUI.Scaleform;

namespace FrontsAndRivals.UI
{
    public sealed class OfficeMenu
    {
        public readonly ObjectPool Pool = new ObjectPool();
        private readonly NativeMenu _office;
        private readonly NativeMenu _fronts;
        private readonly BigMessage _banner;

        public OfficeMenu()
        {
            _office = new NativeMenu("Fronts & Rivals", "The Office", "Run your fronts.");
            _fronts = new NativeMenu("Fronts & Rivals", "Fronts", "Every business you hold.");
            Pool.Add(_office);
            Pool.Add(_fronts);
            _office.AddSubMenu(_fronts);

            _banner = new BigMessage("", "");
            Pool.Add(_banner);
        }

        /// <summary>Adds one row per front: status on the right, defense/income bars in a stats panel.</summary>
        public void AddFrontRow(string frontName, string status, float defense01, float income01)
        {
            var item = new NativeItem(frontName, "Defense and income for " + frontName + ".", status);
            var panel = new NativeStatsPanel(
                new NativeStatsInfo("Defense", (int)(defense01 * 100f)),
                new NativeStatsInfo("Income", (int)(income01 * 100f)));
            item.Panel = panel;
            _fronts.Add(item);
        }

        public void Open() { _office.Visible = true; }

        /// <summary>Front taken / lost / nemesis killed. The Customizable style is the GTA Online shard.</summary>
        public void ShowBanner(string title, string message)
        {
            _banner.Title = title;
            _banner.Message = message;
            _banner.Type = MessageType.Customizable;
            _banner.Visible = true;
        }

        public void HideBanner() { _banner.Visible = false; }

        /// <summary>Call every tick.</summary>
        public void Process() { Pool.Process(); }
    }
}
