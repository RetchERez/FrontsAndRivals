// Fronts & Rivals: the Office. One native GTA menu with submenus, like the Interaction Menu: a single column in the
// top-left corner, three levels deep, one level on screen at a time.
//
//   Office                      Fronts                       Car Wash
//    Next Step: Take the...      Hands On Car Wash  Yours >   Defense 18        $1,000/day
//    Fronts            1 >       Brouge Liquor  Dead Kings >  Fill Empty Posts
//    Guards            2 >       ...                          Arm This Front    $700
//    Hire Recruits     4 >                                    Post 1     Tomas Nguyen
//    Rivals              >                                    Cameras           $7,500
//    Messages            >
//    Overview            >
//    Settings            >
//
// Select (Enter / A) opens the next level, Back (Backspace / B) goes up one; Back at the top closes.
// Opened by the hotkey, the phone contact, or holding the Interaction Menu button (M / Back/View).
// Native look: the game's banner and arrows, the description box under the menu, bars not numbers.
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
        private const int Rows = 12;          // like the game's own menus: scrolls past twelve rows
        private const float MenuWidth = 540f; // wider than the Interaction Menu (431): GTA's own font and size, with room so names and values never touch
        private const string Open = "";

        private enum Section { Overview, Fronts, Guards, Hire, Rivals, Messages, Settings }

        // What each row of the top level opens (null: the Next Step row, which jumps straight to its front).
        private readonly List<Section?> _sectionKeys = new List<Section?>();

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
        private bool _hidingDetail; // switching levels in code must not count as Backspace
        private Section _section = Section.Overview;

        // What the list rows stand for, so the detail panel knows what to show.
        private readonly List<object> _listKeys = new List<object>();
        private int _letGoArmed;
        private bool _rebuild;

        /// <summary>Set when DevTools=true: Settings gets a "Dev tools" row (the controller's way in).</summary>
        public Action OpenDevTools;

        /// <summary>Wipes the empire and starts again (Main keeps a copy of the old one).</summary>
        public Action StartOver;

        /// <summary>Gives the city back to the crews but keeps your people (Main does the world side).</summary>
        public Action ResetCity;
        private bool _resetCityArmed;

        /// <summary>Starts a raid on a rival front (Main supplies the clock). Returns false if it couldn't start.</summary>
        public Func<FrontDef, bool> StartRaid;

        /// <summary>The map waypoint landed on this front: the next time the Office opens, it opens on its page.</summary>
        private string _openOn;
        public void OpenOn(string frontId) { _openOn = frontId; }
        private bool _startOverArmed;

        public OfficeMenu(ContentDb content, Config config, Func<Empire> empire, Func<Random> rng, Action changed, Action save)
        {
            _content = content;
            _config = config;
            _empire = empire;
            _rng = rng;
            _changed = changed;
            _save = save;

            // Three levels in the same spot; only the one you're in is on screen.
            _sections = Panel("Office", "FRONTS & RIVALS", MenuWidth, 0f);
            _list = Panel("Fronts", "", MenuWidth, 0f);
            _detail = Panel("", "", MenuWidth, 0f);
            _panels = new[] { _sections, _list, _detail };

            _sections.SelectedIndexChanged += (s, e) => { if (_open) Safe("section", () => { Section? k = SectionAt(_sections.SelectedIndex); if (k.HasValue) { _section = k.Value; BuildList(true); } }); };
            _list.SelectedIndexChanged += (s, e) => { if (_open && _focus >= 1) Safe("detail", BuildDetail); };
            for (int i = 0; i < _panels.Length; i++)
            {
                int index = i;
                _panels[i].Closing += (s, e) => OnBack(index, e);
            }
        }

        private Section? SectionAt(int index) { return index >= 0 && index < _sectionKeys.Count ? _sectionKeys[index] : null; }

        private NativeMenu Panel(string title, string subtitle, float width, float x)
        {
            var m = new NativeMenu(title, subtitle, "");
            m.Width = width;
            m.Offset = new PointF(x, 0f);
            m.MaxItems = Rows;
            // The game's own Interaction Menu banner texture, titled in GTA's menu font (Chalet), like Rockstar's menus.
            MenuStyle.Banner(m, width);
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
            Tips.Show(Tip.Office);
            _section = Section.Overview;
            Safe("open", () =>
            {
                BuildSections();
                _sections.SelectedIndex = 0;
                Section? first = SectionAt(0);
                if (first.HasValue) _section = first.Value;
                BuildList(true);
            });
            Focus(0);
            if (_openOn != null)
            {
                string id = _openOn;
                _openOn = null;
                Safe("open on front", () => JumpToFront(id));
            }
        }

        /// <summary>Straight to a front's page (a map waypoint, the Next Step row).</summary>
        private void JumpToFront(string id)
        {
            _sections.SelectedIndex = Math.Max(0, _sectionKeys.IndexOf(Section.Fronts)); // rebuilds the list for Fronts
            for (int i = 0; i < _listKeys.Count; i++)
            {
                var key = _listKeys[i] as FrontDef;
                if (key != null && key.Id == id) { _list.SelectedIndex = i; break; }
            }
            Focus(1);
            FocusRight(1);
        }

        private void CloseAll()
        {
            _open = false;
            _startOverArmed = false;
            _resetCityArmed = false;
            _closingAll = true;
            foreach (NativeMenu m in _panels) m.Visible = false;
            _closingAll = false;
        }

        /// <summary>Backspace: up one level, or close from the top.</summary>
        private void OnBack(int panel, CancelEventArgs e)
        {
            if (_closingAll || _hidingDetail || !_open) return;
            if (panel == 0) { e.Cancel = true; CloseAll(); return; }
            e.Cancel = true;
            if (panel == _focus) Focus(panel - 1);
        }

        private void Focus(int panel)
        {
            _focus = panel;
            // One level on screen at a time, like the game's own menus. Hiding a level in code isn't a Back press.
            _hidingDetail = true;
            try { for (int i = 0; i < _panels.Length; i++) { _panels[i].Visible = _open && i == panel; _panels[i].AcceptsInput = i == panel; } }
            finally { _hidingDetail = false; }
            _letGoArmed = 0;
            _startOverArmed = false;
            _resetCityArmed = false;
            // Bottom-right control hints say exactly what each key does in this panel.
            NativeMenu m = _panels[panel];
            m.Buttons.Clear();
            m.Buttons.Add(new InstructionalButton("Select", Control.FrontendAccept));
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
                    if (_focus >= 2) { BuildDetail(); Restore(_detail, d); }
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

        /// <summary>Strong / Fair / Weak against a typical attack on a front this size, from the same rules as the game.</summary>
        private string DefenseWord(Empire e, FrontDef def, double defense)
        {
            if (defense <= 0) return "~r~None~s~";
            double worst = 1.0;
            foreach (CrewDef crew in _content.Crews)
            {
                CrewState cs = Nemesis.Crew(e.State, crew.Id);
                int grudge = cs != null ? cs.Grudge : 0;
                int squad = Resolver.SquadSize(def.Tier, grudge, cs != null ? cs.Manpower : 10);
                double attack = Resolver.AttackStrength(squad, crew.SoldierPower, 10, false, grudge) * Difficulty.Strength(e.State.Difficulty);
                worst = Math.Min(worst, Resolver.WinChance(defense, attack));
            }
            return worst >= 0.7 ? "~g~Strong~s~" : worst >= 0.45 ? "~y~Fair~s~" : "~r~Weak~s~";
        }

        private static string HeatColored(int heat)
        {
            string label = Rules.Heat.Label(heat);
            return (heat < 15 ? "~b~" : heat < 40 ? "~y~" : heat < 70 ? "~o~" : "~r~") + label + "~s~";
        }

        /// <summary>Set by Main: the message log (newest first).</summary>
        public Func<IList<Notifier.Logged>> Messages;

        private void ListMessages()
        {
            SetTitles(_list, "Messages", "NEWEST FIRST");
            IList<Notifier.Logged> log = Messages != null ? Messages() : null;
            if (log == null || log.Count == 0) { AddListRow(Row("Nothing Yet", "Texts and alerts show up here once things get going.", ""), null, false); return; }
            foreach (Notifier.Logged m in log)
                AddListRow(Info(m.Subject, m.Sender + ": " + m.Message, m.When), null, false);
        }

        // ---------- helpers ----------

        private static NativeItem Row(string title, string description, string badge)
        {
            return new NativeItem(title, description, badge ?? "");
        }

        /// <summary>A row that opens the next panel: GTA's own menu arrow on the right, like the Interaction Menu.</summary>
        private static NativeItem Opens(NativeItem item)
        {
            item.RightBadgeSet = new BadgeSet("commonmenu", "arrowright", "arrowright");
            return item;
        }

        /// <summary>An owner in its crew's map colour (yours in green), using the game's own text colour codes.</summary>
        private string OwnerColored(string owner)
        {
            Empire e = _empire();
            string key = owner == Owners.Player ? (e != null && e.State.CrewColor != null ? e.State.CrewColor : CrewColors.DefaultPlayer) : CrewColors.ForCrew(owner);
            return CrewColors.Get(key).Text + OwnerLabel(owner) + "~s~";
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
                case Owners.ForSale: return "For Sale";
                case Owners.CrownKings: return "Dead Kings";
                case Owners.Ironside: return "Hellfire MC";
                case Owners.Velvet: return "Vinewood Elite";
                default: return owner;
            }
        }

        private string PostLabel(GuardState g)
        {
            if (g.Status == GuardStatus.Injured) return "Injured";
            if (g.Escort) return "Bodyguard";
            if (_empire() != null && _empire().OnRaid(g)) return "On a Raid";
            if (!g.IsPosted) return "Reserve";
            FrontDef def = _content.Front(g.Front);
            return (def != null ? def.ShortName : g.Front) + " " + (g.Post + 1);
        }

        private static string Short(GuardState g) { return g.ShortName; }

        private static void SetTitles(NativeMenu m, string title, string subtitle)
        {
            m.BannerText.Text = title;
            // GTA's banner font is big: shrink a long title so it always fits inside its panel.
            float fit = title.Length > 0 ? m.Width / (title.Length * 25f) : 1f;
            m.BannerText.Scale = Math.Max(0.5f, Math.Min(0.85f, 0.85f * fit));
            m.Name = subtitle;
        }

        /// <summary>Select: down one level (the front's page, a guard, a recruit...).</summary>
        private void FocusRight(int from)
        {
            if (from == 1) BuildDetail();
            NativeMenu next = from == 0 ? _list : _detail;
            if (next.Items.Count == 0) return;
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

            _sectionKeys.Clear();
            // What to do next, always first, like the game's objective text. Selecting it goes straight there.
            Empire.Hint next = e.NextStep(_config.Raids);
            NativeItem step = Row("~y~" + next.Title + "~s~", next.Text + " Select to go to it.", "");
            Section? goes = next.FrontId != null ? (Section?)null
                : next.Title.StartsWith("Hire") ? Section.Hire : next.Title.StartsWith("Arm") ? Section.Fronts
                : next.Title.StartsWith("Post") ? Section.Guards : (Section?)Section.Fronts;
            Opens(step);
            if (next.FrontId != null)
            {
                string target = next.FrontId;
                step.Activated += (s, a) => Safe("next step", () => JumpToFront(target));
            }
            else
            {
                Section where = goes.Value;
                step.Activated += (s, a) => Safe("next step", () => { _section = where; BuildList(true); FocusRight(0); });
            }
            _sections.Add(step);
            _sectionKeys.Add(null);

            AddSection(Section.Fronts, "Fronts", "Every business in the city, who holds it, and yours to manage.", e.OwnedCount() + " of " + _content.Fronts.Count);
            int free = e.FreeGuards().Count;
            AddSection(Section.Guards, "Guards", "Your people: their post, rank, gun and skills. Free guards are in the reserve, ready to post.",
                e.LivingCount() + (free > 0 ? " · " + free + " Free" : "") + (injured > 0 ? " · " + injured + " Hurt" : ""));
            AddSection(Section.Hire, "Hire Recruits", Economy.RecruitsPerDay + " new faces turn up every morning at 06:00. Hire them before midnight.", e.State.Offers.Count + " Today");
            int maxGrudge = 0;
            foreach (CrewState c in e.State.Crews) maxGrudge = Math.Max(maxGrudge, c.Grudge);
            AddSection(Section.Rivals, "Rivals", "The three crews and their leaders. Street heat rises with every front you take and cools each night: the hotter it is, the sooner they hit back.",
                "Heat: " + HeatColored(e.State.Heat));
            AddSection(Section.Messages, "Messages", "Every text and alert from Fronts & Rivals, newest first, so you can read them again.", (Messages != null ? Messages().Count : 0).ToString());
            AddSection(Section.Overview, "Overview", "Income, wages, street heat and your people at a glance.", Money(net) + "/day");
            AddSection(Section.Settings, "Settings", "Difficulty, attacks, notifications, sound, map icons and more.", "");
        }

        private NativeItem AddSection(Section key, string title, string description, string badge)
        {
            NativeItem item = Opens(Row(title, description, badge));
            item.Activated += (s, a) => Safe("open section", () => { _section = key; BuildList(true); FocusRight(0); });
            _sections.Add(item);
            _sectionKeys.Add(key);
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
                case Section.Messages: ListMessages(); break;
            }
            if (resetSelection && _list.Items.Count > 0) _list.SelectedIndex = 0;
            if (_focus >= 2) BuildDetail();
        }

        private void AddListRow(NativeItem item, object key, bool opensDetail)
        {
            if (opensDetail) { Opens(item); item.Activated += (s, a) => FocusRight(1); }
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
            AddListRow(Info("Daily Income", "Paid at every in-game midnight to whoever you're playing.", Money(income)), null, false);
            AddListRow(Info("Daily Wages", "Cheap for a small crew: past " + Economy.FreePayroll + " guards, each extra one adds 4% to everyone's wage (now x"
                + Economy.PayrollFactor(e.LivingCount()).ToString("0.00") + "). Unpaid guards quit, newest first (Loyal ones never do).", Money(wages)), null, false);
            AddListRow(Info("Net per Day", "Income minus wages. Fronts pay more the longer you hold them (up to +30%), and every front adds +1.5% to all of them (up to +20%).", Money(income - wages)), null, false);
            AddListRow(Info("This Week (About)", "Seven days at today's numbers.", Money((income - wages) * 7L)), null, false);
            AddListRow(Info("Last Midnight", "What actually landed last night.", Money(e.State.LastNet)), null, false);
            AddListRow(Info("Fronts Held", "Every front is held by a rival crew until you take it: clear its soldiers and claim the door, or send a crew.", e.OwnedCount() + " of " + _content.Fronts.Count), null, false);
            int held = e.OwnedCount();
            int heatNow = e.State.Heat;
            string heat = "Every front you take raises it; it cools off every night. The hotter it is, the sooner the crews hit back and the harder their fronts fight (more armour, heavier guns). Lie low a night or two to cool it down."
                + (_config.EmpirePressure ? " " + HeatDetail(held) : "");
            NativeItem heatRow = Info("Street Heat", heat, Rules.Heat.Label(heatNow));
            heatRow.Panel = new NativeStatsPanel(new NativeStatsInfo("Heat", heatNow));
            AddListRow(heatRow, null, false);
            AddListRow(Info("Guards", "Hired people, posted or in the reserve.", e.LivingCount() + (injured > 0 ? ", " + injured + " Hurt" : "")), null, false);
            if (fallen > 0) AddListRow(Info("Fallen", "Guards who died at their post.", fallen.ToString()), null, false);
        }

        private static string HeatDetail(int held)
        {
            double f = Rules.Attacks.PressureCooldownFactor(held);
            int bonus = Rules.Attacks.PressureSquadBonus(held);
            if (f >= 1.0 && bonus == 0) return "Past 3 fronts, size draws attention too.";
            int faster = (int)Math.Round((1.0 / f - 1.0) * 100.0);
            return "Your size alone brings attacks " + faster + "% more often" + (bonus > 0 ? ", with " + bonus + " extra " + (bonus == 1 ? "attacker" : "attackers") : "") + ".";
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
                    ? (atk != null && atk.Front == def.Id ? "~r~Under Attack~s~" : OwnerColored(owner) + "  " + GuardsAt(e, def) + "/" + def.Capacity)
                    : OwnerColored(owner);
                NativeItem row = Row(def.Name, def.Area + ". " + OwnerLabel(owner) + ". " + Money(Economy.BaseIncome(def)) + " a night. Select to manage.", badge + Open);
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
                    Economy.RankName((Rank)g.Rank) + ", " + Economy.TraitName((Trait)g.Trait) + ", " + Economy.WeaponName((Weapon)g.Weapon) + ", skills " + Stats.Overall(g) + ". Select to manage.",
                    PostLabel(g) + Open);
                AddListRow(row, g, true);
            }
            if (living == 0)
            {
                NativeItem none = Row("Nobody Yet", "Hire recruits to start a roster.", "");
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
            SetTitles(_list, "Hire Recruits", "LOOKING FOR WORK TODAY");
            if (e.State.Offers.Count == 0)
            {
                NativeItem none = Row("Nobody Today", "New recruits turn up every morning at 06:00.", "");
                none.Enabled = false;
                AddListRow(none, null, false);
                return;
            }
            foreach (GuardState offer in e.State.Offers)
            {
                Trait t = (Trait)offer.Trait;
                NativeItem row = Row(offer.FullName, Economy.TraitName(t) + ". " + Economy.TraitEffect(t) + " Select to hire.",
                    Money(e.OfferPrice(offer)) + Open);
                AddListRow(row, offer, true);
            }
        }

        private void ListSettings(Empire e)
        {
            SetTitles(_list, "Settings", "HOW FRONTS & RIVALS BEHAVES");
            string[] freq = { "Off", "Low", "Normal", "High" };
            string[] levels = { "All", "Important", "Minimal" };
            var notes = new NativeListItem<string>("Notifications", "All: everything. Important: no taunts or street gossip. Minimal: attacks and fronts changing hands only.", levels);
            int la = Array.IndexOf(levels, e.State.NotifyLevel ?? _config.NotifyLevel.ToString());
            notes.SelectedIndex = la < 0 ? 0 : la;
            notes.ItemChanged += (s, a) => { e.State.NotifyLevel = notes.SelectedItem; Changed(false); };
            AddListRow(notes, null, false);

            var sfx = new NativeCheckboxItem("Sound Effects", "The game's own alert and cash sounds for attacks, purchases and the midnight take.", _config.SoundEffects && !e.State.SoundOff);
            sfx.Enabled = _config.SoundEffects;
            sfx.CheckboxChanged += (s, a) => { e.State.SoundOff = !sfx.Checked; Changed(false); if (sfx.Checked) Sounds.Play(Cue.Purchase); };
            AddListRow(sfx, null, false);

            var route = new NativeCheckboxItem("Route to Attacks", "When one of your fronts is hit, your GPS shows the way there.", !e.State.RouteOff);
            route.CheckboxChanged += (s, a) => { e.State.RouteOff = !route.Checked; Changed(true); };
            AddListRow(route, null, false);

            var blips = new NativeCheckboxItem("Map Blips", "Show fronts on the map and minimap.", !e.State.HideBlips);
            blips.CheckboxChanged += (s, a) => { e.State.HideBlips = !blips.Checked; Changed(true); };
            AddListRow(blips, null, false);

            var color = new NativeListItem<string>("Crew Colour", "The colour your crew wears, and your fronts and bodyguards show on the map. Change it any time. "
                + "The Dead Kings wear forest green, Hellfire MC orange and the Vinewood Elite royal blue.", CrewColors.Names().ToArray());
            color.SelectedIndex = CrewColors.IndexOf(e.State.CrewColor ?? CrewColors.DefaultPlayer);
            color.ItemChanged += (s, a) =>
            {
                e.State.CrewColor = CrewColors.All[color.SelectedIndex].Key;
                if (CrewColors.TakenByRival(e.State.CrewColor)) GTA.UI.Notification.Show("Heads up: a rival crew already wears " + CrewColors.All[color.SelectedIndex].Name.ToLowerInvariant() + ".");
                Changed(true);
            };
            AddListRow(color, null, false);

            var diff = new NativeListItem<string>("Difficulty", "How hard the crews push. Higher: stronger attacks that come more often, better-defended rival "
                + "fronts, and less time to get there. Relaxed x0.7 and half again the wait, Easy x0.85, Normal, Hard x1.2, Brutal x1.45 and attacks 30% sooner.",
                Difficulty.Names);
            diff.SelectedIndex = Difficulty.Clamp(e.State.Difficulty) + 2;
            diff.ItemChanged += (s, a) => { e.State.Difficulty = diff.SelectedIndex - 2; Changed(false); };
            AddListRow(diff, null, false);

            var f = new NativeListItem<string>("Attack Frequency", "How often crews come for your fronts, in free-roam time: Low about every 90 minutes, Normal 60, High 35. Off stops attacks.", freq);
            int at = Array.IndexOf(freq, e.State.Frequency ?? "Normal");
            f.SelectedIndex = at < 0 ? 2 : at;
            f.ItemChanged += (s, a) => { e.State.Frequency = f.SelectedItem; Changed(false); };
            AddListRow(f, null, false);

            string turfAt = e.State.TurfFrequency ?? _config.TurfFrequency.ToString();
            var turf = new NativeListItem<string>("Rival Turf Moves", "How often crews grab fronts for sale and fight each other at night. Off freezes the map.", freq);
            int ta = Array.IndexOf(freq, turfAt);
            turf.SelectedIndex = ta < 0 ? 2 : ta;
            turf.ItemChanged += (s, a) => { e.State.TurfFrequency = turf.SelectedItem; Changed(false); };
            AddListRow(turf, null, false);

            var live = new NativeCheckboxItem("Live Defenses", "On: attacks become fights when you're within 300 m. Off: they always resolve on their own.", _config.LiveDefense && !e.State.LiveDefenseOff);
            live.Enabled = _config.LiveDefense;
            live.CheckboxChanged += (s, a) => { e.State.LiveDefenseOff = !live.Checked; Changed(false); };
            AddListRow(live, null, false);

            var death = new NativeCheckboxItem("Permanent Death", "On: a guard killed at their post is gone for good. Off: they're hurt and back in two days.",
                e.PermanentDeath(_config.PermanentDeath));
            death.Enabled = _config.PermanentDeath && !_config.ProtectedMode;
            death.CheckboxChanged += (s, a) => { e.State.GuardsRecover = !death.Checked; Changed(false); };
            AddListRow(death, null, false);


            if (OpenDevTools != null)
            {
                NativeItem dev = Row("Dev Tools", "Testing tools: force an attack, add cash, scout fronts. Shown because DevTools=true.", Open.Trim());
                dev.Activated += (s, a) => { CloseAll(); OpenDevTools(); };
                AddListRow(dev, null, false);
            }

            NativeItem tips = Row("Show Tips Again", "The short first-time tips (the Office, posting guards, attacks, buying, bodyguards) show again when they come up.", "");
            tips.Activated += (s, a) => { Tips.Reset(); Changed(false); GTA.UI.Notification.Show("Tips will show again."); };
            AddListRow(tips, null, false);

            if (ResetCity != null)
            {
                NativeItem again = Row(_resetCityArmed ? "Reset the City? Select Again to Confirm" : "Reset the City",
                    "Go again: every front returns to where it started and the crews come back at full strength. You keep your guards, "
                    + "their ranks, skills and guns (all back in the reserve). Front upgrades are lost. Runs so far: " + e.State.CityResets + ".", "");
                again.Activated += (s, a) =>
                {
                    if (!_resetCityArmed) { _resetCityArmed = true; _rebuild = true; return; }
                    _resetCityArmed = false;
                    CloseAll();
                    ResetCity();
                };
                AddListRow(again, null, false);
            }

            if (StartOver != null)
            {
                NativeItem fresh = Row(_startOverArmed ? "Start a New Empire? Select Again to Confirm" : "Start a New Empire",
                    "For a new story game, or to start over: every front, guard and rival goes back to day one. Your current empire is kept as empire.old.json.", "");
                fresh.Activated += (s, a) =>
                {
                    if (!_startOverArmed) { _startOverArmed = true; _rebuild = true; return; }
                    _startOverArmed = false;
                    CloseAll();
                    StartOver();
                };
                AddListRow(fresh, null, false);
            }

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
            if (!show) { if (_focus == 2) Focus(1); return; }

            if (key is FrontDef) DetailFront(e, (FrontDef)key);
            else if (key is CrewState) DetailCrew(e, (CrewState)key);
            else if (key is GuardState && _section == Section.Hire) DetailRecruit(e, (GuardState)key);
            else if (key is GuardState) DetailGuard(e, (GuardState)key);
        }

        private static NativeStatsPanel FrontBars(double defense, int income)
        {
            return new NativeStatsPanel(new NativeStatsInfo("Defense", Bar(defense, 300)), new NativeStatsInfo("Income", Bar(income, 3000)));
        }

        private void DetailFront(Empire e, FrontDef def)
        {
            string owner = e.OwnerOf(def.Id);
            double defense = owner == Owners.Player ? e.Defense(def.Id, Hour()) : 0;
            SetTitles(_detail, def.ShortName, (def.Name + " · " + def.Area + " · " + OwnerLabel(owner)).ToUpperInvariant());

            string pays;
            int tonight = Economy.BaseIncome(def);
            if (owner == Owners.Player)
            {
                FrontState fsHeld = e.Find(def.Id);
                int days = Math.Max(0, e.State.Day - fsHeld.HeldSinceDay);
                tonight = e.FrontIncome(def.Id);
                int est = (int)Math.Round((Economy.EstablishedFactor(days) - 1) * 100), net = (int)Math.Round((Economy.NetworkFactor(e.OwnedCount()) - 1) * 100);
                pays = "Pays " + Money(tonight) + " tonight: " + Money(Economy.BaseIncome(def)) + " base, +" + est + "% for " + days + (days == 1 ? " day" : " days") + " held (up to +30%), +"
                     + net + "% from your other fronts.";
            }
            else pays = "Pays " + Money(Economy.BaseIncome(def)) + " a night to start, more the longer you hold it.";
            string rating = owner == Owners.Player ? DefenseWord(e, def, defense) : "";
            NativeItem header = Info(owner == Owners.Player ? "Defense: " + rating : "Pays", (owner == Owners.Player ? "How well the guards here would hold off a typical attack right now (night is harder). " : "") + pays, Money(tonight) + "/day");
            header.Panel = FrontBars(defense, Economy.BaseIncome(def));
            _detail.Add(header);

            if (owner == Owners.Player)
            {
                AddQuickActions(e, def);
                for (int post = 0; post < def.Capacity; post++) _detail.Add(PostRow(e, def, post));
            }
            else
            {
                int need = Economy.TrustNeeded(def.Tier);
                bool dug = Rules.Attacks.DugIn(e.State, e.Find(def.Id));
                NativeItem held = Row("Held by the " + OwnerLabel(owner), dug ? "They just took it and they're dug in. You can claim it back after midnight."
                    : e.Trusted(def) ? "Clear their soldiers, then claim the door to take it."
                    : "Too big for you yet: you can take it once you hold " + need + " fronts. You hold " + e.OwnedCount() + ".", e.Trusted(def) ? "" : "Hold " + need);
                held.Enabled = false;
                _detail.Add(held);
                if (_config.Raids) AddRaidRow(e, def);
            }

            if (owner == Owners.Player) AddUpgrades(e, def);

            NativeItem waypoint = Row("Set Waypoint", "Marks the front's door on your map.", "");
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

        /// <summary>Send a crew: your strongest guards try to take this rival front without you.</summary>
        private void AddRaidRow(Empire e, FrontDef def)
        {
            RaidState current = e.State.Raid;
            if (current != null && current.Front == def.Id)
            {
                _detail.Add(Info("Raid Under Way", "Your people are hitting the " + def.ShortName + ". You'll hear in a few minutes. Odds when they left: about "
                    + (int)Math.Round(current.Odds * 100) + "%.", "Out"));
                NativeItem back = Row("Call Them Back", "Calls the raid off before it starts: nobody fights, everyone goes home to their posts.", "");
                back.Activated += (s, a) => Safe("call back", () =>
                {
                    e.CancelRaid();
                    GTA.UI.Notification.Show("Raid on the " + def.ShortName + " called off. Your people are heading home.");
                    Changed(true);
                });
                _detail.Add(back);
                return;
            }
            string blocked = e.RaidBlocked(def);
            List<GuardState> team = e.RaidTeam();
            var names = new List<string>();
            foreach (GuardState g in team) names.Add(g.ShortName);
            int odds = blocked == null ? (int)Math.Round(e.RaidOdds(def, Hour()) * 100) : 0;
            NativeItem raid = Row("Send a Crew", blocked ?? ("Sends your strongest from the reserve: " + string.Join(", ", names.ToArray()) + ". About " + odds
                + "% to take it. One raid a day; they're gone a few minutes, and if it goes wrong they can be hurt or killed."),
                blocked == null ? odds + "%" : "");
            raid.Enabled = blocked == null;
            raid.Activated += (s, a) => Safe("raid", () =>
            {
                if (StartRaid == null || !StartRaid(def)) return;
                Sounds.Play(Cue.Purchase);
                GTA.UI.Notification.Show("Your crew is on the way to the " + def.ShortName + ".");
                Changed(true);
            });
            _detail.Add(raid);
        }

        /// <summary>One click instead of four: fill every empty post, or give everyone here their next gun.</summary>
        private void AddQuickActions(Empire e, FrontDef def)
        {
            int empty = e.EmptyPosts(def.Id), free = e.FreeGuards().Count;
            NativeItem fill = Row("Fill Empty Posts", empty == 0 ? "Every post here is covered."
                : free == 0 ? "Nobody is waiting in the reserve. Hire recruits first."
                : "Posts your strongest guards from the reserve: " + empty + " empty " + (empty == 1 ? "post" : "posts") + ", " + free + " free.",
                empty > 0 ? empty + " Empty" : "Full");
            fill.Enabled = empty > 0 && free > 0;
            fill.Activated += (s, a) => Safe("fill", () =>
            {
                int n = e.FillPosts(def.Id);
                if (n > 0) { Sounds.Play(Cue.Purchase); GTA.UI.Notification.Show("Posted " + n + (n == 1 ? " guard" : " guards") + " at the " + def.ShortName + "."); }
                Changed(true);
            });
            _detail.Add(fill);

            int cost = e.ArmFrontPrice(def.Id);
            NativeItem arm = Row("Arm This Front", cost > 0 ? "Every guard here steps up to their next gun, all at once (as far as their rank allows)."
                : "Nobody here can carry a better gun yet: they're at the best their rank allows, or nobody is posted.",
                cost > 0 ? Money(cost) : "");
            arm.Enabled = cost > 0;
            arm.Activated += (s, a) => Safe("arm", () =>
            {
                int total = e.ArmFrontPrice(def.Id);
                if (total <= 0) return;
                if (Game.Player.Money < total) { GTA.UI.Notification.Show("You need " + Money(total) + " to arm the " + def.ShortName + "."); return; }
                Game.Player.Money -= total;
                int n = e.ArmFront(def.Id);
                Sounds.Play(Cue.Purchase);
                GTA.UI.Notification.Show("Armed " + n + (n == 1 ? " guard" : " guards") + " at the " + def.ShortName + " for " + Money(total) + ".");
                Changed(true);
            });
            _detail.Add(arm);

            int vests = e.VestFrontPrice(def.Id);
            NativeItem vest = Row("Vest This Front", vests > 0 ? "Body armour for everyone posted here who isn't wearing it: they last longer in a fight and die less. "
                + Money(Economy.VestPrice) + " a guard." : "Everyone posted here already wears body armour (or nobody is posted).", vests > 0 ? Money(vests) : "");
            vest.Enabled = vests > 0;
            vest.Activated += (s, a) => Safe("vests", () =>
            {
                int total = e.VestFrontPrice(def.Id);
                if (total <= 0) return;
                if (Game.Player.Money < total) { GTA.UI.Notification.Show("You need " + Money(total) + " to vest the " + def.ShortName + "."); return; }
                Game.Player.Money -= total;
                int n = e.VestFront(def.Id);
                Sounds.Play(Cue.Purchase);
                GTA.UI.Notification.Show("Put " + n + (n == 1 ? " guard" : " guards") + " at the " + def.ShortName + " in body armour.");
                Changed(true);
            });
            _detail.Add(vest);
        }

        private void AddUpgrades(Empire e, FrontDef def)
        {
            FrontState fs = e.Find(def.Id);
            if (fs == null) return;
            var owned = new List<string>();
            if (fs.Cameras) owned.Add("Cameras");
            if (fs.Doors) owned.Add("Reinforced Doors");
            if (fs.Floodlights) owned.Add("Floodlights");
            if (fs.Armory) owned.Add("Armory");
            if (owned.Count > 0)
            {
                NativeItem have = Info("Upgrades", "Installed here: " + string.Join(", ", owned.ToArray()) + ". They stay with the building, even if it changes hands.",
                    owned.Count + " of " + Math.Max(owned.Count, Economy.UpgradeSlots(def.Tier)));
                have.Enabled = false;
                _detail.Add(have);
            }
            int t = def.Tier;
            AddUpgrade(fs, t, "Cameras", Economy.UpgradePrice(15000, t), "+10% defense, and you see trouble sooner: your people hold 2 minutes longer when the front is hit.", fs.Cameras, () => fs.Cameras = true);
            AddUpgrade(fs, t, "Reinforced Doors", Economy.UpgradePrice(20000, t), "+20 defense. Worth the most to a lightly guarded front.", fs.Doors, () => fs.Doors = true);
            AddUpgrade(fs, t, "Floodlights", Economy.UpgradePrice(10000, t), "No night penalty for the guards here.", fs.Floodlights, () => fs.Floodlights = true);
            AddUpgrade(fs, t, "Armory", Economy.UpgradePrice(30000, t), "Guards here fight one weapon tier higher (carbine rifles get +10%).", fs.Armory, () => fs.Armory = true);
        }

        private void AddUpgrade(FrontState fs, int tier, string name, int price, string effect, bool owned, Action install)
        {
            if (owned) return; // listed together on the Upgrades line
            if (!Economy.CanInstall(name, tier)) return; // too small a place for it
            NativeItem row = Row(name, effect + " Permanent, even if the front changes hands.", Money(price));
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

        /// <summary>Shooting, Strength, Stamina and Stealth as bars, like the player's Skills screen.</summary>
        private static NativeItem SkillsRow(GuardState g, string note)
        {
            NativeItem row = Info("Skills", "Shooting " + g.Shooting + " (aim), Strength " + g.Strength + " (power and health), Stamina " + g.Stamina
                + " (fewer injuries), Stealth " + g.Stealth + " (fewer deaths). " + note, "Overall " + Stats.Overall(g));
            row.Panel = new NativeStatsPanel(new NativeStatsInfo("Shooting", g.Shooting), new NativeStatsInfo("Strength", g.Strength),
                                             new NativeStatsInfo("Stamina", g.Stamina), new NativeStatsInfo("Stealth", g.Stealth));
            return row;
        }

        private void DetailGuard(Empire e, GuardState g)
        {
            Rank rank = (Rank)g.Rank;
            int next = Economy.NextRankXp(rank);
            SetTitles(_detail, g.First + " " + g.Last, ((string.IsNullOrEmpty(g.Nick) ? "" : "\"" + g.Nick + "\" · ") + Economy.RankName(rank)).ToUpperInvariant());

            NativeItem header = Info(Economy.TraitName((Trait)g.Trait), Economy.TraitEffect((Trait)g.Trait) + " Kills " + g.Kills + ", defenses survived " + g.Defenses
                + ". " + (next > 0 ? g.Xp + " of " + next + " XP to the next rank." : "Top rank."), Economy.RankName(rank));
            header.Panel = new NativeStatsPanel(new NativeStatsInfo("Rank", Bar((int)rank + 1, 4)), new NativeStatsInfo("Experience", next > 0 ? Bar(g.Xp, next) : 100));
            _detail.Add(header);
            _detail.Add(SkillsRow(g, "They get better the longer they work for you: every day on a post, every fight, every kill."));

            // Post: the reserve, or any post at a front you own (taking an occupied one swaps the two).
            var targets = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>(null, -1) };
            var labels = new List<string> { "Reserve" };
            int selected = 0;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!e.PlayerOwns(def.Id)) continue;
                for (int post = 0; post < def.Capacity; post++)
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

            int escorts = e.EscortCount(), held = e.OwnedCount();
            int slots = FrontsAndRivals.Spawn.EscortRules.Slots(held), nextAt = FrontsAndRivals.Spawn.EscortRules.NextAt(held);
            bool trusted = FrontsAndRivals.Spawn.EscortRules.Trusted(g.Rank);
            string why = slots == 0 ? "Bodyguards come with a bigger empire: your first at " + FrontsAndRivals.Spawn.EscortRules.FirstAt + " fronts (you hold " + held + ")."
                : !trusted ? "Only a Soldier or better can watch your back. " + g.ShortName + " is still a Recruit."
                : !g.Escort && escorts >= slots ? "You have " + slots + (slots == 1 ? " bodyguard" : " bodyguards") + " already." + (nextAt > 0 ? " Another at " + nextAt + " fronts." : "")
                : "On: they leave their post and stay at your side, on foot or in your car, and fight whoever comes for you. Off: back to their post. You can have "
                  + slots + (nextAt > 0 ? " now, another at " + nextAt + " fronts." : ".");
            var guard = new NativeCheckboxItem("Bodyguard", why, g.Escort);
            guard.Enabled = g.Escort || (g.Status == GuardStatus.Active && trusted && escorts < slots);
            guard.CheckboxChanged += (s, a) => Safe("bodyguard", () =>
            {
                if (!e.SetEscort(g, guard.Checked)) { GTA.UI.Notification.Show(why); }
                else
                {
                    GTA.UI.Notification.Show(guard.Checked ? g.ShortName + " is on the way to you." : g.ShortName + " is heading back to their post.");
                    if (guard.Checked) Tips.Show(Tip.Bodyguard);
                }
                Changed(true);
            });
            if (_config.Bodyguards) _detail.Add(guard);

            int price = e.WeaponUpgradePrice(g);
            Weapon w = (Weapon)g.Weapon;
            NativeItem weapon;
            if (price > 0 && Empire.WeaponLocked(g))
            {
                Weapon locked = w + 1;
                weapon = Row("Next Gun: " + Economy.WeaponName(locked), "Carries " + Economy.WithArticle(w) + ". Only a " + Economy.RankName(Economy.RankFor(locked))
                    + " or higher is trusted with " + Economy.WithArticle(locked) + ". Ranks come from fights.", Economy.RankName(Economy.RankFor(locked)));
                weapon.Enabled = false;
            }
            else if (price > 0)
            {
                Weapon nextGun = w + 1;
                weapon = Row("Buy " + Economy.WeaponName(nextGun),
                    "Carries " + Economy.WithArticle(w) + " (x" + DefenseRating.WeaponPower(w).ToString("0.0#") + " power). "
                    + Economy.WeaponName(nextGun) + ": x" + DefenseRating.WeaponPower(nextGun).ToString("0.0#")
                    + ". Step " + (int)nextGun + " of " + (int)DefenseRating.TopWeapon + ".", Money(price));
            }
            else weapon = Row("Carbine Rifle", "The best gun on the ladder (x2.0 power).", "Best");
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
            weapon.Enabled = price > 0 && !Empire.WeaponLocked(g);
            _detail.Add(weapon);

            NativeItem armour = g.Vest ? Info("Body Armour", "Wears a vest: lasts longer in a live fight, and is hurt and killed less when a fight is decided without you.", "Worn")
                : Row("Buy Body Armour", "A vest: lasts longer in a live fight, and is hurt and killed less when a fight is decided without you.", Money(Economy.VestPrice));
            if (!g.Vest)
                armour.Activated += (s, a) => Safe("vest", () =>
                {
                    if (Game.Player.Money < Economy.VestPrice) { GTA.UI.Notification.Show("You need " + Money(Economy.VestPrice) + " for body armour."); return; }
                    Game.Player.Money -= Economy.VestPrice;
                    g.Vest = true;
                    Sounds.Play(Cue.Purchase);
                    Changed(true);
                });
            else armour.Enabled = false;
            _detail.Add(armour);

            NativeItem letGo = Row(_letGoArmed == g.Id ? "Fire " + g.First + "? Select Again to Confirm" : "Fire Guard",
                "Off the payroll for good: no wage from tonight, no severance, and their skills walk out with them.", "");
            letGo.Activated += (s, a) => Safe("fire", () =>
            {
                if (_letGoArmed != g.Id) { _letGoArmed = g.Id; _rebuild = true; return; }
                e.LetGo(g);
                Sounds.Play(Cue.Purchase);
                GTA.UI.Notification.Show(g.ShortName + " is off the payroll.");
                Log.Write("Office: fired guard " + g.Id + " " + g.FullName);
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
                string desc = def.Style + ". Holds " + held + (held == 1 ? " front. " : " fronts. ") + (quiet ? "Gone quiet after losing their leaders." : "Select for their leaders.");
                NativeItem row = Row(def.Name, desc, (quiet ? "Quiet" : "Grudge " + c.Grudge) + Open);
                AddListRow(row, c, true);
            }
        }

        private void DetailCrew(Empire e, CrewState c)
        {
            CrewDef def = _content.Crew(c.Id);
            SetTitles(_detail, def.Name, def.Style.ToUpperInvariant());
            NativeItem header = Info("Last Move", string.IsNullOrEmpty(c.LastMove) ? "Nothing yet. They're watching." : c.LastMove + ".", "Grudge " + c.Grudge);
            header.Panel = new NativeStatsPanel(new NativeStatsInfo("Grudge", c.Grudge), new NativeStatsInfo("Manpower", Bar(c.Manpower, Nemesis.MaxManpower)));
            _detail.Add(header);

            if (!string.IsNullOrEmpty(c.FeudWith))
            {
                CrewDef enemy = _content.Crew(c.FeudWith);
                NativeItem feud = Info("At War With", "Their latest turf fight was with the " + (enemy != null ? enemy.Name : c.FeudWith) + ". Crews at war bleed each other: a chance for you.",
                    enemy != null ? enemy.Name : c.FeudWith);
                feud.Enabled = false;
                _detail.Add(feud);
            }

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
            NativeItem waypoint = Row("Set Waypoint to Their Front",
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
            SetTitles(_detail, offer.First + " " + offer.Last, (string.IsNullOrEmpty(offer.Nick) ? "" : "\"" + offer.Nick + "\" · ") + "RECRUIT");
            _detail.Add(Info(Economy.TraitName(t), Economy.TraitEffect(t), ""));
            _detail.Add(SkillsRow(offer, "Where they start. Skills grow on the job; better skills cost more to hire."));
            _detail.Add(Info("Wage", "Paid every in-game midnight. More with rank, and more for everyone once you have over " + Economy.FreePayroll + " guards.",
                Money(Economy.WageFor(Rank.Recruit, e.LivingCount() + 1, 1.0)) + "/day"));
            _detail.Add(Info("Starts With", "A knife. Arm them on their page, one gun at a time: pistol, pump shotgun, SMG, carbine rifle.", "Knife"));
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
                    Tips.Show(Tip.Posting);
                    Focus(1);
                    Changed(false);
                    break;
                case Empire.HireResult.NotEnoughCash: GTA.UI.Notification.Show("You need " + Money(price) + " to hire " + offer.First + "."); break;
                case Empire.HireResult.ReserveFull: GTA.UI.Notification.Show("Your reserve is full (6). Post someone first."); break;
                case Empire.HireResult.RosterFull: GTA.UI.Notification.Show("Your roster is full (" + Empire.MaxRoster + ")."); break;
            }
        }
    }
}
