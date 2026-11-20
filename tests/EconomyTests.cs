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
        T.Check("Recruit wage $500", Economy.Wage(Rank.Recruit), 500, 0);
        T.Check("Lieutenant wage $2,000", Economy.Wage(Rank.Lieutenant), 2000, 0);
        bool inRange = true;
        foreach (Trait t in Empire.RecruitTraits)
        {
            int p = Economy.HirePrice(t);
            if (t == Trait.Nervous ? p != 1250 : (p < 2500 || p > 5000)) inRange = false;
        }
        T.True("Hire prices $2,500-$5,000, Nervous half price", inRange);
        T.Check("Knife -> pistol costs a pistol ($350)", Economy.UpgradePrice(Weapon.Knife), 350, 0);
        T.Check("Pistol -> pump shotgun $550", Economy.UpgradePrice(Weapon.Pistol), 550, 0);
        T.Check("Shotgun -> micro SMG $850", Economy.UpgradePrice(Weapon.Shotgun), 850, 0);
        T.Check("Micro SMG -> SMG $1,300", Economy.UpgradePrice(Weapon.MicroSmg), 1300, 0);
        T.Check("SMG -> assault rifle $1,400", Economy.UpgradePrice(Weapon.Smg), 1400, 0);
        T.Check("Assault rifle -> carbine rifle $2,100", Economy.UpgradePrice(Weapon.AssaultRifle), 2100, 0);
        T.Check("Carbine rifle can't be upgraded", Economy.UpgradePrice(Weapon.CarbineRifle), -1, 0);
        T.True("Articles read right", Economy.WithArticle(Weapon.Smg) == "an SMG" && Economy.WithArticle(Weapon.AssaultRifle) == "an assault rifle" && Economy.WithArticle(Weapon.MicroSmg) == "a micro SMG" && Economy.WithArticle(Weapon.Shotgun) == "a pump shotgun");
        T.Check("Knife to carbine: $6,550 a guard", Economy.FullLadderPrice(), 6550, 0);
        bool rising = true;
        for (Weapon w = Weapon.Knife; w < DefenseRating.TopWeapon; w++) if (DefenseRating.WeaponPower(w + 1) <= DefenseRating.WeaponPower(w)) rising = false;
        T.True("Every step up the ladder is stronger", rising);
        T.Check("Armory: knife fights like a pistol", DefenseRating.WeaponMultiplier(Weapon.Knife, true), 1.0, 0.001);
        T.Check("Armory: carbine +10%", DefenseRating.WeaponMultiplier(Weapon.CarbineRifle, true), 1.54, 0.001);

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
        T.Check("Two recruits cost $1,000", pay.Wages, 1000, 0);
        T.Check("Net $3,000", pay.Net, 3000, 0);
        var doubled = Economy.Midnight(state, content, 0, 2.0, 0.5);
        T.True("INI multipliers apply (x2 income, x0.5 wages)", doubled.Income == 8000 && doubled.Wages == 500);

        // Broke: lose the front so there's no income, can't cover wages -> newest non-Loyal guard quits.
        e.SetOwner("carwash_handson", Owners.CrownKings);
        a.Trait = (int)Trait.Loyal;
        var broke = Economy.Midnight(state, content, 600, 1.0, 1.0);
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
        T.Check("3 recruits a day", s2.Offers.Count, 3, 0);
        T.True("Every recruit has a trait", s2.Offers.TrueForAll(o => o.Trait != (int)Trait.None));
        T.True("Offers aren't on the roster yet", s2.Guards.Count == 0);
        GuardState offer = s2.Offers[0];
        int price;
        T.True("Not enough cash: refused", e2.Hire(offer, 100, out price) == Empire.HireResult.NotEnoughCash && s2.Guards.Count == 0);
        T.True("Enough cash: hired", e2.Hire(offer, 10000, out price) == Empire.HireResult.Ok);
        T.True("Hired guard gets a real id and waits in the reserve", offer.Id > 0 && !offer.IsPosted && s2.Guards.Contains(offer));
        T.Check("Price matches the trait", price, Economy.HirePrice((Trait)offer.Trait), 0);
        T.True("Can't hire the same offer twice", e2.Hire(offer, 10000, out price) == Empire.HireResult.Gone);
        e2.RefreshOffers(rng, 1);
        T.True("Next morning: 3 new faces, unhired ones gone", s2.Offers.Count == 3 && s2.OffersDay == 1);

        GuardState g = s2.Guards[0];
        T.True("New hire carries a knife", g.Weapon == (int)Weapon.Knife);
        e2.UpgradeWeapon(g);
        T.True("Weapon upgrade: knife -> pistol, next is the shotgun", g.Weapon == (int)Weapon.Pistol && e2.WeaponUpgradePrice(g) == 550);
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
        e3.Assign(e3.Recruit(rng), "carwash_handson", 0);
        e3.Assign(e3.Recruit(rng), "carwash_handson", 1);
        T.Check("Front defense: 2 knife recruits by day = 12", e3.Defense("carwash_handson", 12), 12, 0.001);
        T.Check("...and 9 at night", e3.Defense("carwash_handson", 23), 9, 0.001);
        T.Check("Daily income/wages shown in the Office", e3.DailyIncome() - e3.DailyWages(), 3000, 0);
    }
}
