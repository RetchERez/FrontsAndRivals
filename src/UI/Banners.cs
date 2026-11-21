// Fronts & Rivals: the big centre-screen shard (the game's MP_BIG_MESSAGE_FREEMODE, via LemonUI's BigMessage).
// Design doc: used for three moments only - front taken, front lost, nemesis killed - plus "front defended" when you were there.
using GTA;
using LemonUI;
using LemonUI.Scaleform;

namespace FrontsAndRivals.UI
{
    public sealed class Banners
    {
        private const int ShowMs = 6000;

        private readonly ObjectPool _pool = new ObjectPool();
        private readonly BigMessage _message = new BigMessage("", "", MessageType.Customizable);
        private int _hideAt;

        public Banners()
        {
            _pool.Add(_message);
        }

        public void Show(string title, string message) { Show(title, message, false); }

        /// <param name="passed">true: Story Mode's own "mission passed" shard (gold title, message under it), for the
        /// moments that feel like finishing a job yourself, like taking a front.</param>
        public void Show(string title, string message, bool passed)
        {
            _message.Title = title;
            _message.Message = message;
            _message.Type = passed ? MessageType.MissionPassedOldGen : MessageType.Customizable;
            _message.Visible = true;
            _hideAt = Game.GameTime + ShowMs;
        }

        /// <summary>Every tick, also while paused.</summary>
        public void Process()
        {
            if (_message.Visible && Game.GameTime > _hideAt) _message.Visible = false;
            _pool.Process();
        }

        public void Hide() { _message.Visible = false; }
    }
}
