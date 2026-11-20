// Fronts & Rivals: the one SHVDN script that drives everything.
// M1: pause gate, scheduler, log, crash-safe save, factions, registry + orphan sweep, dev tools.
// M2: content (fronts.json), fronts and guards in the save, the Streamer, front blips, in-game scouting.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using GTA;
using FrontsAndRivals.Attacks;
using FrontsAndRivals.Data;
using FrontsAndRivals.Dev;
using FrontsAndRivals.Rules;
using FrontsAndRivals.Spawn;
using FrontsAndRivals.UI;

namespace FrontsAndRivals.Core
{
    public sealed class Main : Script
    {
        public const string ModVersion = "0.2.0-m2";
        private const int SaveEveryMs = 120000;          // one in-game hour = 2 real minutes
        private const long InjuryRecoveryMs = 96L * 60000; // 2 in-game days = 96 real minutes of free roam

        private readonly Config _config;
        private readonly Scheduler _scheduler;
        private readonly Registry _registry = new Registry();
        private readonly Director _director = new Director();
        private readonly Notifier _notifier = new Notifier();
        private readonly FrontBlips _blips = new FrontBlips();
        private readonly SaveService _saves;
        private readonly ContentDb _content;
        private readonly Streamer _streamer;
        private readonly DevTools _dev;
        private readonly FailureWindow _tickFailures = new FailureWindow();

        private EmpireState _state;
        private Empire _empire;
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
            _content = new ContentDb(dataDir, Log.Write);
            _content.Load(); // files only, no game calls
            _streamer = new Streamer(_registry, _content, ActiveNow, OnGuardDown);

            if (_config.DevTools)
            {
                _dev = new DevTools(_registry, _director, SaveNow, () => _gate);
                new ScoutMenu(_dev, _content, () => _empire, () => _rng, WorldChanged, () => _gate);
                _dev.ExtraStatus = () => "streams in " + _streamer.StreamIns + " / out " + _streamer.StreamOuts
                                       + "   leftovers " + _streamer.Leftovers;
                KeyDown += OnKeyDown;
                Log.Write("DevTools on: F9 copies position, F10 opens the dev menu");
            }

            // Each system at its own rate (design doc: Background systems).
            _scheduler.Add("Notifier", "notifications", 0, () => _notifier.Update(), runWhilePaused: true);
            if (_dev != null)
            {
                _scheduler.Add("DevUI", "dev tools", 0, () => _dev.Draw(_gate), runWhilePaused: true);
                _scheduler.Add("DevCounter", "dev tools", 500, () => _dev.RefreshCounter(_gate), runWhilePaused: true);
            }
            _scheduler.Add("Streamer", "guards", 500, StreamerTick);
            _scheduler.Add("Blips", "map blips", 2000, () => _blips.Refresh(_empire, _content));
            _scheduler.Add("World", "guard recovery", 60000, WorldTick);
            _scheduler.Add("Director", "attacks", 60000, () => _director.Update());
            _scheduler.Add("Saver", "saving", SaveEveryMs, SaveNow);

            Tick += OnTick;
            Aborted += OnAborted; // script reload or game exit
        }

        private long ActiveNow() { return _activeBase + _scheduler.ActiveMs; }

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

            _empire = new Empire(_state, _content);
            _streamer.Attach(_empire);
            try { _blips.Refresh(_empire, _content); } catch (Exception ex) { Log.Error("Blips", ex); }
            Log.Write("Empire: " + _empire.LivingCount() + " guards, " + _state.Fronts.Count + " fronts tracked");

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
            _streamer.Update();
            if (_dev != null) _dev.UpdateSpawns();
        }

        private void WorldTick()
        {
            if (_empire == null) return;
            int back = _empire.Recover(ActiveNow());
            if (back > 0) { Log.Write(back + " injured guard(s) back on duty"); SaveNow(); }
        }

        /// <summary>A streamed guard died in the world. Permanent death unless PermanentDeath=false.</summary>
        private void OnGuardDown(GuardState g, Ped body, FrontDef front)
        {
            bool permanent = _config.PermanentDeath;
            _empire.GuardDown(g, "the " + front.Name, permanent, ActiveNow(), InjuryRecoveryMs);
            Log.Write("Guard " + g.Id + " " + g.FullName + (permanent ? " died" : " was injured") + " at " + front.Id);
            string text = permanent
                ? g.First + " \"" + g.Nick + "\" didn't make it out of the " + front.Name + "."
                : g.First + " \"" + g.Nick + "\" got hurt at the " + front.Name + ". Back in two days.";
            _notifier.PostWithMugshot(body, front.Name, permanent ? "Guard down" : "Guard injured", text);
            SaveNow();
        }

        /// <summary>Content or ownership changed (scouting, dev tools): re-sync the streamer and blips, then save.</summary>
        private void WorldChanged()
        {
            if (_empire == null) return;
            _empire.EnsureFronts();
            _streamer.DespawnAll(); // respawn at the new spots
            _streamer.Rebuild();
            _blips.Refresh(_empire, _content);
            SaveNow();
        }

        private void ClearWorld(string why)
        {
            try
            {
                if (_dev != null) _dev.CancelPending();
                int n = _registry.DeleteAll();
                if (_streamer != null) _streamer.Reset();
                if (n > 0) Log.Write("Cleared " + n + " mod entities (" + why + ")");
            }
            catch (Exception ex) { Log.Error("ClearWorld", ex); }
        }

        private void ShutDownForOnline()
        {
            Log.Write("NETWORK_IS_SESSION_STARTED is true. Fronts & Rivals is switching itself off (single-player only).");
            ClearWorld("online");
            _blips.DeleteAll();
            SaveNow();
            _shutDown = true;
        }

        private void SaveNow()
        {
            if (_state == null) return;
            if (_rng != null) _state.RngState = _rng.State;
            _state.ActiveMs = ActiveNow();
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
            try { _blips.DeleteAll(); } catch (Exception ex) { Log.Error("Blips on abort", ex); }
            try { SaveNow(); } catch (Exception ex) { Log.Error("Save on abort", ex); }
            Log.Write("==== Fronts & Rivals stopped");
        }
    }
}
