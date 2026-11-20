// Fronts & Rivals: "3 failures within 60 seconds" tracker. Pure, unit-tested.
namespace FrontsAndRivals.Core
{
    public sealed class FailureWindow
    {
        public const int MaxFailures = 3;
        public const int WindowMs = 60000;

        // Times of the most recent failures, oldest first once full.
        private readonly int[] _times = new int[MaxFailures];
        private int _count;

        public int Count { get { return _count; } }

        /// <summary>Records a failure at nowMs. Returns true when this is the 3rd failure within 60 s.</summary>
        public bool Record(int nowMs)
        {
            if (_count < MaxFailures)
            {
                _times[_count++] = nowMs;
            }
            else
            {
                for (int i = 1; i < MaxFailures; i++) _times[i - 1] = _times[i];
                _times[MaxFailures - 1] = nowMs;
            }
            return _count == MaxFailures && nowMs - _times[0] <= WindowMs;
        }
    }
}
