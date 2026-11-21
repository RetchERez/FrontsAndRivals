// Fronts & Rivals - pure game rules.
// No GTA calls in this file, so it can be unit-tested outside the game.
// Every number matches the v1.0 design document.
using System;
using System.Collections.Generic;

namespace FrontsAndRivals.Rules
{
    public enum Rank { Recruit, Soldier, Veteran, Lieutenant }
    /// <summary>The guard weapon ladder, bottom to top. Guards start with a knife; every step is a big jump.
    /// Saved as an int. Schema 1: 0 pistol, 1 SMG, 2 rifle. Schema 2 had 7 steps. Migrations maps both.</summary>
    public enum Weapon { Knife, Pistol, Shotgun, Smg, CarbineRifle }
    /// <summary>Lookout is retired (every guard keeps watch); the value stays so old saves load, and Migrations clears it.</summary>
    public enum Trait { None, Sharpshooter, Tough, Hothead, NightOwl, Lookout, Medic, Loyal, Nervous }

    public sealed class GuardRecord
    {
        public string Name;
        public Rank Rank;
        public Weapon Weapon;
        public Trait Trait;
        public bool Injured;
        // Skills (Rules.Stats). 50 is average, so a record without them counts as an average guard.
        public int Shooting = Stats.Average, Strength = Stats.Average, Stamina = Stats.Average, Stealth = Stats.Average;
    }

    public sealed class FrontUpgrades
    {
        public bool Cameras;      // +10% defense
        public bool Doors;        // +20 defense
        public bool Floodlights;  // removes night penalty
        public bool Armory;       // +1 weapon tier (top tier: +10%)
    }

    public static class DefenseRating
    {
        public static double RankPower(Rank r)
        {
            switch (r)
            {
                case Rank.Recruit: return 10;
                case Rank.Soldier: return 16;
                case Rank.Veteran: return 24;
                default: return 34; // Lieutenant
            }
        }

        public const Weapon TopWeapon = Weapon.CarbineRifle;
        public const double DoorsBonus = 20.0;

        /// <summary>Power by weapon. Each step is at least +23%: knife x0.6, pistol x1.0, pump shotgun x1.3,
        /// SMG x1.6, carbine rifle x2.0. (Balance pass: the micro SMG and assault rifle were cut, they were +4-8% steps.)</summary>
        public static double WeaponPower(Weapon w)
        {
            switch (w)
            {
                case Weapon.Knife: return 0.6;
                case Weapon.Pistol: return 1.0;
                case Weapon.Shotgun: return 1.3;
                case Weapon.Smg: return 1.6;
                default: return 2.0; // carbine rifle
            }
        }

        /// <summary>The Armory makes every guard fight one weapon tier higher; at the top tier it's +10%.</summary>
        public static double WeaponMultiplier(Weapon w, bool armory)
        {
            if (!armory) return WeaponPower(w);
            return w >= TopWeapon ? WeaponPower(TopWeapon) * 1.1 : WeaponPower(w + 1);
        }

        public static double TraitMultiplier(Trait t)
        {
            switch (t)
            {
                case Trait.Sharpshooter: return 1.2;
                case Trait.Tough: return 1.15;
                case Trait.Hothead: return 1.25;
                case Trait.Nervous: return 0.7;
                default: return 1.0;
            }
        }

        /// <summary>Night is 21:00 to 05:00 in-game.</summary>
        public static bool IsNight(int hour) => hour >= 21 || hour < 5;

        /// <summary>g = R x W x T x N x S (S from Shooting and Strength, 1.0 for an average guard)</summary>
        public static double GuardPower(GuardRecord g, FrontUpgrades up, int hour)
        {
            double night = (IsNight(hour) && !up.Floodlights && g.Trait != Trait.NightOwl) ? 0.75 : 1.0;
            return RankPower(g.Rank) * WeaponMultiplier(g.Weapon, up.Armory) * TraitMultiplier(g.Trait) * night
                   * Stats.PowerFactor(g.Shooting, g.Strength);
        }

        /// <summary>D = (sum g) x O x (1 + C) + F</summary>
        public static double Compute(IList<GuardRecord> posted, FrontUpgrades up, int hour)
        {
            double sum = 0;
            bool officer = false;
            foreach (var g in posted)
            {
                if (g.Injured) continue;
                sum += GuardPower(g, up, hour);
                if (g.Rank == Rank.Lieutenant) officer = true;
            }
            double o = officer ? 1.1 : 1.0;
            double c = up.Cameras ? 0.10 : 0.0;
            double f = up.Doors ? DoorsBonus : 0.0;
            return sum * o * (1.0 + c) + f;
        }
    }

    public static class Resolver
    {
        /// <summary>P(win) = D^2 / (D^2 + A^2), clamped to 5%..95%.</summary>
        public static double WinChance(double defense, double attack)
        {
            if (defense <= 0) return 0.05;
            if (attack <= 0) return 0.95;
            double p = (defense * defense) / (defense * defense + attack * attack);
            return Math.Max(0.05, Math.Min(0.95, p));
        }

        /// <summary>A = (n x p + L) x t x (1 + grudge/200)</summary>
        public static double AttackStrength(int squad, double soldierPower, double leaderBonus, bool ruthless, int grudge)
        {
            double t = ruthless ? 1.2 : 1.0;
            int g = Math.Max(0, Math.Min(100, grudge));
            return (squad * soldierPower + leaderBonus) * t * (1.0 + g / 200.0);
        }

        /// <summary>n = min(9, manpower, 2 + tier + floor(grudge/25))</summary>
        public static int SquadSize(int frontTier, int grudge, int manpower)
        {
            return SquadSize(frontTier, grudge, manpower, 0);
        }

        /// <summary>The same, plus extra attackers (empire-size pressure). Still never over 9 or the crew's manpower.</summary>
        public static int SquadSize(int frontTier, int grudge, int manpower, int bonus)
        {
            int want = 2 + frontTier + (Math.Max(0, grudge) / 25) + Math.Max(0, bonus);
            return Math.Max(0, Math.Min(9, Math.Min(manpower, want)));
        }

        public sealed class GuardOutcome { public bool Injured; public bool Dead; }

        public sealed class Result
        {
            public bool DefendersWon;
            public List<GuardOutcome> Guards = new List<GuardOutcome>();
            public int AttackersLost;
        }

        /// <summary>Auto-resolve one attack. Pass the save's seeded Random so results reproduce.</summary>
        public static Result Resolve(Random rng, IList<GuardRecord> posted, double defense, double attack, int squad, bool medicPresent)
        {
            var res = new Result();
            res.DefendersWon = rng.NextDouble() < WinChance(defense, attack);
            double r = attack / (defense + attack);
            foreach (var g in posted)
            {
                if (g.Injured) { res.Guards.Add(new GuardOutcome()); continue; }
                double injury = (res.DefendersWon ? 0.6 : 0.9) * r;
                double death = (res.DefendersWon ? 0.15 : 0.35) * r;
                if (g.Trait == Trait.Tough) injury *= 0.5;
                if (medicPresent) injury *= 0.5;
                if (g.Trait == Trait.Hothead) death *= 2.0;
                injury *= Stats.InjuryFactor(g.Stamina);
                death *= Stats.DeathFactor(g.Stealth);
                var o = new GuardOutcome();
                double roll = rng.NextDouble();
                if (roll < death) o.Dead = true;
                else if (roll < death + injury) o.Injured = true;
                res.Guards.Add(o);
            }
            res.AttackersLost = res.DefendersWon
                ? (int)Math.Round(squad * (1.0 - r))
                : (int)Math.Round(squad * r * 0.5);
            return res;
        }
    }
}
