// Fronts & Rivals dev tools: "Auto-Test". One button, hands off for a few minutes: it plays through the mod's main
// systems on its own and logs PASS or FAIL for each, then puts everything back as it was (your empire, cash, position,
// the time of day). Steps: free roam check, taking a rival front, staffing it, a live defense, an attack while you're
// away, a raid, a midnight payout and a save. The full report goes to the log and to autotest-report.txt.
// A state machine stepped from the Streamer job: no Wait loops on the game thread. A mission or any pause cancels it,
// and it restores everything at once.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Attacks;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Dev
{
    public sealed class AutoTest
    {
        /// <summary>What the test needs from Main.</summary>
        public sealed class Host
        {
            public Func<Empire> Empire;
            public ContentDb Content;
            public Registry Registry;
            public Director Director;
            public Func<long> Now;
            public Func<PauseReason> Gate;
            public Action SaveNow;
            public Func<string> SavePath;
            public Func<bool> Snapshot;   // copies the empire aside; false = don't run
            public Action Restore;        // puts the copy back and reloads it
        }

        private enum Step { Idle, FreeRoam, GoToFront, WaitSoldiers, WaitTaken, CheckAfterTake, Staff, WaitGuards,
                            ForceLive, WaitLiveEnd, GoAway, ForceAway, WaitAwayEnd, Raid, WaitRaid, Midnight, WaitMidnight, Save, Done }

        private readonly Host _h;
        private Step _step = Step.Idle;
        private int _stepAt;
        private readonly List<string> _report = new List<string>();
        private int _pass, _fail;
        private FrontDef _target;
        private Vector3 _returnPos;
        private float _returnHeading;
        private int _returnMoney, _returnHour, _returnMinute, _returnDay, _returnMonth, _returnYear, _dayBefore;

        public bool Running { get { return _step != Step.Idle; } }

        public AutoTest(Host host) { _h = host; }

        public void Start()
        {
            if (Running) { DevTools.Notify("Auto-Test is already running."); return; }
            if (_h.Gate() != PauseReason.None) { DevTools.Notify("Auto-Test needs free roam."); return; }
            if (!_h.Snapshot()) { DevTools.Notify("Couldn't copy your empire aside, so Auto-Test didn't start. See log."); return; }
            Ped me = Game.Player.Character;
            _returnPos = me.Position; _returnHeading = me.Heading; _returnMoney = Game.Player.Money;
            _returnHour = GTA.World.CurrentTimeOfDay.Hours; _returnMinute = GTA.World.CurrentTimeOfDay.Minutes;
            _returnDay = Function.Call<int>(Hash.GET_CLOCK_DAY_OF_MONTH); _returnMonth = Function.Call<int>(Hash.GET_CLOCK_MONTH); _returnYear = Function.Call<int>(Hash.GET_CLOCK_YEAR);
            _report.Clear(); _pass = _fail = 0;
            me.IsInvincible = true;
            Function.Call(Hash.SET_MAX_WANTED_LEVEL, 0);
            Game.Player.WantedLevel = 0;
            Log.Write("AutoTest: starting (your empire is copied aside and comes back at the end)");
            DevTools.Notify("Auto-Test running, about 5 minutes. Hands off.");
            Go(Step.FreeRoam);
        }

        /// <summary>A mission or other pause: stop and put everything back.</summary>
        public void Cancel(string why)
        {
            if (!Running) return;
            Result(false, "Cancelled: " + why);
            Finish();
        }

        private void Go(Step s) { _step = s; _stepAt = Game.GameTime; }
        private int Waited { get { return Game.GameTime - _stepAt; } }

        private void Result(bool ok, string what)
        {
            if (ok) _pass++; else _fail++;
            string line = (ok ? "PASS " : "FAIL ") + what;
            _report.Add(line);
            Log.Write("AutoTest: " + line);
        }

        /// <summary>Every 0.5 s from the Streamer job.</summary>
        public void Update()
        {
            if (!Running) return;
            try { Tick(); }
            catch (Exception ex) { Log.Error("AutoTest " + _step, ex); Result(false, _step + " threw " + ex.GetType().Name + " (see log)"); Finish(); }
        }

        private void Tick()
        {
            Empire e = _h.Empire();
            if (e == null) { Cancel("no empire"); return; }
            EmpireState s = e.State;
            Ped me = Game.Player.Character;

            switch (_step)
            {
                case Step.FreeRoam:
                {
                    Result(_h.Gate() == PauseReason.None, "Free roam: the mod is running (gate open)");
                    var running = new List<string>();
                    foreach (string name in FreeRoamRules.Blocking)
                        if (Function.Call<int>(Hash.GET_NUMBER_OF_THREADS_RUNNING_THE_SCRIPT_WITH_THIS_HASH, Game.GenerateHash(name)) > 0) running.Add(name);
                    Result(running.Count == 0, "Free roam: no mission or activity script running" + (running.Count > 0 ? " (running: " + string.Join(", ", running.ToArray()) + ")" : ""));
                    Log.Write("AutoTest: " + MissionStarts.Count + " mission start blips on the map");
                    foreach (string line in MissionStarts.DumpAll(900)) Log.Write("AutoTest: blip " + line);
                    _target = PickRivalFront(e, s, me.Position);
                    if (_target == null) { Result(false, "Taking a front: no small rival front available to test on"); Go(Step.Staff); return; }
                    Go(Step.GoToFront);
                    return;
                }
                case Step.GoToFront:
                {
                    Vector3 door = Door(_target);
                    double h = _target.Door.H * Math.PI / 180.0;
                    Vector3 want = door + new Vector3(-(float)Math.Sin(h) * 40f, (float)Math.Cos(h) * 40f, 0f);
                    Vector3 safe = GTA.World.GetSafeCoordForPed(want, true);
                    me.Position = safe != Vector3.Zero ? safe : want;
                    Log.Write("AutoTest: at " + _target.Id + " (" + _target.Name + "), held by " + e.OwnerOf(_target.Id));
                    Go(Step.WaitSoldiers);
                    return;
                }
                case Step.WaitSoldiers:
                {
                    List<Ped> soldiers = CrewPeds(Door(_target), 90f, e.OwnerOf(_target.Id));
                    if (soldiers.Count >= 2 || Waited > 25000)
                    {
                        Result(soldiers.Count >= 2, "Taking a front: " + soldiers.Count + " soldiers guard the " + _target.ShortName);
                        foreach (Ped p in soldiers) p.Kill();
                        Go(Step.WaitTaken);
                    }
                    return;
                }
                case Step.WaitTaken:
                {
                    foreach (Ped p in CrewPeds(Door(_target), 90f, null)) p.Kill(); // late arrivals and backup
                    if (e.PlayerOwns(_target.Id)) { Result(true, "Taking a front: clearing it makes it yours"); Go(Step.CheckAfterTake); return; }
                    if (Waited > 25000) { Result(false, "Taking a front: still " + e.OwnerOf(_target.Id) + "'s 25 s after clearing it"); Go(Step.Staff); }
                    return;
                }
                case Step.CheckAfterTake:
                {
                    if (Waited < 7000) return; // the establishing shot is 3.5 s
                    Result(GTA.World.RenderingCamera == null || !GTA.World.RenderingCamera.Exists() || !GTA.World.RenderingCamera.IsActive,
                           "Taking a front: the camera is back with you after the takeover shot");
                    int dots = 0;
                    foreach (Ped p in GTA.World.GetNearbyPeds(Door(_target), 120f))
                        if (p.IsDead && p.AttachedBlip != null && p.AttachedBlip.Exists() && _h.Registry.Owns(p)) dots++;
                    Result(dots == 0, "Taking a front: no crew dots left on the bodies" + (dots > 0 ? " (" + dots + " left)" : ""));
                    Go(Step.Staff);
                    return;
                }
                case Step.Staff:
                {
                    if (_target == null || !e.PlayerOwns(_target.Id)) { Result(false, "Staffing: skipped, no front of yours to staff"); Go(Step.Raid); return; }
                    var rng = new Random(7);
                    for (int i = 0; i < 4; i++) e.Recruit(rng);
                    int posted = e.FillPosts(_target.Id);
                    Result(posted > 0, "Staffing: " + posted + " recruits posted at the " + _target.ShortName);
                    Go(Step.WaitGuards);
                    return;
                }
                case Step.WaitGuards:
                {
                    int guards = 0;
                    foreach (Ped p in GTA.World.GetNearbyPeds(Door(_target), 40f))
                        if (!p.IsDead && p.RelationshipGroup == Factions.Guards && _h.Registry.Owns(p)) guards++;
                    if (guards > 0 || Waited > 20000)
                    {
                        Result(guards > 0, "Staffing: " + guards + " of your guards standing at their posts");
                        Go(Step.ForceLive);
                    }
                    return;
                }
                case Step.ForceLive:
                {
                    if (s.Attack != null) { Result(false, "Live defense: another attack was already under way"); Go(Step.GoAway); return; }
                    _h.Director.RequestForcedAttack();
                    Go(Step.WaitLiveEnd);
                    return;
                }
                case Step.WaitLiveEnd:
                {
                    if (s.Attack != null && s.Attack.Phase == AttackPhase.Window && s.Attack.Front != _target.Id) s.Attack.Front = _target.Id; // the test front, where you are
                    if (s.Attack != null && s.Attack.Phase == AttackPhase.Live)
                        foreach (Ped p in CrewPeds(Door(_target), 200f, s.Attack.Crew)) if (Waited > 15000) p.Kill(); // let them arrive, then end it
                    if (s.Attack == null && Waited > 5000)
                    {
                        Result(e.PlayerOwns(_target.Id), "Live defense: the attack was fought off and the " + _target.ShortName + " is still yours");
                        Go(Step.GoAway);
                        return;
                    }
                    if (Waited > 150000) { Result(false, "Live defense: still going after 150 s (phase " + (s.Attack != null ? s.Attack.Phase : "none") + ")"); Go(Step.GoAway); }
                    return;
                }
                case Step.GoAway:
                {
                    if (s.Attack != null) { if (Waited > 30000) { s.Attack.WindowEnds = _h.Now(); } return; } // let a leftover attack finish first
                    Vector3 away = Door(_target) + new Vector3(700f, 0f, 0f);
                    Vector3 safe = GTA.World.GetSafeCoordForPed(away, true);
                    me.Position = safe != Vector3.Zero ? safe : _returnPos;
                    Go(Step.ForceAway);
                    return;
                }
                case Step.ForceAway:
                {
                    if (Waited < 3000) return; // let the front stream out
                    _h.Director.RequestForcedAttack();
                    Go(Step.WaitAwayEnd);
                    return;
                }
                case Step.WaitAwayEnd:
                {
                    if (s.Attack != null && s.Attack.Phase == AttackPhase.Window && s.Attack.WindowEnds > _h.Now()) s.Attack.WindowEnds = _h.Now(); // skip the hold timer
                    if (s.Attack == null && Waited > 4000)
                    {
                        Result(true, "Attack while you're away: settled off-screen (the " + _target.ShortName + " is " + (e.PlayerOwns(_target.Id) ? "still yours" : "lost") + ")");
                        Go(Step.Raid);
                        return;
                    }
                    if (Waited > 60000) { Result(false, "Attack while you're away: not settled after 60 s"); Go(Step.Raid); }
                    return;
                }
                case Step.Raid:
                {
                    var rng = new Random(11);
                    for (int i = 0; i < 4; i++) e.Recruit(rng);
                    FrontDef raidOn = PickRivalFront(e, s, me.Position);
                    if (raidOn == null) { Result(false, "Raid: no rival front to raid"); Go(Step.Midnight); return; }
                    string blocked = e.RaidBlocked(raidOn);
                    if (blocked != null) { Result(false, "Raid: blocked (" + blocked + ")"); Go(Step.Midnight); return; }
                    int hour = GTA.World.CurrentTimeOfDay.Hours;
                    bool sent = e.StartRaid(raidOn, _h.Now(), hour);
                    Result(sent && s.Raid != null, "Raid: sent on the " + raidOn.ShortName + " (shown odds " + (int)(e.RaidOdds(raidOn, hour) * 100) + "%)");
                    if (s.Raid != null) s.Raid.EndsMs = _h.Now(); // word comes back now
                    Go(Step.WaitRaid);
                    return;
                }
                case Step.WaitRaid:
                {
                    if (s.Raid == null) { Result(true, "Raid: word came back and the raid was settled"); Go(Step.Midnight); return; }
                    if (Waited > 20000) { Result(false, "Raid: not settled after 20 s"); Go(Step.Midnight); }
                    return;
                }
                case Step.Midnight:
                {
                    _dayBefore = s.Day;
                    Function.Call(Hash.SET_CLOCK_TIME, 23, 59, 40);
                    Go(Step.WaitMidnight);
                    return;
                }
                case Step.WaitMidnight:
                {
                    if (s.Day > _dayBefore) { Result(true, "Midnight: the day turned over and the payout ran (net $" + s.LastNet + ")"); Go(Step.Save); return; }
                    if (Waited > 45000) { Result(false, "Midnight: the day didn't turn over within 45 s"); Go(Step.Save); }
                    return;
                }
                case Step.Save:
                {
                    string path = _h.SavePath();
                    DateTime before = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                    _h.SaveNow();
                    bool ok = File.Exists(path) && File.GetLastWriteTimeUtc(path) >= before && new FileInfo(path).Length > 100;
                    Result(ok, "Save: the empire was written to disk");
                    Finish();
                    return;
                }
            }
        }

        private void Finish()
        {
            _step = Step.Idle;
            try
            {
                Ped me = Game.Player.Character;
                _h.Restore();
                me.Position = _returnPos; me.Heading = _returnHeading;
                Game.Player.Money = _returnMoney;
                Function.Call(Hash.SET_CLOCK_DATE, _returnDay, _returnMonth, _returnYear); // the test's midnight never counts as a real one
                Function.Call(Hash.SET_CLOCK_TIME, _returnHour, _returnMinute, 0);
                me.IsInvincible = false;
                Function.Call(Hash.SET_MAX_WANTED_LEVEL, 5);
            }
            catch (Exception ex) { Log.Error("AutoTest restore", ex); _report.Add("FAIL putting things back (see log)"); _fail++; }
            string summary = "Auto-Test done: " + _pass + " passed, " + _fail + " failed.";
            Log.Write("AutoTest: " + summary);
            try { File.WriteAllLines(Path.Combine(Path.GetDirectoryName(_h.SavePath()), "autotest-report.txt"), new[] { summary, "" }.Concat(_report)); }
            catch (Exception ex) { Log.Error("AutoTest report", ex); }
            DevTools.Notify(summary + (_fail > 0 ? " See autotest-report.txt." : ""));
        }

        private static Vector3 Door(FrontDef f) { return new Vector3(f.Door.X, f.Door.Y, f.Door.Z); }

        /// <summary>A small rival front that can be taken right now (not dug in, not the car wash), nearest first.</summary>
        private FrontDef PickRivalFront(Empire e, EmpireState s, Vector3 from)
        {
            FrontDef best = null;
            float bestD = float.MaxValue;
            foreach (FrontDef f in _h.Content.Fronts)
            {
                string owner = e.OwnerOf(f.Id);
                if (!f.IsScouted || f.Tier != 0 || f.Starter || owner == Owners.Player || !(owner == Owners.CrownKings || owner == Owners.Ironside || owner == Owners.Velvet)) continue;
                if (FrontsAndRivals.Rules.Attacks.DugIn(s, e.Find(f.Id)) || !e.Trusted(f)) continue;
                float d = f.Door.DistanceTo(from.X, from.Y, from.Z);
                if (d < bestD) { bestD = d; best = f; }
            }
            return best;
        }

        /// <summary>Living mod people of a crew (any crew when null) within range.</summary>
        private List<Ped> CrewPeds(Vector3 at, float range, string crewId)
        {
            var list = new List<Ped>();
            foreach (Ped p in GTA.World.GetNearbyPeds(at, range))
            {
                if (p == null || !p.Exists() || p.IsDead || !_h.Registry.Owns(p)) continue;
                int g = p.RelationshipGroup.Hash;
                bool crew = crewId != null ? g == Factions.CrewGroup(crewId).Hash
                          : g == Factions.CrownKings.Hash || g == Factions.Ironside.Hash || g == Factions.Velvet.Hash;
                if (crew) list.Add(p);
            }
            return list;
        }
    }
}
