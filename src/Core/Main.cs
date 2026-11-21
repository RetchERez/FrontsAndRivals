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
        public const string ModVersion = "1.0.0";
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
        private readonly DevTools _dev;
        private readonly AutoPostPlacer _placer = new AutoPostPlacer();
        private readonly OfficeMenu _office;
        private readonly object _phone; // PhoneContact, held as object so a missing iFruitAddon2.dll can't break Main
        private readonly Keys _officeKey = Keys.None;
        private readonly HoldButton _officeHold = new HoldButton(500); // hold Back/View (M on keyboard)
        private readonly AutoScout _autoScout;
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
            Nemesis.Names = _content.Names; // new crew lieutenants draw from the same name pool
            _streamer = new Streamer(_registry, _content, ActiveNow, OnGuardDown);
            _live = new LiveDefense(_registry, _streamer);
            _workers = new Workers(_registry, _content, () => _empire);
            _escorts = new Escorts(_registry, () => _empire, OnBodyguardDown);
            _director = new Director(_content, _config, () => _empire, () => _rng, ActiveNow, () => _scheduler.ActiveMs,
                                     EmpireChanged, _banners, _notifier, _streamer, _live);
            _notifier.UseSettings(NoteSettingsNow);
            Tips.State = () => _state;
            Sounds.Enabled = () => _config.SoundEffects && (_state == null || !_state.SoundOff);
            _streamer.AlertOnFootRange = _config.RivalAlertRange;
            _blips.ShowForSaleFronts = _config.ShowForSaleFronts;
            _office = new OfficeMenu(_content, _config, () => _empire, () => _rng, EmpireChanged, SaveNow);
            _office.StartOver = StartOver;
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
            if (!string.IsNullOrEmpty(_config.OfficeHotkey) && !_config.OfficeHotkey.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse(_config.OfficeHotkey, true, out key)) { _officeKey = key; Log.Write("Office hotkey: " + key); }
                else Log.Write("Config: OfficeHotkey '" + _config.OfficeHotkey + "' is not a key name, no hotkey");
            }
            KeyDown += OnKeyDown;

            if (_config.DevTools)
            {
                _dev = new DevTools(_registry, _director, SaveNow, () => _gate);
                _autoScout = new AutoScout(_content, _placer, WorldChanged);
                _office.OpenDevTools = _dev.OpenSoon;
                new ScoutMenu(_dev, _content, () => _empire, () => _rng, WorldChanged, () => _gate, _placer, _autoScout);
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
            if (!_config.OfficeButton) return;
            // Hold the Interaction Menu button (Back/View on a pad, M on keyboard) for half a second, like GTA Online.
            // A tap still changes the camera view; once the hold fires the camera change is blocked until you let go.
            if (_officeHold.Update(Game.IsControlPressed(GTA.Control.InteractionMenu), Game.GameTime))
            {
                _office.Toggle();
            }
            if (_officeHold.Fired) Game.DisableControlThisFrame(GTA.Control.NextCamera);
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
            _streamer.Update();
            _director.UpdateLive();
            if (_dev != null) _dev.UpdateSpawns();
            if (_autoScout != null) _autoScout.Update();
        }

        /// <summary>Any front with a door but no posts gets posts the first time the player comes within 100 m.
        /// Shipped fronts.json already has posts; this is the fallback for new or hand-edited fronts.</summary>
        private void AutoPostsTick()
        {
            if (_empire == null || (_autoScout != null && _autoScout.Running)) return;
            if (_placer.Update(_content, ActiveNow()) > 0)
            {
                _content.SaveFronts();
                WorldChanged();
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
                if (_director != null) _director.OnWorldCleared(); // a live fight is auto-resolved before its peds go
                if (_dev != null) _dev.CancelPending();
                if (_autoScout != null) _autoScout.Cancel();
                int n = _registry.DeleteAll();
                if (_streamer != null) _streamer.Reset();
                if (_workers != null) _workers.Forget();
                if (_escorts != null) _escorts.Forget();
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
        private void ClockTick()
        {
            if (_empire == null) return;
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
                string turfFreq = string.IsNullOrEmpty(_state.TurfFrequency) ? _config.TurfFrequency.ToString() : _state.TurfFrequency;
                foreach (Turf.Move move in Turf.Nightly(_state, _content, _rng, Turf.ChanceFor(turfFreq)))
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
                    _notifier.Send(NoteKind.StreetWord, "Street word", where.Area, _content.Lines.Pick(_rng, move.FromCrew != null ? "turfWar" : "turfGrab", t));
                    told = true;
                }
                Economy.Payout pay = Economy.Midnight(_state, _content, Game.Player.Money, _config.IncomeMultiplier, _config.WageMultiplier);
                foreach (GuardState up in _empire.DayOnDuty())
                {
                    Log.Write("Midnight: " + up.FullName + " promoted to " + Economy.RankName((Rank)up.Rank));
                    _notifier.Send(NoteKind.Staff, up.ShortName, "Promoted", "Made " + Economy.RankName((Rank)up.Rank) + ". "
                        + (Economy.MaxWeapon((Rank)up.Rank) > Economy.MaxWeapon((Rank)(up.Rank - 1)) ? "Trusted with " + Economy.WithArticle(Economy.MaxWeapon((Rank)up.Rank)) + " now." : "Thanks for the faith, boss."));
                }
                foreach (GuardState q in pay.Quit)
                {
                    Log.Write("Midnight: " + q.FullName + " quit over unpaid wages");
                    _notifier.Send(NoteKind.Staff, q.ShortName, "Unpaid", "No money, no me. I'm out.");
                }
                if (pay.Income == 0 && pay.Wages == 0) continue;
                Game.Player.Money += pay.Net;
                _state.LastNet = pay.Net;
                Log.Write("Midnight day " + _state.Day + ": income " + pay.Income + ", wages " + pay.Wages + ", net " + pay.Net);
                if (pay.Net > 0) Sounds.Play(Cue.MidnightTake);
                _notifier.Send(NoteKind.Midnight, "The Office", "Midnight take",
                    "Fronts brought in $" + pay.Income.ToString("N0") + ". Wages $" + pay.Wages.ToString("N0") + ". Net " + (pay.Net < 0 ? "-$" : "$") + Math.Abs(pay.Net).ToString("N0") + ".");
                changed = true;
            }
            if (nights > 0) EmpireChanged(); // quitters leave their posts

            // The Broker's first message: once you can afford the car wash, on a new empire.
            if (!_state.IntroSent && _empire.OwnedCount() == 0 && Economy.BrokerReady(Game.Player.Money, _scheduler.ActiveMs))
            {
                _state.IntroSent = true;
                _notifier.Send(NoteKind.Broker, "The Broker", "Business", _content.Lines.Pick(_rng, "brokerIntro", null));
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
            try { SaveNow(); } catch (Exception ex) { Log.Error("Save on abort", ex); }
            Log.Write("==== Fronts & Rivals stopped");
        }
    }
}
