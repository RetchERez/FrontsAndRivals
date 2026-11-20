// Fronts & Rivals: keeps the feed from getting spammy. Every message the world sends you (not replies to your
// own menu actions) goes through this queue:
//   - a minimum gap between messages (INI MinSecondsBetween, default 20 s); important ones jump the line
//   - the notification level drops whole classes (All / Important / Minimal) and switches drop kinds
//   - low-priority gossip that waited too long is dropped instead of arriving stale
// Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;

namespace FrontsAndRivals.Core
{
    public enum NoteKind
    {
        AttackWarning,  // a crew is coming for your front
        AttackResult,   // held, lost, damaged
        FrontChange,    // you bought or took a front
        GuardDeath,
        Taunt,          // Showboat leaders
        Nemesis,        // a leader is back, promoted, died, crew collapsed or rebuilt
        StreetWord,     // turf moves between crews, counter-strike openings
        Midnight,       // the nightly take
        Staff,          // a guard quit over wages
        Broker          // the Broker's messages
    }

    public enum NoteLevel { All, Important, Minimal }

    public sealed class NoteSettings
    {
        public NoteLevel Level = NoteLevel.All;
        public int MinSecondsBetween = 20;
        public bool Taunts = true;
        public bool StreetWord = true;
        public bool MidnightReport = true;
    }

    public sealed class Note
    {
        public NoteKind Kind;
        public string Picture, Sender, Subject, Message;
        public long QueuedAt;
    }

    public sealed class NotifyQueue
    {
        public const int MaxQueued = 12;
        public const long StaleLowMs = 5 * 60000;

        private readonly List<Note> _queue = new List<Note>();
        private long _lastSent = long.MinValue / 2;

        public int Count { get { return _queue.Count; } }

        /// <summary>0 = must arrive (attack warnings and results, fronts changing hands, the Broker),
        /// 1 = story (deaths, nemesis, taunts, midnight, quitting), 2 = gossip (street word).</summary>
        public static int Priority(NoteKind k)
        {
            switch (k)
            {
                case NoteKind.AttackWarning:
                case NoteKind.AttackResult:
                case NoteKind.FrontChange:
                case NoteKind.Broker: return 0;
                case NoteKind.StreetWord: return 2;
                default: return 1;
            }
        }

        /// <summary>Does this kind get through at all with these settings?</summary>
        public static bool Allowed(NoteKind k, NoteSettings s)
        {
            if (k == NoteKind.Taunt && !s.Taunts) return false;
            if (k == NoteKind.StreetWord && !s.StreetWord) return false;
            if (k == NoteKind.Midnight && !s.MidnightReport) return false;
            int p = Priority(k);
            if (s.Level == NoteLevel.Minimal) return p == 0;
            if (s.Level == NoteLevel.Important) return p <= 1 && k != NoteKind.Taunt;
            return true;
        }

        /// <summary>Queues a message. Returns false if the settings filtered it out.</summary>
        public bool Enqueue(Note n, NoteSettings s, long nowMs)
        {
            if (n == null || string.IsNullOrEmpty(n.Message) || !Allowed(n.Kind, s)) return false;
            n.QueuedAt = nowMs;
            _queue.Add(n);
            // Too many waiting: drop the least important, oldest first.
            while (_queue.Count > MaxQueued)
            {
                int worst = 0;
                for (int i = 1; i < _queue.Count; i++)
                    if (Priority(_queue[i].Kind) > Priority(_queue[worst].Kind)) worst = i;
                _queue.RemoveAt(worst);
            }
            return true;
        }

        /// <summary>The next message to show now, or null while the gap hasn't passed or nothing is waiting.</summary>
        public Note Next(NoteSettings s, long nowMs)
        {
            // Stale gossip goes.
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (Priority(_queue[i].Kind) == 2 && nowMs - _queue[i].QueuedAt > StaleLowMs) _queue.RemoveAt(i);
            if (_queue.Count == 0) return null;
            if (nowMs - _lastSent < Math.Max(0, s.MinSecondsBetween) * 1000L) return null;

            int best = 0;
            for (int i = 1; i < _queue.Count; i++)
            {
                int pi = Priority(_queue[i].Kind), pb = Priority(_queue[best].Kind);
                if (pi < pb || (pi == pb && _queue[i].QueuedAt < _queue[best].QueuedAt)) best = i;
            }
            Note n = _queue[best];
            _queue.RemoveAt(best);
            _lastSent = nowMs;
            return n;
        }

        /// <summary>Something was shown outside the queue (a mugshot message): the gap starts now.</summary>
        public void MarkSent(long nowMs) { _lastSent = nowMs; }

        public void Clear() { _queue.Clear(); }
    }
}
