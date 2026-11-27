// Fronts & Rivals: the one SHVDN script that drives everything.
// M1: pause gate, scheduler, log, crash-safe save, factions, registry + orphan sweep, dev tools.
// M2: content (fronts.json), fronts and guards in the save, the Streamer, front blips, in-game scouting.
// M3+: the Office, hiring, payouts; v1.0: crews, attacks, live defenses, taking fronts, the nemesis loop.
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
        public const string ModVersion = "3.1.0";
        private const int SaveEveryMs = 120000;          // one in-game hour = 2 real minutes
        private const long InjuryRecoveryMs = 96L * 60000; // 2 in-game days = 96 real minutes of free roam

        private readonly Config _config;
        private readonly Scheduler _scheduler;
        private readonly Registry _registry = new Registry();
        private readonly Director _director;
        private readonly LiveDefense _live;
        private readonly Banners _banners = new Banners();
        private readonly Notifier _notifier = new Notifier();
        private readonly FrontBlips _blips = new FrontBlips();
        private readonly SaveService _saves;
        private readonly ContentDb _content;
        private readonly Streamer _streamer;
        private Workers _workers;
        private Escorts _escorts;
        private Hangouts _hangouts;
        private readonly PurchaseShot _shot = new PurchaseShot();
        private readonly DevTools _dev;
        private readonly AutoPostPlacer _placer = new AutoPostPlacer();
        private readonly OfficeMenu _office;
        private readonly object _phone; // PhoneContact, held as object so a missing iFruitAddon2.dll can't break Main
        private readonly Keys _officeKey = Keys.None;
        private readonly AutoScout _autoScout;
        private readonly AutoTest _autoTest;
        private readonly FailureWindow _tickFailures = new FailureWindow();

        private EmpireState _state;
        private Empire _empire;
        private Rng _rng;
        private long _activeBase; // free-roam time from earlier sessions
        private PauseReason _gate = PauseReason.PlayerNotPlaying;
        private bool _franklinDone; // a mission just ended while playing Franklin
        private bool _gateKnown;
        private bool _started;
        private bool _shutDown;

        private string dataDir() { return Path.Combine(BaseDirectory, "FrontsAndRivals"); }
        private string _saveDir;

        /// <summary>Where the empire is saved: Documents\Fronts & Rivals, outside the game folder, so a reinstall, a mod
        /// manager redeploying the scripts folder or a game-folder cleanup can never wipe it. An empire saved by an older
        /// version in scripts\FrontsAndRivals is copied over the first time. If Documents can't be written, the old folder
        /// is used as before.</summary>
        private static string SaveFolder(string oldDir)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs)) return oldDir;
                string dir = Path.Combine(docs, "Fronts & Rivals");
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, "write-check.tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                if (!File.Exists(Path.Combine(dir, "empire.json")) && File.Exists(Path.Combine(oldDir, "empire.json")))
                {
                    int moved = 0;
                    foreach (string f in Directory.GetFiles(oldDir, "empire*"))
                    {
                        File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), false);
                        moved++;
                    }
                    Log.Write("Saves: copied your empire (" + moved + " files) from " + oldDir + " to " + dir);
                }
                Log.Write("Saves: " + dir);
                return dir;
            }
            catch (Exception ex)
            {
                Log.Error("Save folder in Documents (using " + oldDir + " instead)", ex);
                return oldDir;
            }
        }

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

            // LemonUI draws every menu. Without it the mod can't run, so say so on screen instead of failing silently.
            string lemon = Path.Combine(BaseDirectory, "LemonUI.SHVDN3.dll");
            if (!File.Exists(lemon))
            {
                Log.Write("LemonUI.SHVDN3.dll is missing from the scripts folder. Fronts & Rivals needs LemonUI (SHVDN3 build) and is switching off.");
                _shutDown = true;
                bool told = false;
                Tick += (s, a) => { if (!told && !Game.IsLoading) { told = true; GTA.UI.Notification.Show("~r~Fronts & Rivals~s~ needs LemonUI (the SHVDN3 build) in your scripts folder. See the readme."); } };
                return;
            }
            try
            {
                Version lv = System.Reflection.AssemblyName.GetAssemblyName(lemon).Version;
                if (lv < new Version(2, 2)) Log.Write("WARNING: LemonUI " + lv + " is older than 2.2, the version Fronts & Rivals is built for. Menus may misbehave; update LemonUI.");
            }
            catch (Exception ex) { Log.Error("LemonUI version", ex); }

            Interval = 0; // every frame; the Scheduler spreads the real work out
            _saveDir = SaveFolder(dataDir);
            _saves = new SaveService(_saveDir, Log.Write);
            _scheduler = new Scheduler(OnSystemFailure);
            _content = new ContentDb(dataDir, Log.Write);
            _content.Load(); // files only, no game calls
            Nemesis.Names = _content.Names; // new crew lieutenants draw from the same name pool
            _streamer = new Streamer(_registry, _content, ActiveNow, OnGuardDown);
            _live = new LiveDefense(_registry, _streamer);
            _live.Heat = () => _state != null ? _state.Heat : 0;
            Wardrobe.Registry = _registry;
            Wardrobe.PlayerColour = () => _state != null && _state.CrewColor != null ? _state.CrewColor : CrewColors.DefaultPlayer;
            Wardrobe.PlayerStyle = () => _state != null && _state.CrewStyle != null ? _state.CrewStyle : CrewColors.DefaultStyle;
            try { Wardrobe.Data = OutfitData.Load(System.IO.Path.Combine(_content.ContentDir, "outfits.json")); Log.Write("Outfits: " + (Wardrobe.Enabled ? "loaded" : "missing, using the old models")); }
            catch (Exception ex) { Log.Error("Outfits", ex); }
            _live.Grudge = id => { CrewState c = _state != null ? Nemesis.Crew(_state, id) : null; return c != null ? c.Grudge : 0; };
            _workers = new Workers(_registry, _content, () => _empire);
            _escorts = new Escorts(_registry, () => _empire, OnBodyguardDown);
            _hangouts = new Hangouts(_registry, _content, () => _empire);
            _hangouts.PoliceOn = () => _config.PoliceRespond;
            _director = new Director(_content, _config, () => _empire, () => _rng, ActiveNow, () => _scheduler.ActiveMs,
                                     EmpireChanged, _banners, _notifier, _streamer, _live);
            _director.OnTaken = def => // set after the Director exists
            {
                try { if (_config.PurchaseShot) _shot.Play(def); }
                catch (Exception ex) { _shot.End(); Log.Error("Takeover shot", ex); }
            };
            _notifier.UseSettings(NoteSettingsNow);
            Tips.State = () => _config.Tips ? _state : null;
            Sounds.Enabled = () => _config.SoundEffects && (_state == null || !_state.SoundOff);
            _streamer.AlertOnFootRange = _config.RivalAlertRange;
            _office = new OfficeMenu(_content, _config, () => _empire, () => _rng, EmpireChanged, SaveNow);
            _office.StartOver = StartOver;
            _office.Messages = () => _notifier.Log;
            _notifier.Store = () => { if (_state == null) return null; if (_state.Messages == null) _state.Messages = new System.Collections.Generic.List<MessageEntry>(); return _state.Messages; };
            _notifier.Day = () => _state != null ? _state.Day : 0;
            _office.ResetCity = () =>
            {
                if (_empire == null) return;
                ClearWorld("reset the city");
                _director.Reset();
                _notifier.Clear();
                _empire.ResetCity(_rng, ActiveNow());
                EmpireChanged();
                Log.Write("City reset, run " + (_state.CityResets + 1));
                _banners.Show("LOS SANTOS IS UP FOR GRABS", "The crews are back at full strength. Your people are waiting in the reserve.");
            };
            _office.StartRaid = def =>
            {
                if (_empire == null || !_empire.StartRaid(def, ActiveNow(), GTA.World.CurrentTimeOfDay.Hours)) return false;
                Log.Write("Raid sent at " + def.Id);
                SaveNow();
                return true;
            };

            try { _phone = PhoneHost.TryCreate(() => _office.OpenOffice()); Log.Write("Phone contact \"The Office\" added"); }
            catch (Exception ex) { _phone = null; Log.Write("Phone contact unavailable (is iFruitAddon2 installed?): " + ex.GetType().Name + ": " + ex.Message); }

            Keys key;
            // Older INIs: F11 is the game's helmet visor key and F4 opens the Script Hook V .NET console, so both move to O.
            if (string.Equals(_config.OfficeHotkey, "F11", StringComparison.OrdinalIgnoreCase) || string.Equals(_config.OfficeHotkey, "F4", StringComparison.OrdinalIgnoreCase))
            {
                Log.Write("Config: OfficeHotkey " + _config.OfficeHotkey + " clashes with the game or Script Hook V .NET, using O instead (change it in FrontsAndRivals.ini)");
                _config.OfficeHotkey = "O";
            }
            // 1.4's INI had the old fight range (35 m); 1.5 made it the warning range, 25 m.
            if (_config.RivalAlertRange == 35) { _config.RivalAlertRange = 25; _streamer.AlertOnFootRange = 25; Log.Write("Config: AlertRange 35 is the old fight range, using 25 (the warning range)"); }
            if (!string.IsNullOrEmpty(_config.OfficeHotkey) && !_config.OfficeHotkey.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse(_config.OfficeHotkey, true, out key)) { _officeKey = key; Log.Write("Office hotkey: " + key); }
                else Log.Write("Config: OfficeHotkey '" + _config.OfficeHotkey + "' is not a key name, no hotkey");
            }
            KeyDown += OnKeyDown;
            Tips.OfficeKey = () => _officeKey != Keys.None ? _officeKey.ToString() : null;
            // Always a way in: with no phone contact (iFruitAddon2 missing), no hotkey and the controller hold switched off,
            // the Office would be unreachable, so the hold comes back on.
            if (_phone == null && _officeKey == Keys.None && !_config.OfficeButton)
            {
                _config.OfficeButton = true;
                Log.Write("No phone contact and no OfficeHotkey: OfficeButton switched on so the Office can still be opened (RB + D-pad Left)");
            }

            if (_config.DevTools)
            {
                _dev = new DevTools(_registry, _director, SaveNow, () => _gate);
                _autoScout = new AutoScout(_content, _placer, WorldChanged);
                _office.OpenDevTools = _dev.OpenSoon;
                new ScoutMenu(_dev, _content, () => _empire, () => _rng, WorldChanged, () => _gate, _placer, _autoScout);
                _autoTest = new AutoTest(new AutoTest.Host
                {
                    Empire = () => _empire, Content = _content, Registry = _registry, Director = _director, Now = ActiveNow,
                    Gate = () => _gate, SaveNow = SaveNow, SavePath = () => _saves.MainPath,
                    Snapshot = TestSnapshot, Restore = TestRestore
                });
                var mapColours = new BlipColourCheck();
                _dev.AddAction("Map Colour Check", "Puts every candidate green, orange, blue and red map colour north of you, each named with its number, to pick the exact match for each crew.", mapColours.Show);
                _dev.AddAction("Clear Map Colours", "Removes the map colour check.", mapColours.Clear);
                var lineup = new WardrobeLineup(_registry);
                _dev.AddAction("Wardrobe Lineup", "Four rows in front of you: Dead Kings, Hellfire MC, Vinewood Elite and your crew, each with three everyday looks, a masked attacker and a leader. Stand somewhere open.", lineup.Show);
                _dev.AddAction("Clear Lineup", "Removes the wardrobe lineup.", lineup.Clear);
                _dev.AddAction("Auto-Test", "Plays through taking a front, staffing it, a live defense, an attack while away, a raid, midnight and a save on its own, about 5 minutes, then puts your empire, cash, place and time back. Results in autotest-report.txt.", _autoTest.Start);
                _dev.ExtraStatus = () => "streams in " + _streamer.StreamIns + " / out " + _streamer.StreamOuts
                                       + "   leftovers " + _streamer.Leftovers;
                Log.Write("DevTools on: F9 copies position, F10 opens the dev menu");
            }

            // Each system at its own rate (design doc: Background systems).
            _scheduler.Add("Notifier", "notifications", 0, () => _notifier.Update(), runWhilePaused: true);
            _scheduler.Add("Office", "the Office menu", 0, () => _office.Process(), runWhilePaused: true);
            if (_phone != null) _scheduler.Add("Phone", "the phone contact", 0, () => PhoneHost.Update(_phone), runWhilePaused: true);
            _scheduler.Add("Banners", "banners", 0, () => { _banners.Process(); _live.Draw(); }, runWhilePaused: true);
            _scheduler.Add("Prompts", "prompts", 0, PromptsTick);
            _scheduler.Add("HoldBar", "attack timer", 0, () => _director.DrawHold());
            _scheduler.Add("Shot", "purchase shot", 0, () => { try { _shot.Update(_gate == PauseReason.None); } catch { _shot.End(); throw; } }, runWhilePaused: true); // a failure always hands the camera back
            _scheduler.Add("Waypoint", "map waypoint", 1000, WaypointTick);
            _scheduler.Add("Clock", "income and recruits", 2000, ClockTick);
            if (_dev != null)
            {
                _scheduler.Add("DevUI", "dev tools", 0, () => _dev.Draw(_gate), runWhilePaused: true);
                _scheduler.Add("DevCounter", "dev tools", 500, () => _dev.RefreshCounter(_gate), runWhilePaused: true);
            }
            _scheduler.Add("Streamer", "guards", 500, StreamerTick);
            _scheduler.Add("AutoPosts", "guard posts", 1000, AutoPostsTick);
            if (_config.FrontStaff) _scheduler.Add("Workers", "front staff", 1000, () => _workers.Update());
            _scheduler.Add("Escorts", "bodyguards", 500, () => _escorts.Update());
            if (_config.Hangouts) _scheduler.Add("Hangouts", "crew hangouts", 1000, () => _hangouts.Update());
            _scheduler.Add("Blips", "map blips", 2000, () => _blips.Refresh(_empire, _content));
            _scheduler.Add("World", "guard recovery", 60000, WorldTick);
            _scheduler.Add("Director", "attacks", 2000, () => _director.Update());
            _scheduler.Add("Saver", "saving", SaveEveryMs, SaveNow);

            Tick += OnTick;
            Aborted += OnAborted; // script reload or game exit
        }

        private long ActiveNow() { return _activeBase + _scheduler.ActiveMs; }

        /// <summary>Every tick with the gate open: the claim prompt, and the controller's Office button.</summary>
        private void PromptsTick()
        {
            _director.UpdateFrame();
            Tips.Update();
            try { WatchFx.Frame(); } catch (Exception ex) { Log.Error("Shot watch", ex); }
            if (!_config.OfficeButton) return;
            // Controller: RB + D-pad Left toggles the Office (the combo Menyoo uses). Back/View is left alone:
            // holding it opens the game's own Interaction Menu, and disabling the control doesn't stop that.
            // Only on a gamepad, so keyboard arrows and E never open it by accident. Either order works; holding
            // D-pad Left first keeps RB from ducking you into cover (RB is also the cover button).
            if (Game.LastInputMethod != InputMethod.GamePad) return;
            bool left = Game.IsControlPressed(GTA.Control.FrontendLeft);
            bool rb = Game.IsControlPressed(GTA.Control.FrontendRb);
            if (left) Game.DisableControlThisFrame(GTA.Control.Cover);
            if (left && rb && (Game.IsControlJustPressed(GTA.Control.FrontendLeft) || Game.IsControlJustPressed(GTA.Control.FrontendRb)))
            {
                _office.Toggle();
            }
        }

        /// <summary>Notification settings: the INI, with the Office's Notifications choice on top.</summary>
        private NoteSettings NoteSettingsNow()
        {
            var n = new NoteSettings
            {
                Level = _config.NotifyLevel,
                MinSecondsBetween = _config.MinSecondsBetweenTexts,
                Taunts = _config.Taunts,
                StreetWord = _config.StreetWord,
                MidnightReport = _config.MidnightReport
            };
            NoteLevel level;
            if (_state != null && !string.IsNullOrEmpty(_state.NotifyLevel) && Enum.TryParse(_state.NotifyLevel, true, out level)) n.Level = level;
            return n;
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
            if (loaded.Recovered)
                GTA.UI.Notification.Show(loaded.Source == "new"
                    ? "~r~Fronts & Rivals~s~: your save couldn't be read, so a new empire started. The old file is kept in " + _saveDir + ". See log."
                    : "~y~Fronts & Rivals~s~: your save couldn't be read, so the last backup (" + loaded.Source + ") was loaded. See log.");
            try { WarnIfInstalledTwice(); } catch (Exception ex) { Log.Error("Install check", ex); }
            _heatBand = -1; // a different save: no "cooled down" text for the switch
            _angerBands.Clear();
            if (loaded.Source != "new" && loaded.Source != "locked" && _state.ModVersion != ModVersion)
            {
                // A different version is about to use this empire: keep a copy of it as it was, just in case.
                try
                {
                    string from = Path.Combine(_saveDir, "empire.json");
                    string copy = Path.Combine(_saveDir, "empire.before-" + ModVersion + ".json");
                    if (File.Exists(from) && !File.Exists(copy)) { File.Copy(from, copy); Log.Write("Kept a copy of the empire from " + (_state.ModVersion ?? "an older version") + " as " + Path.GetFileName(copy)); }
                }
                catch (Exception ex) { Log.Error("Version backup", ex); }
            }
            _state.ModVersion = ModVersion;
            _rng = new Rng(_state.RngState);
            _activeBase = _state.ActiveMs - _scheduler.ActiveMs;
            Log.Write("Empire loaded from " + loaded.Source + " (save #" + _state.SaveCount + ", schema " + _state.SchemaVersion + ")");

            _empire = new Empire(_state, _content);
            Nemesis.EnsureCrews(_state, _content, _rng);
            _blips.ShowRivalFronts = _config.ShowRivalFronts;
            _streamer.Attach(_empire, _config.ShowRivalFronts);
            if (_config.SkipIntro && !_state.IntroSent) ApplySkipIntro();
            try { _blips.Refresh(_empire, _content); } catch (Exception ex) { Log.Error("Blips", ex); }
            Log.Write("Empire: " + _empire.LivingCount() + " guards, " + _state.Fronts.Count + " fronts tracked");

            if (loaded.Source == "new") { _state.Difficulty = DifficultyStep(_config.Difficulty); SaveNow(); } // create the file right away
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
                    Log.Write("Pause gate: " + (_gateKnown ? _gate.ToString() : "start") + " -> " + gate
                              + (gate == PauseReason.SideJob && PauseGate.LastScript != null ? " (" + PauseGate.LastScript + ")" : ""));
                    if (PauseGate.ClearsWorld(gate)) ClearWorld("gate " + gate);
                    // Franklin's first mission (Franklin and Lamar) just ended: the moment to introduce the mod.
                    if (_gate == PauseReason.Mission && gate == PauseReason.None && Game.Player.Character.Model == new Model(PedHash.Franklin)) _franklinDone = true;
                    _gate = gate;
                    _gateKnown = true;
                }

                bool open = gate == PauseReason.None;
                _notifier.Hold = !open;
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
            Wardrobe.Keep();
            _streamer.Update();
            _director.UpdateLive();
            if (_dev != null) _dev.UpdateSpawns();
            if (_autoScout != null) _autoScout.Update();
            if (_autoTest != null) _autoTest.Update();
        }

        /// <summary>Any front with a door but no posts gets posts the first time the player comes within 100 m.
        /// Shipped fronts.json already has posts; this is the fallback for new or hand-edited fronts.</summary>
        private void AutoPostsTick()
        {
            if (_empire == null || (_autoScout != null && _autoScout.Running)) return;
            if (_placer.Update(_content, ActiveNow()) > 0)
            {
                _content.SaveFronts();
                _empire.EnsureFronts();
                _streamer.Rebuild(); // the front had no posts, so it wasn't streamed: nothing else needs to respawn
                _blips.Refresh(_empire, _content);
                SaveNow();
            }
        }

        private void WorldTick()
        {
            if (_empire == null) return;
            int back = _empire.Recover(ActiveNow());
            if (back > 0) { Log.Write(back + " injured guard(s) back on duty"); SaveNow(); }
        }

        private GTA.Math.Vector3 _lastWaypoint;
        private bool _hadWaypoint;

        /// <summary>The game's own map: drop a waypoint on a front and the Office opens on that front's page next time,
        /// with Send a crew ready for a rival one.</summary>
        private void WaypointTick()
        {
            if (_empire == null) return;
            if (!Game.IsWaypointActive) { _hadWaypoint = false; return; }
            GTA.Math.Vector3 w = GTA.World.WaypointPosition;
            if (_hadWaypoint && Math.Abs(w.X - _lastWaypoint.X) < 2f && Math.Abs(w.Y - _lastWaypoint.Y) < 2f) return;
            _hadWaypoint = true;
            _lastWaypoint = w;
            FrontDef best = null;
            float bestD = 80f;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet) continue;
                float dx = def.Door.X - w.X, dy = def.Door.Y - w.Y;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < bestD) { bestD = d; best = def; }
            }
            if (best == null) return;
            _office.OpenOn(best.Id);
            string owner = _empire.OwnerOf(best.Id);
            if (Owners.IsCrew(owner))
            {
                CrewDef crew = _content.Crew(owner);
                _notifier.Send(NoteKind.Staff, "The Office", best.ShortName, "Marked the " + best.Name + " (" + (crew != null ? crew.Name : owner)
                    + "). Open the Office and choose Send a Crew, or drive there and take it yourself.");
            }
        }

        private static int DifficultyStep(string name)
        {
            int i = Array.IndexOf(Difficulty.Names, name);
            if (i < 0) for (int k = 0; k < Difficulty.Names.Length; k++) if (string.Equals(Difficulty.Names[k], name, StringComparison.OrdinalIgnoreCase)) i = k;
            return i < 0 ? 0 : i - 2;
        }

        private bool _testRestoring;
        private string TestCopyPath { get { return Path.Combine(Path.GetDirectoryName(_saves.MainPath), "empire.before-autotest.json"); } }

        /// <summary>Auto-Test: the empire is copied aside before the test touches it.</summary>
        private bool TestSnapshot()
        {
            if (_state == null) return false;
            try { SaveNow(); File.Copy(_saves.MainPath, TestCopyPath, true); return true; }
            catch (Exception ex) { Log.Error("AutoTest snapshot", ex); return false; }
        }

        /// <summary>Auto-Test is over: everything it spawned goes, and the empire copied aside comes back exactly.</summary>
        private void TestRestore()
        {
            _testRestoring = true;
            try
            {
                ClearWorld("auto-test over");
                File.Copy(TestCopyPath, _saves.MainPath, true);
                SaveService.LoadResult loaded = _saves.Load();
                _director.Reset();
                _state = loaded.State;
                _heatBand = -1;
                _angerBands.Clear();
                _rng = new Rng(_state.RngState);
                _activeBase = _state.ActiveMs - _scheduler.ActiveMs;
                _empire = new Empire(_state, _content);
                Nemesis.EnsureCrews(_state, _content, _rng);
                _streamer.Attach(_empire, _config.ShowRivalFronts);
                EmpireChanged();
                File.Delete(TestCopyPath);
                Log.Write("AutoTest: your empire is back as it was (save #" + _state.SaveCount + ")");
            }
            finally { _testRestoring = false; }
        }

        /// <summary>Office > Settings > Start a new empire. The old one is copied to empire.old.json first.</summary>
        private void StartOver()
        {
            if (_state == null) return;
            try { SaveNow(); File.Copy(_saves.MainPath, Path.Combine(Path.GetDirectoryName(_saves.MainPath), "empire.old.json"), true); }
            catch (Exception ex) { Log.Error("StartOver backup", ex); GTA.UI.Notification.Show("Fronts & Rivals: couldn't back up the old empire, nothing changed. See log."); return; }
            ClearWorld("start over");
            Tips.Clear();
            _notifier.Clear();
            _director.Reset();
            _placer.ResetAll();
            _state = SaveService.NewEmpire();
            _heatBand = -1;
            _angerBands.Clear();
            _state.Difficulty = DifficultyStep(_config.Difficulty);
            _rng = new Rng(_state.RngState);
            _activeBase = -_scheduler.ActiveMs; // the new empire's clock starts at zero
            _empire = new Empire(_state, _content);
            Nemesis.EnsureCrews(_state, _content, _rng);
            _streamer.Attach(_empire, _config.ShowRivalFronts);
            EmpireChanged();
            Log.Write("Started a new empire (old one in empire.old.json)");
            GTA.UI.Notification.Show("Fronts & Rivals: a fresh start. Your old empire is saved as empire.old.json.");
        }

        /// <summary>Did you do it? Shot, blown up or run over by a car you were driving.</summary>
        private static bool KilledByPlayer(Ped body)
        {
            if (body == null || !body.Exists()) return false;
            Ped me = Game.Player.Character;
            Entity k = body.Killer;
            if (k == null) return false;
            if (k.Handle == me.Handle) return true;
            var v = k as Vehicle;
            // Run over by your car counts; dying inside your car in a crash doesn't.
            return v != null && v.Exists() && v.Driver != null && v.Driver.Handle == me.Handle && !body.IsInVehicle(v);
        }

        /// <summary>You killed one of your own. Same death or injury rules; the texts say who did it, and recruits
        /// ask for danger money until tomorrow.</summary>
        private void OnKilledByBoss(GuardState g, Ped body, string where)
        {
            bool permanent = _empire.PermanentDeath(_config.PermanentDeath);
            _empire.GuardDown(g, where, permanent, ActiveNow(), InjuryRecoveryMs);
            _empire.KilledByBoss(g);
            Log.Write("Guard " + g.Id + " " + g.FullName + (permanent ? " killed" : " hurt") + " by the player at " + where);
            _notifier.PostWithMugshot(body, "The Office", permanent ? "Your own" : "Friendly fire",
                permanent ? "You killed " + g.ShortName + ". The rest of your people saw it. Recruits will want danger money tomorrow."
                          : "You put " + g.ShortName + " in hospital. Back in two days, and they won't forget it.");
            SaveNow();
        }

        /// <summary>A bodyguard died at your side. Same rules as at a post.</summary>
        private void OnBodyguardDown(GuardState g, Ped body)
        {
            if (KilledByPlayer(body)) { OnKilledByBoss(g, body, "your side"); return; }
            bool permanent = _empire.PermanentDeath(_config.PermanentDeath);
            _empire.GuardDown(g, "your side", permanent, ActiveNow(), InjuryRecoveryMs);
            Log.Write("Bodyguard " + g.Id + " " + g.FullName + (permanent ? " died" : " was injured"));
            _notifier.PostWithMugshot(body, "Bodyguard", permanent ? "Guard down" : "Guard injured",
                permanent ? g.ShortName + " took a bullet for you." : g.ShortName + " got hurt covering you. Back in two days.");
            SaveNow();
        }

        /// <summary>A streamed guard died in the world. Permanent death unless PermanentDeath=false.</summary>
        private void OnGuardDown(GuardState g, Ped body, FrontDef front)
        {
            if (KilledByPlayer(body)) { OnKilledByBoss(g, body, "the " + front.Name); return; }
            bool permanent = _empire.PermanentDeath(_config.PermanentDeath);
            _empire.GuardDown(g, "the " + front.Name, permanent, ActiveNow(), InjuryRecoveryMs);
            Log.Write("Guard " + g.Id + " " + g.FullName + (permanent ? " died" : " was injured") + " at " + front.Id);
            string text = permanent
                ? g.ShortName + " didn't make it out of the " + front.Name + "."
                : g.ShortName + " got hurt at the " + front.Name + ". Back in two days.";
            _notifier.PostWithMugshot(body, front.Name, permanent ? "Guard down" : "Guard injured", text);
            SaveNow();
        }

        /// <summary>The empire changed (ownership, posts, hires): the streamer re-reads posts by itself, so guards
        /// already standing stay put and only moved or new ones change. Blips refresh, then save.</summary>
        private void EmpireChanged()
        {
            if (_empire == null) return;
            _empire.EnsureFronts();
            _streamer.Rebuild();
            _blips.Refresh(_empire, _content);
            SaveNow();
        }

        /// <summary>Content changed (scouting moved doors or posts): respawn everyone at the new spots, then save.</summary>
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
                _shot.End(); // never leave a scripted camera running
                if (_director != null) _director.OnWorldCleared(); // a live fight is auto-resolved before its peds go
                if (_dev != null) _dev.CancelPending();
                if (_autoScout != null) _autoScout.Cancel();
                if (_autoTest != null && !_testRestoring) _autoTest.Cancel(why);
                int n = _registry.DeleteAll();
                if (_streamer != null) _streamer.Reset();
                if (_workers != null) _workers.Forget();
                if (_escorts != null) _escorts.Forget();
                if (_hangouts != null) _hangouts.Forget();
                if (n > 0) Log.Write("Cleared " + n + " mod entities (" + why + ")");
            }
            catch (Exception ex) { Log.Error("ClearWorld", ex); }
        }

        /// <summary>SkipIntro=true: start with the car wash, 2 recruits posted, and the Broker's message done.</summary>
        private void ApplySkipIntro()
        {
            _state.IntroSent = true;
            if (_empire.OwnedCount() > 0) return;
            _empire.SetOwner("carwash_handson", Owners.Player);
            _empire.StarterCrew(_content.Front("carwash_handson"), _rng);
            Log.Write("SkipIntro: started with the car wash and 2 recruits");
            EmpireChanged();
        }

        private void ShutDownForOnline()
        {
            Log.Write("NETWORK_IS_SESSION_STARTED is true. Fronts & Rivals is switching itself off (single-player only).");
            ClearWorld("online");
            _blips.DeleteAll();
            _blips.RestoreGameBlips();
            SaveNow();
            _shutDown = true;
        }

        private bool _saveFailTold;

        /// <summary>Writes the empire. A failure is logged with its reason and, the first time, shown to the player, so a
        /// save that can't be written (a read-only folder, a full disk, antivirus) is never a silent surprise.</summary>
        /// <summary>Two copies of FrontsAndRivals.dll under scripts would both run and fight over the save: say so.</summary>
        private void WarnIfInstalledTwice()
        {
            string dir = BaseDirectory;
            for (string d = dir; d != null; d = Path.GetDirectoryName(d))
                if (string.Equals(Path.GetFileName(d), "scripts", StringComparison.OrdinalIgnoreCase)) { dir = d; break; }
            string[] copies = Directory.GetFiles(dir, "FrontsAndRivals*.dll", SearchOption.AllDirectories);
            if (copies.Length <= 1) return;
            Log.Write("WARNING: Fronts & Rivals is installed more than once: " + string.Join(" | ", copies));
            GTA.UI.Notification.Show("~r~Fronts & Rivals~s~ is installed twice, so two copies are running. Keep only scripts\\FrontsAndRivals.dll. See log.");
        }

        private void SaveNow()
        {
            if (_state == null) return;
            if (_rng != null) _state.RngState = _rng.State;
            _state.ActiveMs = ActiveNow();
            try { _saves.Save(_state); }
            catch (Exception ex)
            {
                Log.Error("Save to " + _saves.MainPath, ex);
                if (!_saveFailTold)
                {
                    _saveFailTold = true;
                    GTA.UI.Notification.Show("~r~Fronts & Rivals~s~ couldn't save your empire (" + ex.GetType().Name + "). Check " + _saveDir + " isn't read-only. See log.");
                }
            }
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
            if (_shutDown) return;
            try
            {
                if (_officeKey != Keys.None && e.KeyCode == _officeKey) _office.Toggle();
                else if (_dev != null) _dev.OnKeyDown(e.KeyCode);
            }
            catch (Exception ex) { Log.Error("Key " + e.KeyCode, ex); }
        }

        /// <summary>Every 2 s of free roam: process in-game midnights (wages, then income, one feed message)
        /// and the Broker's morning recruits. Skipped midnights (sleep, switches) pay up to 3.</summary>
        private int _heatBand = -1; // Cool/Warm/Hot/Blazing last time we looked, for the "streets are heating up" texts
        private readonly System.Collections.Generic.Dictionary<string, int> _angerBands = new System.Collections.Generic.Dictionary<string, int>(); // each crew's anger band, likewise

        private void ClockTick()
        {
            if (_empire == null) return;
            int band = FrontsAndRivals.Rules.Heat.Band(_state.Heat);
            if (_heatBand >= 0 && band != _heatBand)
            {
                string heatText = FrontsAndRivals.Rules.Heat.BandChangeText(_heatBand, band);
                if (heatText != null) _notifier.Send(NoteKind.StreetWord, "Street word", "Street Heat: " + FrontsAndRivals.Rules.Heat.Label(_state.Heat), heatText);
            }
            _heatBand = band;
            foreach (CrewState crewNow in _state.Crews)
            {
                int angerBand = Nemesis.AngerBand(crewNow.Grudge);
                int was;
                if (_angerBands.TryGetValue(crewNow.Id, out was) && was != angerBand)
                {
                    CrewDef crewDef = _content.Crew(crewNow.Id);
                    string crewName = crewDef != null ? crewDef.Name : crewNow.Id;
                    string angerText = Nemesis.AngerChangeText(crewName, was, angerBand);
                    if (angerText != null) _notifier.Send(NoteKind.Nemesis, "Street word", crewName + ": " + Nemesis.AngerLabel(crewNow.Grudge), angerText);
                }
                _angerBands[crewNow.Id] = angerBand;
            }
            DateTime date = GTA.World.CurrentDate;
            int nights = GameCalendar.MidnightsSince(_state.LastGameDate, date);
            bool first = _state.LastGameDate == null;
            _state.LastGameDate = GameCalendar.Stamp(date);
            bool changed = first;

            for (int i = 0; i < nights; i++)
            {
                _state.Day++;
                Nemesis.Daily(_state, _state.Day);
                foreach (CrewState crew in _state.Crews)
                {
                    Nemesis.Event rebuilt = Nemesis.Rebuild(_state, _content, crew, _rng, _state.Day);
                    if (rebuilt != null) _notifier.Send(NoteKind.Nemesis, "Street word", "Back in business", _content.Lines.Pick(_rng, rebuilt.Moment, rebuilt.Tokens));
                }
                // The city moves on its own: crews grab fronts for sale and take each other's. One text a night at most.
                bool told = false;
                var report = new System.Collections.Generic.List<string>(); // one midnight report instead of a text for each thing
                string turfFreq = string.IsNullOrEmpty(_state.TurfFrequency) ? _config.TurfFrequency.ToString() : _state.TurfFrequency;
                foreach (Turf.Move move in _empire.InTheGame() ? Turf.Nightly(_state, _content, _rng, Turf.ChanceFor(turfFreq)) : new System.Collections.Generic.List<Turf.Move>()) // quiet until your first front
                {
                    if (!move.Success) continue;
                    CrewDef mover = _content.Crew(move.Crew);
                    FrontDef where = _content.Front(move.Front);
                    Log.Write("Turf: " + move.Crew + " took " + move.Front + (move.FromCrew != null ? " from " + move.FromCrew : ""));
                    if (told || mover == null || where == null) continue;
                    var t = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "crew", mover.Name }, { "front", where.ShortName }, { "area", where.Area },
                        { "rival", move.FromCrew != null ? _content.Crew(move.FromCrew).Name : "" }
                    };
                    report.Add(_content.Lines.Pick(_rng, move.FromCrew != null ? "turfWar" : "turfGrab", t));
                    told = true;
                }
                if (_empire.BrokerRescue())
                {
                    Log.Write("Broker rescue: the car wash is thinly held for two days");
                    _notifier.Send(NoteKind.Broker, "Street Word", "Car Wash", "Word is the Dead Kings pulled most of their people off the Hands On Car Wash. Go take it back.");
                }
                Economy.Payout pay = Economy.Midnight(_state, _content, Game.Player.Money, _config.IncomeMultiplier, _config.WageMultiplier);
                var promoted = new System.Collections.Generic.List<string>();
                foreach (GuardState up in _empire.DayOnDuty())
                {
                    Log.Write("Midnight: " + up.FullName + " promoted to " + Economy.RankName((Rank)up.Rank));
                    promoted.Add(up.ShortName + " (" + Economy.RankName((Rank)up.Rank) + ")");
                }
                var quit = new System.Collections.Generic.List<string>();
                foreach (GuardState q in pay.Quit)
                {
                    Log.Write("Midnight: " + q.FullName + " quit over unpaid wages");
                    quit.Add(q.ShortName);
                }
                if (promoted.Count > 0) report.Add("Promoted: " + string.Join(", ", promoted.ToArray()) + ".");
                if (quit.Count > 0)
                {
                    string q = "Quit over unpaid wages: " + string.Join(", ", quit.ToArray()) + ".";
                    if (_notifier.Allows(NoteKind.Midnight)) report.Add(q); else _notifier.Send(NoteKind.Staff, "The Office", "Unpaid", q); // never missed
                }
                if (pay.Income == 0 && pay.Wages == 0)
                {
                    if (report.Count > 0) _notifier.Send(NoteKind.Midnight, "The Office", "Midnight report", string.Join(" ", report.ToArray()));
                    continue;
                }
                // Never below zero: only Loyal guards are left waiting for their money if it doesn't cover them.
                Game.Player.Money = (int)Math.Max(0L, (long)Game.Player.Money + pay.Net);
                _state.LastNet = pay.Net;
                Log.Write("Midnight day " + _state.Day + ": income " + pay.Income + ", wages " + pay.Wages + ", net " + pay.Net);
                if (pay.Net > 0) Sounds.Play(Cue.MidnightTake);
                report.Insert(0, "Fronts brought in $" + pay.Income.ToString("N0") + ", wages $" + pay.Wages.ToString("N0") + ", net " + (pay.Net < 0 ? "-$" : "$") + Math.Abs(pay.Net).ToString("N0") + ".");
                _notifier.Send(NoteKind.Midnight, "The Office", "Midnight report", string.Join(" ", report.ToArray()));
                changed = true;
            }
            if (nights > 0) EmpireChanged(); // quitters leave their posts

            // The Broker's first message: when Franklin's first mission ends (see OnTick), or after ten minutes of free
            // roam in a game that's already past it.
            if (!_state.IntroSent && _empire.OwnedCount() == 0 && (_franklinDone || Economy.BrokerReady(_scheduler.ActiveMs)))
            {
                _state.IntroSent = true;
                // A GTA help box, like the game introducing a new activity, and the map icon flashes.
                var introRng = new Random(_rng.Next());
                string intro = _content.Lines.Pick(introRng, "brokerIntro", null);
                GTA.UI.Screen.ShowHelpText(intro.Replace("{officeKeys}", Tips.OfficeKeys(false)), 15000, true, false);
                _notifier.Remember("Fronts & Rivals", "Your first front", intro.Replace("{officeKeys}", Tips.OfficeKeys(true)));
                _blips.Flash("carwash_handson");
                changed = true;
            }

            // The Broker's recruits: a fresh set each morning from 06:00, and right away on a brand-new empire.
            if (_state.OffersDay < 0 || (_state.OffersDay != _state.Day && GTA.World.CurrentTimeOfDay.Hours >= 6))
            {
                _empire.RefreshOffers(_rng, _state.Day);
                Log.Write("Broker: " + _state.Offers.Count + " new recruits for day " + _state.Day);
                changed = true;
            }
            if (changed) SaveNow();
        }

        private void OnAborted(object sender, EventArgs e)
        {
            // Cleanup first: nothing may be left behind for the game to deal with.
            ClearWorld("script stopped");
            try { _blips.DeleteAll(); } catch (Exception ex) { Log.Error("Blips on abort", ex); }
            try { _blips.RestoreGameBlips(); } catch (Exception ex) { Log.Error("Game blips on abort", ex); }
            try { SaveNow(); } catch (Exception ex) { Log.Error("Save on abort", ex); }
            Log.Write("==== Fronts & Rivals stopped");
        }
    }
}
