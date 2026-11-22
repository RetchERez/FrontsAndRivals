// Fronts & Rivals: first-time tips, in GTA's own help box (top left), each shown once per save.
// Office > Settings > "Show tips again" brings them back.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.UI
{
    public enum Tip { Office = 1, Posting = 2, Attack = 4, BuyAtDoor = 8, Bodyguard = 16, Warned = 32 }

    public static class Tips
    {
        private const int ShowMs = 9000;

        /// <summary>The save the "seen" flags live in. Set by Main.</summary>
        public static Func<EmpireState> State;

        private static readonly Queue<string> _queue = new Queue<string>();
        private static int _busyUntil;

        public static string Text(Tip t)
        {
            switch (t)
            {
                case Tip.Office: return "This is the Office. ~INPUT_FRONTEND_ACCEPT~ opens a section, ~INPUT_FRONTEND_CANCEL~ goes back. Fronts lists every business, Guards your people, Hire Recruits today's new faces.";
                case Tip.Posting: return "New hires wait in the reserve. Open one of your fronts and choose Fill Empty Posts, or pick a post on the guard's own page.";
                case Tip.Attack: return "One of your fronts is under attack. Your guards hold for a few minutes (the timer, bottom right): get there to fight it yourself for a bonus, or leave it to them.";
                case Tip.BuyAtDoor: return "Clear a rival front's soldiers, then stand at the door to claim it.";
                case Tip.Warned: return "Rival crews warn you before they shoot. Walk away, or start the fight on your terms. Crouch and stay out of sight to get closer before they notice you.";
                case Tip.Bodyguard: return "Your bodyguard follows you everywhere, even into your car. Untick Bodyguard on their page to send them back to their post.";
                default: return "";
            }
        }

        /// <summary>Queues a tip the first time it comes up in this save.</summary>
        public static void Show(Tip t)
        {
            EmpireState s = State != null ? State() : null;
            if (s == null || (s.TipsSeen & (int)t) != 0) return;
            s.TipsSeen |= (int)t;
            _queue.Enqueue(Text(t));
        }

        /// <summary>Every tick: one tip at a time, never over a prompt that's already showing.</summary>
        public static void Update()
        {
            if (_queue.Count == 0 || GTA.Game.GameTime < _busyUntil || GTA.UI.Screen.IsHelpTextDisplayed) return;
            GTA.UI.Screen.ShowHelpText(_queue.Dequeue(), ShowMs, true, false);
            _busyUntil = GTA.Game.GameTime + ShowMs + 500;
        }

        public static void Reset()
        {
            EmpireState s = State != null ? State() : null;
            if (s != null) s.TipsSeen = 0;
        }

        public static void Clear() { _queue.Clear(); }
    }
}
