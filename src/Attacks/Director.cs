// Fronts & Rivals: the Director. Runs every attack from start to finish, and the claiming of rival fronts.
//   plan (cooldown, then 25% a minute) -> warning text (+ a taunt from a Showboat) -> window opens at the front
//   -> within 300 m: live defense, else auto-resolve when the window closes -> result, banner, nemesis texts.
// Every rule lives in Rules.Attacks / Rules.Nemesis (pure, tested); this class does the game side.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using FrontsAndRivals.Spawn;
using FrontsAndRivals.UI;

namespace FrontsAndRivals.Attacks
{
    public sealed class Director
    {
        private const long ClaimWindowMs = 60000;
        private const float ClaimRange = 3f;

        private readonly ContentDb _content;
        private readonly Config _config;
        private readonly Func<Empire> _empire;
        private readonly Func<Random> _rng;
        private readonly Func<long> _now;          // free-roam clock
        private readonly Func<long> _sinceLoad;    // free-roam time since this session started
        private readonly Action _changed;          // streamer + blips + save
        private readonly Banners _banners;
        private readonly Notifier _notifier;
        private readonly Streamer _streamer;
        private readonly LiveDefense _live;

        private long _nextPlanRoll;
        private AttackState _liveFailed; // a convoy that could not spawn: auto-resolve, don't retry every 2 s
        private bool _forceRequested;
        private string _claimFront;
        private string _claimCrew;
        private long _claimUntil;

        public Director(ContentDb content, Config config, Func<Empire> empire, Func<Random> rng, Func<long> now, Func<long> sinceLoad,
                        Action changed, Banners banners, Notifier notifier, Streamer streamer, LiveDefense live)
        {
            _content = content; _config = config; _empire = empire; _rng = rng; _now = now; _sinceLoad = sinceLoad;
            _changed = changed; _banners = banners; _notifier = notifier; _streamer = streamer; _live = live;
            _streamer.OnCrewGuardDown = OnCrewGuardDown;
        }

        /// <summary>Dev tools: plan an attack right away with a 30-second warning.</summary>
        public void RequestForcedAttack()
        {
            _forceRequested = true;
            Log.Write("Director: forced attack requested");
        }

        public bool ForceRequested { get { return _forceRequested; } }

        private EmpireState State { get { return _empire().State; } }

        private string Frequency { get { string f = State.Frequency; return string.IsNullOrEmpty(f) ? _config.Frequency.ToString() : f; } }

        /// <summary>Every 2 s while the pause gate is open.</summary>
        public void Update()
        {
            Empire e = _empire();
            if (e == null) return;
            EmpireState s = e.State;
            long now = _now();
            int hour = GTA.World.CurrentTimeOfDay.Hours;

            NemesisTexts(s, now);

            if (s.Attack == null)
            {
                bool force = _forceRequested;
                if (force || now >= _nextPlanRoll)
                {
                    _nextPlanRoll = now + Rules.Attacks.Minute;
                    bool can = force ? s.Attack == null
                             : Rules.Attacks.CanPlan(s, now, _sinceLoad(), Frequency, _config.CooldownMinutes, _config.FirstAttackDelayMinutes);
                    if (can && (force || _rng().NextDouble() < Rules.Attacks.PlanChancePerMinute)) Plan(e, hour, now, force);
                    _forceRequested = false;
                }
                return;
            }

            AttackState a = s.Attack;
            FrontDef front = _content.Front(a.Front);
            if (front == null || !e.PlayerOwns(a.Front)) { Log.Write("Director: target " + a.Front + " no longer yours, attack called off"); s.Attack = null; _changed(); return; }

            if (a.Phase == AttackPhase.Warning && now >= a.WarnEnds)
            {
                a.Phase = AttackPhase.Window;
                Sounds.Play(Cue.AttackStarted);
                Log.Write("Director: window open at " + a.Front);
                _changed();
            }
            if (a.Phase == AttackPhase.Window)
            {
                Vector3 p = Game.Player.Character.Position;
                bool liveOn = _config.LiveDefense && !State.LiveDefenseOff;
                bool near = liveOn && front.IsScouted && front.Door.DistanceTo(p.X, p.Y, p.Z) < Rules.Attacks.LiveRange;
                if (near && _liveFailed != a && StartLive(e, a, front)) return;
                if (now >= a.WindowEnds) FinishAuto(e, a, front, hour, now, null, 0);
            }
            else if (a.Phase == AttackPhase.Live && !_live.Active)
            {
                // The live fight ended without reporting (cleared by a mission): auto-resolve what's left.
                FinishAuto(e, a, front, hour, now, null, 0);
            }
        }

        /// <summary>Every 0.5 s: steps the live defense and tidies up after it.</summary>
        public void UpdateLive()
        {
            Empire e = _empire();
            if (e == null) return;
            _live.Cleanup();
            if (!_live.Active) return;
            LiveDefense.Result r = _live.Update();
            if (r == null) return;
            EmpireState s = e.State;
            AttackState a = s.Attack;
            if (a == null) return;
            FinishLive(e, a, r);
        }

        /// <summary>A mission started or the world is being cleared: stop the fight and auto-resolve the rest.</summary>
        public void OnWorldCleared()
        {
            Empire e = _empire();
            if (e == null || !_live.Active) return;
            LiveDefense.Result r = _live.Abort("world cleared");
            _live.ForceClear();
            AttackState a = e.State.Attack;
            if (a != null && r != null) FinishLive(e, a, r);
        }

        // ---------------------------------------------------------------- planning

        private void Plan(Empire e, int hour, long now, bool force)
        {
            EmpireState s = e.State;
            AttackState a = Rules.Attacks.Plan(s, _content, e, _rng(), _config.WarningHours, hour, now);
            if (a == null) { if (force) DevNotice("No crew can attack right now (do you own a front?)."); return; }
            if (force) { a.WarnEnds = now + 30000; a.WindowEnds = a.WarnEnds + Rules.Attacks.WindowMs; }
            s.Attack = a;
            CrewDef crew = _content.Crew(a.Crew);
            CrewMember leader = Nemesis.Crew(s, a.Crew).Member(a.Leader);
            FrontDef front = _content.Front(a.Front);
            Log.Write("Director: " + crew.Name + " led by " + leader.DisplayName + " will hit " + a.Front + " (squad " + a.Squad + ", A " + a.Strength.ToString("0") + ")");

            var tokens = Tokens(crew, front, leader, null);
            _notifierPost(NoteKind.AttackWarning, "Street word", front.ShortName, _content.Lines.Pick(_rng(), "warning", tokens));
            Sounds.Play(Cue.AttackWarning);
            if (leader.LeaderTrait == LeaderTrait.Showboat)
                _notifierPost(NoteKind.Taunt, leader.DisplayName, crew.Name, _content.Lines.Pick(_rng(), "taunt", tokens));
            Nemesis.Crew(s, a.Crew).LastMove = "Planning a hit on your " + front.ShortName;
            _changed();
        }

        // ---------------------------------------------------------------- live defense

        private bool StartLive(Empire e, AttackState a, FrontDef front)
        {
            if (_live.Active) return true;
            CrewDef crew = _content.Crew(a.Crew);
            CrewMember leader = Nemesis.Crew(e.State, a.Crew).Member(a.Leader);
            if (!_live.Start(a, front, crew, leader, _config.PoliceRespond)) { _liveFailed = a; return false; }
            a.Phase = AttackPhase.Live;
            _streamer.SetFighting(front.Id, true);
            GTA.UI.Screen.ShowSubtitle("The ~r~" + crew.Name + "~s~ are coming for the " + front.ShortName + ". Hold the door.", 6000);
            _changed();
            return true;
        }

        private void FinishLive(Empire e, AttackState a, LiveDefense.Result r)
        {
            EmpireState s = e.State;
            FrontDef front = _content.Front(a.Front);
            long now = _now();
            int hour = GTA.World.CurrentTimeOfDay.Hours;

            // Kill credit: 1 XP per 3 kills.
            foreach (KeyValuePair<int, int> kv in r.KillsByGuard)
            {
                GuardState g = e.Guard(kv.Key);
                if (g == null) continue;
                g.Kills += kv.Value;
                g.Xp += (g.Kills / 3) - ((g.Kills - kv.Value) / 3);
            }

            if (!r.Finished)
            {
                // Aborted: the rest of the squad is auto-resolved with what's left of it.
                if (r.AttackersLeft <= 0) { FinishLiveResolved(e, a, front, true, r, hour, now); return; }
                CrewDef crew = _content.Crew(a.Crew);
                CrewMember leader = Nemesis.Crew(s, a.Crew).Member(a.Leader);
                a.Squad = r.AttackersLeft;
                a.Strength = Resolver.AttackStrength(a.Squad, crew.SoldierPower, r.LeaderKilled ? 0 : Nemesis.LeaderBonus(leader),
                    leader != null && leader.LeaderTrait == LeaderTrait.Ruthless, Nemesis.Crew(s, a.Crew).Grudge);
                if (r.LeaderKilled && leader != null && leader.Alive)
                {
                    leader.Alive = false;
                    leader.AddLog("Killed at the " + front.ShortName + ".");
                    foreach (Nemesis.Event ev in Nemesis.OnMemberDied(s, _content, Nemesis.Crew(s, a.Crew), leader, front.ShortName, _rng(), s.Day))
                    {
                        string line = _content.Lines.Pick(_rng(), ev.Moment, ev.Tokens);
                        if (!string.IsNullOrEmpty(line)) _notifierPost(KindFor(ev.Moment), crew.Name, Title(ev.Moment), line);
                    }
                }
                Nemesis.Crew(s, a.Crew).Manpower = Math.Max(0, Nemesis.Crew(s, a.Crew).Manpower - r.AttackersLost);
                FinishAuto(e, a, front, hour, now, null, 0);
                return;
            }
            FinishLiveResolved(e, a, front, r.DefendersWon, r, hour, now);
        }

        private void FinishLiveResolved(Empire e, AttackState a, FrontDef front, bool won, LiveDefense.Result r, int hour, long now)
        {
            EmpireState s = e.State;
            CrewState cs = Nemesis.Crew(s, a.Crew);
            CrewDef crew = _content.Crew(a.Crew);
            CrewMember leader = cs.Member(a.Leader);
            var guards = new List<GuardState>();
            foreach (GuardState g in s.Guards) if (g.Front == a.Front && g.Status == GuardStatus.Active) guards.Add(g);

            Rules.Attacks.Outcome o = Rules.Attacks.Resolve(s, _content, e, a, _rng(), hour, now, _config.PermanentDeath, _config.ProtectedMode,
                won, r.AttackersLost, InjuryRecoveryMs);
            // Live rewards: 2 XP per surviving guard (Resolve already gave 1).
            if (won) foreach (GuardState g in guards) if (g.Status == GuardStatus.Active) { g.Xp++; Rules.Attacks.Promote(g); }

            if (leader != null)
            {
                if (r.LeaderKilled && leader.Alive)
                {
                    leader.Alive = false;
                    leader.AddLog("Killed at the " + front.ShortName + ".");
                    o.Events.AddRange(Nemesis.OnMemberDied(s, _content, cs, leader, front.ShortName, _rng(), s.Day));
                    _banners.Show("NEMESIS KILLED", leader.DisplayName + " won't be back.");
                    Sounds.Play(Cue.NemesisKilled);
                }
                else if (won && leader.Alive)
                {
                    leader.Rep += 1;
                    leader.Scars.Add("ran from the " + front.ShortName);
                    leader.AddLog("Ran from the " + front.ShortName + " with your guards shooting.");
                    leader.ReturnTextAt = now + Nemesis.ReturnTextDelayMs;
                    o.Events.Add(Nemesis.Ev("leaderEscaped", "leader", leader.DisplayName, "front", front.ShortName));
                }
            }
            Report(e, a, front, crew, leader, o, true);
        }

        // ---------------------------------------------------------------- auto-resolve and reporting

        private const long InjuryRecoveryMs = 96L * 60000;

        private void FinishAuto(Empire e, AttackState a, FrontDef front, int hour, long now, bool? live, int lost)
        {
            EmpireState s = e.State;
            CrewState cs = Nemesis.Crew(s, a.Crew);
            CrewDef crew = _content.Crew(a.Crew);
            CrewMember leader = cs != null ? cs.Member(a.Leader) : null;
            Rules.Attacks.Outcome o = Rules.Attacks.Resolve(s, _content, e, a, _rng(), hour, now, _config.PermanentDeath, _config.ProtectedMode, live, lost, InjuryRecoveryMs);
            Report(e, a, front, crew, leader, o, false);
        }

        private void Report(Empire e, AttackState a, FrontDef front, CrewDef crew, CrewMember leader, Rules.Attacks.Outcome o, bool wasThere)
        {
            Random rng = _rng();
            string guardName = "Your people";
            foreach (GuardState g in e.State.Guards) if (g.Front == a.Front && g.Status != GuardStatus.Dead) { guardName = g.First + " \"" + g.Nick + "\""; break; }
            var tokens = Tokens(crew, front, leader, guardName);
            tokens["lost"] = o.AttackersLost.ToString();

            if (o.DefendersWon)
            {
                _notifierPost(NoteKind.AttackResult, "The Office", front.ShortName + " held", _content.Lines.Pick(rng, "held", tokens));
                if (wasThere) _banners.Show("FRONT DEFENDED", "The " + crew.Name + " couldn't take the " + front.ShortName + ".");
                Sounds.Play(Cue.FrontDefended);
                if (leader != null && leader.LeaderTrait == LeaderTrait.Showboat && leader.Alive)
                    _notifierPost(NoteKind.Taunt, leader.DisplayName, crew.Name, _content.Lines.Pick(rng, "tauntAfterLoss", tokens));
            }
            else if (o.FrontDamaged)
            {
                _notifierPost(NoteKind.AttackResult, "The Office", front.ShortName + " damaged", _content.Lines.Pick(rng, "damaged", tokens));
            }
            else
            {
                _notifierPost(NoteKind.AttackResult, "The Office", front.ShortName + " lost", _content.Lines.Pick(rng, "lost", tokens));
                _banners.Show("FRONT LOST", "The " + crew.Name + " took the " + front.ShortName + ".");
                Sounds.Play(Cue.FrontLost);
                if (leader != null && leader.LeaderTrait == LeaderTrait.Showboat)
                    _notifierPost(NoteKind.Taunt, leader.DisplayName, crew.Name, _content.Lines.Pick(rng, "tauntAfterWin", tokens));
            }
            foreach (GuardState g in o.Dead)
                _notifierPost(NoteKind.GuardDeath, "The Office", "Guard down", _content.Lines.Pick(rng, "guardDied", Tokens(crew, front, leader, g.First + " \"" + g.Nick + "\"")));
            foreach (Nemesis.Event ev in o.Events)
            {
                string line = _content.Lines.Pick(rng, ev.Moment, Merge(tokens, ev.Tokens));
                if (!string.IsNullOrEmpty(line)) _notifierPost(KindFor(ev.Moment), crew.Name, Title(ev.Moment), line);
            }
            Log.Write("Director: " + a.Front + " " + (o.DefendersWon ? "held" : o.FrontDamaged ? "damaged" : "lost") + ", dead " + o.Dead.Count + ", injured " + o.Injured.Count
                      + ", attackers lost " + o.AttackersLost + (o.LeaderDied ? ", leader died" : o.LeaderEscaped ? ", leader escaped" : ""));
            _changed();
        }

        private static string Title(string moment)
        {
            switch (moment)
            {
                case "promotion": return "New lieutenant";
                case "newBoss": return "New boss";
                case "collapsed": return "Gone quiet";
                case "rebuilt": return "Back in business";
                case "leaderDied": return "Leader dead";
                case "leaderEscaped": return "Got away";
                case "counterStrike": return "Opening";
                default: return "Word";
            }
        }

        /// <summary>Leaders who escaped text you two in-game days later, quoting their scar.</summary>
        private void NemesisTexts(EmpireState s, long now)
        {
            foreach (CrewState cs in s.Crews)
                foreach (CrewMember m in cs.Members)
                {
                    if (!m.Alive || m.ReturnTextAt <= 0 || now < m.ReturnTextAt) continue;
                    m.ReturnTextAt = 0;
                    CrewDef crew = _content.Crew(cs.Id);
                    var t = new Dictionary<string, string>
                    {
                        { "leader", m.DisplayName }, { "crew", crew != null ? crew.Name : cs.Id },
                        { "scar", m.Scars.Count > 0 ? m.Scars[m.Scars.Count - 1] : "hurt me" },
                        { "front", m.Scars.Count > 0 ? m.Scars[m.Scars.Count - 1].Replace("put me down at the ", "").Replace("ran from the ", "") : "that night" }
                    };
                    _notifierPost(NoteKind.Nemesis, m.DisplayName, crew != null ? crew.Name : cs.Id, _content.Lines.Pick(_rng(), "nemesisReturns", t));
                    Log.Write("Nemesis: " + m.DisplayName + " is back");
                }
        }

        // ---------------------------------------------------------------- taking rival fronts

        private void OnCrewGuardDown(FrontDef front, string crewId, Ped ped, bool lastOne)
        {
            Empire e = _empire();
            if (e == null) return;
            CrewState cs = Nemesis.Crew(e.State, crewId);
            if (!lastOne)
            {
                if (cs != null) { cs.Manpower = Math.Max(0, cs.Manpower - 1); cs.LastContactDay = e.State.Day; }
                return;
            }
            _claimFront = front.Id;
            _claimCrew = crewId;
            _claimUntil = _now() + ClaimWindowMs;
            GTA.UI.Screen.ShowSubtitle("The " + front.ShortName + " is clear. Get to the door to claim it.", 5000);
            Log.Write("Director: " + front.Id + " cleared, claim window open");
        }

        /// <summary>Every tick while the gate is open: the claim prompt at a cleared rival front's door.</summary>
        public void UpdateFrame()
        {
            if (_claimFront == null) return;
            Empire e = _empire();
            if (e == null || _now() > _claimUntil || e.OwnerOf(_claimFront) != _claimCrew) { _claimFront = null; return; }
            FrontDef front = _content.Front(_claimFront);
            Vector3 p = Game.Player.Character.Position;
            if (front.Door.DistanceTo(p.X, p.Y, p.Z) > ClaimRange) return;
            GTA.UI.Screen.ShowHelpTextThisFrame("Press ~INPUT_CONTEXT~ to claim the " + front.ShortName + ".");
            if (!Game.IsControlJustPressed(Control.Context)) return;

            FrontState fs = e.Find(front.Id);
            CrewDef crew = _content.Crew(_claimCrew);
            Rules.Attacks.TakeFront(e.State, e, fs, _claimCrew);
            _claimFront = null;
            _banners.Show("FRONT TAKEN", "The " + front.Name + " is yours.");
            Sounds.Play(Cue.FrontTaken);
            _notifierPost(NoteKind.FrontChange, "The Broker", front.ShortName, _content.Lines.Pick(_rng(), "frontTaken", Tokens(crew, front, null, null)));
            Log.Write("Director: player took " + front.Id + " from " + crew.Name);
            _changed();
        }

        // ---------------------------------------------------------------- helpers

        private Dictionary<string, string> Tokens(CrewDef crew, FrontDef front, CrewMember leader, string guard)
        {
            return new Dictionary<string, string>
            {
                { "crew", crew != null ? crew.Name : "" },
                { "front", front != null ? front.ShortName : "" },
                { "area", front != null ? front.Area : "" },
                { "leader", leader != null ? leader.DisplayName : "their leader" },
                { "guard", guard ?? "Your people" }
            };
        }

        private static Dictionary<string, string> Merge(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            var d = new Dictionary<string, string>(a);
            foreach (var kv in b) d[kv.Key] = kv.Value;
            return d;
        }

        private void _notifierPost(NoteKind kind, string sender, string subject, string message)
        {
            _notifier.Send(kind, sender, subject, message);
        }

        private static NoteKind KindFor(string moment) { return moment == "counterStrike" ? NoteKind.StreetWord : NoteKind.Nemesis; }

        private static void DevNotice(string text) { GTA.UI.Notification.Show("Fronts & Rivals dev: " + text); }
    }
}
