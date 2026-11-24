// Fronts & Rivals: feed messages. Everything the world sends you goes through Send(), which queues it
// (NotifyQueue: priorities, a minimum gap, the notification level and switches) and releases one at a time.
// Messages wait while you're in a mission or cutscene. Mugshot messages show the real guard's face. Long texts are
// sent in pieces, so they're never cut off.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using FrontsAndRivals.Core;

namespace FrontsAndRivals.UI
{
    public sealed class Notifier
    {
        private sealed class PendingShot
        {
            public int Handle;
            public string Sender, Subject, Message;
            public int StartedAt;
        }

        private readonly List<PendingShot> _shots = new List<PendingShot>();
        private readonly NotifyQueue _queue = new NotifyQueue();

        /// <summary>Drops everything still waiting to be sent (a new empire starting).</summary>
        public void Clear() { _queue.Clear(); }

        public const int LogSize = 25;
        private readonly System.Collections.Generic.List<FrontsAndRivals.Data.MessageEntry> _fallback = new System.Collections.Generic.List<FrontsAndRivals.Data.MessageEntry>();

        /// <summary>Where the Office's Messages page lives: the empire's save (Main supplies it), so it survives a reload.</summary>
        public Func<System.Collections.Generic.List<FrontsAndRivals.Data.MessageEntry>> Store;

        /// <summary>The in-game day stamped on each message (Main supplies it).</summary>
        public Func<int> Day = () => 0;

        /// <summary>Newest first. GTA's own feed isn't archived, so the Office is.</summary>
        public System.Collections.Generic.List<FrontsAndRivals.Data.MessageEntry> Log
        {
            get { var s = Store != null ? Store() : null; return s ?? _fallback; }
        }

        /// <summary>Keeps a message for the Office without showing it again.</summary>
        public void Remember(string sender, string subject, string message)
        {
            string when = "";
            try { var t = GTA.World.CurrentTimeOfDay; when = t.Hours.ToString("00") + ":" + t.Minutes.ToString("00"); } catch { }
            var log = Log;
            log.Insert(0, new FrontsAndRivals.Data.MessageEntry { Sender = sender, Subject = subject, Message = message, When = when, Day = Day() });
            while (log.Count > LogSize) log.RemoveAt(log.Count - 1);
        }
        private Func<NoteSettings> _settings = () => new NoteSettings();

        /// <summary>True while a mission, cutscene or switch is on: queued messages wait.</summary>
        public bool Hold;

        public void UseSettings(Func<NoteSettings> settings) { _settings = settings; }

        /// <summary>This kind of message gets through the current settings.</summary>
        public bool Allows(NoteKind kind) { return NotifyQueue.Allowed(kind, _settings()); }

        /// <summary>Queue a message from the world (warnings, results, texts). Filtered and spaced out by the settings.</summary>
        public void Send(NoteKind kind, string sender, string subject, string message)
        {
            Remember(sender, subject, message);
            if (string.IsNullOrEmpty(message)) return;
            _queue.Enqueue(new Note { Kind = kind, Picture = "CHAR_DEFAULT", Sender = sender, Subject = subject, Message = message },
                           _settings(), Game.GameTime);
        }

        /// <summary>Plain feed message right now, with a stock contact picture. Use for replies to the player's own actions.</summary>
        public static void Post(string picture, string sender, string subject, string message)
        {
            // Long texts go in 99-character pieces (the most the game takes in one piece) under a label made of up to
            // 10 pieces, so nothing is ever cut off.
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "CELL_EMAIL_BCON");
            foreach (string piece in NotifyQueue.Pieces(message ?? "", 99, 10)) Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, piece);
            // NativeDB: txdName, textureName, flash, iconType, sender, subject
            Function.Call<int>(Hash.END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT, picture, picture, false, 1, sender, subject);
        }

        /// <summary>A guard's death or injury with their real face. Respects the notification settings.</summary>
        public void PostWithMugshot(Ped ped, string sender, string subject, string message)
        {
            if (!NotifyQueue.Allowed(NoteKind.GuardDeath, _settings())) return;
            _queue.MarkSent(Game.GameTime);
            if (ped == null || !ped.Exists()) { Post("CHAR_DEFAULT", sender, subject, message); return; }
            int handle = Function.Call<int>(Hash.REGISTER_PEDHEADSHOT, ped);
            _shots.Add(new PendingShot { Handle = handle, Sender = sender, Subject = subject, Message = message, StartedAt = Game.GameTime });
        }

        /// <summary>Every tick, also while paused (mugshots finish; the queue only releases when not held).</summary>
        public void Update()
        {
            if (!Hold)
            {
                Note n = _queue.Next(_settings(), Game.GameTime);
                if (n != null) Post(n.Picture, n.Sender, n.Subject, n.Message);
            }
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                PendingShot s = _shots[i];
                bool ready = Function.Call<bool>(Hash.IS_PEDHEADSHOT_READY, s.Handle)
                          && Function.Call<bool>(Hash.IS_PEDHEADSHOT_VALID, s.Handle);
                if (ready)
                {
                    string txd = Function.Call<string>(Hash.GET_PEDHEADSHOT_TXD_STRING, s.Handle);
                    Post(txd, s.Sender, s.Subject, s.Message);
                    Function.Call(Hash.UNREGISTER_PEDHEADSHOT, s.Handle);
                    _shots.RemoveAt(i);
                }
                else if (Game.GameTime - s.StartedAt > 3000)
                {
                    Function.Call(Hash.UNREGISTER_PEDHEADSHOT, s.Handle);
                    Post("CHAR_DEFAULT", s.Sender, s.Subject, s.Message); // fall back, never lose the message
                    _shots.RemoveAt(i);
                }
            }
        }
    }
}
