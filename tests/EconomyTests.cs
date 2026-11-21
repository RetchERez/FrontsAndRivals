using System;
using System.IO;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

static class EconomyTests
{
    public static void Run()
    {
        T.Section("Economy (wages, prices, midnight payout)");
        T.Check("Recruit wage $150", Economy.Wage(Rank.Recruit), 150, 0);
        T.Check("Lieutenant wage $500", Economy.Wage(Rank.Lieutenant), 500, 0);
        T.True("Payroll: base up to 12 guards, +4% each after, capped x3", Economy.PayrollFactor(12) == 1.0 && Math.Abs(Economy.PayrollFactor(22) - 1.4) < 1e-9 && Economy.PayrollFactor(200) == 3.0);
        T.True("Established: +5% a day, capped at +50%", Math.Abs(Economy.EstablishedFactor(4) - 1.2) < 1e-9 && Economy.EstablishedFactor(30) == 1.5 && Economy.EstablishedFactor(0) == 1.0);
        T.True("Network: +2% per extra front, capped at +30%", Economy.NetworkFactor(1) == 1.0 && Math.Abs(Economy.NetworkFactor(6) - 1.1) < 1e-9 && Economy.NetworkFactor(29) == 1.3);
        bool alwaysMore = true;
        for (int n = 1; n < 29; n++) if (Economy.NetworkFactor(n + 1) * (n + 1) <= Economy.NetworkFactor(n) * n) alwaysMore = false;
        T.True("Another front always means more total income", alwaysMore);
        bool pays = true;
        foreach (int inc in new[] { 4000, 4500, 5000 })
        {
            double take = inc * Economy.EstablishedFactor(10); // Lieutenants take weeks; by then the front is established
            if (take - (Economy.Wage(Rank.Lieutenant) + 3 * Economy.Wage(Rank.Veteran)) < take * 0.5) pays = false;
            if (inc - 4 * Economy.Wage(Rank.Recruit) < inc * 0.6) pays = false; // and a fresh one with recruits pays well from night one
        }
        T.True("Small fronts always pay: 60%+ with recruits, 50%+ with veterans once established", pays);
        double small = 4 * Economy.WageFor(Rank.Soldier, 8, 1.0), big = 4 * Economy.WageFor(Rank.Soldier, 60, 1.0);
        T.True("Wages are pocket change for a small crew, real money for a big one", small < 4500 * 0.25 && big > small * 2.5);
        bool inRange = true;
        foreach (Trait t in Empire.RecruitTraits)
        {
            int p = Economy.HirePrice(t);
            if (t == Trait.Nervous ? p != 1250 : (p < 2500 || p > 5000)) inRange = false;
        }
        T.True("Hire prices $2,500-$5,000, Nervous half price", inRange);
        T.Check("Knife -> pistol $750", Economy.UpgradePrice(Weapon.Knife), 750, 0);
        T.Check("Pistol -> pump shotgun $1,500", Economy.UpgradePrice(Weapon.Pistol), 1500, 0);
        T.Check("Shotgun -> SMG $3,000", Economy.UpgradePrice(Weapon.Shotgun), 3000, 0);
        T.Check("SMG -> carbine rifle $6,000", Economy.UpgradePrice(Weapon.Smg), 6000, 0);
        T.Check("Carbine rifle can't be upgraded", Economy.UpgradePrice(Weapon.CarbineRifle), -1, 0);
        T.True("Articles read right", Economy.WithArticle(Weapon.Smg) == "an SMG" && Economy.WithArticle(Weapon.Shotgun) == "a pump shotgun" && Economy.WithArticle(Weapon.CarbineRifle) == "a carbine rifle");
        T.Check("Knife to carbine: $11,250 a guard", Economy.FullLadderPrice(), 11250, 0);
        T.True("Four guards to SMGs costs about a small front", Math.Abs(4 * (750 + 1500 + 3000) - 4500 * 5) < 2000);
        T.True("Guns unlock with rank", Economy.MaxWeapon(Rank.Recruit) == Weapon.Shotgun && Economy.MaxWeapon(Rank.Soldier) == Weapon.Smg && Economy.MaxWeapon(Rank.Veteran) == Weapon.CarbineRifle);
        T.True("Broker trust: small 0, mid 3, big 8", Economy.TrustNeeded(0) == 0 && Economy.TrustNeeded(1) == 3 && Economy.TrustNeeded(2) == 8);
        bool bigSteps = true;
        for (Weapon w = Weapon.Knife; w < DefenseRating.TopWeapon; w++) if (DefenseRating.WeaponPower(w + 1) < DefenseRating.WeaponPower(w) * 1.2) bigSteps = false;
        T.True("Every step up the ladder is at least +20%", bigSteps);
        T.Check("Armory: knife fights like a pistol", DefenseRating.WeaponMultiplier(Weapon.Knife, true), 1.0, 0.001);
        T.Check("Armory: carbine +10%", DefenseRating.WeaponMultiplier(Weapon.CarbineRifle, true), 2.2, 0.001);

        var content = new ContentDb(Path.Combine(Path.GetTempPath(), "fr_none_" + Guid.NewGuid().ToString("N")), null);
        content.Load();
        var state = SaveService.NewEmpire();
        var e = new Empire(state, content);
        var rng = new Rng(7);

        var empty = Economy.Midnight(state, content, 0, 1.0, 1.0);
        T.True("No fronts, no guards: nothing paid", empty.Income == 0 && empty.Wages == 0);

        e.SetOwner("carwash_handson", Owners.Player);
        GuardState a = e.Recruit(rng), b = e.Recruit(rng);
        var pay = Economy.Midnight(state, content, 0, 1.0, 1.0);
        T.Check("Car wash pays $4,000", pay.Income, 4000, 0);
        T.Check("Two recruits cost $300", pay.Wages, 300, 0);
        T.Check("Net $3,700", pay.Net, 3700, 0);
        var doubled = Economy.Midnight(state, content, 0, 2.0, 0.5);
        T.True("INI multipliers apply (x2 income, x0.5 wages)", doubled.Income == 8000 && doubled.Wages == 150);

        // Broke: lose the front so there's no income, can't cover wages -> newest non-Loyal guard quits.
        e.SetOwner("carwash_handson", Owners.CrownKings);
        a.Trait = (int)Trait.Loyal;
        var broke = Economy.Midnight(state, content, 100, 1.0, 1.0);
        T.True("Can't pay: the newest non-Loyal guard quits", broke.Quit.Count == 1 && broke.Quit[0] == b);
        T.True("...and leaves the roster", !state.Guards.Contains(b));
        var loyalOnly = Economy.Midnight(state, content, 0, 1.0, 1.0);
        T.True("Loyal guards never quit over wages", loyalOnly.Quit.Count == 0 && state.Guards.Contains(a));

        T.Section("Game calendar (midnights, sleeping, switches)");
        var d = new DateTime(2013, 9, 17);
        T.Check("First check: nothing to pay", GameCalendar.MidnightsSince(null, d), 0, 0);
        T.Check("Same day: 0", GameCalendar.MidnightsSince("2013-09-17", d), 0, 0);
        T.Check("Next day: 1 midnight", GameCalendar.MidnightsSince("2013-09-16", d), 1, 0);
        T.Check("Slept 2 days: 2 midnights", GameCalendar.MidnightsSince("2013-09-15", d), 2, 0);
        T.Check("Skipped 10 days: capped at 3", GameCalendar.MidnightsSince("2013-09-07", d), 3, 0);
        T.Check("Clock set backwards: never pays", GameCalendar.MidnightsSince("2013-09-20", d), 0, 0);
        T.Check("Garbage stamp: 0", GameCalendar.MidnightsSince("nope", d), 0, 0);

        T.Section("Hiring (the Broker's recruits)");
        var s2 = SaveService.NewEmpire();
        var e2 = new Empire(s2, content);
        e2.RefreshOffers(rng, 0);
        T.Check("4 recruits a day", s2.Offers.Count, Economy.RecruitsPerDay, 0);
        T.True("Every recruit has a trait", s2.Offers.TrueForAll(o => o.Trait != (int)Trait.None));
        T.True("Offers aren't on the roster yet", s2.Guards.Count == 0);
        GuardState offer = s2.Offers[0];
        int price;
        T.True("Not enough cash: refused", e2.Hire(offer, 100, out price) == Empire.HireResult.NotEnoughCash && s2.Guards.Count == 0);
        T.True("Enough cash: hired", e2.Hire(offer, 10000, out price) == Empire.HireResult.Ok);
        T.True("Hired guard gets a real id and waits in the reserve", offer.Id > 0 && !offer.IsPosted && s2.Guards.Contains(offer));
        T.Check("Price matches the trait and skills", price, Economy.HirePrice((Trait)offer.Trait, Stats.Overall(offer)), 0);
        T.True("Better skills cost more", Economy.HirePrice(Trait.Tough, 60) > Economy.HirePrice(Trait.Tough, 38) && Economy.HirePrice(Trait.Tough, 38) > Economy.HirePrice(Trait.Tough, 20));
        T.Check("An average recruit costs the trait price", Economy.HirePrice(Trait.Tough, 38), 4000, 0);
        T.True("Offers come with skills", Stats.HasSkills(offer));
        T.True("Can't hire the same offer twice", e2.Hire(offer, 10000, out price) == Empire.HireResult.Gone);
        e2.RefreshOffers(rng, 1);
        T.True("Next morning: new faces, unhired ones gone", s2.Offers.Count == Economy.RecruitsPerDay && s2.OffersDay == 1);

        GuardState g = s2.Guards[0];
        T.True("New hire carries a knife", g.Weapon == (int)Weapon.Knife);
        T.True("Permanent death: INI on and Office on", e2.PermanentDeath(true));
        s2.GuardsRecover = true;
        T.True("Office switch turns it off", !e2.PermanentDeath(true));
        s2.GuardsRecover = false;
        T.True("INI off can't be overridden on", !e2.PermanentDeath(false));
        e2.UpgradeWeapon(g);
        T.True("Weapon upgrade: knife -> pistol, next is the shotgun", g.Weapon == (int)Weapon.Pistol && e2.WeaponUpgradePrice(g) == 1500);
        for (int i = 0; i < 10; i++) e2.UpgradeWeapon(g);
        T.True("A Recruit stops at the shotgun", g.Weapon == (int)Weapon.Shotgun && Empire.WeaponLocked(g));
        g.Rank = (int)Rank.Veteran;
        for (int i = 0; i < 10; i++) e2.UpgradeWeapon(g);
        T.True("Carbine rifle is the top tier", g.Weapon == (int)Weapon.CarbineRifle && e2.WeaponUpgradePrice(g) == -1);
        e2.LetGo(g);
        T.True("Let go: off the roster", !s2.Guards.Contains(g));

        T.True("The car wash is for sale at start", e2.CanBuy("carwash_handson"));
        T.True("Crew fronts aren't for sale", !e2.CanBuy("autoshop_atomic"));

        // Defense bar uses the doc formula: 2 recruits with pistols by day = 20.
        var content2 = new ContentDb(Path.Combine(Path.GetTempPath(), "fr_none_" + Guid.NewGuid().ToString("N")), null);
        content2.Load();
        FrontDef wash = content2.Front("carwash_handson");
        for (int i = 0; i < 2; i++) wash.Posts.Add(new PostDef { X = i, Y = 0, Z = 0 });
        var s3 = SaveService.NewEmpire();
        var e3 = new Empire(s3, content2);
        e3.SetOwner("carwash_handson", Owners.Player);
        GuardState r1 = e3.Recruit(rng), r2 = e3.Recruit(rng);
        Stats.Set(r1, new[] { 50, 50, 50, 50 }); Stats.Set(r2, new[] { 50, 50, 50, 50 }); // average skills
        e3.Assign(r1, "carwash_handson", 0);
        e3.Assign(r2, "carwash_handson", 1);
        T.Check("Front defense: 2 knife recruits by day = 12", e3.Defense("carwash_handson", 12), 12, 0.001);
        T.Check("...and 9 at night", e3.Defense("carwash_handson", 23), 9, 0.001);
        T.Check("Daily income/wages shown in the Office", e3.DailyIncome() - e3.DailyWages(), 3700, 0);
    }
}
