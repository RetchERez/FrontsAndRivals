using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;
using FrontsAndRivals.Spawn;

static class FrontTests
{
    static readonly string RepoContent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Content"));

    static void WorkerAndNameTests()
    {
        T.Section("Front staff and the NPC pool");
        T.True("Staff only with 12 slots to spare", WorkerRules.Fits(10, 0, 28) && !WorkerRules.Fits(16, 0, 28));
        T.True("Staff leave when the budget gets tight", WorkerRules.MustYield(15, 2, 28) && !WorkerRules.MustYield(10, 2, 28));
        T.True("Two by day at a bigger front, one at night", WorkerRules.Count(1, 12) == 2 && WorkerRules.Count(0, 12) == 1 && WorkerRules.Count(2, 23) == 1);
        T.True("A bar gets bar staff", WorkerRules.Kind("bar_mirrorpark") == "bar" && System.Array.IndexOf(WorkerRules.Models("bar_mirrorpark"), "s_m_y_barman_01") >= 0);
        bool mixed = true;
        foreach (string id in new[] { "carwash_x", "autoshop_x", "cafe_x", "shop_x", "motel_x", "bar_x", "club_x", "depot_x", "jewelry_x", "other_x" })
        {
            bool f = false, m = false;
            foreach (string model in WorkerRules.Models(id)) { if (model.Contains("_f_")) f = true; if (model.Contains("_m_")) m = true; }
            if (!f || !m) mixed = false;
        }
        T.True("Every kind of business has women and men on staff", mixed);
        T.True("Night staff smoke or take a call", WorkerRules.Scenario("bar_x", 0, 23) == "WORLD_HUMAN_SMOKING");

        var content = new ContentDb(RepoContent, null);
        content.Load();
        T.True("Name pool: 70+ men, 50+ women, 80+ surnames, 50 nicknames", content.Names.MaleNames.Count >= 70 && content.Names.FemaleNames.Count >= 50
               && content.Names.Surnames.Count >= 80 && content.Names.Nicknames.Count >= 50);
        T.True("Guard models: 30+ men and 15 women, no uniformed security", content.Models.GuardsMale.Count >= 30 && content.Models.GuardsFemale.Count >= 15
               && !content.Models.GuardsMale.Exists(x => x.StartsWith("s_m_")));
        bool crewMix = true;
        foreach (CrewDef cd in content.Crews) { if (cd.Models.Count < 20 || !cd.Models.Exists(x => x.StartsWith("a_f_"))) crewMix = false; }
        T.True("Every crew: 20+ looks, women among them", crewMix);

        var es = SaveService.NewEmpire();
        var emp = new Empire(es, content);
        var rng = new System.Random(7);
        int nick = 0, n = 0;
        for (int day = 0; day < 6; day++)
        {
            emp.RefreshOffers(rng, day);
            foreach (GuardState g in es.Offers.ToList())
            {
                n++; if (!string.IsNullOrEmpty(g.Nick)) nick++;
                int price; emp.Hire(g, 1000000, out price);
            }
        }
        var seen = new HashSet<string>();
        bool unique = true;
        foreach (GuardState g in es.Guards.Concat(es.Offers)) if (!seen.Add(g.First + " " + g.Last)) unique = false;
        T.True("Only some recruits have a street name (" + nick + " of " + n + ")", nick > 0 && nick < n * 0.7);
        T.True("No two recruits share a full name", unique);
        int women = 0;
        foreach (GuardState g in es.Guards.Concat(es.Offers)) if (content.Models.GuardsFemale.Contains(g.Model)) women++;
        int people = es.Guards.Count + es.Offers.Count;
        T.True("Women and men among recruits (" + women + " of " + people + " women)", women > 0 && women < people);
        T.Section("Bodyguards");
        T.True("Up to two, with room kept for a live fight", EscortRules.Fits(17, 28) && !EscortRules.Fits(18, 28) && EscortRules.Max == 2);
        var bs = SaveService.NewEmpire();
        var be = new Empire(bs, content);
        be.SetOwner("carwash_handson", Owners.Player);
        GuardState b1 = be.Recruit(rng), b2 = be.Recruit(rng), b3 = be.Recruit(rng);
        be.Assign(b1, "carwash_handson", 0); be.Assign(b2, "carwash_handson", 1);
        double before = be.Defense("carwash_handson", 12);
        T.True("Call two bodyguards", be.SetEscort(b1, true) && be.SetEscort(b2, true) && be.EscortCount() == 2);
        T.True("A third is refused", !be.SetEscort(b3, true) && !b3.Escort);
        T.True("Their front is weaker while they're with you", be.Defense("carwash_handson", 12) < before);
        T.True("They keep their post", b1.Front == "carwash_handson" && b1.Post == 0);
        be.GuardDown(b1, "your side", false, 0, 1000);
        T.True("Hurt: no longer a bodyguard", !b1.Escort && be.EscortCount() == 1);
        T.True("Send back", be.SetEscort(b2, false) && be.EscortCount() == 0);
        be.RefreshOffers(rng, bs.Day);
        GuardState offerNow = bs.Offers[0];
        int calm = be.OfferPrice(offerNow);
        be.KilledByBoss(b2);
        T.True("Kill one of your own: recruits want danger money", be.OfferPrice(offerNow) > calm);
        bs.Day += 2;
        T.True("...and it passes after a day", be.OfferPrice(offerNow) == calm);
        T.Section("One-click front management");
        var qs = SaveService.NewEmpire();
        var qe = new Empire(qs, content);
        qe.SetOwner("carwash_handson", Owners.Player);
        GuardState q1 = qe.Recruit(rng), q2 = qe.Recruit(rng), q3 = qe.Recruit(rng);
        Stats.Set(q1, new[] { 20, 20, 20, 20 }); Stats.Set(q2, new[] { 80, 80, 80, 80 }); Stats.Set(q3, new[] { 50, 50, 50, 50 });
        int posts = content.Front("carwash_handson").Posts.Count;
        T.Check("All posts empty to start", qe.EmptyPosts("carwash_handson"), posts, 0);
        int placedNow = qe.FillPosts("carwash_handson");
        T.Check("Fill posts uses everyone free (up to the posts)", placedNow, Math.Min(3, posts), 0);
        if (posts < 3) T.True("The strongest go first", q2.IsPosted && !q1.IsPosted);
        int armCost = qe.ArmFrontPrice("carwash_handson");
        T.Check("Arming knife guards costs a pistol each", armCost, Economy.UpgradePrice(Weapon.Knife) * placedNow, 0);
        T.Check("Arm this front upgrades everyone posted", qe.ArmFront("carwash_handson"), placedNow, 0);
        T.True("...one step each", q2.Weapon == (int)Weapon.Pistol);
        var plain = new GuardState { First = "Ana", Nick = "", Last = "Diaz" };
        T.True("No empty quotes for a guard without a nickname", plain.ShortName == "Ana Diaz" && plain.FullName == "Ana Diaz");
    }

    public static void Run()
    {
        WorkerAndNameTests();
        ContentTests();
        EmpireTests();
        StreamRuleTests();
        LayLowTests();
        AutoPostTests();
    }

    static void AutoPostTests()
    {
        T.Section("Automatic guard posts");
        Spot door = Spot.At(100f, 200f, 30f, 0f); // facing north

        var probes = AutoPosts.Probes(door);
        T.Check("18 probe points (three rings of 6)", probes.Count, 18, 0);
        T.Check("First probe is 5 m straight out front (north)", probes[0][1] - door.Y, 5, 0.01);
        T.Check("...with no sideways offset", probes[0][0] - door.X, 0, 0.01);
        T.Check("Heading 90 (west) puts the first probe to the west", AutoPosts.Probes(Spot.At(0, 0, 0, 90f))[0][0], -5, 0.01);

        var chosen = new List<PostDef>();
        T.True("A spot 5 m out on the same floor is accepted", AutoPosts.Accept(door, chosen, 100f, 205f, 29f));
        chosen.Add(AutoPosts.MakePost(door, 0, 100f, 205f, 29f));
        T.True("A second spot 2 m from the first is refused (spacing)", !AutoPosts.Accept(door, chosen, 101f, 206.5f, 29f));
        T.True("A spot 4 m from the first is accepted", AutoPosts.Accept(door, chosen, 104f, 205f, 29f));
        T.True("Standing in the doorway is refused", !AutoPosts.Accept(door, new List<PostDef>(), 100.5f, 200.5f, 29f));
        T.True("More than 18 m away is refused", !AutoPosts.Accept(door, new List<PostDef>(), 100f, 219f, 29f));
        T.True("A rooftop 6 m up is refused", !AutoPosts.Accept(door, new List<PostDef>(), 100f, 205f, 35f));

        PostDef p = chosen[0];
        T.Check("Post stands 1 m above the ground the navmesh gave", p.Z, 30, 0.001);
        T.Check("Guard north of the door faces north (away from it)", p.H, 0, 0.01);
        T.Check("Guard west of the door faces west (90)", AutoPosts.FacingAway(door, 90f, 200f), 90, 0.01);
        T.Check("Guard south of the door faces south (180)", AutoPosts.FacingAway(door, 100f, 190f), 180, 0.01);
        T.Check("Guard east of the door faces east (270)", AutoPosts.FacingAway(door, 110f, 200f), 270, 0.01);
        T.Equal("Idles rotate: post 1 stands guard", AutoPosts.MakePost(door, 0, 0, 0, 0).Scenario, StreamRules.GuardStand);
        T.Equal("Idles rotate: post 2 smokes", AutoPosts.MakePost(door, 1, 0, 0, 0).Scenario, "WORLD_HUMAN_SMOKING");
        T.Equal("Idles rotate: post 4 is on the phone", AutoPosts.MakePost(door, 3, 0, 0, 0).Scenario, "WORLD_HUMAN_STAND_MOBILE");
    }

    static void ContentTests()
    {
        T.Section("Content (fronts.json, names.json, models.json)");

        // The shipped files load cleanly.
        var log = new List<string>();
        var shipped = new ContentDb(RepoContent, log.Add);
        shipped.Load();
        T.Check("Shipped fronts.json has 29 fronts", shipped.Fronts.Count, 29, 0);
        T.True("Exactly one starter front, and it is the car wash", shipped.Fronts.FindAll(x => x.Starter).Count == 1 && shipped.Front("carwash_handson").Starter);
        T.True("Front numbers are unique and fit a plate (1-99)", shipped.Fronts.TrueForAll(x => x.Number >= 1 && x.Number <= 99) && new System.Collections.Generic.HashSet<int>(shipped.Fronts.ConvertAll(x => x.Number)).Count == shipped.Fronts.Count);
        T.True("Every crew starts with fronts on its own turf", new[] { "CK", "IR", "VS" }.All(id => shipped.Fronts.Exists(x => x.StartOwner == id)));
        T.True("No fallback warnings for shipped files", !log.Exists(l => l.Contains("built-in") || l.Contains("could not")));
        T.True("Car wash is for sale at start", shipped.Front("carwash_handson").StartOwner == Owners.ForSale);
        T.True("Cold storage depot starts with Ironside", shipped.Front("depot_fridgit").StartOwner == Owners.Ironside);
        T.Check("Cold storage depot income $12,000", shipped.Front("depot_fridgit").Income, 12000, 0);
        T.True("Shipped car wash has its guard posts", shipped.Front("carwash_handson").IsScouted);
        T.True("Every shipped front has its door set", shipped.Fronts.TrueForAll(f => f.Door.IsSet));
        T.True("Built-in defaults match the shipped doors", ContentDb.DefaultFronts().TrueForAll(d => d.Door.IsSet && shipped.Front(d.Id) != null && Math.Abs(shipped.Front(d.Id).Door.X - d.Door.X) < 0.01f));
        T.True("Shipped name pools are filled", shipped.Names.MaleNames.Count >= 20 && shipped.Names.FemaleNames.Count >= 10);
        T.True("Shipped guard models are filled", shipped.Models.GuardsMale.Count >= 20 && shipped.Models.GuardsFemale.Count >= 3);

        string dir = Path.Combine(Path.GetTempPath(), "fr_content_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var missing = new ContentDb(dir, null);
            missing.Load();
            T.Check("No files: built-in 6 fronts", missing.Fronts.Count, 6, 0);
            T.True("No files: built-in names and models", missing.Names.MaleNames.Count > 0 && missing.Models.GuardsMale.Count > 0);

            // Scouting writes fronts.json; it reads back identically.
            FrontDef wash = missing.Front("carwash_handson");
            wash.Door = Spot.At(-50.5f, -1390.25f, 29.3f, 270f);
            wash.Posts.Add(new PostDef { X = -48f, Y = -1392f, Z = 29.3f, H = 90f, Scenario = "WORLD_HUMAN_SMOKING" });
            wash.Posts.Add(new PostDef { X = -52f, Y = -1388f, Z = 29.3f, H = 180f, Scenario = "WORLD_HUMAN_GUARD_STAND" });
            wash.ConvoySpawns.Add(Spot.At(300f, -1500f, 29f, 45f));
            missing.SaveFronts();
            T.True("fronts.json written, no temp left", File.Exists(missing.FrontsPath) && !File.Exists(missing.FrontsPath + ".tmp"));

            var reread = new ContentDb(dir, null);
            reread.Load();
            FrontDef w2 = reread.Front("carwash_handson");
            T.True("Scouted front reads back scouted", w2.IsScouted);
            T.Check("Door X round trip", w2.Door.X, -50.5, 0.001);
            T.Check("Two posts round trip", w2.Posts.Count, 2, 0);
            T.Equal("Post scenario round trip", w2.Posts[0].Scenario, "WORLD_HUMAN_SMOKING");
            T.Check("Convoy spawn round trip", w2.ConvoySpawns.Count, 1, 0);
            T.True("Written file uses the doc's layout", File.ReadAllText(missing.FrontsPath).Contains("\"door\": { \"x\": -50.50, \"y\": -1390.25, \"z\": 29.30, \"h\": 270.0 }"));

            // A file saved by Notepad with a byte-order mark still loads.
            byte[] raw = File.ReadAllBytes(missing.FrontsPath);
            byte[] withBom = new byte[raw.Length + 3];
            withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
            Array.Copy(raw, 0, withBom, 3, raw.Length);
            File.WriteAllBytes(missing.FrontsPath, withBom);
            var bom = new ContentDb(dir, null);
            bom.Load();
            T.True("fronts.json with a byte-order mark still loads", bom.Front("carwash_handson").IsScouted);

            // A second save keeps a .bak of the first.
            reread.SaveFronts();
            T.True("Second save keeps fronts.json.bak", File.Exists(reread.FrontsPath + ".bak"));

            // Broken or odd files never crash the load.
            File.WriteAllText(Path.Combine(dir, "fronts.json"), "[ { \"id\": \"a\", \"posts\": [1,2,");
            var broken = new ContentDb(dir, null);
            broken.Load();
            T.Check("Corrupt fronts.json falls back to built-ins", broken.Fronts.Count, 6, 0);

            File.WriteAllText(Path.Combine(dir, "fronts.json"),
                "[{\"id\":\"x\",\"startOwner\":\"martians\",\"posts\":[{},{},{},{},{},{}]},{\"id\":\"x\"},{\"name\":\"no id\"}]");
            var odd = new ContentDb(dir, null);
            odd.Load();
            T.Check("Duplicate and id-less fronts skipped", odd.Fronts.Count, 1, 0);
            T.True("Unknown owner becomes forsale", odd.Fronts[0].StartOwner == Owners.ForSale);
            T.Check("More than 4 posts trimmed to 4", odd.Fronts[0].Posts.Count, 4, 0);
            T.True("Missing door filled in, not null", odd.Fronts[0].Door != null && !odd.Fronts[0].Door.IsSet);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    static ContentDb ScoutedContent()
    {
        var c = new ContentDb(Path.Combine(Path.GetTempPath(), "fr_none_" + Guid.NewGuid().ToString("N")), null);
        c.Load();
        FrontDef wash = c.Front("carwash_handson");
        wash.Door = Spot.At(1, 1, 1, 0);
        for (int i = 0; i < 4; i++) wash.Posts.Add(new PostDef { X = i, Y = 1, Z = 1, Scenario = "WORLD_HUMAN_SMOKING" });
        FrontDef pawn = c.Front("jewelry_discountjewels");
        pawn.Door = Spot.At(2, 2, 2, 0);
        pawn.Posts.Add(new PostDef { X = 2, Y = 2, Z = 2 });
        return c;
    }

    static void EmpireTests()
    {
        T.Section("Empire (fronts and guards as data)");
        var content = ScoutedContent();
        var state = SaveService.NewEmpire();
        var e = new Empire(state, content);
        var rng = new Rng(42);

        T.Check("Every front gets a save entry", state.Fronts.Count, 6, 0);
        T.True("Fronts start with their doc owners", e.OwnerOf("autoshop_atomic") == Owners.CrownKings && e.OwnerOf("carwash_handson") == Owners.ForSale);

        GuardState g1 = e.Recruit(rng);
        T.True("Recruit has a full name and a model", g1 != null && g1.First != null && g1.Nick != null && g1.Last != null && g1.Model != null);
        T.True("Recruit starts as a knife-carrying Recruit in the reserve", g1.Rank == (int)Rank.Recruit && g1.Weapon == (int)Weapon.Knife && !g1.IsPosted);
        T.True("Female models get female names",
            content.Models.GuardsFemale.Contains(g1.Model) ? content.Names.FemaleNames.Contains(g1.First) : content.Names.MaleNames.Contains(g1.First));

        T.True("Can't post at a front you don't own", !e.Assign(g1, "carwash_handson", 0));
        e.SetOwner("carwash_handson", Owners.Player);
        T.True("Can post once you own it", e.Assign(g1, "carwash_handson", 0));
        T.True("AtPost finds the guard", e.AtPost("carwash_handson", 0) == g1);
        T.True("Post beyond the scouted posts refused", !e.Assign(g1, "jewelry_discountjewels", 3) && !e.Assign(g1, "carwash_handson", 4));

        GuardState g2 = e.Recruit(rng);
        e.Assign(g2, "carwash_handson", 0);
        T.True("Posting onto an occupied post swaps: new guard in", e.AtPost("carwash_handson", 0) == g2);
        T.True("...old guard goes to where the new one was (reserve)", !g1.IsPosted);

        // Reserve limit 6.
        var fresh = new Empire(SaveService.NewEmpire(), content);
        int hired = 0;
        while (fresh.Recruit(rng) != null) hired++;
        T.Check("Reserve holds 6 unposted guards", hired, Empire.MaxReserve, 0);

        // Losing a front sends its guards home.
        e.Assign(g1, "carwash_handson", 1);
        e.SetOwner("carwash_handson", Owners.CrownKings);
        T.True("Lost front: guards back in the reserve", !g1.IsPosted && !g2.IsPosted);

        // Death and injury.
        e.SetOwner("carwash_handson", Owners.Player);
        e.Assign(g1, "carwash_handson", 0);
        e.GuardDown(g1, "the Hands On Car Wash", true, 0, 1000);
        T.True("Permanent death: dead, off the post, memorial line", g1.Status == GuardStatus.Dead && !g1.IsPosted && g1.FellAt == "the Hands On Car Wash");
        T.True("Dead guards can't be posted", !e.Assign(g1, "carwash_handson", 0));
        T.True("Dead guards don't count as living", e.LivingCount() == 1);

        e.Assign(g2, "carwash_handson", 2);
        Stats.Set(g2, new[] { 50, 50, 50, 50 }); // average Stamina: the base recovery time
        e.GuardDown(g2, "x", false, 5000, 1000);
        T.True("Injury: keeps the post, status injured", g2.Status == GuardStatus.Injured && g2.Post == 2);
        T.Check("Not recovered before the time", e.Recover(5999), 0, 0);
        T.Check("Recovered after the time", e.Recover(6000), 1, 0);
        T.True("Back on duty at the same post", g2.Status == GuardStatus.Active && e.AtPost("carwash_handson", 2) == g2);

        // Save round trip keeps guards and fronts; ids keep counting up.
        string dir = Path.Combine(Path.GetTempPath(), "fr_emp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var saves = new SaveService(dir, null);
            saves.Save(state);
            var back = saves.Load().State;
            T.Check("Guards survive save/load", back.Guards.Count, state.Guards.Count, 0);
            T.True("Front owner survives save/load", back.Fronts.Find(f => f.Id == "carwash_handson").Owner == Owners.Player);
            T.True("Post survives save/load", back.Guards.Find(g => g.Id == g2.Id).Post == 2);
            var e2 = new Empire(back, content);
            T.True("New ids continue after load", e2.Recruit(rng).Id > g2.Id);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        // An M1 save (no fronts/guards fields) upgrades cleanly.
        string dir2 = Path.Combine(Path.GetTempPath(), "fr_m1_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir2);
            File.WriteAllText(Path.Combine(dir2, "empire.json"),
                "{\"schemaVersion\":1,\"createdUtc\":\"x\",\"saveCount\":4,\"rngState\":5,\"activeMs\":1,\"ownedFronts\":[],\"grudge\":[]}");
            var m1 = new SaveService(dir2, null).Load();
            var em = new Empire(m1.State, content);
            T.True("M1 save loads with empty roster and all 6 fronts", m1.Source == "empire.json" && m1.State.Guards.Count == 0 && m1.State.Fronts.Count == 6);
            T.True("M1 save: first guard id is 1", em.Recruit(rng).Id == 1);
        }
        finally { try { Directory.Delete(dir2, true); } catch { } }
    }

    static void StreamRuleTests()
    {
        T.Section("Streaming rules");
        T.True("Spawn inside 250 m", StreamRules.ShouldStream(false, 249f));
        T.True("Not spawned at 260 m when not streamed", !StreamRules.ShouldStream(false, 260f));
        T.True("Stays spawned at 290 m (no flicker)", StreamRules.ShouldStream(true, 290f));
        T.True("Despawns past 300 m", !StreamRules.ShouldStream(true, 301f));

        // Driving back and forth across 250-300 m never flickers.
        bool streamed = false; int changes = 0;
        float[] path = { 400, 280, 260, 249, 260, 290, 299, 280, 251, 249, 299, 301, 299, 251, 249 };
        foreach (float d in path) { bool n = StreamRules.ShouldStream(streamed, d); if (n != streamed) changes++; streamed = n; }
        T.Check("Back-and-forth drive: only 3 state changes", changes, 3, 0);

        T.Equal("Day: post's own idle", StreamRules.GuardScenario("WORLD_HUMAN_SMOKING", Trait.None, 12), "WORLD_HUMAN_SMOKING");
        T.Equal("Night (23:00): torch", StreamRules.GuardScenario("WORLD_HUMAN_SMOKING", Trait.None, 23), StreamRules.Torch);
        T.Equal("05:00 is day again", StreamRules.GuardScenario("WORLD_HUMAN_SMOKING", Trait.None, 5), "WORLD_HUMAN_SMOKING");
        T.Equal("Lookout: binoculars, even at night", StreamRules.GuardScenario("WORLD_HUMAN_SMOKING", Trait.Lookout, 23), StreamRules.Binoculars);
        T.Equal("No scenario set: stand guard", StreamRules.GuardScenario(null, Trait.None, 12), StreamRules.GuardStand);
    }

    static void LayLowTests()
    {
        T.Section("Laying low (wanted near a front)");
        var t = new LayLowTimer();
        T.True("No stars: visible", !t.Update(0, false, true, false));
        T.True("Stars within 250 m: hidden", t.Update(1000, true, true, false));
        T.True("Still wanted but drove off: still hidden", t.Update(2000, true, false, false));
        T.True("Stars grey (searching): still hidden", t.Update(3000, false, false, true));
        T.True("Stars cleared at 4 s: hidden for 60 s", t.Update(4000, false, false, false));
        T.True("...still hidden at 63 s", t.Update(63000, false, false, false));
        T.True("...back at 64 s", !t.Update(64000, false, false, false));
        T.True("Stars far from the front don't hide it", !new LayLowTimer().Update(0, true, false, false));
    }
}
