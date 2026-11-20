// Fronts & Rivals: the Office. Three native GTA menus (LemonUI) side by side, all visible at once:
//
//   [ SECTIONS ]   [ LIST ]                [ DETAIL ]
//    Overview       every front / guard     the highlighted front's page,
//    Fronts   ›     / recruit / setting     guard's page or recruit
//    Guards   ›
//    Hire     ›     updates as you move     updates as you move
//    Rivals
//    Settings ›
//
// Enter moves one panel to the right, Backspace one panel to the left; Backspace on the left panel closes.
// Only the focused panel takes input. Design doc: native look, badges on the right, bars not numbers, one accent colour.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using GTA;
using GTA.Math;
using LemonUI;
using LemonUI.Elements;
using LemonUI.Menus;
using LemonUI.Scaleform;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using Weapon = FrontsAndRivals.Rules.Weapon;

namespace FrontsAndRivals.UI
{
    public sealed class OfficeMenu
    {
        private const int Rows = 8;
        private const float SectionsWidth = 380f, ListWidth = 500f, DetailWidth = 500f, Gap = 12f;
        private const string Open = "  ›";

        private enum Section { Overview, Fronts, Guards, Hire, Rivals, Settings }

        public readonly ObjectPool Pool = new ObjectPool();

        private readonly ContentDb _content;
        private readonly Config _config;
        private readonly Func<Empire> _empire;
        private readonly Func<Random> _rng;
        private readonly Action _changed; // empire changed: streamer + blips + save
        private readonly Action _save;

        private readonly NativeMenu _sections, _list, _detail;
        private readonly NativeMenu[] _panels;
        private int _focus;            // 0 sections, 1 list, 2 detail
        private bool _open;
        private bool _closingAll;
        private Section _section = Section.Overview;

        // What the list rows stand for, so the detail panel knows what to show.
        private readonly List<object> _listKeys = new List<object>();
        private int _letGoArmed;
        private bool _rebuild;

        public OfficeMenu(ContentDb content, Config config, Func<Empire> empire, Func<Random> rng, Action changed, Action save)
        {
            _content = content;
            _config = config;
            _empire = empire;
            _rng = rng;
            _changed = changed;
            _save = save;

            _sections = Panel("The Office", "FRONTS & RIVALS", SectionsWidth, 0f);
            _list = Panel("Fronts", "", ListWidth, SectionsWidth + Gap);
            _detail = Panel("", "", DetailWidth, SectionsWidth + Gap + ListWidth + Gap);
            _panels = new[] { _sections, _list, _detail };

            _sections.SelectedIndexChanged += (s, e) => { if (_open) Safe("section", () => { _section = (Section)_sections.SelectedIndex; BuildList(true); }); };
            _list.SelectedIndexChanged += (s, e) => { if (_open) Safe("detail", BuildDetail); };
            for (int i = 0; i < _panels.Length; i++)
            {
                int index = i;
                _panels[i].Closing += (s, e) => OnBack(index, e);
            }
        }

        private NativeMenu Panel(string title, string subtitle, float width, float x)
        {
            var m = new NativeMenu(title, subtitle, "");
            m.Width = width;
            m.Offset = new PointF(x, 0f);
            m.MaxItems = Rows;
            // The game's own Interaction Menu banner texture, titled in GTA's menu font (Chalet), like Rockstar's menus.
            m.Banner = new ScaledTexture(PointF.Empty, new SizeF(width, 107f), "commonmenu", "interaction_bgd");
            m.BannerText.Font = GTA.UI.Font.ChaletLondon;
            m.BannerText.Scale = 0.85f;
            m.RotateCamera = false;
            m.MouseBehavior = MenuMouseBehavior.Disabled; // keyboard and controller only, like the Interaction Menu
            Pool.Add(m);
            return m;
        }

        public bool IsOpen { get { return _open; } }

        public void Toggle() { if (_open) CloseAll(); else OpenOffice(); }

        /// <summary>Opens the Office (phone contact, hotkey).</summary>
        public void OpenOffice()
        {
            if (_empire() == null) { GTA.UI.Notification.Show("Fronts & Rivals: still loading. Try again in a moment."); return; }
            if (_open) return;
            _open = true;
            _section = Section.Overview;
            Safe("open", () =>
            {
                BuildSections();
                _sections.SelectedIndex = 0;
                BuildList(true);
            });
            foreach (NativeMenu m in _panels) m.Visible = true;
            Focus(0);
        }

        private void CloseAll()
        {
            _open = false;
            _closingAll = true;
            foreach (NativeMenu m in _panels) m.Visible = false;
            _closingAll = false;
        }

        /// <summary>Backspace: one panel left, or close from the left panel.</summary>
        private void OnBack(int panel, CancelEventArgs e)
        {
            if (_closingAll || !_open) return;
            if (panel == 0) { e.Cancel = true; CloseAll(); return; }
            e.Cancel = true;
            if (panel == _focus) Focus(panel - 1);
        }

        private void Focus(int panel)
        {
            _focus = panel;
            for (int i = 0; i < _panels.Length; i++) _panels[i].AcceptsInput = i == panel;
            _letGoArmed = 0;
            // Bottom-right control hints say exactly what each key does in this panel.
            NativeMenu m = _panels[panel];
            m.Buttons.Clear();
            m.Buttons.Add(new InstructionalButton(panel == 2 ? "Select" : "Open", Control.FrontendAccept));
            m.Buttons.Add(new InstructionalButton(panel == 0 ? "Close" : "Back", Control.FrontendCancel));
            if (panel > 0) m.Buttons.Add(new InstructionalButton("Change", Control.FrontendRight));
        }

        /// <summary>Every tick, also while paused, so the menu never freezes.</summary>
        public void Process()
        {
            if (_open && _rebuild)
            {
                _rebuild = false;
                int s = _sections.SelectedIndex, l = _list.SelectedIndex, d = _detail.SelectedIndex;
                Safe("refresh", () =>
                {
                    BuildSections();
                    Restore(_sections, s);
                    BuildList(false);
                    Restore(_list, l);
                    BuildDetail();
                    Restore(_detail, d);
                });
            }
            Pool.Process();
        }

        private static void Restore(NativeMenu m, int index)
        {
            if (m.Items.Count > 0) m.SelectedIndex = Math.Max(0, Math.Min(index, m.Items.Count - 1));
        }

        /// <summary>Something changed: redraw all three panels on the next frame (never inside an item's own event).</summary>
        private void Changed(bool empire)
        {
            if (empire) _changed(); else _save();
            _rebuild = true;
        }

        private void Safe(string where, Action build)
        {
            try { build(); }
            catch (Exception ex) { Log.Error("Office " + where, ex); }
        }

        // ---------- helpers ----------

        private static NativeItem Row(string title, string description, string badge)
        {
            return new NativeItem(title, description, badge ?? "");
        }

        private static NativeItem Info(string title, string description, string value)
        {
            return new NativeItem(title, description, value ?? "");
        }

        private static string Money(long v) { return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("N0"); }

        private static int Hour() { return GTA.World.CurrentTimeOfDay.Hours; }

        private static int Bar(double value, double max) { return (int)Math.Max(0, Math.Min(100, Math.Round(value / max * 100.0))); }

        private static string OwnerLabel(string owner)
        {
            switch (owner)
            {
                case Owners.Player: return "Yours";
                case Owners.ForSale: return "For sale";
                case Owners.CrownKings: return "Crown Kings";
                case Owners.Ironside: return "Ironside MC";
                case Owners.Velvet: return "Velvet Syndicate";
                default: return owner;
            }
        }

        private string PostLabel(GuardState g)
        {
            if (g.Status == GuardStatus.Injured) return "Injured";
            if (!g.IsPosted) return "Reserve";
            FrontDef def = _content.Front(g.Front);
            return (def != null ? def.ShortName : g.Front) + " " + (g.Post + 1);
        }

        private static string Short(GuardState g) { return g.First + " \"" + g.Nick + "\""; }

        private static void SetTitles(NativeMenu m, string title, string subtitle)
        {
            m.BannerText.Text = title;
            m.Name = subtitle;
        }

        private void FocusRight(int from)
        {
            NativeMenu next = from == 0 ? _list : _detail;
            if (!next.Visible || next.Items.Count == 0) return;
            bool anyEnabled = false;
            foreach (NativeItem it in next.Items) if (it.Enabled) { anyEnabled = true; break; }
            if (!anyEnabled) return;
            Focus(from + 1);
            // Land on the first usable row.
            for (int i = 0; i < next.Items.Count; i++) if (next.Items[i].Enabled) { next.SelectedIndex = i; break; }
        }

        // ---------- left: sections ----------

        private void BuildSections()
        {
            Empire e = _empire();
            _sections.Clear();
            if (e == null) return;
            int net = e.DailyIncome() - e.DailyWages();
            int injured = 0;
            foreach (GuardState g in e.State.Guards) if (g.Status == GuardStatus.Injured) injured++;

            AddSection("Overview", "Your empire at a glance: income, wages, fronts and people.", Money(net) + "/day");
            AddSection("Fronts", "Every business: who holds it, its defense and income. Enter to manage one.", e.OwnedCount() + " held" + Open);
            AddSection("Guards", "Your roster: rank, trait and post. Enter to manage one.", e.LivingCount() + (injured > 0 ? " (" + injured + " hurt)" : "") + Open);
            AddSection("Hire recruits", "The Broker sends 3 new faces every morning at 06:00.", e.State.Offers.Count + " today" + Open);
            int maxGrudge = 0;
            foreach (CrewState c in e.State.Crews) maxGrudge = Math.Max(maxGrudge, c.Grudge);
            AddSection("Rivals", "The three crews, their leaders, and how much they hate you.", "Grudge " + maxGrudge + Open);
            AddSection("Settings", "Attack frequency and map blips.", Open.Trim());
        }

        private NativeItem AddSection(string title, string description, string badge)
        {
            NativeItem item = Row(title, description, badge);
            item.Activated += (s, a) => FocusRight(0);
            _sections.Add(item);
            return item;
        }

        // ---------- middle: the section's list ----------

        private void BuildList(bool resetSelection)
        {
            Empire e = _empire();
            _list.Clear();
            _listKeys.Clear();
            if (e == null) return;
            switch (_section)
            {
                case Section.Overview: ListOverview(e); break;
                case Section.Fronts: ListFronts(e); break;
                case Section.Guards: ListGuards(e); break;
                case Section.Hire: ListRecruits(e); break;
                case Section.Settings: ListSettings(e); break;
                case Section.Rivals: ListRivals(e); break;
            }
            if (resetSelection && _list.Items.Count > 0) _list.SelectedIndex = 0;
            BuildDetail();
        }

        private void AddListRow(NativeItem item, object key, bool opensDetail)
        {
            if (opensDetail) item.Activated += (s, a) => FocusRight(1);
            _list.Add(item);
            _listKeys.Add(key);
        }

        private void ListOverview(Empire e)
        {
            SetTitles(_list, "Overview", "THIS IS YOUR OUTFIT");
            int income = e.DailyIncome(), wages = e.DailyWages();
            int injured = 0, fallen = 0;
            foreach (GuardState g in e.State.Guards)
            {
                if (g.Status == GuardStatus.Injured) injured++;
                if (g.Status == GuardStatus.Dead) fallen++;
            }
            AddListRow(Info("Daily income", "Paid at every in-game midnight to whoever you're playing.", Money(income)), null, false);
            AddListRow(Info("Daily wages", "Taken out before income. Unpaid guards quit, newest first (Loyal ones never do).", Money(wages)), null, false);
            AddListRow(Info("Net per day", "Income minus wages.", Money(income - wages)), null, false);
            AddListRow(Info("This week, about", "Seven days at today's numbers.", Money((income - wages) * 7L)), null, false);
            AddListRow(Info("Last midnight", "What actually landed last night.", Money(e.State.LastNet)), null, false);
            AddListRow(Info("Fronts held", "Buy the car wash from the Broker; the rest are held by rival crews.", e.OwnedCount() + " of " + _content.Fronts.Count), null, false);
            AddListRow(Info("Guards", "Hired people, posted or in the reserve.", e.LivingCount() + (injured > 0 ? ", " + injured + " hurt" : "")), null, false);
            if (fallen > 0) AddListRow(Info("Fallen", "Guards who died at their post.", fallen.ToString()), null, false);
        }

        private void ListFronts(Empire e)
        {
            SetTitles(_list, "Fronts", e.OwnedCount() + " OF " + _content.Fronts.Count + " ARE YOURS");
            var ordered = new List<FrontDef>(_content.Fronts);
            ordered.Sort((x, y) =>
            {
                int c = OwnerRank(e.OwnerOf(x.Id)).CompareTo(OwnerRank(e.OwnerOf(y.Id)));
                return c != 0 ? c : string.Compare(x.Area + x.Name, y.Area + y.Name, StringComparison.Ordinal);
            });
            foreach (FrontDef def in ordered)
            {
                string owner = e.OwnerOf(def.Id);
                AttackState atk = e.State.Attack;
                string badge = owner == Owners.Player
                    ? (atk != null && atk.Front == def.Id ? (atk.Phase == AttackPhase.Warning ? "Threatened" : "Under attack") : GuardsAt(e, def) + "/" + def.Posts.Count + " guards")
                    : OwnerLabel(owner);
                NativeItem row = Row(def.Name, def.Area + ". " + OwnerLabel(owner) + ". " + Money(def.Income) + " a day. Enter to manage.", badge + Open);
                AddListRow(row, def, true);
            }
        }

        private static int OwnerRank(string owner)
        {
            switch (owner)
            {
                case Owners.Player: return 0;
                case Owners.ForSale: return 1;
                case Owners.CrownKings: return 2;
                case Owners.Ironside: return 3;
                default: return 4;
            }
        }

        private static int GuardsAt(Empire e, FrontDef def)
        {
            int n = 0;
            for (int p = 0; p < FrontDef.MaxPosts; p++) if (e.AtPost(def.Id, p) != null) n++;
            return n;
        }

        private void ListGuards(Empire e)
        {
            SetTitles(_list, "Guards", "EVERYONE ON YOUR PAYROLL");
            int living = 0;
            foreach (GuardState g in e.State.Guards)
            {
                if (g.Status == GuardStatus.Dead) continue;
                living++;
                NativeItem row = Row(Short(g) + " " + g.Last,
                    Economy.RankName((Rank)g.Rank) + ", " + Economy.TraitName((Trait)g.Trait) + ", " + Economy.WeaponName((Weapon)g.Weapon) + ". Enter to manage.",
                    PostLabel(g) + Open);
                AddListRow(row, g, true);
            }
            if (living == 0)
            {
                NativeItem none = Row("Nobody yet", "Hire recruits to start a roster.", "");
                none.Enabled = false;
                AddListRow(none, null, false);
            }
            foreach (GuardState g in e.State.Guards)
            {
                if (g.Status != GuardStatus.Dead) continue;
                NativeItem row = Row(Short(g) + " " + g.Last, "Fell at " + (g.FellAt ?? "a front") + ". " + Economy.RankName((Rank)g.Rank) + ".", "Fallen");
                row.Enabled = false;
                AddListRow(row, null, false);
            }
        }

        private void ListRecruits(Empire e)
        {
            SetTitles(_list, "Hire", "TODAY'S RECRUITS FROM THE BROKER");
            if (e.State.Offers.Count == 0)
            {
                NativeItem none = Row("Nobody today", "The Broker sends new recruits every morning at 06:00.", "");
                none.Enabled = false;
                AddListRow(none, null, false);
                return;
            }
            foreach (GuardState offer in e.State.Offers)
            {
                Trait t = (Trait)offer.Trait;
                NativeItem row = Row(Short(offer) + " " + offer.Last, Economy.TraitName(t) + ". " + Economy.TraitEffect(t) + " Enter to hire.",
                    Money(e.OfferPrice(offer)) + Open);
                AddListRow(row, offer, true);
            }
        }

        private void ListSettings(Empire e)
        {
            SetTitles(_list, "Settings", "HOW FRONTS & RIVALS BEHAVES");
            string[] freq = { "Off", "Low", "Normal", "High" };
            var f = new NativeListItem<string>("Attack frequency", "How often crews come for your fronts: Low every ~75 min, Normal ~45, High ~25. Off stops attacks.", freq);
            int at = Array.IndexOf(freq, e.State.Frequency ?? "Normal");
            f.SelectedIndex = at < 0 ? 2 : at;
            f.ItemChanged += (s, a) => { e.State.Frequency = f.SelectedItem; Changed(false); };
            AddListRow(f, null, false);

            string turfAt = e.State.TurfFrequency ?? _config.TurfFrequency.ToString();
            var turf = new NativeListItem<string>("Rival turf moves", "How often crews grab fronts for sale and fight each other at night. Off freezes the map.", freq);
            int ta = Array.IndexOf(freq, turfAt);
            turf.SelectedIndex = ta < 0 ? 2 : ta;
            turf.ItemChanged += (s, a) => { e.State.TurfFrequency = turf.SelectedItem; Changed(false); };
            AddListRow(turf, null, false);

            string[] levels = { "All", "Important", "Minimal" };
            var notes = new NativeListItem<string>("Notifications", "All: everything. Important: no taunts or street gossip. Minimal: attacks and fronts changing hands only.", levels);
            int la = Array.IndexOf(levels, e.State.NotifyLevel ?? _config.NotifyLevel.ToString());
            notes.SelectedIndex = la < 0 ? 0 : la;
            notes.ItemChanged += (s, a) => { e.State.NotifyLevel = notes.SelectedItem; Changed(false); };
            AddListRow(notes, null, false);

            var live = new NativeCheckboxItem("Live defenses", "On: attacks become fights when you're within 300 m. Off: they always resolve on their own.", _config.LiveDefense && !e.State.LiveDefenseOff);
            live.Enabled = _config.LiveDefense;
            live.CheckboxChanged += (s, a) => { e.State.LiveDefenseOff = !live.Checked; Changed(false); };
            AddListRow(live, null, false);

            var sfx = new NativeCheckboxItem("Sound effects", "The game's own alert and cash sounds for attacks, purchases and the midnight take.", _config.SoundEffects && !e.State.SoundOff);
            sfx.Enabled = _config.SoundEffects;
            sfx.CheckboxChanged += (s, a) => { e.State.SoundOff = !sfx.Checked; Changed(false); if (sfx.Checked) Sounds.Play(Cue.Purchase); };
            AddListRow(sfx, null, false);

            var blips = new NativeCheckboxItem("Map blips", "Show fronts on the map and minimap.", !e.State.HideBlips);
            blips.CheckboxChanged += (s, a) => { e.State.HideBlips = !blips.Checked; Changed(true); };
            AddListRow(blips, null, false);

            NativeItem version = Row("Version", "Fronts & Rivals, a free single-player mod for Grand Theft Auto V.", Main.ModVersion);
            version.Enabled = false;
            AddListRow(version, null, false);
        }

        // ---------- right: detail of the highlighted list row ----------

        private void BuildDetail()
        {
            Empire e = _empire();
            _detail.Clear();
            object key = _list.SelectedIndex >= 0 && _list.SelectedIndex < _listKeys.Count ? _listKeys[_list.SelectedIndex] : null;
            bool show = e != null && key != null;
            _detail.Visible = _open && show;
            if (!show) { if (_focus == 2) Focus(1); return; }

            if (key is FrontDef) DetailFront(e, (FrontDef)key);
            else if (key is CrewState) DetailCrew(e, (CrewState)key);
            else if (key is GuardState && _section == Section.Hire) DetailRecruit(e, (GuardState)key);
            else if (key is GuardState) DetailGuard(e, (GuardState)key);
        }

        private static NativeStatsPanel FrontBars(double defense, int income)
        {
            return new NativeStatsPanel(new NativeStatsInfo("Defense", Bar(defense, 200)), new NativeStatsInfo("Income", Bar(income, 12000)));
        }

        private void DetailFront(Empire e, FrontDef def)
        {
            string owner = e.OwnerOf(def.Id);
            double defense = owner == Owners.Player ? e.Defense(def.Id, Hour()) : 0;
            SetTitles(_detail, def.Name, def.Area.ToUpperInvariant() + " · " + OwnerLabel(owner).ToUpperInvariant());

            NativeItem header = Info("Defense " + defense.ToString("0"), "Total power of the guards here, adjusted for night. Pays " + Money(def.Income) + " every midnight while you hold it.",
                Money(def.Income) + "/day");
            header.Panel = FrontBars(defense, def.Income);
            _detail.Add(header);

            if (owner == Owners.ForSale)
            {
                NativeItem buy = Row("Buy from the Broker", (def.Starter ? "The starter price. " : _config.FrontPriceDays + " days of its takings. ") + "It's yours the moment you pay; then post guards here.", Money(Economy.FrontPrice(def, _config.FrontPriceDays)));
                buy.Activated += (s, a) => Safe("buy", () => BuyFront(e, def));
                _detail.Add(buy);
            }
            else if (owner == Owners.Player)
            {
                for (int post = 0; post < FrontDef.MaxPosts; post++) _detail.Add(PostRow(e, def, post));
            }
            else
            {
                NativeItem held = Row("Held by the " + OwnerLabel(owner), "Clear their guards and claim the door to take it. Taking fronts arrives with the rival crews.", "");
                held.Enabled = false;
                _detail.Add(held);
            }

            if (owner == Owners.Player) AddUpgrades(e, def);

            NativeItem waypoint = Row("Set waypoint", "Marks the front's door on your map.", "");
            waypoint.Activated += (s, a) =>
            {
                GTA.World.WaypointPosition = new Vector3(def.Door.X, def.Door.Y, def.Door.Z);
                GTA.UI.Notification.Show("Waypoint set: " + def.Name + ".");
            };
            waypoint.Enabled = def.Door.IsSet;
            _detail.Add(waypoint);
        }

        /// <summary>One row per post. Left/right steps through: Empty, then every guard who is free or already here,
        /// in hire order, so the order never jumps around.</summary>
        private NativeItem PostRow(Empire e, FrontDef def, int post)
        {
            if (post >= def.Posts.Count)
            {
                NativeItem none = Row("Post " + (post + 1), "There's no safe spot for a fourth guard at this front.", "None");
                none.Enabled = false;
                return none;
            }
            GuardState current = e.AtPost(def.Id, post);
            var choices = new List<GuardState> { null };
            foreach (GuardState g in e.State.Guards)
            {
                if (g.Status == GuardStatus.Dead) continue;
                if (!g.IsPosted || g == current) choices.Add(g);
            }
            string[] labels = new string[choices.Count];
            int selected = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                GuardState g = choices[i];
                labels[i] = g == null ? "Empty" : Short(g) + (g.Status == GuardStatus.Injured ? " (hurt)" : "");
                if (g != null && g == current) selected = i;
            }
            var item = new NativeListItem<string>("Post " + (post + 1),
                "Who stands here. Left and right step through your free guards. Guards already posted elsewhere aren't listed.", labels);
            item.SelectedIndex = selected;
            item.ItemChanged += (s, a) => Safe("assign", () =>
            {
                GuardState pick = choices[item.SelectedIndex];
                GuardState now = e.AtPost(def.Id, post);
                if (pick == null) { if (now != null) e.SendToReserve(now); }
                else e.Assign(pick, def.Id, post);
                Changed(true);
            });
            return item;
        }

        // ---------- upgrades ----------

        private void AddUpgrades(Empire e, FrontDef def)
        {
            FrontState fs = e.Find(def.Id);
            if (fs == null) return;
            AddUpgrade(fs, "Cameras", 15000, "+10% defense, and attack warnings arrive an hour earlier.", fs.Cameras, () => fs.Cameras = true);
            AddUpgrade(fs, "Reinforced doors", 25000, "+15 defense.", fs.Doors, () => fs.Doors = true);
            AddUpgrade(fs, "Floodlights", 10000, "No night penalty for the guards here.", fs.Floodlights, () => fs.Floodlights = true);
            AddUpgrade(fs, "Armory", 30000, "Guards here fight one weapon tier higher (rifles get +10%).", fs.Armory, () => fs.Armory = true);
        }

        private void AddUpgrade(FrontState fs, string name, int price, string effect, bool owned, Action install)
        {
            NativeItem row = Row(name, effect + (owned ? " Installed." : " Permanent, even if the front changes hands."), owned ? "Installed" : Money(price));
            row.Enabled = !owned;
            row.Activated += (s, a) => Safe("upgrade", () =>
            {
                if (Game.Player.Money < price) { GTA.UI.Notification.Show("You need " + Money(price) + " for " + name.ToLowerInvariant() + "."); return; }
                Game.Player.Money -= price;
                Sounds.Play(Cue.Purchase);
                install();
                Log.Write("Office: bought " + name + " at " + fs.Id);
                Changed(true);
            });
            _detail.Add(row);
        }

        private void BuyFront(Empire e, FrontDef def)
        {
            if (!e.CanBuy(def.Id)) return;
            Player p = Game.Player;
            int price = Economy.FrontPrice(def, _config.FrontPriceDays);
            if (p.Money < price)
            {
                GTA.UI.Notification.Show("The Broker wants " + Money(price) + " for the " + def.Name + ".");
                return;
            }
            p.Money -= price;
            Sounds.Play(Cue.Purchase);
            e.SetOwner(def.Id, Owners.Player);
            Log.Write("Office: bought " + def.Id + " for " + price);
            Notifier.Post("CHAR_DEFAULT", "The Broker", def.Name, "It's yours. Put some people on it before somebody else gets ideas.");
            Changed(true);
        }

        private void DetailGuard(Empire e, GuardState g)
        {
            Rank rank = (Rank)g.Rank;
            int next = Economy.NextRankXp(rank);
            SetTitles(_detail, g.First + " " + g.Last, ("\"" + g.Nick + "\" · " + Economy.RankName(rank)).ToUpperInvariant());

            NativeItem header = Info(Economy.TraitName((Trait)g.Trait), Economy.TraitEffect((Trait)g.Trait) + " Kills " + g.Kills + ", defenses survived " + g.Defenses
                + ". " + (next > 0 ? g.Xp + " of " + next + " XP to the next rank." : "Top rank."), Economy.RankName(rank));
            header.Panel = new NativeStatsPanel(new NativeStatsInfo("Rank", Bar((int)rank + 1, 4)), new NativeStatsInfo("Experience", next > 0 ? Bar(g.Xp, next) : 100));
            _detail.Add(header);

            // Post: the reserve, or any post at a front you own (taking an occupied one swaps the two).
            var targets = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(null, -1) };
            var labels = new List<string> { "Reserve" };
            int selected = 0;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!e.PlayerOwns(def.Id)) continue;
                for (int post = 0; post < def.Posts.Count; post++)
                {
                    GuardState there = e.AtPost(def.Id, post);
                    if (g.Front == def.Id && g.Post == post) selected = labels.Count;
                    targets.Add(new KeyValuePair<string, int>(def.Id, post));
                    labels.Add(def.ShortName + " " + (post + 1) + (there != null && there != g ? " (swap " + there.First + ")" : ""));
                }
            }
            var postItem = new NativeListItem<string>("Post", "Where this guard stands. Left and right to change. Picking an occupied post swaps the two.", labels.ToArray());
            postItem.SelectedIndex = selected;
            postItem.ItemChanged += (s, a) => Safe("reassign", () =>
            {
                KeyValuePair<string, int> t = targets[postItem.SelectedIndex];
                if (t.Key == null) e.SendToReserve(g); else e.Assign(g, t.Key, t.Value);
                Changed(true);
            });
            _detail.Add(postItem);

            int price = e.WeaponUpgradePrice(g);
            Weapon w = (Weapon)g.Weapon;
            NativeItem weapon = Row(price > 0 ? "Buy " + (w == Weapon.Pistol ? "an SMG" : "a rifle") : "Weapon: rifle",
                price > 0 ? "Carries a " + Economy.WeaponName(w) + " now. The " + (w == Weapon.Pistol ? "SMG is x1.2" : "rifle is x1.4") + " power."
                          : "Already carries a rifle, the best tier.", price > 0 ? Money(price) : "Best");
            weapon.Activated += (s, a) => Safe("weapon", () =>
            {
                int cost = e.WeaponUpgradePrice(g);
                if (cost <= 0) return;
                if (Game.Player.Money < cost) { GTA.UI.Notification.Show("You need " + Money(cost) + " for that."); return; }
                Game.Player.Money -= cost;
                Sounds.Play(Cue.Purchase);
                e.UpgradeWeapon(g);
                Changed(true);
            });
            weapon.Enabled = price > 0;
            _detail.Add(weapon);

            NativeItem letGo = Row(_letGoArmed == g.Id ? "Let go: press Enter again" : "Let go", "They leave your roster for good. No refund.", "");
            letGo.Activated += (s, a) => Safe("let go", () =>
            {
                if (_letGoArmed != g.Id) { _letGoArmed = g.Id; _rebuild = true; return; }
                e.LetGo(g);
                Log.Write("Office: let go guard " + g.Id + " " + g.FullName);
                _letGoArmed = 0;
                Focus(1);
                Changed(true);
            });
            _detail.Add(letGo);
        }

        // ---------- rivals ----------

        private void ListRivals(Empire e)
        {
            SetTitles(_list, "Rivals", "THE CREWS WHO WANT WHAT YOU HAVE");
            foreach (CrewState c in e.State.Crews)
            {
                CrewDef def = _content.Crew(c.Id);
                if (def == null) continue;
                int held = 0;
                foreach (FrontState f in e.State.Fronts) if (f.Owner == c.Id) held++;
                bool quiet = Nemesis.IsQuiet(c, e.State.Day);
                string desc = def.Style + ". Holds " + held + (held == 1 ? " front. " : " fronts. ") + (quiet ? "Gone quiet after losing their leaders." : "Enter for their leaders.");
                NativeItem row = Row(def.Name, desc, (quiet ? "Quiet" : "Grudge " + c.Grudge) + Open);
                AddListRow(row, c, true);
            }
        }

        private void DetailCrew(Empire e, CrewState c)
        {
            CrewDef def = _content.Crew(c.Id);
            SetTitles(_detail, def.Name, def.Style.ToUpperInvariant());
            NativeItem header = Info("Last move", string.IsNullOrEmpty(c.LastMove) ? "Nothing yet. They're watching." : c.LastMove + ".", "Grudge " + c.Grudge);
            header.Panel = new NativeStatsPanel(new NativeStatsInfo("Grudge", c.Grudge), new NativeStatsInfo("Manpower", Bar(c.Manpower, Nemesis.MaxManpower)));
            _detail.Add(header);

            CrewMember boss = c.Boss;
            if (boss != null) _detail.Add(MemberRow(boss, "Boss"));
            foreach (CrewMember m in c.Lieutenants()) _detail.Add(MemberRow(m, "Lieutenant"));
            foreach (CrewMember m in c.Members)
            {
                if (m.Alive) continue;
                NativeItem dead = Row(m.DisplayName, m.Log.Count > 0 ? m.Log[m.Log.Count - 1] : "Dead.", "Dead");
                dead.Enabled = false;
                _detail.Add(dead);
            }

            FrontDef theirs = null;
            foreach (FrontState f in e.State.Fronts) if (f.Owner == c.Id) { theirs = _content.Front(f.Id); break; }
            NativeItem waypoint = Row("Waypoint to their front",
                theirs != null ? "Marks the " + theirs.Name + ". Clear its guards and claim the door to take it." : "They hold no fronts right now.", "");
            waypoint.Enabled = theirs != null && theirs.Door.IsSet;
            waypoint.Activated += (s, a) =>
            {
                GTA.World.WaypointPosition = new Vector3(theirs.Door.X, theirs.Door.Y, theirs.Door.Z);
                GTA.UI.Notification.Show("Waypoint set: " + theirs.Name + " (" + def.Name + ").");
            };
            _detail.Add(waypoint);
        }

        private static NativeItem MemberRow(CrewMember m, string role)
        {
            string last = m.Log.Count > 0 ? " " + m.Log[m.Log.Count - 1] : "";
            string scars = m.Scars.Count > 0 ? ", " + m.Scars.Count + (m.Scars.Count == 1 ? " scar" : " scars") : "";
            return Info(m.DisplayName, role + ", " + m.LeaderTrait + ". Rep " + m.Rep + scars + "." + last, role + " · Rep " + m.Rep);
        }

        private void DetailRecruit(Empire e, GuardState offer)
        {
            Trait t = (Trait)offer.Trait;
            SetTitles(_detail, offer.First + " " + offer.Last, ("\"" + offer.Nick + "\" · RECRUIT"));
            _detail.Add(Info(Economy.TraitName(t), Economy.TraitEffect(t), ""));
            _detail.Add(Info("Wage", "Paid every in-game midnight.", Money(Economy.Wage(Rank.Recruit)) + "/day"));
            _detail.Add(Info("Starts with", "A pistol. Buy better weapons on the guard's page.", "Pistol"));
            int price = e.OfferPrice(offer);
            NativeItem hire = Row("Hire for " + Money(price), "They join your reserve and wait for a post.", Money(price));
            hire.Activated += (s, a) => Safe("hire", () => Hire(e, offer));
            _detail.Add(hire);
        }

        private void Hire(Empire e, GuardState offer)
        {
            int price;
            Empire.HireResult r = e.Hire(offer, Game.Player.Money, out price);
            switch (r)
            {
                case Empire.HireResult.Ok:
                    Game.Player.Money -= price;
                    Sounds.Play(Cue.Purchase);
                    Log.Write("Office: hired " + offer.FullName + " for " + price);
                    GTA.UI.Notification.Show("Hired " + Short(offer) + ". Post them from Fronts.");
                    Focus(1);
                    Changed(false);
                    break;
                case Empire.HireResult.NotEnoughCash: GTA.UI.Notification.Show("You need " + Money(price) + " to hire " + offer.First + "."); break;
                case Empire.HireResult.ReserveFull: GTA.UI.Notification.Show("Your reserve is full (6). Post someone first."); break;
                case Empire.HireResult.RosterFull: GTA.UI.Notification.Show("Your roster is full (30)."); break;
            }
        }
    }
}
