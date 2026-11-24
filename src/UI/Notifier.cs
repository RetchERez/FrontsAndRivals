// Fronts & Rivals: feed messages. Everything the world sends you goes through Send(), which queues it
// (NotifyQueue: priorities, a minimum gap, the notification level and switches) and releases one at a time.
// Messages wait while you're in a mission or cutscene. They show as general notifications, never cut off.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using FrontsAndRivals.Core;

namespace FrontsAndRivals.UI
{
    public sealed class Notifier
    {
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

        /// <summary>Queue a message from the world (warnings, results, texts). Filtered and spaced out by the settings.</summary>
        public void Send(NoteKind kind, string sender, string subject, string message)
        {
            Remember(sender, subject, message);
            if (string.IsNullOrEmpty(message)) return;
            _queue.Enqueue(new Note { Kind = kind, Picture = "CHAR_DEFAULT", Sender = sender, Subject = subject, Message = message },
                           _settings(), Game.GameTime);
        }

        /// <summary>A general notification right now (the game's plain feed, not a text message): the subject in bold on
        /// the first line, who it's from in brackets when it isn't the Office, then the message. Long messages are sent in
        /// 99-character pieces (the most the game takes in one piece), so nothing is ever cut off.</summary>
        public static void Post(string picture, string sender, string subject, string message)
        {
            string head = subject ?? "";
            if (!string.IsNullOrEmpty(sender) && sender != "The Office" && sender != subject) head += " (" + sender + ")";
            string text = (head.Length > 0 ? "~h~" + head + "~h~~n~" : "") + (message ?? "");
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "CELL_EMAIL_BCON"); // a label made of up to 10 pieces
            foreach (string piece in NotifyQueue.Pieces(text, 99, 10)) Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, piece);
            // NativeDB: blink, showInBrief (so it's also in the pause menu's Brief)
            Function.Call<int>(Hash.END_TEXT_COMMAND_THEFEED_POST_TICKER, false, true);
        }

        /// <summary>A guard's death or injury. Respects the notification settings.</summary>
        public void PostWithMugshot(Ped ped, string sender, string subject, string message)
        {
            if (!NotifyQueue.Allowed(NoteKind.GuardDeath, _settings())) return;
            _queue.MarkSent(Game.GameTime);
            Post(null, sender, subject, message);
        }

        /// <summary>Every tick, also while paused (the queue only releases when not held).</summary>
        public void Update()
        {
            if (!Hold)
            {
                Note n = _queue.Next(_settings(), Game.GameTime);
                if (n != null) Post(n.Picture, n.Sender, n.Subject, n.Message);
            }
        }
    }
}
