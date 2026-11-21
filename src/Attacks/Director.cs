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

        /// <summary>Dev tools: start an attack on one of your fronts right away.</summary>
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
            if (s.Raid != null && now >= s.Raid.EndsMs) FinishRaid(e, hour, now);

            if (s.Attack == null)
            {
                bool force = _forceRequested;
                if (force || now >= _nextPlanRoll)
                {
                    _nextPlanRoll = now + Rules.Attacks.Minute;
                    bool can = force ? s.Attack == null
                             : Rules.Attacks.CanPlan(s, now, _sinceLoad(), Frequency, _config.CooldownMinutes, _config.FirstAttackDelayMinutes, _config.EmpirePressure);
                    if (can && (force || _rng().NextDouble() < Rules.Attacks.PlanChancePerMinute)) Plan(e, hour, now, force);
                    _forceRequested = false;
                }
                return;
            }

            AttackState a = s.Attack;
            FrontDef front = _content.Front(a.Front);
            if (front == null || !e.PlayerOwns(a.Front))
            {
                Log.Write("Director: target " + a.Front + " no longer yours, attack called off");
                if (_live.Active) { _live.Abort("called off"); _live.ForceClear(); }
                s.Attack = null;
                _changed();
                return;
            }

            if (a.Phase == AttackPhase.Warning)
            {
                // Only an attack saved by an older version is ever in this phase: start it now, with the alert.
                a.Phase = AttackPhase.Window;
                a.WindowEnds = Math.Max(a.WindowEnds, now + Rules.Attacks.DefaultHoldMinutes * Rules.Attacks.Minute);
                Alert(e, a, now);
            }
            if (a.Phase == AttackPhase.Window)
            {
                Vector3 p = Game.Player.Character.Position;
                bool liveOn = _config.LiveDefense && !State.LiveDefenseOff;
                bool near = liveOn && front.IsScouted && front.Door.DistanceTo(p.X, p.Y, p.Z) < Rules.Attacks.LiveRange;
                if (near && _liveFailed != a && StartLive(e, a, front)) return;
                float dist = front.Door.DistanceTo(p.X, p.Y, p.Z);
                if (now >= a.WindowEnds && liveOn && !a.Waited && _liveFailed != a && front.IsScouted
                    && dist >= Rules.Attacks.LiveRange && dist < Rules.Attacks.OnTheWayRange)
                {
                    // You're on your way: the crew gathers outside a little longer, once.
                    a.Waited = true;
                    a.WindowEnds = now + Rules.Attacks.OnTheWayMs;
                    CrewDef waiting = _content.Crew(a.Crew);
                    GTA.UI.Screen.ShowSubtitle("Your people are barely holding the " + front.ShortName + " against the ~r~" + (waiting != null ? waiting.Name : "crew") + "~s~. Get there.", 6000);
                }
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
            int bonus = _config.EmpirePressure ? Rules.Attacks.PressureSquadBonus(Rules.Attacks.HeldCount(s)) : 0;
            AttackState a = Rules.Attacks.Plan(s, _content, e, _rng(), _config.HoldMinutes, hour, now, bonus);
            if (a == null) { if (force) DevNotice("No crew can attack right now (do you own a front?)."); return; }
            s.Attack = a;
            CrewDef crew = _content.Crew(a.Crew);
            CrewMember leader = Nemesis.Crew(s, a.Crew).Member(a.Leader);
            FrontDef front = _content.Front(a.Front);
            Log.Write("Director: " + crew.Name + " led by " + leader.DisplayName + " will hit " + a.Front + " (squad " + a.Squad + ", A " + a.Strength.ToString("0") + ")");

            Alert(e, a, now);
        }

        /// <summary>The attack is on: one alert from the Office, with how long your people can hold and what it pays to be there.</summary>
        private void Alert(Empire e, AttackState a, long now)
        {
            EmpireState s = e.State;
            CrewDef crew = _content.Crew(a.Crew);
            CrewMember leader = Nemesis.Crew(s, a.Crew).Member(a.Leader);
            FrontDef front = _content.Front(a.Front);
            if (crew == null || front == null) return;
            var tokens = Tokens(crew, front, leader, null);
            Sounds.Play(Cue.AttackStarted);
            Tips.Show(Tip.Attack);
            int minutes = (int)Math.Max(1, Math.Round((a.WindowEnds - now) / (double)Rules.Attacks.Minute));
            bool live = _config.LiveDefense && !s.LiveDefenseOff;
            string text = "The " + crew.Name + " are hitting the " + front.ShortName + " right now. Your people can hold about " + minutes
                + (minutes == 1 ? " minute." : " minutes.");
            if (live) text += " Get there and hold the door yourself: the Broker pays $" + Economy.DefendBonus(e.FrontIncome(front.Id), _config.IncomeMultiplier).ToString("N0") + "."
                + (s.RouteOff || s.HideBlips ? "" : " Route's on your GPS.");
            _notifierPost(NoteKind.AttackWarning, "The Office", front.ShortName + " under attack", text);
            if (leader != null && leader.LeaderTrait == LeaderTrait.Showboat)
                _notifierPost(NoteKind.Taunt, leader.DisplayName, crew.Name, _content.Lines.Pick(_rng(), "taunt", tokens));
            Nemesis.Crew(s, a.Crew).LastMove = "Hitting your " + front.ShortName;
            Log.Write("Director: " + crew.Name + " hitting " + a.Front + ", guards hold " + minutes + " min");
            _changed();
        }

        // ---------------------------------------------------------------- the hold timer (GTA-style bar while you race there)

        private readonly LemonUI.ObjectPool _holdPool = new LemonUI.ObjectPool();
        private readonly LemonUI.TimerBars.TimerBarCollection _holdBars = new LemonUI.TimerBars.TimerBarCollection();
        private readonly LemonUI.TimerBars.TimerBar _holdBar = new LemonUI.TimerBars.TimerBar("HOLDING", "0:00");
        private bool _holdReady;

        /// <summary>Every frame with the gate open: "CAR WASH HOLDS 3:42" while your people hold and you're not there yet.</summary>
        public void DrawHold()
        {
            if (!_holdReady) { _holdBars.Add(_holdBar); _holdPool.Add(_holdBars); _holdReady = true; }
            Empire e = _empire();
            AttackState a = e != null ? e.State.Attack : null;
            bool show = a != null && a.Phase == AttackPhase.Window && !_live.Active;
            if (show)
            {
                FrontDef front = _content.Front(a.Front);
                long left = Math.Max(0, a.WindowEnds - _now());
                _holdBar.Title = ((front != null ? front.ShortName : "FRONT") + " holds").ToUpperInvariant();
                _holdBar.Info = (left / 60000) + ":" + ((left / 1000) % 60).ToString("00");
            }
            _holdBars.Visible = show;
            _holdPool.Process();
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
                Stats.Add(g, Stats.Kills(kv.Value));
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
            foreach (GuardState g in s.Guards) if (g.Front == a.Front && g.Status == GuardStatus.Active && !g.Escort && !e.OnRaid(g)) guards.Add(g);

            Rules.Attacks.Outcome o = Rules.Attacks.Resolve(s, _content, e, a, _rng(), hour, now, e.PermanentDeath(_config.PermanentDeath), _config.ProtectedMode,
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
            Rules.Attacks.Outcome o = Rules.Attacks.Resolve(s, _content, e, a, _rng(), hour, now, e.PermanentDeath(_config.PermanentDeath), _config.ProtectedMode, live, lost, InjuryRecoveryMs);
            Report(e, a, front, crew, leader, o, false);
        }

        private void Report(Empire e, AttackState a, FrontDef front, CrewDef crew, CrewMember leader, Rules.Attacks.Outcome o, bool wasThere)
        {
            Random rng = _rng();
            string guardName = "Your people";
            foreach (GuardState g in e.State.Guards) if (g.Front == a.Front && g.Status != GuardStatus.Dead) { guardName = g.ShortName; break; }
            var tokens = Tokens(crew, front, leader, guardName);
            tokens["lost"] = o.AttackersLost.ToString();

            if (o.DefendersWon)
            {
                _notifierPost(NoteKind.AttackResult, "The Office", front.ShortName + " held", _content.Lines.Pick(rng, "held", tokens));
                if (wasThere)
                {
                    int bonus = Economy.DefendBonus(e.FrontIncome(front.Id), _config.IncomeMultiplier);
                    Game.Player.Money += bonus;
                    _banners.Show("FRONT DEFENDED", "The " + crew.Name + " couldn't take the " + front.ShortName + ". The Broker pays you $" + bonus.ToString("N0") + ".");
                    Log.Write("Director: defended in person, bonus $" + bonus);
                }
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
                _notifierPost(NoteKind.GuardDeath, "The Office", "Guard down", _content.Lines.Pick(rng, "guardDied", Tokens(crew, front, leader, g.ShortName)));
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

        // ---------------------------------------------------------------- raids

        /// <summary>Word comes back from a raid: the front is yours, or your people come home beaten.</summary>
        private void FinishRaid(Empire e, int hour, long now)
        {
            Rules.Raids.Outcome o = Rules.Raids.Resolve(e.State, _content, e, _rng(), hour, now, e.PermanentDeath(_config.PermanentDeath), InjuryRecoveryMs);
            FrontDef front = _content.Front(o.FrontId);
            CrewDef crew = _content.Crew(o.CrewId);
            if (front == null || crew == null) { _changed(); return; }
            var lost = new List<string>();
            foreach (GuardState g in o.Dead) lost.Add(g.ShortName + " (dead)");
            foreach (GuardState g in o.Injured) lost.Add(g.ShortName + " (hurt)");
            string cost = lost.Count == 0 ? " Everyone came back." : " " + string.Join(", ", lost.ToArray()) + ".";
            if (o.Won)
            {
                Sounds.Play(Cue.FrontTaken);
                if (!CheckRuledAll(e)) _banners.Show("FRONT TAKEN", "Your crew took the " + front.Name + " from the " + crew.Name + ".");
                _notifierPost(NoteKind.FrontChange, "The Office", front.ShortName + " is ours", "The raid worked: the " + crew.Name + " are out of the " + front.ShortName + "." + cost + " Post some guards there.");
            }
            else
            {
                Sounds.Play(Cue.FrontLost);
                _notifierPost(NoteKind.AttackResult, "The Office", "Raid failed", "The " + crew.Name + " held the " + front.ShortName + "." + cost);
            }
            foreach (GuardState g in o.Dead)
                _notifierPost(NoteKind.GuardDeath, "The Office", "Guard down", _content.Lines.Pick(_rng(), "guardDied", Tokens(crew, front, null, g.ShortName)));
            Log.Write("Director: raid on " + o.FrontId + (o.Won ? " won" : " failed") + ", dead " + o.Dead.Count + ", hurt " + o.Injured.Count);
            _changed();
        }

        /// <summary>Called when you buy a front at its door (Main plays the establishing shot).</summary>
        public Action<FrontDef> OnPurchased;

        /// <summary>A new empire (Office > Settings > Start a new empire): forget everything tied to the old one's clock.</summary>
        public void Reset()
        {
            _nextPlanRoll = 0;
            _claimFront = null;
            _claimCrew = null;
            _claimUntil = 0;
            _liveFailed = null;
            _forceRequested = false;
        }

        /// <summary>Every tick while the gate is open: the claim prompt at a cleared rival front's door.</summary>
        public void UpdateFrame()
        {
            BuyPrompt();
            if (_claimFront == null) return;
            Empire e = _empire();
            if (e == null || _now() > _claimUntil || e.OwnerOf(_claimFront) != _claimCrew) { _claimFront = null; return; }
            FrontDef front = _content.Front(_claimFront);
            Vector3 p = Game.Player.Character.Position;
            if (front.Door.DistanceTo(p.X, p.Y, p.Z) > ClaimRange) return;
            if (Rules.Attacks.DugIn(e.State, e.Find(front.Id)))
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("The " + front.ShortName + " is clear, but they only just took it. Claim it back after midnight.");
                return;
            }
            if (!e.Trusted(front))
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("The " + front.ShortName + " is clear, but the Broker won't back your claim until you hold " + Economy.TrustNeeded(front.Tier) + " fronts.");
                return;
            }
            GTA.UI.Screen.ShowHelpTextThisFrame("Press ~INPUT_CONTEXT~ to claim the " + front.ShortName + ".");
            if (!Game.IsControlJustPressed(Control.Context)) return;

            FrontState fs = e.Find(front.Id);
            CrewDef crew = _content.Crew(_claimCrew);
            Rules.Attacks.TakeFront(e.State, e, fs, _claimCrew);
            Rules.Attacks.GracePeriod(e.State, front.Id, _now());
            _claimFront = null;
            if (!CheckRuledAll(e)) _banners.Show("FRONT TAKEN", "The " + front.Name + " is yours.");
            Sounds.Play(Cue.FrontTaken);
            _notifierPost(NoteKind.FrontChange, "The Broker", front.ShortName, _content.Lines.Pick(_rng(), "frontTaken", Tokens(crew, front, null, null)));
            Log.Write("Director: player took " + front.Id + " from " + crew.Name);
            _changed();
        }

        // ---------------------------------------------------------------- buying at the door

        private const float BuyRange = 3f;

        /// <summary>Fronts for sale are bought in person, like GTA's own properties: walk up to the door on foot.</summary>
        private void BuyPrompt()
        {
            Empire e = _empire();
            if (e == null) return;
            Ped me = Game.Player.Character;
            if (me.IsInVehicle() || me.IsDead) return;
            Vector3 p = me.Position;
            FrontDef front = null;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Door.IsSet || !e.CanBuy(def.Id)) continue;
                float d = def.Door.DistanceTo(p.X, p.Y, p.Z);
                if (d < 30f) Tips.Show(Tip.BuyAtDoor);
                if (d <= BuyRange) { front = def; break; }
            }
            if (front == null) return;
            if (!e.Trusted(front))
            {
                GTA.UI.Screen.ShowHelpTextThisFrame("The Broker won't sell you the " + front.Name + " until you hold " + Economy.TrustNeeded(front.Tier) + " fronts.");
                return;
            }
            int price = Economy.FrontPrice(front, _config.FrontPriceDays);
            GTA.UI.Screen.ShowHelpTextThisFrame("Press ~INPUT_CONTEXT~ to buy the " + front.Name + " for $" + price.ToString("N0") + ".");
            if (!Game.IsControlJustPressed(Control.Context)) return;
            if (Game.Player.Money < price)
            {
                GTA.UI.Notification.Show("The Broker wants $" + price.ToString("N0") + " for the " + front.Name + ".");
                return;
            }
            Game.Player.Money -= price;
            e.SetOwner(front.Id, Owners.Player);
            Rules.Attacks.GracePeriod(e.State, front.Id, _now());
            int stayed = e.StarterCrew(front, _rng());
            Sounds.Play(Cue.FrontTaken);
            if (!CheckRuledAll(e)) _banners.Show("FRONT PURCHASED", "The " + front.Name + " is yours. " + (stayed > 0 ? "Two of the old crew stayed on." : "Post some guards from the Office."));
            _notifierPost(NoteKind.FrontChange, "The Broker", front.ShortName, stayed > 0
                ? "It's yours, and two of the old owner's people are staying on. They've only got knives: arm them before somebody gets ideas."
                : "It's yours. Put some people on it before somebody else gets ideas.");
            Log.Write("Director: player bought " + front.Id + " at the door for " + price);
            if (OnPurchased != null) OnPurchased(front);
            _changed();
        }

        /// <summary>The endgame moment, the first time every front in the city is yours (bought or taken). Shows its banner
        /// and returns true then. The crews still recruit off the street and will try to take fronts back.</summary>
        private bool CheckRuledAll(Empire e)
        {
            if (e.State.RuledAll) return false;
            foreach (FrontDef def in _content.Fronts)
                if (def.Door.IsSet && !e.PlayerOwns(def.Id)) return false;
            e.State.RuledAll = true;
            _banners.Show("LOS SANTOS IS YOURS", "Every front in the city answers to you. The crews won't stay gone.");
            Log.Write("Director: player holds every front");
            return true;
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
