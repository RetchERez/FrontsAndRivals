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
            if (s.SchemaVersion == 1)
            {
                // Schema 1 weapons were 0 pistol, 1 SMG, 2 rifle. Keep everyone's gun: pistol 1, SMG 4, carbine rifle 6.
                if (s.Guards != null) foreach (GuardState g in s.Guards) g.Weapon = MapV1Weapon(g.Weapon);
                if (s.Offers != null) foreach (GuardState g in s.Offers) g.Weapon = MapV1Weapon(g.Weapon);
                s.SchemaVersion = 2;
            }
            if (s.SchemaVersion == 2)
            {
                // Schema 2's seven steps onto the five-step ladder: micro SMG becomes an SMG, the assault rifle a carbine.
                if (s.Guards != null) foreach (GuardState g in s.Guards) g.Weapon = MapV2Weapon(g.Weapon);
                if (s.Offers != null) foreach (GuardState g in s.Offers) g.Weapon = MapV2Weapon(g.Weapon);
                s.SchemaVersion = 3;
            }
            Normalize(s);
            return true;
        }

        public static int MapV1Weapon(int old)
        {
            switch (old)
            {
                case 1: return 4;  // SMG (schema 2 numbering; MapV2Weapon takes it the rest of the way)
                case 2: return 6;  // carbine rifle
                default: return 1; // pistol
            }
        }

        public static int MapV2Weapon(int old)
        {
            switch (old)
            {
                case 0: return 0;  // knife
                case 1: return 1;  // pistol
                case 2: return 2;  // pump shotgun
                case 3: case 4: return 3;  // micro SMG, SMG -> SMG
                default: return 4; // assault rifle, carbine rifle -> carbine rifle
            }
        }

        /// <summary>Fills anything a hand-edited or partial file left out.</summary>
        public static void Normalize(EmpireState s)
        {
            if (s.OwnedFronts == null) s.OwnedFronts = new List<string>();
            if (s.Grudge == null) s.Grudge = new Dictionary<string, int>();
            if (s.Fronts == null) s.Fronts = new List<FrontState>();
            if (s.Guards == null) s.Guards = new List<GuardState>();
            if (s.NextGuardId < 1) s.NextGuardId = 1;
            if (s.Offers == null) { s.Offers = new List<GuardState>(); s.OffersDay = -1; }
            if (s.Crews == null) s.Crews = new List<CrewState>();
            if (s.FrontAttackedMs == null) s.FrontAttackedMs = new Dictionary<string, long>();
            if (s.Memorial == null) s.Memorial = new List<string>();
            if (s.NextMemberId < 1) s.NextMemberId = 1;
            if (s.LastLossMs == 0 && s.SaveCount > 0) s.LastLossMs = -10000000;
            foreach (CrewState c in s.Crews)
            {
                if (c.Members == null) c.Members = new List<CrewMember>();
                foreach (CrewMember m in c.Members)
                {
                    if (m.Log == null) m.Log = new List<string>();
                    if (m.Scars == null) m.Scars = new List<string>();
                    if (m.Id >= s.NextMemberId) s.NextMemberId = m.Id + 1;
                }
            }
            foreach (GuardState g in s.Guards)
            {
                if (string.IsNullOrEmpty(g.Status)) g.Status = GuardStatus.Active;
                if (g.Trait == (int)FrontsAndRivals.Rules.Trait.Lookout) g.Trait = (int)FrontsAndRivals.Rules.Trait.None; // retired trait
                if (g.Front == null) g.Post = GuardState.Reserve;
                if (g.Id >= s.NextGuardId) s.NextGuardId = g.Id + 1;
                // Saves from before skills: give everyone a steady, rank-based line.
                if (g.Shooting + g.Strength + g.Stamina + g.Stealth == 0)
                {
                    int[] v = FrontsAndRivals.Rules.Stats.FromRank((FrontsAndRivals.Rules.Rank)g.Rank, g.Id);
                    g.Shooting = v[0]; g.Strength = v[1]; g.Stamina = v[2]; g.Stealth = v[3];
                }
            }
            // Offers saved before skills: give them a line too, so they're priced and fight like everyone else.
            foreach (GuardState g in s.Offers)
            {
                if (g.Trait == (int)FrontsAndRivals.Rules.Trait.Lookout) g.Trait = (int)FrontsAndRivals.Rules.Trait.None;
                if (g.Shooting + g.Strength + g.Stamina + g.Stealth == 0)
                {
                    int[] v = FrontsAndRivals.Rules.Stats.FromRank(FrontsAndRivals.Rules.Rank.Recruit, g.Id + g.First.Length);
                    g.Shooting = v[0]; g.Strength = v[1]; g.Stamina = v[2]; g.Stealth = v[3];
                }
            }
            // Saves from before "danger money" have FearUntilDay = 0 (fields load as 0); 0 can only mean unset.
            if (s.FearUntilDay == 0) s.FearUntilDay = -1;
            if (s.ActiveMs < 0) s.ActiveMs = 0;
            if (s.SaveCount < 0) s.SaveCount = 0;
        }
    }
}
