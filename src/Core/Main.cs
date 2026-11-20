// Fronts & Rivals - the one SHVDN script that drives everything.
using System;
using GTA;
using FrontsAndRivals.Data;
using FrontsAndRivals.Spawn;
using FrontsAndRivals.UI;

namespace FrontsAndRivals.Core
{
    public sealed class Main : Script
    {
        private readonly Scheduler _scheduler = new Scheduler();
        private readonly Registry _registry = new Registry();
        private readonly GuardSpawner _spawner;
        private readonly OfficeMenu _menu = new OfficeMenu();
        private readonly Notifier _notifier = new Notifier();
        private readonly PhoneContact _phone;
        private EmpireState _state;
        private bool _started;

        public Main()
        {
            Interval = 0; // run every frame; the Scheduler spreads the real work out
            _spawner = new GuardSpawner(_registry);
            _phone = new PhoneContact(_menu);

            Tick += OnTick;
            Aborted += OnAborted; // script reload or game exit
        }

        private void Start()
        {
            Factions.Setup();
            Registry.SweepOrphans();
            _state = SaveService.Load();

            // Each system at its own rate (design doc: Background systems).
            _scheduler.Add("Streamer", 500, () => { _registry.Prune(); _spawner.Update(); });
            _scheduler.Add("Director", 60000, () => { /* attack planning goes here */ });
            _scheduler.Add("Saver", 120000, () => SaveService.Save(_state)); // one in-game hour = 2 real minutes
            _started = true;
        }

        private void OnTick(object sender, EventArgs e)
        {
            // UI always runs so menus and banners never freeze.
            _menu.Process();
            _phone.Update();
            _notifier.Update();

            if (!PauseGate.CanAct()) return;
            if (!_started) Start();
            _scheduler.Update();
        }

        private void OnAborted(object sender, EventArgs e)
        {
            try { if (_state != null) SaveService.Save(_state); } catch (Exception ex) { Log.Write("Save on abort failed: " + ex); }
            _registry.DeleteAll();
        }
    }
}
