using System;
using System.Collections.Generic;
using FrontsAndRivals.Rules;

static class RulesTests
{
    static void Check(string name, double got, double want, double tol = 0.05) { T.Check(name, got, want, tol); }
    static GuardRecord G(Rank r, Weapon w, Trait t = Trait.None) => new GuardRecord { Rank = r, Weapon = w, Trait = t };

    static List<GuardRecord> Many(int n, Rank r, Weapon w) { var l = new List<GuardRecord>(); for (int i = 0; i < n; i++) l.Add(G(r, w)); return l; }

    /// <summary>The balance pass's target fights (Crown Kings, soldier power 12). Preparation should win, neglect should lose.</summary>
    static void BalanceTests()
    {
        T.Section("Balance: a prepared front wins, a neglected one loses");
        var none = new FrontUpgrades();
        double lt = FrontsAndRivals.Rules.Nemesis.LieutenantBonus, boss = FrontsAndRivals.Rules.Nemesis.BossBonus;
        double earlyA = Resolver.AttackStrength(2, 12, lt, false, 0);
        double p1 = Resolver.WinChance(DefenseRating.Compute(Many(2, Rank.Recruit, Weapon.Knife), none, 12), earlyA);
        double p2 = Resolver.WinChance(DefenseRating.Compute(Many(4, Rank.Recruit, Weapon.Pistol), none, 12), earlyA);
        T.True("Start, 2 knife recruits vs the first squad: mostly lost (5-20%)", p1 >= 0.05 && p1 <= 0.20);
        T.True("Car wash with 4 pistol recruits: favoured (50-70%)", p2 >= 0.50 && p2 <= 0.70);

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
        Check("Squad: tier 2, grudge 60, manpower 20", Resolver.SquadSize(2,60,20), 6, 0);
        Check("Squad capped by manpower", Resolver.SquadSize(2,100,3), 3, 0);
        Check("Squad cap 9", Resolver.SquadSize(2,100,20), 8, 0);
        Check("A: 6 Ironside (16) + lieutenant, grudge 60", Resolver.AttackStrength(6,16,25,false,60), (6*16+25)*1.3);
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
