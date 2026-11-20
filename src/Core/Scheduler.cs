// Fronts & Rivals: runs each system at its own rate on one free-roam clock.
// - Gated jobs only run while the pause gate is open, and their timers freeze while it's closed.
// - Every job runs inside try/catch. 3 failures within 60 s switches that job off for the session.
// Pure: no game calls (the caller passes the time in), so it is unit-tested.
using System;
using System.Collections.Generic;

namespace FrontsAndRivals.Core
{
    public sealed class Scheduler
    {
        /// <summary>Longest step the free-roam clock takes in one tick, so a long hitch or a pause menu can't fast-forward timers.</summary>
        public const int MaxStepMs = 1000;

        public delegate void FailureHandler(string jobName, string displayName, Exception error, bool disabledNow);

        private sealed class Job
        {
            public string Name;
            public string DisplayName;
            public int PeriodMs;
            public long NextAt;
            public Action Run;
            public bool RunWhilePaused;
            public bool Disabled;
            public readonly FailureWindow Failures = new FailureWindow();
        }

        private readonly List<Job> _jobs = new List<Job>();
        private readonly FailureHandler _onFailure;
        private long _activeMs;
        private int _lastRealMs;
        private bool _hasLast;

        public Scheduler(FailureHandler onFailure)
        {
            _onFailure = onFailure;
        }

        /// <summary>Milliseconds of open-gate time since the script started. Frozen while paused.</summary>
        public long ActiveMs { get { return _activeMs; } }

        /// <param name="name">Log name, e.g. "Director".</param>
        /// <param name="displayName">What the player reads if it switches off, e.g. "attacks".</param>
        /// <param name="periodMs">0 = every tick.</param>
        /// <param name="runWhilePaused">UI jobs run even while the gate is closed, so menus never freeze.</param>
        public void Add(string name, string displayName, int periodMs, Action run, bool runWhilePaused = false)
        {
            _jobs.Add(new Job
            {
                Name = name,
                DisplayName = displayName,
                PeriodMs = periodMs,
                Run = run,
                RunWhilePaused = runWhilePaused,
                NextAt = 0
            });
        }

        public bool IsDisabled(string name)
        {
            for (int i = 0; i < _jobs.Count; i++)
                if (_jobs[i].Name == name) return _jobs[i].Disabled;
            return false;
        }

        /// <summary>Call once per tick.</summary>
        /// <param name="realNowMs">A real-time millisecond clock (Game.GameTime in the game).</param>
        /// <param name="gateOpen">PauseGate result for this tick.</param>
        public void Update(int realNowMs, bool gateOpen)
        {
            if (_hasLast && gateOpen)
            {
                int step = realNowMs - _lastRealMs;
                if (step < 0) step = 0;
                if (step > MaxStepMs) step = MaxStepMs;
                _activeMs += step;
            }
            _lastRealMs = realNowMs;
            _hasLast = true;

            for (int i = 0; i < _jobs.Count; i++)
            {
                Job job = _jobs[i];
                if (job.Disabled) continue;

                if (job.RunWhilePaused)
                {
                    // Paused-safe jobs are UI, timed on the real clock.
                    if (job.PeriodMs > 0 && realNowMs < job.NextAt) continue;
                    if (job.PeriodMs > 0) job.NextAt = realNowMs + job.PeriodMs;
                }
                else
                {
                    if (!gateOpen) continue;
                    if (_activeMs < job.NextAt) continue;
                    job.NextAt = _activeMs + job.PeriodMs;
                }

                try
                {
                    job.Run();
                }
                catch (Exception ex)
                {
                    bool disable = job.Failures.Record(realNowMs);
                    if (disable) job.Disabled = true;
                    if (_onFailure != null)
                    {
                        try { _onFailure(job.Name, job.DisplayName, ex, disable); } catch { }
                    }
                }
            }
        }
    }
}
