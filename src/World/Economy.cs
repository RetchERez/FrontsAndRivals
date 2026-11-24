// Fronts & Rivals: prices, wages and the midnight payout. Pure: no game calls, unit-tested.
// Numbers from design doc v1.0 (Guards, Fronts). Starting values; multipliers come from the INI.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Economy
    {

        /// <summary>Story Mode scale. A front pays about what a Story Mode property does (a small one about $7,000 a week,
        /// like Smoke on the Water's $9,300) and the running costs scale with it. Front prices don't: like property, a
        /// front takes a good while to pay for itself. fronts.json keeps the design-doc incomes; this brings them down.</summary>
        public const double StoryScale = 0.25;

        /// <summary>What a front pays a night before its bonuses.</summary>
        public static int BaseIncome(FrontDef def) { return def == null ? 0 : (int)Math.Round(def.Income * StoryScale); }

        /// <summary>The Broker's first text comes after Franklin's first mission; this is the fallback for a game already
        /// past it (ten minutes of free roam).</summary>
        public const long BrokerFallbackMs = 10 * 60000L;
        public static bool BrokerReady(long freeRoamMs)
        {
            return freeRoamMs > BrokerFallbackMs;
        }

        public const int RecruitsPerDay = 4;
        public const int MaxNightsPaidOnSkip = 3; // sleeping across midnights pays at most 3

        /// <summary>Wage per in-game day by rank.</summary>
        public static int Wage(Rank r)
        {
            // Fun over realism: cheap for a small crew. A big crew gets expensive through PayrollFactor, not these.
            switch (r)
            {
                case Rank.Recruit: return 150;
                case Rank.Soldier: return 250;
                case Rank.Veteran: return 350;
                default: return 500;
            }
        }

        /// <summary>Hiring price for a recruit: the trait's price, then more for better skills and less for weaker ones
        /// (x0.86 at overall 20, x1.0 at 38, x1.18 at 60), rounded to $50.</summary>
        public const int MinHire = 150;

        /// <summary>What a recruit asks: their trait's price scaled by their skills, from about x0.9 for a weak one (skills
        /// around 20) to x2 for a standout (65), never under $150. A typical recruit costs $250-$450, the best about $700.</summary>
        public static int HirePrice(Trait t, int overallSkill)
        {
            double f = 0.4 + Math.Max(0, Math.Min(100, overallSkill)) / 40.0;
            return Math.Max(MinHire, (int)(Math.Round(HirePrice(t) * f / 25.0) * 25));
        }

        /// <summary>A trait's base hiring price, $150-$300 (skills then scale it).</summary>
        public static int HirePrice(Trait t)
        {
            switch (t)
            {
                // Before skills: about what a Story Mode character pays someone to watch their back for a while.
                case Trait.Sharpshooter: return 300;
                case Trait.Tough: return 275;
                case Trait.Medic: return 275;
                case Trait.Hothead: return 250;
                case Trait.NightOwl: return 225;
                case Trait.Loyal: return 225;
                case Trait.Nervous: return 150;
                default: return 200;
            }
        }

        public static string TraitName(Trait t)
        {
            switch (t)
            {
                case Trait.NightOwl: return "Night Owl";
                case Trait.None: return "No Trait";
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
                case Trait.Medic: return "Halves the injury chance for every guard at the same front.";
                case Trait.Loyal: return "Never quits over unpaid wages.";
                case Trait.Nervous: return "x0.7 power, but costs half as much to hire.";
                default: return "";
            }
        }

        public static string RankName(Rank r) { return r.ToString(); }

        public static string WeaponName(Weapon w)
        {
            switch (w)
            {
                case Weapon.Knife: return "Knife";
                case Weapon.Pistol: return "Pistol";
                case Weapon.Shotgun: return "Pump Shotgun";
                case Weapon.Smg: return "SMG";
                default: return "Carbine Rifle";
            }
        }

        /// <summary>"a pistol", "an SMG", "an assault rifle".</summary>
        public static string WithArticle(Weapon w)
        {
            string name = w == Weapon.Smg ? WeaponName(w) : WeaponName(w).ToLowerInvariant();
            return ("aeiouAEIOU".IndexOf(name[0]) >= 0 || name.StartsWith("SMG") ? "an " : "a ") + name;
        }

        /// <summary>What each gun costs: its Story Mode Ammu-Nation price (GTABase). At Story Mode scale these fit:
        /// a pistol is a third of a small front's night, a carbine about two nights. Rank gates the top (MaxWeapon).</summary>
        public static int WeaponPrice(Weapon w)
        {
            switch (w)
            {
                case Weapon.Knife: return 0;
                case Weapon.Pistol: return 350;
                case Weapon.Shotgun: return 550;
                case Weapon.Smg: return 1300;
                default: return 2100; // carbine rifle
            }
        }

        // ---- 2. guns unlock with rank ----

        /// <summary>Body armour for one guard. Every rank can wear it; it's money, not experience.</summary>
        public const int VestPrice = 750;

        /// <summary>The best gun a guard of this rank will carry: Recruits up to the pump shotgun, Soldiers the SMG,
        /// Veterans and Lieutenants the carbine rifle.</summary>
        public static Weapon MaxWeapon(Rank r)
        {
            switch (r)
            {
                case Rank.Recruit: return Weapon.Shotgun;
                case Rank.Soldier: return Weapon.Smg;
                default: return Weapon.CarbineRifle;
            }
        }

        /// <summary>The rank a guard needs before they can carry this gun.</summary>
        public static Rank RankFor(Weapon w)
        {
            if (w <= Weapon.Shotgun) return Rank.Recruit;
            if (w == Weapon.Smg) return Rank.Soldier;
            return Rank.Veteran;
        }

        // ---- 3. the Broker's trust ----

        /// <summary>Fronts you must already hold before the Broker sells you, or backs your claim on, a front of this tier:
        /// small ones from the start, mid-size from 3, the biggest (clubs, factories, depots) from 8.</summary>
        public static int TrustNeeded(int tier) { return tier >= 2 ? 8 : tier == 1 ? 3 : 0; }

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

        /// <summary>Price of the next gun up the ladder (its Ammu-Nation price), or -1 at the carbine rifle.</summary>
        public static int UpgradePrice(Weapon current)
        {
            return current >= DefenseRating.TopWeapon ? -1 : WeaponPrice(current + 1);
        }

        /// <summary>Total to take one guard from a knife to the carbine rifle.</summary>
        public static int FullLadderPrice()
        {
            int sum = 0;
            for (Weapon w = Weapon.Knife; w < DefenseRating.TopWeapon; w++) sum += UpgradePrice(w);
            return sum;
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
        public const int FreePayroll = 12;

        /// <summary>Wages only bite with a big crew: up to 12 guards pay the base wage; every guard past 12 adds 4% to
        /// everyone's wage, up to x3 (60 guards is about x2.9).</summary>
        public static double PayrollFactor(int livingGuards)
        {
            return Math.Min(3.0, 1.0 + 0.04 * Math.Max(0, livingGuards - FreePayroll));
        }

        /// <summary>Tonight's wage for one guard, with the big-crew surcharge.</summary>
        public static int WageFor(Rank r, int livingGuards, double wageMult)
        {
            return (int)Math.Round(Wage(r) * StoryScale * PayrollFactor(livingGuards) * wageMult);
        }

        // ---- more fronts, more money ----

        /// <summary>A front pays more the longer you keep it: +3% for every in-game day held, up to +30% (ten days).
        /// Losing it resets this. Gentle on purpose, so money still matters in the mid-game.</summary>
        public static double EstablishedFactor(int daysHeld) { return 1.0 + Math.Min(0.3, 0.03 * Math.Max(0, daysHeld)); }

        /// <summary>Every front you hold makes the rest pay more (shared suppliers, laundering, reputation):
        /// +1.5% on all fronts per front past the first, up to +20%. More fronts always means more money, never runaway.</summary>
        public static double NetworkFactor(int frontsHeld) { return 1.0 + Math.Min(0.2, 0.015 * Math.Max(0, frontsHeld - 1)); }

        /// <summary>Which upgrades a front can take, by size: small shops Cameras and Floodlights; mid-size places add
        /// Reinforced Doors; the big ones add an Armory. (Anything bought before this rule stays.)</summary>
        public static bool CanInstall(string upgrade, int tier)
        {
            switch (upgrade)
            {
                case "Cameras": case "Floodlights": return true;
                case "Reinforced Doors": return tier >= 1;
                case "Armory": return tier >= 2;
                default: return false;
            }
        }

        public static int UpgradeSlots(int tier) { return tier >= 2 ? 4 : tier == 1 ? 3 : 2; }

        /// <summary>Front upgrades cost more on bigger businesses: x1 small, x1.5 mid-size, x2.5 big. Kitting out a club
        /// stays a real decision even when the money is rolling in.</summary>
        public static int UpgradePrice(int basePrice, int tier)
        {
            double f = tier >= 2 ? 2.5 : tier == 1 ? 1.5 : 1.0;
            return (int)(Math.Round(basePrice * 0.5 * f / 250.0) * 250); // half the design price at Story Mode scale
        }

        /// <summary>What one front pays tonight.</summary>
        public static int FrontIncome(FrontDef def, FrontState f, int day, int frontsHeld, double incomeMult)
        {
            if (def == null) return 0;
            return (int)Math.Round(def.Income * StoryScale * EstablishedFactor(day - (f != null ? f.HeldSinceDay : day)) * NetworkFactor(frontsHeld) * incomeMult);
        }

        public static Payout Midnight(EmpireState s, ContentDb content, long playerCash, double incomeMult, double wageMult)
        {
            var p = new Payout();
            int held = 0;
            foreach (FrontState f in s.Fronts) if (f.Owner == Owners.Player) held++;
            foreach (FrontState f in s.Fronts)
            {
                if (f.Owner != Owners.Player) continue;
                if (f.Damaged) { f.Damaged = false; p.DamagedSkipped++; continue; } // protected mode: no take tonight, fixed by morning
                p.Income += FrontIncome(content.Front(f.Id), f, s.Day, held, incomeMult);
            }
            while (true)
            {
                p.Wages = 0;
                int living = 0;
                foreach (GuardState g in s.Guards) if (g.Status != GuardStatus.Dead) living++;
                foreach (GuardState g in s.Guards)
                    if (g.Status != GuardStatus.Dead) p.Wages += WageFor((Rank)g.Rank, living, wageMult);
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
