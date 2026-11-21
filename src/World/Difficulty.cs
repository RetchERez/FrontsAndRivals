// Fronts & Rivals: one difficulty setting (Office > Settings), Relaxed to Brutal. Pure: no game calls, unit-tested.
// Saved as a step from Normal (-2..+2), so saves from before the setting read as Normal.
namespace FrontsAndRivals.Rules
{
    public static class Difficulty
    {
        public static readonly string[] Names = { "Relaxed", "Easy", "Normal", "Hard", "Brutal" };

        public static int Clamp(int step) { return step < -2 ? -2 : step > 2 ? 2 : step; }

        public static string Name(int step) { return Names[Clamp(step) + 2]; }

        /// <summary>How hard the crews push: attack strength and the defense of their own fronts.</summary>
        public static double Strength(int step)
        {
            switch (Clamp(step))
            {
                case -2: return 0.7;
                case -1: return 0.85;
                case 1: return 1.2;
                case 2: return 1.45;
                default: return 1.0;
            }
        }

        /// <summary>The wait between attacks: longer when it's easier, shorter when it's harder.</summary>
        public static double Cooldown(int step)
        {
            switch (Clamp(step))
            {
                case -2: return 1.5;
                case -1: return 1.2;
                case 1: return 0.85;
                case 2: return 0.7;
                default: return 1.0;
            }
        }
    }
}
