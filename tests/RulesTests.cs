using System;
using System.Collections.Generic;
using FrontsAndRivals.Rules;

static class RulesTests
{
    static void Check(string name, double got, double want, double tol = 0.05) { T.Check(name, got, want, tol); }
    static GuardRecord G(Rank r, Weapon w, Trait t = Trait.None) => new GuardRecord { Rank = r, Weapon = w, Trait = t };

    static List<GuardRecord> Many(int n, Rank r, Weapon w) { var l = new List<GuardRecord>(); for (int i = 0; i < n; i++) l.Add(G(r, w)); return l; }

    static void StatsTests()
    {
        T.Section("Guard skills (Shooting, Strength, Stamina, Stealth)");
        T.Check("Average skills: no change to power", Stats.PowerFactor(50, 50), 1.0, 0.0001);
        T.True("Power x0.8 to x1.2", Math.Abs(Stats.PowerFactor(0, 0) - 0.8) < 1e-9 && Math.Abs(Stats.PowerFactor(100, 100) - 1.2) < 1e-9);
        T.True("Shooting counts more than Strength", Stats.PowerFactor(80, 50) > Stats.PowerFactor(50, 80));
        T.True("Stamina cuts injuries, Stealth cuts deaths", Stats.InjuryFactor(100) < Stats.InjuryFactor(50) && Stats.DeathFactor(100) < Stats.DeathFactor(0));
        T.True("Live-fight aim and health", Stats.Accuracy(0) == 15 && Stats.Accuracy(100) == 65 && Stats.Health(0) == 300 && Stats.Health(100) == 500);
        var rng = new Random(3);
        bool inRange = true, sharpAim = true;
        for (int i = 0; i < 200; i++)
        {
            int[] v = Stats.Seed(Trait.None, rng);
            foreach (int x in v) if (x < 20 || x > 80) inRange = false;
            if (Stats.Seed(Trait.Sharpshooter, rng)[0] < 40) sharpAim = false;
        }
        T.True("Fresh recruits start 20-60 in each skill (a standout up to 80)", inRange);
        var seen = new HashSet<string>();
        for (int i = 0; i < 60; i++) seen.Add(string.Join(",", Stats.Seed(Trait.None, rng)));
        T.True("Recruits vary: 60 rolls, " + seen.Count + " different", seen.Count >= 55);
        T.True("Sharpshooters start with good aim", sharpAim);

        var g = new FrontsAndRivals.Data.GuardState();
        Stats.Set(g, new[] { 30, 30, 30, 30 });
        for (int d = 0; d < 30; d++) Stats.Add(g, Stats.DayOnDuty);
        T.True("A month on a post builds Stamina and Stealth", g.Stamina == 60 && g.Stealth == 60 && g.Shooting == 30);
        Stats.Add(g, Stats.Defense(true)); Stats.Add(g, Stats.Kills(4));
        T.True("Fights and kills build aim", g.Shooting == 37 && g.Strength == 32);
        for (int i = 0; i < 100; i++) Stats.Add(g, Stats.Promotion);
        T.True("Skills cap at 100", g.Shooting == 100 && g.Stealth == 100);
        int[] old = Stats.FromRank(Rank.Veteran, 12);
        var ce = new FrontsAndRivals.Data.ContentDb(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Content")), null);
        ce.Load();
        var cs = FrontsAndRivals.Data.SaveService.NewEmpire();
        var cemp = new Empire(cs, ce);
        cemp.SetOwner("carwash_handson", FrontsAndRivals.Data.Owners.Player);
        var rookie = cemp.Recruit(new Random(1));
        cemp.Assign(rookie, "carwash_handson", 0);
        int promotedAt = -1;
        for (int night = 1; night <= 20 && promotedAt < 0; night++) if (cemp.DayOnDuty().Contains(rookie)) promotedAt = night;
        T.Check("Time served: a posted Recruit makes Soldier after 3 nights", promotedAt, 3, 0);
        T.True("Old saves get rank-based skills", old[0] >= 56 && old[0] <= 62 && Stats.FromRank(Rank.Recruit, 3)[1] < old[1]);

        var raw = G(Rank.Soldier, Weapon.Smg); raw.Shooting = 80; raw.Strength = 70;
        T.True("A skilled guard out-guns an average one", DefenseRating.GuardPower(raw, new FrontUpgrades(), 12) > DefenseRating.GuardPower(G(Rank.Soldier, Weapon.Smg), new FrontUpgrades(), 12));
    }

    static void VestTests()
    {
        T.Section("Body armour");
        var plain = G(Rank.Soldier, Weapon.Smg); var vested = G(Rank.Soldier, Weapon.Smg); vested.Vest = true;
        T.True("A vest makes a guard a little stronger", DefenseRating.GuardPower(vested, new FrontUpgrades(), 12) > DefenseRating.GuardPower(plain, new FrontUpgrades(), 12));
        var rng = new Random(11);
        int deadPlain = 0, deadVest = 0;
        for (int i = 0; i < 4000; i++)
        {
            if (Resolver.Resolve(rng, new List<GuardRecord> { plain }, 30, 80, 5, false).Guards[0].Dead) deadPlain++;
            if (Resolver.Resolve(rng, new List<GuardRecord> { vested }, 30, 80, 5, false).Guards[0].Dead) deadVest++;
        }
        T.True("Vested guards die less (" + deadVest + " vs " + deadPlain + " in 4,000 fights)", deadVest < deadPlain * 0.8);
    }

    /// <summary>Raids should be a gamble early, a good bet mid-game, and strong but never certain late.</summary>
    static void RaidBalance()
    {
        T.Section("Balance: raids are fun, not free");
        {
            // Late game, big front, hot streets: what a raid is worth with the party sizes (printed for the balance notes).
            var ir = new FrontsAndRivals.Data.CrewDef { SoldierPower = 16 };
            var big = new FrontsAndRivals.Data.FrontDef { Tier = 2 };
            var midFront = new FrontsAndRivals.Data.FrontDef { Tier = 1 };
            foreach (int n in new[] { 4, 5, 6 })
                foreach (Rank r in new[] { Rank.Veteran, Rank.Lieutenant })
                {
                    double pw = DefenseRating.Compute(Many(n, r, Weapon.CarbineRifle), new FrontUpgrades(), 12);
                    Console.WriteLine("  raid odds: " + n + " " + r + "s with rifles vs big front (8 soldiers) heat 0/50/80: "
                        + (int)(100 * Raids.WinChance(pw, Raids.FrontDefense(ir, big, 8, 0))) + "% / " + (int)(100 * Raids.WinChance(pw, Raids.FrontDefense(ir, big, 8, 50))) + "% / " + (int)(100 * Raids.WinChance(pw, Raids.FrontDefense(ir, big, 8, 80)))
                        + "%;  mid front (5) heat 50: " + (int)(100 * Raids.WinChance(pw, Raids.FrontDefense(ir, midFront, 5, 50))) + "%");
                }
            T.True("Raid party: 4, then 5 at 10 fronts, 6 at 20", Raids.PartySize(0) == 4 && Raids.PartySize(9) == 4 && Raids.PartySize(10) == 5 && Raids.PartySize(20) == 6);
        }
        double early = Raids.WinChance(DefenseRating.Compute(Many(3, Rank.Recruit, Weapon.Pistol), new FrontUpgrades(), 12), Raids.CrewDefense(new FrontsAndRivals.Data.CrewDef { SoldierPower = 12 }, 2));
        double mid = Raids.WinChance(DefenseRating.Compute(Many(3, Rank.Soldier, Weapon.Smg), new FrontUpgrades(), 12), Raids.CrewDefense(new FrontsAndRivals.Data.CrewDef { SoldierPower = 12 }, 3));
        double late = Raids.WinChance(DefenseRating.Compute(Many(4, Rank.Veteran, Weapon.CarbineRifle), new FrontUpgrades(), 12), Raids.CrewDefense(new FrontsAndRivals.Data.CrewDef { SoldierPower = 16 }, 4));
        T.True("Early raid (3 pistol recruits vs a small front): a long shot, 25-45% (" + (int)(early * 100) + "%)", early >= 0.25 && early <= 0.45);
        T.True("Mid-game raid (3 SMG soldiers vs a mid front): a fair fight, 50-65% (" + (int)(mid * 100) + "%)", mid >= 0.50 && mid <= 0.65);
        T.True("Late raid (4 carbine veterans vs a big front): strong, 65-85% (" + (int)(late * 100) + "%)", late >= 0.65 && late <= 0.85);
    }

    /// <summary>The balance pass's target fights (Dead Kings, soldier power 12). Preparation should win, neglect should lose.</summary>
    static void BalanceTests()
    {
        T.Section("Balance: a prepared front wins, a neglected one loses");
        var none = new FrontUpgrades();
        double lt = FrontsAndRivals.Rules.Nemesis.LieutenantBonus, boss = FrontsAndRivals.Rules.Nemesis.BossBonus;
        double earlyA = Resolver.AttackStrength(Resolver.SquadSize(0, 0, 10), 12, lt, false, 0);
        double p1 = Resolver.WinChance(DefenseRating.Compute(Many(2, Rank.Recruit, Weapon.Knife), none, 12), earlyA);
        double p2 = Resolver.WinChance(DefenseRating.Compute(Many(2, Rank.Recruit, Weapon.Pistol), none, 12), earlyA);
        double p2b = Resolver.WinChance(DefenseRating.Compute(Many(2, Rank.Recruit, Weapon.Shotgun), none, 12), earlyA);
        T.True("Small front, 2 knife recruits vs the first squad: mostly lost (15-35%)", p1 >= 0.15 && p1 <= 0.35);
        T.True("2 pistol recruits: close to even (40-55%)", p2 >= 0.40 && p2 <= 0.55);
        T.True("2 shotgun recruits: favoured (55-70%)", p2b >= 0.55 && p2b <= 0.70);

        double midA = Resolver.AttackStrength(5, 12, lt, false, 30);
        double p3 = Resolver.WinChance(DefenseRating.Compute(Many(4, Rank.Soldier, Weapon.Smg), new FrontUpgrades { Cameras = true }, 12), midA);
        double p3b = Resolver.WinChance(DefenseRating.Compute(Many(2, Rank.Soldier, Weapon.Pistol), none, 12), midA);
        T.True("Mid game, 4 SMG soldiers + cameras: favoured (55-75%)", p3 >= 0.55 && p3 <= 0.75);
        T.True("Mid game, 2 pistol soldiers: lost (under 25%)", p3b < 0.25);

        double lateA = Resolver.AttackStrength(8, 12, boss, true, 60);
        var all = new FrontUpgrades { Cameras = true, Doors = true, Floodlights = true, Armory = true };
        var best = Many(3, Rank.Veteran, Weapon.CarbineRifle); best.Add(G(Rank.Lieutenant, Weapon.CarbineRifle));
        double p4 = Resolver.WinChance(DefenseRating.Compute(best, all, 12), lateA);
        double p5 = Resolver.WinChance(DefenseRating.Compute(Many(2, Rank.Soldier, Weapon.Smg), none, 12), lateA);
        T.True("Late game, maxed front vs the boss: favoured but not safe (60-80%)", p4 >= 0.60 && p4 <= 0.80);
        T.True("Late game, neglected front vs the boss: lost (under 15%)", p5 < 0.15);
    }

    public static void Run()
    {
        BalanceTests();
        RaidBalance();
        VestTests();
        StatsTests();
        T.Section("Rules (design doc numbers)");
        var none = new FrontUpgrades();
        // Design doc worked examples
        Check("Car wash day", DefenseRating.Compute(new List<GuardRecord>{G(Rank.Recruit,Weapon.Pistol),G(Rank.Recruit,Weapon.Pistol)}, none, 12), 20);
        Check("Car wash night", DefenseRating.Compute(new List<GuardRecord>{G(Rank.Recruit,Weapon.Pistol),G(Rank.Recruit,Weapon.Pistol)}, none, 23), 15);
        var cams = new FrontUpgrades{Cameras=true};
        Check("Pawn shop mid-game", DefenseRating.Compute(new List<GuardRecord>{G(Rank.Soldier,Weapon.Smg),G(Rank.Soldier,Weapon.Smg),G(Rank.Soldier,Weapon.Smg),G(Rank.Soldier,Weapon.Smg)}, cams, 12), 112.64);
        var late = new FrontUpgrades{Cameras=true, Doors=true, Floodlights=true};
        Check("Chop shop late game (night)", DefenseRating.Compute(new List<GuardRecord>{G(Rank.Lieutenant,Weapon.CarbineRifle),G(Rank.Veteran,Weapon.Smg),G(Rank.Veteran,Weapon.Smg),G(Rank.Veteran,Weapon.Smg)}, late, 23), 241.672);
        // Win-chance table
        Check("P at D=0.5A", Resolver.WinChance(50,100), 0.20, 0.001);
        Check("P at D=A", Resolver.WinChance(100,100), 0.50, 0.001);
        Check("P at D=1.5A", Resolver.WinChance(150,100), 0.6923, 0.001);
        Check("P at D=2A", Resolver.WinChance(200,100), 0.80, 0.001);
        Check("P at D=3A", Resolver.WinChance(300,100), 0.90, 0.001);
        Check("P clamp high", Resolver.WinChance(1000,1), 0.95, 0.0001);
        Check("P clamp low", Resolver.WinChance(1,1000), 0.05, 0.0001);
        // Squad size and attack strength
        Check("Squad: tier 2, grudge 60, manpower 20", Resolver.SquadSize(2,60,20), 5, 0);
        Check("Squad capped by manpower", Resolver.SquadSize(2,100,3), 3, 0);
        Check("Grudge adds at most 2 attackers", Resolver.SquadSize(2,100,20), 6, 0);
        Check("A: 6 Ironside (16) + lieutenant, grudge 60", Resolver.AttackStrength(6,16,25,false,60), (6*16+25)*1.15);
        // Monte Carlo: 10,000 auto-resolves at D=A should win ~50%
        var rng = new Random(12345); int wins = 0;
        var posted = new List<GuardRecord>{G(Rank.Soldier,Weapon.Smg)};
        for (int i=0;i<10000;i++) if (Resolver.Resolve(rng, posted, 100, 100, 4, false).DefendersWon) wins++;
        Check("Monte Carlo win rate at D=A (10,000 fights)", wins/10000.0, 0.50, 0.015);
        wins = 0;
        for (int i=0;i<10000;i++) if (Resolver.Resolve(rng, posted, 200, 100, 4, false).DefendersWon) wins++;
        Check("Monte Carlo win rate at D=2A (10,000 fights)", wins/10000.0, 0.80, 0.015);
    }
}
