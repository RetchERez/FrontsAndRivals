// Fronts & Rivals: feed messages that show the real guard's or rival's face.
using System.Collections.Generic;
using GTA;
using GTA.Native;

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

        /// <summary>Plain feed message with a stock contact picture, e.g. "CHAR_DEFAULT".</summary>
        public static void Post(string picture, string sender, string subject, string message)
        {
            Function.Call(Hash.BEGIN_TEXT_COMMAND_THEFEED_POST, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, message);
            // NativeDB: txdName, textureName, flash, iconType, sender, subject
            Function.Call<int>(Hash.END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT, picture, picture, false, 1, sender, subject);
        }

        /// <summary>Feed message using a mugshot of a live ped. The headshot takes a few frames to render.</summary>
        public void PostWithMugshot(Ped ped, string sender, string subject, string message)
        {
            if (ped == null || !ped.Exists()) { Post("CHAR_DEFAULT", sender, subject, message); return; }
            int handle = Function.Call<int>(Hash.REGISTER_PEDHEADSHOT, ped);
            _shots.Add(new PendingShot { Handle = handle, Sender = sender, Subject = subject, Message = message, StartedAt = Game.GameTime });
        }

        /// <summary>Call every tick while shots are pending.</summary>
        public void Update()
        {
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
