// Fronts & Rivals: the one seeded random generator per save.
// A System.Random whose whole state is one 64-bit number, so it can be saved and restored exactly.
// Algorithm: SplitMix64 (public-domain constants by Sebastiano Vigna). Pure, unit-tested.
using System;

namespace FrontsAndRivals.Core
{
    public sealed class Rng : Random
    {
        private const double TwoPow53 = 9007199254740992.0;
        private ulong _state;

        public Rng(ulong state) : base(0)
        {
            _state = state;
        }

        /// <summary>Everything needed to continue the exact sequence later.</summary>
        public ulong State
        {
            get { return _state; }
            set { _state = value; }
        }

        /// <summary>A fresh state for a new empire.</summary>
        public static ulong NewState()
        {
            ulong a = (ulong)DateTime.UtcNow.Ticks;
            ulong b = (ulong)(uint)Environment.TickCount;
            return a ^ (b << 32) ^ 0x5DEECE66DUL;
        }

        public ulong NextUInt64()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>[0, 1)</summary>
        protected override double Sample()
        {
            return (NextUInt64() >> 11) * (1.0 / TwoPow53);
        }

        public override double NextDouble()
        {
            return Sample();
        }

        /// <summary>[0, int.MaxValue)</summary>
        public override int Next()
        {
            return (int)(Sample() * int.MaxValue);
        }

        /// <summary>[0, maxValue)</summary>
        public override int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException("maxValue");
            return (int)(Sample() * maxValue);
        }

        /// <summary>[minValue, maxValue)</summary>
        public override int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException("minValue");
            long range = (long)maxValue - minValue;
            return (int)(minValue + (long)(Sample() * range));
        }

        public override void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(NextUInt64() >> 56);
        }
    }
}
