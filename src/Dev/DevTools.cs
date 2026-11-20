// Fronts & Rivals: developer tools. Only exist when DevTools=true in FrontsAndRivals.ini.
//   F9  copies the player's position + heading as a fronts.json snippet (clipboard + log)
//   F10 opens the dev menu: force attack, add cash, test spawns, cleanup, save
//   An entity counter at the top of the screen: mod peds/vehicles against the budget, plus the pause-gate state.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using LemonUI;
using LemonUI.Menus;
using FrontsAndRivals.Attacks;
using FrontsAndRivals.Core;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Dev
{
    public sealed class DevTools
    {
        public const Keys CopyPositionKey = Keys.F9;
        public const Keys MenuKey = Keys.F10;
        private const int ModelTimeoutMs = 5000;

        private sealed class PendingSpawn
        {
            public Model Model;
            public bool IsVehicle;
            public int StartedAt;
        }

        private readonly Registry _registry;
        private readonly Director _director;
        private readonly Action _saveNow;
        private readonly Func<PauseReason> _gate;

        private readonly ObjectPool _pool = new ObjectPool();
        private readonly NativeMenu _menu;
        private readonly GTA.UI.TextElement _counter;
        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
        private bool _showCounter = true;

        /// <summary>Other dev panels (ScoutMenu) hang off this pool and menu.</summary>
        public ObjectPool Pool { get { return _pool; } }
        public NativeMenu Menu { get { return _menu; } }
        /// <summary>Extra text for the counter line, e.g. streaming stats.</summary>
        public Func<string> ExtraStatus;

        public DevTools(Registry registry, Director director, Action saveNow, Func<PauseReason> gate)
        {
            _registry = registry;
            _director = director;
            _saveNow = saveNow;
            _gate = gate;

            _counter = new GTA.UI.TextElement("", new PointF(640f, 4f), 0.3f, Color.White, GTA.UI.Font.ChaletLondon, GTA.UI.Alignment.Center, true, true);

            _menu = new NativeMenu("Fronts & Rivals", "Dev tools", "Only shown with DevTools=true.");
            _pool.Add(_menu);

            Add("Copy position (F9)", "Copies position and heading as a fronts.json snippet.", CopyPosition);
            Add("Force attack", "Plans an attack on one of your fronts now, with a 30-second warning.", ForceAttack);
            Add("Add $10,000", "Adds cash to the character you're playing.", () => AddCash(10000));
            Add("Add $100,000", "Adds cash to the character you're playing.", () => AddCash(100000));
            Add("Spawn test guard", "An FR_GUARDS ped in front of you. Reload scripts to check cleanup.", () => QueueSpawn("a_m_y_business_01", false));
            Add("Spawn test car", "A car with an FR plate in front of you. Reload scripts to check cleanup.", () => QueueSpawn("sultan", true));
            Add("Delete all mod entities", "Deletes everything the registry tracks.", DeleteAll);
            Add("Run orphan sweep", "Deletes any FR_ ped or FR-plated vehicle in the world.", Sweep);
            Add("Save now", "Writes empire.json.", () => { _saveNow(); Notify("Saved."); });

            var counter = new NativeCheckboxItem("Entity counter", "Mod peds and vehicles against the 28 / 8 budget, and the pause gate.", true);
            counter.CheckboxChanged += (s, e) => _showCounter = counter.Checked;
            _menu.Add(counter);
        }

        private void Add(string title, string description, Action action)
        {
            var item = new NativeItem(title, description);
            item.Activated += (s, e) =>
            {
                try { action(); }
                catch (Exception ex) { Log.Error("Dev " + title, ex); Notify("Failed, see log."); }
            };
            _menu.Add(item);
        }

        public static void Notify(string text)
        {
            GTA.UI.Notification.Show("Fronts & Rivals dev: " + text);
        }

        private bool GateOpen(string action)
        {
            PauseReason r = _gate();
            if (r == PauseReason.None) return true;
            Notify(action + " blocked: paused (" + r + ").");
            return false;
        }

        /// <summary>Key handler (Script.KeyDown).</summary>
        public void OnKeyDown(Keys key)
        {
            if (key == CopyPositionKey) CopyPosition();
            else if (key == MenuKey) _menu.Visible = !_menu.Visible;
        }

        /// <summary>Every tick, also while paused, so the menu never freezes.</summary>
        public void Draw(PauseReason gate)
        {
            _pool.Process();
            if (_showCounter)
            {
                _counter.Draw();
            }
        }

        /// <summary>Every 0.5 s, also while paused: refreshes the counter text.</summary>
        public void RefreshCounter(PauseReason gate)
        {
            _counter.Caption = "FR dev   peds " + _registry.PedCount + "/" + Registry.MaxPeds
                             + "   vehicles " + _registry.VehicleCount + "/" + Registry.MaxVehicles
                             + "   gate " + (gate == PauseReason.None ? "open" : gate.ToString())
                             + (ExtraStatus != null ? "   " + ExtraStatus() : "");
        }

        /// <summary>From the Streamer job (gate open): finishes test spawns whose model has loaded.</summary>
        public void UpdateSpawns()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                PendingSpawn p = _pending[i];
                if (!p.Model.IsLoaded)
                {
                    if (Game.GameTime - p.StartedAt > ModelTimeoutMs)
                    {
                        Log.Write("Dev spawn: model " + p.Model.Hash + " did not load in 5 s, giving up");
                        p.Model.MarkAsNoLongerNeeded();
                        _pending.RemoveAt(i);
                    }
                    continue;
                }
                _pending.RemoveAt(i);
                try { Spawn(p); }
                finally { p.Model.MarkAsNoLongerNeeded(); }
            }
        }

        /// <summary>Drops queued spawns, e.g. when a mission starts.</summary>
        public void CancelPending()
        {
            foreach (PendingSpawn p in _pending) p.Model.MarkAsNoLongerNeeded();
            _pending.Clear();
        }

        private void QueueSpawn(string modelName, bool vehicle)
        {
            if (!GateOpen("Spawn")) return;
            if (vehicle ? !_registry.CanSpawnVehicle : !_registry.CanSpawnPed) { Notify("Budget full."); return; }
            var model = new Model(modelName);
            if (!model.IsValid || (vehicle ? !model.IsVehicle : !model.IsPed)) { Notify("Bad model " + modelName); return; }
            model.Request();
            _pending.Add(new PendingSpawn { Model = model, IsVehicle = vehicle, StartedAt = Game.GameTime });
        }

        private void Spawn(PendingSpawn p)
        {
            Ped player = Game.Player.Character;
            if (p.IsVehicle)
            {
                if (!_registry.CanSpawnVehicle) { Notify("Budget full."); return; }
                Vector3 pos = player.Position + player.ForwardVector * 6f;
                Vehicle v = World.CreateVehicle(p.Model, pos, player.Heading + 90f);
                if (v == null) { Notify("Car spawn failed."); return; }
                _registry.Track(v, Markers.MakePlate(99, Markers.OwnerDev));
                Log.Write("Dev spawn: test car " + v.Handle);
            }
            else
            {
                if (!_registry.CanSpawnPed) { Notify("Budget full."); return; }
                Vector3 pos = player.Position + player.ForwardVector * 3f;
                Ped ped = World.CreatePed(p.Model, pos, player.Heading + 180f);
                if (ped == null) { Notify("Ped spawn failed."); return; }
                ped.RelationshipGroup = Factions.Guards;
                ped.BlockPermanentEvents = true;
                _registry.Track(ped);
                Log.Write("Dev spawn: test guard " + ped.Handle);
            }
        }

        private void CopyPosition()
        {
            Ped player = Game.Player.Character;
            Vector3 pos = player.Position;
            string json = DevFormat.PositionJson(pos.X, pos.Y, pos.Z, player.Heading);
            Log.Write("F9 position: " + json);
            SetClipboard(json);
            Notify("Copied " + json);
        }

        /// <summary>The clipboard needs an STA thread; the script thread isn't one. Fire and forget, never blocks the game.</summary>
        private static void SetClipboard(string text)
        {
            var t = new Thread(() =>
            {
                try { Clipboard.SetText(text); }
                catch (Exception ex) { Log.Write("Clipboard failed (position is still in the log): " + ex.Message); }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }

        private void ForceAttack()
        {
            _director.RequestForcedAttack();
            Notify("Forced attack requested. Warning in a moment; the attack starts 30 s later.");
        }

        private void AddCash(int amount)
        {
            if (!GateOpen("Add cash")) return;
            Game.Player.Money += amount;
            Log.Write("Dev: added $" + amount);
            Notify("Added $" + amount.ToString("N0") + ".");
        }

        private void DeleteAll()
        {
            CancelPending();
            int n = _registry.DeleteAll();
            Notify("Deleted " + n + " entities.");
        }

        private void Sweep()
        {
            if (!GateOpen("Sweep")) return;
            int n = Registry.SweepOrphans();
            _registry.Prune();
            Notify("Sweep removed " + n + ".");
        }
    }
}
