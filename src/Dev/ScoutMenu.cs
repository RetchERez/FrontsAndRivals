// Fronts & Rivals dev tools: scout fronts in-game instead of hand-editing fronts.json.
// Pick a front, stand where you want something, press the item. Every change writes fronts.json right away.
using System;
using GTA;
using GTA.Math;
using LemonUI.Menus;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Dev
{
    public sealed class ScoutMenu
    {
        private readonly ContentDb _content;
        private readonly Func<Empire> _empire;
        private readonly Func<Random> _rng;
        private readonly Action _worldChanged; // rebuild streamer + blips, save
        private readonly Func<PauseReason> _gate;

        private readonly NativeMenu _menu;
        private readonly NativeListItem<string> _front;
        private readonly NativeListItem<int> _post;
        private readonly NativeListItem<string> _scenario;
        private readonly NativeItem _status;

        public ScoutMenu(DevTools dev, ContentDb content, Func<Empire> empire, Func<Random> rng, Action worldChanged, Func<PauseReason> gate)
        {
            _content = content;
            _empire = empire;
            _rng = rng;
            _worldChanged = worldChanged;
            _gate = gate;

            _menu = new NativeMenu("Fronts & Rivals", "Scout fronts", "Stand on a spot, then press an item. Saves fronts.json.");
            dev.Pool.Add(_menu);
            dev.Menu.AddSubMenu(_menu);

            string[] names = new string[_content.Fronts.Count];
            for (int i = 0; i < names.Length; i++) names[i] = _content.Fronts[i].Name;
            _front = new NativeListItem<string>("Front", "Which front you're scouting.", names);
            _front.ItemChanged += (s, e) => RefreshStatus();
            _menu.Add(_front);

            _status = new NativeItem("Status", "What this front has so far.");
            _menu.Add(_status);

            Add("Teleport to its door", "Only works once the door is set.", TeleportToDoor);
            Add("Set door here", "Where the front's door is: blip, help prompts and attackers' target.", SetDoor);

            _post = new NativeListItem<int>("Post", "Which of the 4 guard posts to set.", 1, 2, 3, 4);
            _menu.Add(_post);
            _scenario = new NativeListItem<string>("Idle", "What the guard does at this post (night and Lookout override it).", StreamRules.PostScenarios);
            _menu.Add(_scenario);
            Add("Set post here", "Puts the selected post where you stand, facing where you face.", SetPost);
            Add("Remove last post", "Drops the highest-numbered post.", RemoveLastPost);
            Add("Set owner-car spot here", "Where the ownership car parks (used in a later milestone).", SetOwnerSpot);
            Add("Add convoy spawn here", "A road spot 350-450 m away where attackers start. Stand on the road, face the front.", AddConvoy);
            Add("Clear convoy spawns", "Removes all convoy spawns for this front.", ClearConvoys);
            Add("Own this front (dev)", "Gives you the front so its guards stream in.", OwnFront);
            Add("Fill posts with test guards", "Hires recruits and posts one at every empty post.", FillPosts);
            Add("Send guards to reserve", "Takes this front's guards off their posts.", ClearGuards);

            _menu.Shown += (s, e) => RefreshStatus();
        }

        private void Add(string title, string description, Action action)
        {
            var item = new NativeItem(title, description);
            item.Activated += (s, e) =>
            {
                try { action(); RefreshStatus(); }
                catch (Exception ex) { Log.Error("Scout " + title, ex); DevTools.Notify("Failed, see log."); }
            };
            _menu.Add(item);
        }

        private FrontDef Selected { get { return _content.Fronts[_front.SelectedIndex]; } }

        private static Spot Here()
        {
            Ped p = Game.Player.Character;
            Vector3 v = p.Position;
            return Spot.At(v.X, v.Y, v.Z, p.Heading);
        }

        private void Saved(string what)
        {
            _content.SaveFronts();
            _worldChanged();
            Log.Write("Scout: " + Selected.Id + " " + what);
            DevTools.Notify(Selected.Name + ": " + what + ".");
        }

        private void TeleportToDoor()
        {
            FrontDef f = Selected;
            if (!f.Door.IsSet) { DevTools.Notify("Set the door first."); return; }
            if (_gate() != PauseReason.None) { DevTools.Notify("Not during " + _gate() + "."); return; }
            Ped p = Game.Player.Character;
            p.Position = new Vector3(f.Door.X, f.Door.Y, f.Door.Z);
            p.Heading = f.Door.H;
        }

        private void SetDoor() { Selected.Door = Here(); Saved("door set"); }

        private void SetPost()
        {
            FrontDef f = Selected;
            int index = _post.SelectedItem - 1;
            if (index > f.Posts.Count) index = f.Posts.Count; // fill the next free post, no gaps
            Spot s = Here();
            var post = new PostDef { X = s.X, Y = s.Y, Z = s.Z, H = s.H, Scenario = _scenario.SelectedItem };
            if (index < f.Posts.Count) f.Posts[index] = post; else f.Posts.Add(post);
            Saved("post " + (index + 1) + " set (" + post.Scenario + ")");
        }

        private void RemoveLastPost()
        {
            FrontDef f = Selected;
            if (f.Posts.Count == 0) return;
            int last = f.Posts.Count - 1;
            Empire e = _empire();
            if (e != null) { GuardState g = e.AtPost(f.Id, last); if (g != null) e.SendToReserve(g); }
            f.Posts.RemoveAt(last);
            Saved("post " + (last + 1) + " removed");
        }

        private void SetOwnerSpot() { Selected.OwnerSpot = Here(); Saved("owner-car spot set"); }

        private void AddConvoy()
        {
            FrontDef f = Selected;
            Spot s = Here();
            if (f.Door.IsSet)
            {
                float d = f.Door.DistanceTo(s.X, s.Y, s.Z);
                if (d < 350f || d > 450f) DevTools.Notify("Note: " + d.ToString("0") + " m from the door; the doc asks for 350-450 m.");
            }
            f.ConvoySpawns.Add(s);
            Saved("convoy spawn " + f.ConvoySpawns.Count + " added");
        }

        private void ClearConvoys() { Selected.ConvoySpawns.Clear(); Saved("convoy spawns cleared"); }

        private void OwnFront()
        {
            Empire e = _empire();
            if (e == null) { DevTools.Notify("Empire not loaded yet."); return; }
            e.SetOwner(Selected.Id, Owners.Player);
            _worldChanged();
            DevTools.Notify("You own " + Selected.Name + ".");
        }

        private void FillPosts()
        {
            Empire e = _empire();
            FrontDef f = Selected;
            if (e == null) { DevTools.Notify("Empire not loaded yet."); return; }
            if (!e.PlayerOwns(f.Id)) { DevTools.Notify("Own the front first."); return; }
            int hired = 0;
            for (int post = 0; post < f.Posts.Count; post++)
            {
                if (e.AtPost(f.Id, post) != null) continue;
                GuardState g = e.Recruit(_rng());
                if (g == null) { DevTools.Notify("Roster or reserve full."); break; }
                e.Assign(g, f.Id, post);
                Log.Write("Dev: hired " + g.FullName + " (" + g.Model + ") for " + f.Id + " post " + (post + 1));
                hired++;
            }
            _worldChanged();
            DevTools.Notify("Posted " + hired + " test guards at " + f.Name + ".");
        }

        private void ClearGuards()
        {
            Empire e = _empire();
            if (e == null) return;
            for (int post = 0; post < FrontDef.MaxPosts; post++)
            {
                GuardState g = e.AtPost(Selected.Id, post);
                if (g != null) e.SendToReserve(g);
            }
            _worldChanged();
            DevTools.Notify("Guards sent to the reserve.");
        }

        private void RefreshStatus()
        {
            FrontDef f = Selected;
            Empire e = _empire();
            int guards = 0;
            if (e != null) for (int i = 0; i < FrontDef.MaxPosts; i++) if (e.AtPost(f.Id, i) != null) guards++;
            _status.AltTitle = (f.Door.IsSet ? "door" : "no door") + " | posts " + f.Posts.Count + "/4 | convoy " + f.ConvoySpawns.Count
                             + " | " + (e != null ? e.OwnerOf(f.Id) : "?") + " | guards " + guards;
        }
    }
}
