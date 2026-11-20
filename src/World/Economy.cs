// Fronts & Rivals: prices, wages and the midnight payout. Pure: no game calls, unit-tested.
// Numbers from design doc v1.0 (Guards, Fronts). Starting values; multipliers come from the INI.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Economy
    {
        public const int SmgPrice = 3000;    // x1.2 power
        public const int RiflePrice = 8000;  // x1.4 power
        public const int StarterFrontPrice = 25000; // the Broker's starter price for the car wash

        /// <summary>Price of a front that's for sale: the starter's fixed price, otherwise six days of its income.</summary>
        public static int FrontPrice(FrontDef def) { return FrontPrice(def, 6); }

        public static int FrontPrice(FrontDef def, int days) { return def.Starter ? StarterFrontPrice : def.Income * Math.Max(1, days); }
        public const int RecruitsPerDay = 3;
        public const int MaxNightsPaidOnSkip = 3; // sleeping across midnights pays at most 3

        /// <summary>Wage per in-game day by rank.</summary>
        public static int Wage(Rank r)
        {
            switch (r)
            {
                case Rank.Recruit: return 500;
                case Rank.Soldier: return 800;
                case Rank.Veteran: return 1200;
                default: return 2000;
            }
        }

        /// <summary>Hiring price, $2,500-$5,000 by trait; Nervous costs half.</summary>
        public static int HirePrice(Trait t)
        {
            switch (t)
            {
                case Trait.Sharpshooter: return 4500;
                case Trait.Tough: return 4000;
                case Trait.Medic: return 4000;
                case Trait.Hothead: return 3500;
                case Trait.Lookout: return 3500;
                case Trait.NightOwl: return 3000;
                case Trait.Loyal: return 3000;
                case Trait.Nervous: return 1250;
                default: return 2500;
            }
        }

        public static string TraitName(Trait t)
        {
            switch (t)
            {
                case Trait.NightOwl: return "Night owl";
                case Trait.None: return "No trait";
                default: return t.ToString();
            }
        }

        public static string TraitEffect(Trait t)
        {
            switch (t)
            {
                case Trait.Sharpshooter: return "x1.2 power.";
                case Trait.Tough: return "x1.15 power, half the injury chance.";
                case Trait.Hothead: return "x1.25 power, double the death chance.";
                case Trait.NightOwl: return "No night penalty.";
                case Trait.Lookout: return "Warnings for this front arrive an hour earlier.";
                case Trait.Medic: return "Halves the injury chance for every guard at the same front.";
                case Trait.Loyal: return "Never quits over unpaid wages.";
                case Trait.Nervous: return "x0.7 power, but costs half as much to hire.";
                default: return "";
            }
        }

        public static string RankName(Rank r) { return r.ToString(); }

        public static string WeaponName(Weapon w) { return w == Weapon.Smg ? "SMG" : w.ToString(); }

        /// <summary>XP needed for the next rank, or -1 at Lieutenant.</summary>
        public static int NextRankXp(Rank r)
        {
            switch (r)
            {
                case Rank.Recruit: return 3;
                case Rank.Soldier: return 8;
                case Rank.Veteran: return 15;
                default: return -1;
            }
        }

        /// <summary>Price to raise a guard's weapon to the next tier, or -1 if they already have a rifle.</summary>
        public static int UpgradePrice(Weapon current)
        {
            if (current == Weapon.Pistol) return SmgPrice;
            if (current == Weapon.Smg) return RiflePrice;
            return -1;
        }

        public sealed class Payout
        {
            public int Income;
            public int Wages;
            public int Net { get { return Income - Wages; } }
            public List<GuardState> Quit = new List<GuardState>();
            public int DamagedSkipped;
        }

        /// <summary>One in-game midnight. Wages come out before income; if the player can't cover them,
        /// the newest non-Loyal guard quits (one at a time until the rest can be paid).</summary>
        public static Payout Midnight(EmpireState s, ContentDb content, long playerCash, double incomeMult, double wageMult)
        {
            var p = new Payout();
            foreach (FrontState f in s.Fronts)
            {
                if (f.Owner != Owners.Player) continue;
                if (f.Damaged) { f.Damaged = false; p.DamagedSkipped++; continue; } // protected mode: no take tonight, fixed by morning
                FrontDef def = content.Front(f.Id);
                if (def != null) p.Income += (int)Math.Round(def.Income * incomeMult);
            }
            while (true)
            {
                p.Wages = 0;
                foreach (GuardState g in s.Guards)
                    if (g.Status != GuardStatus.Dead) p.Wages += (int)Math.Round(Wage((Rank)g.Rank) * wageMult);
                if (playerCash + p.Income >= p.Wages) break;

                GuardState newest = null;
                foreach (GuardState g in s.Guards)
                    if (g.Status != GuardStatus.Dead && g.Trait != (int)Trait.Loyal && (newest == null || g.Id > newest.Id)) newest = g;
                if (newest == null) break; // only Loyal guards left: they wait for their money
                s.Guards.Remove(newest);
                p.Quit.Add(newest);
            }
            return p;
        }
    }

    /// <summary>Counts in-game midnights from the game's calendar date, so sleeping or a character switch
    /// that jumps the clock still pays each skipped day (at most 3).</summary>
    public static class GameCalendar
    {
        public static string Stamp(DateTime gameDate) { return gameDate.ToString("yyyy-MM-dd"); }

        /// <summary>How many midnights to process since the last seen date. 0 the first time.</summary>
        public static int MidnightsSince(string lastStamp, DateTime gameDate)
        {
            if (string.IsNullOrEmpty(lastStamp)) return 0;
            DateTime last;
            if (!DateTime.TryParseExact(lastStamp, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out last)) return 0;
            int days = (int)(gameDate.Date - last.Date).TotalDays;
            if (days <= 0) return 0; // a clock set backwards never pays
            return Math.Min(days, Economy.MaxNightsPaidOnSkip);
        }
    }
}
