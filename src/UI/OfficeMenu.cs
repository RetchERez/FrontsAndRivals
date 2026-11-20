// Fronts & Rivals: the Office. GTA's native menu style via LemonUI (the game's own commonmenu textures).
// Design doc rules: two levels deep at most, at most 7 rows on screen, every row has a one-line description
// and a badge on the right, bars instead of raw numbers, one accent colour.
//
//   The Office ── Fronts ── (front page: bars, 4 posts, buy, waypoint)
//              ├─ Guards ── (guard page: rank bar, post, weapon, let go)
//              ├─ Hire recruits
//              ├─ Rivals (arrives with the crews)
//              └─ Settings
using System;
using System.Collections.Generic;
using System.Drawing;
using GTA;
using GTA.Math;
using LemonUI;
using LemonUI.Elements;
using LemonUI.Menus;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using Weapon = FrontsAndRivals.Rules.Weapon;

namespace FrontsAndRivals.UI
{
    public sealed class OfficeMenu
    {
        private const int Rows = 7;
        private static readonly Color Accent = Color.FromArgb(255, 22, 112, 74); // the green of your front blips

        public readonly ObjectPool Pool = new ObjectPool();

        private readonly ContentDb _content;
        private readonly Func<Empire> _empire;
        private readonly Func<Random> _rng;
        private readonly Action _worldChanged; // streamer + blips + save
        private readonly Action _save;

        private readonly NativeMenu _office, _fronts, _frontPage, _guards, _guardPage, _recruits, _settings;
        private string _frontId;
        private int _guardId;
        private int _letGoArmed;

        public OfficeMenu(ContentDb content, Func<Empire> empire, Func<Random> rng, Action worldChanged, Action save)
        {
            _content = content;
            _empire = empire;
            _rng = rng;
            _worldChanged = worldChanged;
            _save = save;

            _office = Menu("THE OFFICE", "Your fronts, your people.");
            _fronts = Menu("FRONTS", "Every business on your list.");
            _frontPage = Menu("FRONT", "");
            _guards = Menu("GUARDS", "Everyone on your payroll.");
            _guardPage = Menu("GUARD", "");
            _recruits = Menu("HIRE", "Today's recruits from the Broker.");
            _settings = Menu("SETTINGS", "How Fronts & Rivals behaves.");

            _office.Opening += (s, e) => Safe("office", BuildOffice);
            _fronts.Opening += (s, e) => Safe("fronts", BuildFronts);
            _guards.Opening += (s, e) => Safe("guards", BuildGuards);
            _recruits.Opening += (s, e) => Safe("recruits", BuildRecruits);
            _settings.Opening += (s, e) => Safe("settings", BuildSettings);
        }

        private NativeMenu Menu(string subtitle, string description)
        {
            var m = new NativeMenu("Fronts & Rivals", subtitle, description);
            m.MaxItems = Rows;
            var banner = new ScaledRectangle(PointF.Empty, SizeF.Empty);
            banner.Color = Accent;
            m.Banner = banner;
            Pool.Add(m);
            return m;
        }

        public bool IsOpen { get { return Pool.AreAnyVisible; } }

        public void Open()
        {
            if (_empire() == null) { GTA.UI.Notification.Show("Fronts & Rivals: still loading. Try again in a moment."); return; }
            if (Pool.AreAnyVisible) return;
            _office.Visible = true;
        }

        public void Toggle() { if (Pool.AreAnyVisible) Pool.HideAll(); else Open(); }

        /// <summary>Every tick, also while paused, so the menu never freezes.</summary>
        public void Process() { Pool.Process(); }

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

        private static string Money(long v) { return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("N0"); }

        private static int Hour() { return GTA.World.CurrentTimeOfDay.Hours; }

        private static int Bar(double value, double max) { return (int)Math.Max(0, Math.Min(100, Math.Round(value / max * 100.0))); }

        private static NativeStatsPanel FrontBars(double defense, int income)
        {
            return new NativeStatsPanel(new NativeStatsInfo("Defense", Bar(defense, 200)), new NativeStatsInfo("Income", Bar(income, 12000)));
        }

        private void OpenPage(NativeMenu page, NativeMenu parent, Action build)
        {
            page.Parent = parent;
            Safe(page.Name, build);
            parent.Visible = false;
            page.Visible = true;
        }

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
            return (def != null ? def.Name : g.Front) + " " + (g.Post + 1);
        }

        private static string Short(GuardState g) { return g.First + " \"" + g.Nick + "\""; }

        // ---------- The Office (overview) ----------

        private void BuildOffice()
        {
            Empire e = _empire();
            _office.Clear();
            if (e == null) return;
            int net = e.DailyIncome() - e.DailyWages();
            int injured = 0;
            foreach (GuardState g in e.State.Guards) if (g.Status == GuardStatus.Injured) injured++;

            _office.Add(Row("Overview",
                "Fronts held: " + e.OwnedCount() + " of " + _content.Fronts.Count + ". Daily income " + Money(e.DailyIncome())
                + ", wages " + Money(e.DailyWages()) + ". This week about " + Money(net * 7L) + ". Last midnight: " + Money(e.State.LastNet) + ".",
                Money(net) + " / day"));

            var fronts = Row("Fronts", "Every business: who holds it, its defense and income.", e.OwnedCount() + " held");
            fronts.Activated += (s, a) => OpenPage(_fronts, _office, BuildFronts);
            _office.Add(fronts);

            var guards = Row("Guards", "Your roster: rank, trait and where each guard stands.",
                e.LivingCount() + (injured > 0 ? " (" + injured + " hurt)" : ""));
            guards.Activated += (s, a) => OpenPage(_guards, _office, BuildGuards);
            _office.Add(guards);

            var hire = Row("Hire recruits", "The Broker sends new faces every morning. Unhired ones move on.", e.State.Offers.Count + " today");
            hire.Activated += (s, a) => OpenPage(_recruits, _office, BuildRecruits);
            _office.Add(hire);

            var rivals = Row("Rivals", "The Crown Kings, Ironside MC and the Velvet Syndicate. Their leaders show up here once the crews start moving.", "Soon");
            rivals.Enabled = false;
            _office.Add(rivals);

            var settings = Row("Settings", "Attack frequency and map blips.", "");
            settings.Activated += (s, a) => OpenPage(_settings, _office, BuildSettings);
            _office.Add(settings);
        }

        // ---------- Fronts ----------

        private void BuildFronts()
        {
            Empire e = _empire();
            _fronts.Clear();
            if (e == null) return;
            int hour = Hour();
            foreach (FrontDef def in _content.Fronts)
            {
                string owner = e.OwnerOf(def.Id);
                double defense = owner == Owners.Player ? e.Defense(def.Id, hour) : 0;
                string badge = owner == Owners.Player ? "Secure" : OwnerLabel(owner);
                var row = Row(def.Name, def.Area + ". " + OwnerLabel(owner) + ". Income " + Money(def.Income) + " a day"
                    + (owner == Owners.Player ? ", defense " + defense.ToString("0") + "." : "."), badge);
                row.Panel = FrontBars(defense, def.Income);
                string id = def.Id;
                row.Activated += (s, a) => { _frontId = id; OpenPage(_frontPage, _fronts, BuildFrontPage); };
                _fronts.Add(row);
            }
        }

        private void BuildFrontPage()
        {
            Empire e = _empire();
            FrontDef def = _content.Front(_frontId);
            _frontPage.Clear();
            if (e == null || def == null) return;
            _frontPage.Name = def.Name.ToUpperInvariant();
            string owner = e.OwnerOf(def.Id);
            double defense = owner == Owners.Player ? e.Defense(def.Id, Hour()) : 0;

            var header = Row(def.Name, def.Area + ". " + OwnerLabel(owner) + ". Pays " + Money(def.Income) + " every in-game midnight while you hold it.",
                owner == Owners.Player ? "D " + defense.ToString("0") : OwnerLabel(owner));
            header.Panel = FrontBars(defense, def.Income);
            _frontPage.Add(header);

            if (owner == Owners.ForSale)
            {
                var buy = Row("Buy from the Broker", "The Broker's starter price. It's yours the moment you pay; post guards here right after.", Money(Economy.StarterFrontPrice));
                buy.Activated += (s, a) => BuyFront(def);
                _frontPage.Add(buy);
            }
            else if (owner == Owners.Player)
            {
                for (int post = 0; post < FrontDef.MaxPosts; post++) _frontPage.Add(PostRow(e, def, post));
            }
            else
            {
                var held = Row("Held by the " + OwnerLabel(owner), "Clear their guards and claim the door to take it. Taking fronts arrives with the rival crews.", "");
                held.Enabled = false;
                _frontPage.Add(held);
            }

            var waypoint = Row("Set waypoint", "Marks the front's door on your map.", "");
            waypoint.Activated += (s, a) =>
            {
                GTA.World.WaypointPosition = new Vector3(def.Door.X, def.Door.Y, def.Door.Z);
                GTA.UI.Notification.Show("Waypoint set: " + def.Name + ".");
            };
            waypoint.Enabled = def.Door.IsSet;
            _frontPage.Add(waypoint);
        }

        /// <summary>One row per post: left/right picks who stands there (the reserve, or nobody).</summary>
        private NativeItem PostRow(Empire e, FrontDef def, int post)
        {
            if (post >= def.Posts.Count)
            {
                var none = Row("Post " + (post + 1), "This post is placed the first time you visit the front.", "Not placed");
                none.Enabled = false;
                return none;
            }
            GuardState current = e.AtPost(def.Id, post);
            var choices = new List<GuardState> { null };
            if (current != null) choices.Add(current);
            foreach (GuardState g in e.State.Guards)
                if (g.Status != GuardStatus.Dead && !g.IsPosted) choices.Add(g);

            string[] labels = new string[choices.Count];
            for (int i = 0; i < choices.Count; i++)
            {
                GuardState g = choices[i];
                labels[i] = g == null ? "Empty" : Short(g) + (g.Status == GuardStatus.Injured ? " (hurt)" : "");
            }
            var item = new NativeListItem<string>("Post " + (post + 1),
                "Who stands here. Left and right pick from your reserve. The change happens right away.", labels);
            item.SelectedIndex = current != null ? 1 : 0;
            item.ItemChanged += (s, a) => Safe("assign", () =>
            {
                GuardState pick = choices[item.SelectedIndex];
                GuardState now = e.AtPost(def.Id, post);
                if (pick == null) { if (now != null) e.SendToReserve(now); }
                else e.Assign(pick, def.Id, post);
                _worldChanged();
            });
            return item;
        }

        private void BuyFront(FrontDef def)
        {
            Empire e = _empire();
            if (e == null || !e.CanBuy(def.Id)) return;
            Player p = Game.Player;
            if (p.Money < Economy.StarterFrontPrice)
            {
                GTA.UI.Notification.Show("The Broker wants " + Money(Economy.StarterFrontPrice) + " for the " + def.Name + ".");
                return;
            }
            p.Money -= Economy.StarterFrontPrice;
            e.SetOwner(def.Id, Owners.Player);
            Log.Write("Office: bought " + def.Id + " for " + Economy.StarterFrontPrice);
            _worldChanged();
            Notifier.Post("CHAR_DEFAULT", "The Broker", def.Name, "It's yours. Put some people on it before somebody else gets ideas.");
            Safe("front page", BuildFrontPage);
        }

        // ---------- Guards ----------

        private void BuildGuards()
        {
            Empire e = _empire();
            _guards.Clear();
            if (e == null) return;
            int living = 0;
            foreach (GuardState g in e.State.Guards)
            {
                if (g.Status == GuardStatus.Dead) continue;
                living++;
                var row = Row(Short(g) + " " + g.Last,
                    Economy.RankName((Rank)g.Rank) + ", " + Economy.TraitName((Trait)g.Trait) + ", " + Economy.WeaponName((Weapon)g.Weapon)
                    + ". " + Economy.TraitEffect((Trait)g.Trait), PostLabel(g));
                int id = g.Id;
                row.Activated += (s, a) => { _guardId = id; _letGoArmed = 0; OpenPage(_guardPage, _guards, BuildGuardPage); };
                _guards.Add(row);
            }
            if (living == 0)
            {
                var none = Row("Nobody yet", "Hire recruits from the Office to start a roster.", "");
                none.Enabled = false;
                _guards.Add(none);
            }
            // The memorial: who fell, and where.
            foreach (GuardState g in e.State.Guards)
            {
                if (g.Status != GuardStatus.Dead) continue;
                var row = Row(Short(g) + " " + g.Last, "Fell at " + (g.FellAt ?? "a front") + ". " + Economy.RankName((Rank)g.Rank) + ".", "Fallen");
                row.Enabled = false;
                _guards.Add(row);
            }
        }

        private void BuildGuardPage()
        {
            Empire e = _empire();
            GuardState g = e != null ? e.Guard(_guardId) : null;
            _guardPage.Clear();
            if (g == null) return;
            _guardPage.Name = (g.First + " " + g.Last).ToUpperInvariant();
            Rank rank = (Rank)g.Rank;
            int next = Economy.NextRankXp(rank);

            var header = Row(g.FullName,
                Economy.TraitName((Trait)g.Trait) + ": " + Economy.TraitEffect((Trait)g.Trait) + " Kills " + g.Kills + ", defenses survived " + g.Defenses
                + ". " + (next > 0 ? g.Xp + " of " + next + " XP to the next rank." : "Top rank."), Economy.RankName(rank));
            header.Panel = new NativeStatsPanel(
                new NativeStatsInfo("Rank", Bar((int)rank + 1, 4)),
                new NativeStatsInfo("Experience", next > 0 ? Bar(g.Xp, next) : 100));
            _guardPage.Add(header);

            // Post: reserve, or any post at a front you own.
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
                    labels.Add(def.Name + " " + (post + 1) + (there != null && there != g ? " (swap with " + there.First + ")" : ""));
                }
            }
            var postItem = new NativeListItem<string>("Post", "Where this guard stands. Picking an occupied post swaps the two.", labels.ToArray());
            postItem.SelectedIndex = selected;
            postItem.ItemChanged += (s, a) => Safe("reassign", () =>
            {
                KeyValuePair<string, int> t = targets[postItem.SelectedIndex];
                if (t.Key == null) e.SendToReserve(g); else e.Assign(g, t.Key, t.Value);
                _worldChanged();
            });
            postItem.Enabled = g.Status != GuardStatus.Dead;
            _guardPage.Add(postItem);

            int price = e.WeaponUpgradePrice(g);
            Weapon w = (Weapon)g.Weapon;
            var weapon = Row(price > 0 ? "Buy " + (w == Weapon.Pistol ? "an SMG" : "a rifle") : "Weapon",
                price > 0 ? "Carries a " + Economy.WeaponName(w) + " now. The " + (w == Weapon.Pistol ? "SMG is x1.2" : "rifle is x1.4") + " power."
                          : "Already carries a rifle, the best tier.", price > 0 ? Money(price) : Economy.WeaponName(w));
            weapon.Activated += (s, a) => Safe("weapon", () =>
            {
                int cost = e.WeaponUpgradePrice(g);
                if (cost <= 0) return;
                if (Game.Player.Money < cost) { GTA.UI.Notification.Show("You need " + Money(cost) + " for that."); return; }
                Game.Player.Money -= cost;
                e.UpgradeWeapon(g);
                _worldChanged();
                BuildGuardPage();
            });
            weapon.Enabled = price > 0;
            _guardPage.Add(weapon);

            var letGo = Row(_letGoArmed == g.Id ? "Let go: press again to confirm" : "Let go",
                "They leave your roster for good. No refund.", "");
            letGo.Activated += (s, a) => Safe("let go", () =>
            {
                if (_letGoArmed != g.Id) { _letGoArmed = g.Id; BuildGuardPage(); return; }
                e.LetGo(g);
                Log.Write("Office: let go guard " + g.Id + " " + g.FullName);
                _worldChanged();
                _letGoArmed = 0;
                _guardPage.Back();
            });
            _guardPage.Add(letGo);
        }

        // ---------- Hire ----------

        private void BuildRecruits()
        {
            Empire e = _empire();
            _recruits.Clear();
            if (e == null) return;
            if (e.State.Offers.Count == 0)
            {
                var none = Row("Nobody today", "The Broker sends new recruits every morning at 06:00.", "");
                none.Enabled = false;
                _recruits.Add(none);
                return;
            }
            foreach (GuardState offer in e.State.Offers.ToArray())
            {
                Trait t = (Trait)offer.Trait;
                var row = Row(offer.FullName,
                    Economy.TraitName(t) + ": " + Economy.TraitEffect(t) + " Wage " + Money(Economy.Wage(Rank.Recruit)) + " a day. Starts with a pistol, waits at the HQ.",
                    Money(e.OfferPrice(offer)));
                GuardState pick = offer;
                row.Activated += (s, a) => Safe("hire", () => Hire(e, pick));
                _recruits.Add(row);
            }
        }

        private void Hire(Empire e, GuardState offer)
        {
            int price;
            Empire.HireResult r = e.Hire(offer, Game.Player.Money, out price);
            switch (r)
            {
                case Empire.HireResult.Ok:
                    Game.Player.Money -= price;
                    Log.Write("Office: hired " + offer.FullName + " for " + price);
                    GTA.UI.Notification.Show("Hired " + offer.First + " \"" + offer.Nick + "\". Post them from a front's page.");
                    _save();
                    break;
                case Empire.HireResult.NotEnoughCash: GTA.UI.Notification.Show("You need " + Money(price) + " to hire " + offer.First + "."); break;
                case Empire.HireResult.ReserveFull: GTA.UI.Notification.Show("The HQ reserve is full (6). Post someone first."); break;
                case Empire.HireResult.RosterFull: GTA.UI.Notification.Show("Your roster is full (30)."); break;
            }
            BuildRecruits();
        }

        // ---------- Settings ----------

        private void BuildSettings()
        {
            Empire e = _empire();
            _settings.Clear();
            if (e == null) return;
            string[] freq = { "Off", "Low", "Normal", "High" };
            var f = new NativeListItem<string>("Attack frequency", "How often rival crews come for your fronts. Off stops attacks completely.", freq);
            int at = Array.IndexOf(freq, e.State.Frequency ?? "Normal");
            f.SelectedIndex = at < 0 ? 2 : at;
            f.ItemChanged += (s, a) => { e.State.Frequency = f.SelectedItem; _save(); };
            _settings.Add(f);

            var blips = new NativeCheckboxItem("Map blips", "Show your fronts on the map and minimap.", !e.State.HideBlips);
            blips.CheckboxChanged += (s, a) => { e.State.HideBlips = !blips.Checked; _worldChanged(); };
            _settings.Add(blips);

            var version = Row("Version", "Fronts & Rivals, a free single-player mod for Grand Theft Auto V.", Main.ModVersion);
            version.Enabled = false;
            _settings.Add(version);
        }
    }
}
