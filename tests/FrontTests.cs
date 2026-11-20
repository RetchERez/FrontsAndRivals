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

    public static void Run()
    {
        ContentTests();
        EmpireTests();
        StreamRuleTests();
        LayLowTests();
    }

    static void ContentTests()
    {
        T.Section("Content (fronts.json, names.json, models.json)");

        // The shipped files load cleanly.
        var log = new List<string>();
        var shipped = new ContentDb(RepoContent, log.Add);
        shipped.Load();
        T.Check("Shipped fronts.json has 6 fronts", shipped.Fronts.Count, 6, 0);
        T.True("No fallback warnings for shipped files", !log.Exists(l => l.Contains("built-in") || l.Contains("could not")));
        T.True("Car wash is for sale at start", shipped.Front("carwash_littleseoul").StartOwner == Owners.ForSale);
        T.True("Truck depot starts with Ironside", shipped.Front("truckdepot_elysian").StartOwner == Owners.Ironside);
        T.Check("Truck depot income $12,000", shipped.Front("truckdepot_elysian").Income, 12000, 0);
        T.True("Shipped fronts are not scouted yet", !shipped.Front("carwash_littleseoul").IsScouted);
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
            FrontDef wash = missing.Front("carwash_littleseoul");
            wash.Door = Spot.At(-50.5f, -1390.25f, 29.3f, 270f);
            wash.Posts.Add(new PostDef { X = -48f, Y = -1392f, Z = 29.3f, H = 90f, Scenario = "WORLD_HUMAN_SMOKING" });
            wash.Posts.Add(new PostDef { X = -52f, Y = -1388f, Z = 29.3f, H = 180f, Scenario = "WORLD_HUMAN_GUARD_STAND" });
            wash.ConvoySpawns.Add(Spot.At(300f, -1500f, 29f, 45f));
            missing.SaveFronts();
            T.True("fronts.json written, no temp left", File.Exists(missing.FrontsPath) && !File.Exists(missing.FrontsPath + ".tmp"));

            var reread = new ContentDb(dir, null);
            reread.Load();
            FrontDef w2 = reread.Front("carwash_littleseoul");
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
            T.True("fronts.json with a byte-order mark still loads", bom.Front("carwash_littleseoul").IsScouted);

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
        FrontDef wash = c.Front("carwash_littleseoul");
        wash.Door = Spot.At(1, 1, 1, 0);
        for (int i = 0; i < 4; i++) wash.Posts.Add(new PostDef { X = i, Y = 1, Z = 1, Scenario = "WORLD_HUMAN_SMOKING" });
        FrontDef pawn = c.Front("pawnshop_strawberry");
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
        T.True("Fronts start with their doc owners", e.OwnerOf("chopshop_lamesa") == Owners.CrownKings && e.OwnerOf("carwash_littleseoul") == Owners.ForSale);

        GuardState g1 = e.Recruit(rng);
        T.True("Recruit has a full name and a model", g1 != null && g1.First != null && g1.Nick != null && g1.Last != null && g1.Model != null);
        T.True("Recruit starts as a pistol Recruit in the reserve", g1.Rank == (int)Rank.Recruit && g1.Weapon == (int)Weapon.Pistol && !g1.IsPosted);
        T.True("Female models get female names",
            content.Models.GuardsFemale.Contains(g1.Model) ? content.Names.FemaleNames.Contains(g1.First) : content.Names.MaleNames.Contains(g1.First));

        T.True("Can't post at a front you don't own", !e.Assign(g1, "carwash_littleseoul", 0));
        e.SetOwner("carwash_littleseoul", Owners.Player);
        T.True("Can post once you own it", e.Assign(g1, "carwash_littleseoul", 0));
        T.True("AtPost finds the guard", e.AtPost("carwash_littleseoul", 0) == g1);
        T.True("Post beyond the scouted posts refused", !e.Assign(g1, "pawnshop_strawberry", 3) && !e.Assign(g1, "carwash_littleseoul", 4));

        GuardState g2 = e.Recruit(rng);
        e.Assign(g2, "carwash_littleseoul", 0);
        T.True("Posting onto an occupied post swaps: new guard in", e.AtPost("carwash_littleseoul", 0) == g2);
        T.True("...old guard goes to where the new one was (reserve)", !g1.IsPosted);

        // Reserve limit 6.
        var fresh = new Empire(SaveService.NewEmpire(), content);
        int hired = 0;
        while (fresh.Recruit(rng) != null) hired++;
        T.Check("Reserve holds 6 unposted guards", hired, Empire.MaxReserve, 0);

        // Losing a front sends its guards home.
        e.Assign(g1, "carwash_littleseoul", 1);
        e.SetOwner("carwash_littleseoul", Owners.CrownKings);
        T.True("Lost front: guards back in the reserve", !g1.IsPosted && !g2.IsPosted);

        // Death and injury.
        e.SetOwner("carwash_littleseoul", Owners.Player);
        e.Assign(g1, "carwash_littleseoul", 0);
        e.GuardDown(g1, "the Little Seoul Car Wash", true, 0, 1000);
        T.True("Permanent death: dead, off the post, memorial line", g1.Status == GuardStatus.Dead && !g1.IsPosted && g1.FellAt == "the Little Seoul Car Wash");
        T.True("Dead guards can't be posted", !e.Assign(g1, "carwash_littleseoul", 0));
        T.True("Dead guards don't count as living", e.LivingCount() == 1);

        e.Assign(g2, "carwash_littleseoul", 2);
        e.GuardDown(g2, "x", false, 5000, 1000);
        T.True("Injury: keeps the post, status injured", g2.Status == GuardStatus.Injured && g2.Post == 2);
        T.Check("Not recovered before the time", e.Recover(5999), 0, 0);
        T.Check("Recovered after the time", e.Recover(6000), 1, 0);
        T.True("Back on duty at the same post", g2.Status == GuardStatus.Active && e.AtPost("carwash_littleseoul", 2) == g2);

        // Save round trip keeps guards and fronts; ids keep counting up.
        string dir = Path.Combine(Path.GetTempPath(), "fr_emp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var saves = new SaveService(dir, null);
            saves.Save(state);
            var back = saves.Load().State;
            T.Check("Guards survive save/load", back.Guards.Count, state.Guards.Count, 0);
            T.True("Front owner survives save/load", back.Fronts.Find(f => f.Id == "carwash_littleseoul").Owner == Owners.Player);
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
