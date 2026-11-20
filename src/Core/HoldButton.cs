// Fronts & Rivals: a button that fires once after being held, so a tap keeps its normal game use.
// Pure logic, no game calls (unit-tested); Main feeds it the button state every frame.
namespace FrontsAndRivals.Core
{
    public sealed class HoldButton
    {
        private readonly int _holdMs;
        private long _downAt = -1;
        private bool _fired;

        public HoldButton(int holdMs) { _holdMs = holdMs; }

        /// <summary>True from the moment the hold fired until the button is let go
        /// (the caller blocks the button's normal action meanwhile).</summary>
        public bool Fired { get { return _fired; } }

        /// <summary>Call every frame. Returns true exactly once per press, when the hold time is reached.</summary>
        public bool Update(bool pressed, long nowMs)
        {
            if (!pressed)
            {
                _downAt = -1;
                _fired = false;
                return false;
            }
            if (_downAt < 0) _downAt = nowMs;
            if (_fired || nowMs - _downAt < _holdMs) return false;
            _fired = true;
            return true;
        }

        /// <summary>Forget a press in progress (e.g. the mod paused mid-hold).</summary>
        public void Reset() { _downAt = -1; _fired = false; }
    }
}
