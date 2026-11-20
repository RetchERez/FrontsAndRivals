// Fronts & Rivals: the one SHVDN script that drives everything.
// Milestone 1: pause gate, scheduler, log, crash-safe save, factions, registry + orphan sweep, dev tools.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using GTA;
using FrontsAndRivals.Attacks;
using FrontsAndRivals.Data;
using FrontsAndRivals.Dev;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Core
{
    public sealed class Main : Script
    {
        public const string ModVersion = "0.1.0-m1";
        private const int SaveEveryMs = 120000; // one in-game hour = 2 real minutes

        private readonly Config _config;
        private readonly Scheduler _scheduler;
        private readonly Registry _registry = new Registry();
        private readonly Director _director = new Director();
        private readonly SaveService _saves;
        private readonly DevTools _dev;
        private readonly FailureWindow _tickFailures = new FailureWindow();

        private EmpireState _state;
        private Rng _rng;
        private long _activeBase; // free-roam time from earlier sessions
        private PauseReason _gate = PauseReason.PlayerNotPlaying;
        private bool _gateKnown;
        private bool _started;
        private bool _shutDown;

        public Main()
        {
            string dataDir = Path.Combine(BaseDirectory, "FrontsAndRivals");
            Log.Init(dataDir);
            Log.Write("==== Fronts & Rivals " + ModVersion + " loading");
            _config = Config.Load(Path.Combine(BaseDirectory, "FrontsAndRivals.ini"), Log.Write);

            if (!_config.Enabled)
            {
                Log.Write("Enabled=false in FrontsAndRivals.ini. Doing nothing.");
                _shutDown = true;
                return;
            }

            Interval = 0; // every frame; the Scheduler spreads the real work out
            _saves = new SaveService(dataDir, Log.Write);
            _scheduler = new Scheduler(OnSystemFailure);

            if (_config.DevTools)
            {
                _dev = new DevTools(_registry, _director, SaveNow, () => _gate);
                KeyDown += OnKeyDown;
                Log.Write("DevTools on: F9 copies position, F10 opens the dev menu");
            }

            // Each system at its own rate (design doc: Background systems).
            if (_dev != null)
            {
                _scheduler.Add("DevUI", "dev tools", 0, () => _dev.Draw(_gate), runWhilePaused: true);
                _scheduler.Add("DevCounter", "dev tools", 500, () => _dev.RefreshCounter(_gate), runWhilePaused: true);
            }
            _scheduler.Add("Streamer", "streaming", 500, StreamerTick);
            _scheduler.Add("Director", "attacks", 60000, () => _director.Update());
            _scheduler.Add("Saver", "saving", SaveEveryMs, SaveNow);

            Tick += OnTick;
            Aborted += OnAborted; // script reload or game exit
        }

        private void LogVersions()
        {
            try
            {
                // SHVDN's GameVersion enum doesn't know Enhanced builds, so log the exe's own version too.
                ProcessModule exe = Process.GetCurrentProcess().MainModule;
                Log.Write("Game: " + exe.ModuleName + " " + exe.FileVersionInfo.FileVersion + " (SHVDN enum: " + Game.Version + ")");
            }
            catch (Exception ex) { Log.Write("Game version: unknown (" + ex.Message + ")"); }
            try
            {
                var shvdn = typeof(Script).Assembly;
                string file = FileVersionInfo.GetVersionInfo(shvdn.Location).FileVersion;
                Log.Write("SHVDN API " + shvdn.GetName().Version + ", file version " + file);
            }
            catch (Exception ex) { Log.Write("SHVDN version: unknown (" + ex.Message + ")"); }
            Log.Write("Scripts folder: " + BaseDirectory);
        }

        /// <summary>First tick with the gate open. Each step guarded so one failure doesn't stop the others.</summary>
        private void Start()
        {
            _started = true;
            LogVersions();
            try { Factions.Setup(); } catch (Exception ex) { Log.Error("Factions.Setup", ex); }
            try { Registry.SweepOrphans(); } catch (Exception ex) { Log.Error("SweepOrphans", ex); }

            SaveService.LoadResult loaded;
            try { loaded = _saves.Load(); }
            catch (Exception ex)
            {
                Log.Error("Load", ex);
                loaded = new SaveService.LoadResult { State = SaveService.NewEmpire(), Source = "new" };
            }
            _state = loaded.State;
            _rng = new Rng(_state.RngState);
            _activeBase = _state.ActiveMs - _scheduler.ActiveMs;
            Log.Write("Empire loaded from " + loaded.Source + " (save #" + _state.SaveCount + ", schema " + _state.SchemaVersion + ")");
            if (loaded.Source == "new") SaveNow(); // create the file right away
            if (_saves.Locked) GTA.UI.Notification.Show("Fronts & Rivals: your save is from a newer version, so saving is off. See log.");
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_shutDown) return;
            try
            {
                PauseReason gate = PauseGate.Check();
                if (gate == PauseReason.Online) { ShutDownForOnline(); return; }

                if (!_gateKnown || gate != _gate)
                {
                    Log.Write("Pause gate: " + (_gateKnown ? _gate.ToString() : "start") + " -> " + gate);
                    if (PauseGate.ClearsWorld(gate)) ClearWorld("gate " + gate);
                    _gate = gate;
                    _gateKnown = true;
                }

                bool open = gate == PauseReason.None;
                if (open && !_started) Start();
                _scheduler.Update(Game.GameTime, open);
            }
            catch (Exception ex)
            {
                Log.Error("OnTick", ex);
                if (_tickFailures.Record(Game.GameTime))
                {
                    Log.Write("OnTick failed 3 times in 60 s. Fronts & Rivals is switching itself off.");
                    ClearWorld("tick failures");
                    _shutDown = true;
                    GTA.UI.Notification.Show("Fronts & Rivals paused, see log");
                }
            }
        }

        private void StreamerTick()
        {
            _registry.Prune();
            if (_dev != null) _dev.UpdateSpawns();
        }

        private void ClearWorld(string why)
        {
            try
            {
                if (_dev != null) _dev.CancelPending();
                int n = _registry.DeleteAll();
                if (n > 0) Log.Write("Cleared " + n + " mod entities (" + why + ")");
            }
            catch (Exception ex) { Log.Error("ClearWorld", ex); }
        }

        private void ShutDownForOnline()
        {
            Log.Write("NETWORK_IS_SESSION_STARTED is true. Fronts & Rivals is switching itself off (single-player only).");
            ClearWorld("online");
            SaveNow();
            _shutDown = true;
        }

        private void SaveNow()
        {
            if (_state == null) return;
            if (_rng != null) _state.RngState = _rng.State;
            _state.ActiveMs = _activeBase + _scheduler.ActiveMs;
            _saves.Save(_state);
        }

        private void OnSystemFailure(string job, string display, Exception ex, bool disabledNow)
        {
            Log.Error(job, ex);
            if (disabledNow)
            {
                Log.Write(job + " failed 3 times in 60 s and is switched off until the next reload.");
                GTA.UI.Notification.Show("Fronts & Rivals: " + display + " paused, see log");
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (_shutDown || _dev == null) return;
            try { _dev.OnKeyDown(e.KeyCode); }
            catch (Exception ex) { Log.Error("DevTools key", ex); }
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Cleanup first: nothing may be left behind for the game to deal with.
            ClearWorld("script stopped");
            try { SaveNow(); } catch (Exception ex) { Log.Error("Save on abort", ex); }
            Log.Write("==== Fronts & Rivals stopped");
        }
    }
}
