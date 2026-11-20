// Fronts & Rivals: brings old saves up to the current schema instead of breaking them. Pure, unit-tested.
using System.Collections.Generic;

namespace FrontsAndRivals.Data
{
    public static class Migrations
    {
        /// <summary>Upgrades state in place. Returns false if the file is from a newer mod version.</summary>
        public static bool Upgrade(EmpireState s)
        {
            if (s.SchemaVersion > EmpireState.CurrentSchema) return false;
            if (s.SchemaVersion < 1) s.SchemaVersion = 1;
            // Future: if (s.SchemaVersion == 1) { ...; s.SchemaVersion = 2; }
            Normalize(s);
            return true;
        }

        /// <summary>Fills anything a hand-edited or partial file left out.</summary>
        public static void Normalize(EmpireState s)
        {
            if (s.OwnedFronts == null) s.OwnedFronts = new List<string>();
            if (s.Grudge == null) s.Grudge = new Dictionary<string, int>();
            if (s.Fronts == null) s.Fronts = new List<FrontState>();
            if (s.Guards == null) s.Guards = new List<GuardState>();
            if (s.NextGuardId < 1) s.NextGuardId = 1;
            foreach (GuardState g in s.Guards)
            {
                if (string.IsNullOrEmpty(g.Status)) g.Status = GuardStatus.Active;
                if (g.Front == null) g.Post = GuardState.Reserve;
                if (g.Id >= s.NextGuardId) s.NextGuardId = g.Id + 1;
            }
            if (s.ActiveMs < 0) s.ActiveMs = 0;
            if (s.SaveCount < 0) s.SaveCount = 0;
        }
    }
}
