// Fronts & Rivals: runs each system at its own rate, and switches a system off
// if it throws 3 times within 60 seconds, so one bug can't crash the game.
using System;
using System.Collections.Generic;
using GTA;

namespace FrontsAndRivals.Core
{
    public sealed class Scheduler
    {
        private sealed class Job
        {
            public string Name;
            public int PeriodMs;
            public int NextAt;
            public Action Run;
            public int Failures;
            public int WindowStart;
            public bool Disabled;
        }

        private readonly List<Job> _jobs = new List<Job>();

        public void Add(string name, int periodMs, Action run)
        {
            _jobs.Add(new Job { Name = name, PeriodMs = periodMs, Run = run });
        }

        /// <summary>Call once per tick.</summary>
        public void Update()
        {
            int now = Game.GameTime; // milliseconds since the game started
            for (int i = 0; i < _jobs.Count; i++)
            {
                Job job = _jobs[i];
                if (job.Disabled || now < job.NextAt) continue;
                job.NextAt = now + job.PeriodMs;
                try
                {
                    job.Run();
                }
                catch (Exception ex)
                {
                    if (now - job.WindowStart > 60000) { job.WindowStart = now; job.Failures = 0; }
                    job.Failures++;
                    Log.Write("ERROR in " + job.Name + ": " + ex);
                    if (job.Failures >= 3)
                    {
                        job.Disabled = true;
                        GTA.UI.Notification.Show("Fronts & Rivals: " + job.Name + " paused, see log");
                    }
                }
            }
        }
    }
}
