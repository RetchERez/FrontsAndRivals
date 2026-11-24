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
        T.True("Bodyguards are earned: none under 8 fronts, one at 8, two at 14", EscortRules.Slots(7) == 0 && EscortRules.Slots(8) == 1 && EscortRules.Slots(13) == 1 && EscortRules.Slots(14) == 2 && EscortRules.NextAt(9) == 14 && EscortRules.NextAt(14) == -1);
        T.True("...and only Soldiers or better", !EscortRules.CanCall(true, false, 0, 10, 0) && EscortRules.CanCall(true, false, 0, 10, 1) && !EscortRules.CanCall(true, false, 1, 10, 2) && EscortRules.CanCall(true, false, 1, 14, 2));
        var bs = SaveService.NewEmpire();
        var be = new Empire(bs, content);
        be.SetOwner("carwash_handson", Owners.Player);
        foreach (FrontDef fd in content.Fronts) if (be.OwnedCount() < 14) be.SetOwner(fd.Id, Owners.Player); // 14 fronts: both bodyguards earned
        GuardState b1 = be.Recruit(rng), b2 = be.Recruit(rng), b3 = be.Recruit(rng);
        b1.Rank = b2.Rank = b3.Rank = 1; // Soldiers
        be.Assign(b1, "carwash_handson", 0); be.Assign(b2, "carwash_handson", 1);
        double before = be.Defense("carwash_handson", 12);
        var nobody = new Empire(SaveService.NewEmpire(), content);
        GuardState early = nobody.Recruit(rng);
        T.True("No bodyguards before you own a front", !nobody.SetEscort(early, true));
        T.True("Call two bodyguards", be.SetEscort(b1, true) && be.SetEscort(b2, true) && be.EscortCount() == 2);
        foreach (FrontDef fd in content.Fronts) if (be.OwnedCount() > 9 && be.PlayerOwns(fd.Id) && fd.Id != "carwash_handson") be.SetOwner(fd.Id, Owners.CrownKings);
        T.True("Drop to 9 fronts: one bodyguard goes back to their post", be.TrimEscorts().Count == 1 && be.EscortCount() == 1);
        foreach (FrontDef fd in content.Fronts) if (be.OwnedCount() > 5 && be.PlayerOwns(fd.Id) && fd.Id != "carwash_handson") be.SetOwner(fd.Id, Owners.CrownKings);
        T.True("Drop below 8: the last one goes back too", be.TrimEscorts().Count == 1 && be.EscortCount() == 0);
        foreach (FrontDef fd in content.Fronts) if (be.OwnedCount() < 14) be.SetOwner(fd.Id, Owners.Player);
        be.SetEscort(b1, true); be.SetEscort(b2, true); // back up to two for the rest of the checks
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
        T.Section("Guard posts by size, and raids");
        T.True("Small fronts take 2 guards, mid 3, big 4", FrontDef.PostsForTier(0) == 2 && FrontDef.PostsForTier(1) == 3 && FrontDef.PostsForTier(2) == 4);
        T.Check("The car wash has 2 posts", content.Front("carwash_handson").Capacity, 2, 0);
        T.True("Small fronts face one fewer attacker", Resolver.SquadSize(0, 0, 10) == 1 && Resolver.SquadSize(1, 0, 10) == 3);
        var rs = SaveService.NewEmpire();
        var re = new Empire(rs, content);
        Nemesis.EnsureCrews(rs, content, new Random(5));
        re.SetOwner("carwash_handson", Owners.Player);
        FrontDef target = null;
        foreach (FrontDef d in content.Fronts) if (d.Tier == 0 && Owners.IsCrew(re.OwnerOf(d.Id))) { target = d; break; }
        T.True("There's a small crew front to raid", target != null);
        T.True("No team: the raid is blocked", re.RaidBlocked(target) != null);
        for (int i = 0; i < 4; i++) { GuardState rg = re.Recruit(rng); rg.Rank = (int)Rank.Veteran; rg.Weapon = (int)Weapon.CarbineRifle; Stats.Set(rg, new[] { 70, 70, 70, 70 }); }
        T.True("With guards it can go", re.RaidBlocked(target) == null && re.RaidTeam().Count == 4);
        double odds = re.RaidOdds(target, 12);
        T.True("Four armed veterans are favoured against a small front (" + (int)(odds * 100) + "%)", odds > 0.55);
        T.True("Raid sent", re.StartRaid(target, 1000, 12) && rs.Raid != null && re.OnRaid(re.RaidTeam().Count == 0 ? rs.Guards[0] : rs.Guards[0]));
        T.True("Raiders can't also be bodyguards, and don't defend their posts", !re.SetEscort(rs.Guards[0], true));
        T.True("Only one raid at a time", re.RaidBlocked(target) != null);
        int won = 0, trials = 300;
        for (int t = 0; t < trials; t++)
        {
            var ts = SaveService.NewEmpire();
            var te = new Empire(ts, content);
            Nemesis.EnsureCrews(ts, content, new Random(t));
            te.SetOwner("carwash_handson", Owners.Player);
            for (int i = 0; i < 4; i++) { GuardState rg = te.Recruit(rng); rg.Rank = (int)Rank.Veteran; rg.Weapon = (int)Weapon.CarbineRifle; Stats.Set(rg, new[] { 70, 70, 70, 70 }); }
            te.StartRaid(target, 0, 12);
            Raids.Outcome ro = Raids.Resolve(ts, content, te, new Random(t * 7 + 1), 12, 200000, true, 1000);
            if (ro.Won) { won++; if (te.OwnerOf(target.Id) != Owners.Player) won -= 1000; }
            if (ts.Raid != null) won -= 1000;
        }
        T.True("Raids resolve, a win hands you the front (" + won + " of " + trials + " won)", won > trials * 0.5 && won < trials);
        {
            var ts = SaveService.NewEmpire();
            var te = new Empire(ts, content);
            Nemesis.EnsureCrews(ts, content, new Random(5));
            te.SetOwner("carwash_handson", Owners.Player);
            for (int i = 0; i < 4; i++) { GuardState rg = te.Recruit(rng); rg.Rank = (int)Rank.Veteran; rg.Weapon = (int)Weapon.CarbineRifle; }
            te.StartRaid(target, 0, 20);
            T.True("A raid remembers the hour it set off, so it rolls the odds you were shown", ts.Raid.Hour == 20);
            string other = te.OwnerOf(target.Id) == Owners.Velvet ? Owners.Ironside : Owners.Velvet;
            te.SetOwner(target.Id, other); // another crew took it while they were on the way
            Raids.Outcome ro = Raids.Resolve(ts, content, te, new Random(1), 2, 200000, true, 1000);
            T.True("The front changed hands mid-raid: called off, not a failed raid, nobody hurt", ro.Aborted && !ro.Won && ro.Dead.Count == 0 && ro.Injured.Count == 0);
        }

        T.Section("Difficulty and resetting the city");
        T.True("Difficulty: Normal is x1, Brutal pushes harder and sooner, Relaxed eases off",
               Difficulty.Strength(0) == 1.0 && Difficulty.Strength(2) > 1.4 && Difficulty.Cooldown(2) < 1 && Difficulty.Strength(-2) < 0.8 && Difficulty.Cooldown(-2) > 1.4);
        T.True("Old saves (step 0) read as Normal", Difficulty.Name(0) == "Normal" && Difficulty.Name(9) == "Brutal");
        var ds = SaveService.NewEmpire();
        var de = new Empire(ds, content);
        Nemesis.EnsureCrews(ds, content, new Random(2));
        de.SetOwner("carwash_handson", Owners.Player);
        GuardState vet = de.Recruit(rng); vet.Rank = (int)Rank.Veteran; vet.Weapon = (int)Weapon.Smg;
        GuardState vet2 = de.Recruit(rng);
        double normalOdds = de.RaidOdds(target, 12);
        ds.Difficulty = 2;
        T.True("Brutal: rival fronts are harder to raid", de.RaidOdds(target, 12) < normalOdds);
        ds.Difficulty = 0;
        de.Assign(vet, "carwash_handson", 0);
        de.FindFront("carwash_handson").Cameras = true;
        de.ResetCity(new Random(3), 5000);
        T.True("Reset the city: fronts go back to where they started", de.OwnerOf("carwash_handson") == content.Front("carwash_handson").StartOwner && !de.FindFront("carwash_handson").Cameras);
        T.True("...you keep your people, ranks and guns, in the reserve", ds.Guards.Contains(vet) && vet.Rank == (int)Rank.Veteran && vet.Weapon == (int)Weapon.Smg && !vet.IsPosted);
        T.True("...the crews come back and the run is counted", ds.Crews.Count == content.Crews.Count && ds.CityResets == 1 && ds.Attack == null);

        T.Section("Map icons");
        T.True("Each kind of business gets the game's own icon", WorkerRules.MapIcon("carwash_handson", false) == 100 && WorkerRules.MapIcon("bar_himen", false) == 93
               && WorkerRules.MapIcon("club_cockatoos", false) == 614 && WorkerRules.MapIcon("liquor_brouge", false) == 52 && WorkerRules.MapIcon("depot_fridgit", false) == 473);
        T.True("For sale uses the for-sale icons", WorkerRules.MapIcon("cafe_noodleexchange", true) == 375 && WorkerRules.MapIcon("factory_bagco", true) == 474);

        T.Section("Next Step hint");
        var ns = SaveService.NewEmpire();
        var ne = new Empire(ns, content);
        ne.EnsureFronts();
        Nemesis.EnsureCrews(ns, content, new Random(9));
        T.True("New empire: take the car wash", ne.NextStep(true).Title.StartsWith("Take the") && ne.NextStep(true).FrontId == "carwash_handson");
        ne.SetOwner("carwash_handson", Owners.Player);
        ne.StarterCrew(content.Front("carwash_handson"), rng);
        T.True("Knife guards: arm them", ne.NextStep(true).Title.StartsWith("Arm the"));
        ns.Attack = new AttackState { Front = "carwash_handson", Phase = AttackPhase.Window };
        T.True("Under attack comes first", ne.NextStep(true).Title.StartsWith("Defend the") && ne.NextStep(true).FrontId == "carwash_handson");
        ns.Attack = null;

        T.Section("Early-game safeguards");
        var bs2 = SaveService.NewEmpire();
        var be2 = new Empire(bs2, content);
        be2.SetOwner("carwash_handson", Owners.CrownKings); // lost it
        be2.EnsureFronts();
        T.True("No rescue for someone who never held a front", !be2.BrokerRescue());
        T.True("Before your first front you're not in the game yet", !be2.InTheGame());
        T.True("Patrols: the first person holds the door, every second one walks", !StreamRules.Patrols(0, 3) && StreamRules.Patrols(1, 3) && !StreamRules.Patrols(2, 3) && !StreamRules.Patrols(1, 1));
        int[] loop = StreamRules.LoopOrder(new[] { 0f, -10f, 10f, 0f }, new[] { 10f, 0f, 0f, -10f });
        T.True("Patrol points walk a loop around the door", loop[0] == 3 && loop[1] == 2 && loop[2] == 0 && loop[3] == 1);
        T.True("Hangouts: groups of 2 or 3", Enumerable.Range(0, 200).All(i => HangoutRules.GroupSize(i) == 2 || HangoutRules.GroupSize(i) == 3)
            && Enumerable.Range(0, 200).Any(i => HangoutRules.GroupSize(i) == 3));
        T.True("...some round a car, room kept for a fight", Enumerable.Range(0, 100).Count(i => HangoutRules.WithCar(i)) == HangoutRules.CarChancePct
            && HangoutRules.Fits(10, 3, 28) && !HangoutRules.Fits(16, 3, 28));
        T.True("Crews on their streets: hanging out, walking, cruising", Enumerable.Range(0, 100).Select(i => HangoutRules.Kind(i)).Distinct().Count() == 3);
        T.True("Two groups on cool streets, three once it's warm; quicker when hot", HangoutRules.MaxGroups(0) == 2 && HangoutRules.MaxGroups(50) == 3 && HangoutRules.RespawnMs(100) < HangoutRules.RespawnMs(0));
        T.True("Walkers alone or in pairs, cruisers 2-4", Enumerable.Range(0, 100).All(i => HangoutRules.GroupSize(HangoutKind.Walk, i) <= 2 && HangoutRules.GroupSize(HangoutKind.Cruise, i) >= 2 && HangoutRules.GroupSize(HangoutKind.Cruise, i) <= 4));
        T.True("A car kill is spotted (and calls for backup)", StreamRules.VehicleKill(StreamRules.RunOverHash, false) && StreamRules.VehicleKill(0, true) && !StreamRules.VehicleKill(Markers.Hash("WEAPON_PISTOL"), false) && StreamRules.BackupSize == 2 && StreamRules.BackupDelayMs == 20000);

        T.True("Frame-rate safeguard: fewer street groups when the game struggles", HangoutRules.MaxGroups(50, 60) == 3 && HangoutRules.MaxGroups(50, 35) == 1 && HangoutRules.MaxGroups(50, 25) == 0 && HangoutRules.MaxGroups(50, 0) == 3);
        T.True("Street fights on turf borders, now and then", Enumerable.Range(0, 100).Count(i => HangoutRules.Clash(i)) == HangoutRules.ClashChancePct);

        T.True("Street crime is occasional: most groups do nothing", Enumerable.Range(0, 100).Count(i => HangoutRules.CrimeFor(HangoutKind.Walk, i) == StreetCrime.None) == 68
            && Enumerable.Range(0, 100).Count(i => HangoutRules.CrimeFor(HangoutKind.Cruise, i) == StreetCrime.None) == 74 && HangoutRules.CrimeFor(HangoutKind.HangOut, 1) == StreetCrime.None);
        T.True("...one at a time, three minutes apart, and cops come to 40% of street fights", HangoutRules.CrimeCooldownMs == 180000 && Enumerable.Range(0, 100).Count(i => HangoutRules.PoliceCome(i)) == 40);

        var ms2 = SaveService.NewEmpire();
        ms2.Messages = new List<MessageEntry> { new MessageEntry { Sender = "The Office", Subject = "Car Wash is yours", Message = "Arm them.", When = "14:05", Day = 2 } };
        var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(EmpireState));
        var mem = new System.IO.MemoryStream(); ser.WriteObject(mem, ms2); mem.Position = 0;
        var back = (EmpireState)ser.ReadObject(mem);
        T.True("The Messages page is saved with the empire", back.Messages != null && back.Messages.Count == 1 && back.Messages[0].Subject == "Car Wash is yours" && back.Messages[0].Day == 2);

        T.Section("Crew outfits");
        var od = OutfitData.Load(System.IO.Path.Combine(content.ContentDir, "outfits.json"));
        T.True("Outfit data loads", od != null && od.Styles != null && od.Styles.Count >= 7);
        bool allColoured = true;
        foreach (string crewId in new[] { "CK", "IR", "VS" })
            for (int seed = 0; seed < 300; seed++)
            {
                Dressing d = OutfitRules.Choose(od, crewId, CrewColors.ForCrew(crewId), seed);
                if (d == null || !d.Look.Wears(CrewColors.ForCrew(crewId))) allColoured = false;
            }
        T.True("Every rival crew member wears their crew colour on a major piece (900 checked)", allColoured);
        bool playerOk = true;
        foreach (string style in CrewColors.Styles)
            foreach (CrewColor c in CrewColors.All)
                foreach (bool female in new[] { false, true })
                    for (int seed = 0; seed < 20; seed++)
                    {
                        Dressing d = OutfitRules.Choose(od, CrewColors.StyleKey(style), c.Key, seed, female);
                        if (d == null || d.Female != female || !d.Look.Wears(c.Key)) playerOk = false;
                    }
        T.True("Your crew: every style in every colour, for men and women", playerOk);
        var od2 = OutfitData.Load(System.IO.Path.Combine(content.ContentDir, "outfits.json"));
        int dropped = OutfitRules.Prune(od2, (female, c, d, t) => !(c == 11 && d == 384)); // pretend the game lacks one hoodie
        T.True("The outfit check drops pieces the game rejects and keeps the rest", dropped > 0 && od2.Styles["CK"].All(l => !l.Parts.Any(p => p.Component == 11 && p.Drawable == 384))
            && OutfitRules.Choose(od2, "CK", "forest", 3) != null);
        Dressing hoodie = OutfitRules.Choose(od, "player_street", "red", 0, false);
        OutfitLook swap = OutfitRules.FindWhole(od, false, "red", hoodie.Look, l => !l.Parts.Any(p => p.Component == 11 && p.Drawable == hoodie.Look.Parts.First(q => q.Component == 11).Drawable));
        T.True("A look the game can't fully show is swapped for a whole one in the same colour and gender",
            swap != null && swap != hoodie.Look && swap.Gender == "m" && swap.Wears("red"));
        T.True("No whole look at all: none is offered (default clothes instead)", OutfitRules.FindWhole(od, true, "red", null, l => false) == null);
        {
            // Gang wardrobes: extras on top of each look
            var many = Enumerable.Range(0, 400).Select(i => OutfitRules.Choose(od, "CK", "forest", i)).Where(x => x != null).ToList();
            T.True("Dead Kings: some wear a green beanie, and no chain clips through a hoodie", many.Any(x => x.Extras.Any(e => e.Slot == "hat")) && many.Any(x => !x.Extras.Any(e => e.Slot == "hat")) && many.All(x => !x.Extras.Any(e => e.Slot == "neck")));
            T.True("Uniforms: every Dead King in the same Dark Green hoodie, same trousers, same shoes", many.Select(x => x.Look.Parts.First(p => p.Component == 11).Drawable + (x.Female ? 1000 : 0)).Distinct().Count() == 2
                && many.Select(x => x.Look.Parts.First(p => p.Component == 11).TextureFor("forest")).Distinct().Count() <= 2 && many.Where(x => !x.Female).Select(x => x.Legs.Drawable * 100 + x.Legs.Texture).Distinct().Count() == 1 && many.Where(x => !x.Female).Select(x => x.Shoes.Drawable * 100 + x.Shoes.Texture).Distinct().Count() == 1);
            T.True("...but they're individuals: different hair, beards and ages", many.Where(x => !x.Female).Select(x => x.Hair).Distinct().Count() > 5 && many.Any(x => x.Beard >= 0) && many.Any(x => !x.Female && x.Beard < 0) && many.Select(x => x.Age).Distinct().Count() == 3);
            T.True("Women never get a beard", many.Where(x => x.Female).All(x => x.Beard < 0));
            double old = many.Count(x => x.Age == 2) / (double)many.Count;
            T.True("About half in their twenties, the rest in their thirties or around Michael's age (" + (int)(old * 100) + "% around 45)", old > 0.08 && old < 0.25 && many.Count(x => x.Age == 0) > many.Count * 0.38);
            T.True("Under a hat, always a short cut", many.Where(x => x.Extras.Any(e => e.Slot == "hat")).All(x => od.HatHair[x.Female ? "f" : "m"].Contains(x.Hair)));
            T.True("Dead Kings pull up a bandana for attacks only", many.All(x => x.Extras.Where(e => e.Slot == "mask").All(e => e.AttackOnly)) && many.Any(x => x.Extras.Any(e => e.Slot == "mask")));
            var ck = od.Extras["CK"].First(e => e.Slot == "hat").Male[0];
            T.True("...and their beanie is the Dark Green one", many.Where(x => !x.Female).SelectMany(x => x.Extras).Where(e => e.Slot == "hat").All(e => e.Texture == ck.Textures["forest"]));
            var ir = Enumerable.Range(0, 400).Select(i => OutfitRules.Choose(od, "IR", "orange", i)).Where(x => x != null).ToList();
            var irMask = od.Extras["IR"].First(e => e.Slot == "mask").Male[0];
            T.True("Hellfire MC: biker skull caps, wraparounds and skull bandanas", ir.SelectMany(x => x.Extras).Where(e => e.Slot == "mask").All(e => e.Texture == irMask.FixedTexture) && ir.Any(x => x.Extras.Any(e => e.Slot == "glasses"))
                && ir.SelectMany(x => x.Extras).Where(e => e.Slot == "hat").All(e => e.Drawable == (od.Extras["IR"].First(h => h.Slot == "hat").Male[0].Drawable) || e.Drawable == od.Extras["IR"].First(h => h.Slot == "hat").Female[0].Drawable));
            var ckMask = many.SelectMany(x => x.Extras).First(e => e.Slot == "mask");
            T.True("Every crew has its own look: Dead Kings' green bandana isn't Hellfire's skull one, and nobody shares a hat",
                ckMask.Texture != irMask.FixedTexture && od.Extras["CK"].First(e => e.Slot == "hat").Male[0].Drawable != od.Extras["IR"].First(e => e.Slot == "hat").Male[0].Drawable
                && od.Extras["VS"].First(e => e.Slot == "hat").Male[0].Drawable != od.Extras["CK"].First(e => e.Slot == "hat").Male[0].Drawable);
            var vs = Enumerable.Range(0, 400).Select(i => OutfitRules.Choose(od, "VS", "blue", i)).Where(x => x != null).ToList();
            T.True("Vinewood Elite: some of the men in a royal blue trilby", vs.Any(x => x.Extras.Any(e => e.Slot == "hat" && e.Texture == od.Extras["VS"].First(h => h.Slot == "hat").Male[0].Textures["blue"])) && vs.Where(x => x.Female).All(x => !x.Extras.Any(e => e.Slot == "hat")));
            T.True("Vinewood Elite: a tie only with a shirt, never masked", vs.All(x => !x.Extras.Any(e => e.Slot == "neck") || x.Look.Under == "shirt") && vs.Any(x => x.Extras.Any(e => e.Slot == "neck")) && vs.All(x => !x.Extras.Any(e => e.Slot == "mask")));
            var boss = OutfitRules.Choose(od, "CK", "forest", 5, false, true);
            T.True("Bosses and lieutenants wear a gold watch and shades (a chain only on an open collar)", boss.Extras.Any(e => e.Slot == "watch") && boss.Extras.Any(e => e.Slot == "glasses") && !boss.Extras.Any(e => e.Slot == "neck"));
            T.True("The same person always wears the same extras", OutfitRules.Choose(od, "IR", "orange", 77).Extras.Count == OutfitRules.Choose(od, "IR", "orange", 77).Extras.Count);
            var red = Enumerable.Range(0, 300).Select(i => OutfitRules.Choose(od, "player_street", "red", i)).Where(x => x != null).ToList();
            T.True("Your crew never wears a hat in someone else's colour (there's no red beanie)", red.All(x => !x.Extras.Any(e => e.Slot == "hat")));
            var od3 = OutfitData.Load(System.IO.Path.Combine(content.ContentDir, "outfits.json"));
            int gone = OutfitRules.PruneExtras(od3, (f, c, d, t) => true, (f, prop, d, t) => prop != 0); // pretend the game has no hats
            T.True("The outfit check drops extras the game rejects", gone > 0 && Enumerable.Range(0, 100).All(i => { var x = OutfitRules.Choose(od3, "CK", "forest", i); return x == null || !x.Extras.Any(e => e.Slot == "hat"); }));
        }
        T.True("Vinewood Elite all wear the black long suit, the men with a shirt and tie, the women in a plain white shirt", Enumerable.Range(0, 200).All(i => { var x = OutfitRules.Choose(od, "VS", "blue", i); return x.Look.Uniform && x.Look.Under == (x.Female ? "plain" : "shirt"); }));
        T.True("Your crew wears the uniform of your style when it comes in your colour, the nearest look otherwise", Enumerable.Range(0, 100).All(i => OutfitRules.Choose(od, "player_smart", "blue", i).Look.Uniform)
            && Enumerable.Range(0, 100).All(i => { var x = OutfitRules.Choose(od, "player_smart", "black", i); return x != null && !x.Look.Uniform; }));
        T.True("The same guard always looks the same", OutfitRules.Choose(od, "player_street", "red", 42).Hair == OutfitRules.Choose(od, "player_street", "red", 42).Hair);
        T.True("Patrol stops last 12-25 seconds", Enumerable.Range(0, 100).All(i => StreamRules.PatrolDwellMs(i) >= 12000 && StreamRules.PatrolDwellMs(i) < 25000));
        be2.SetOwner("carwash_handson", Owners.Velvet); // lost it to another crew
        bs2.LastHeldDay = 3; bs2.Day = 4;
        T.True("Broker rescue waits two days", !be2.BrokerRescue());
        bs2.Day = 5;
        T.True("...then the car wash goes back to its old crew, thinly held", be2.BrokerRescue() && be2.OwnerOf("carwash_handson") == Owners.CrownKings
            && be2.Find("carwash_handson").WeakUntilDay >= bs2.Day);
        T.True("...once", !be2.BrokerRescue());
        be2.SetOwner("carwash_handson", Owners.Player);
        T.True("Holding a front resets the clock", !be2.BrokerRescue() && bs2.LastHeldDay == bs2.Day);
        var ss = SaveService.NewEmpire();
        var se = new Empire(ss, content);
        se.SetOwner("carwash_handson", Owners.Player);
        T.Check("The starter car wash comes with two posted guards", se.StarterCrew(content.Front("carwash_handson"), rng), 2, 0);
        T.True("...only the first time", se.StarterCrew(content.Front("carwash_handson"), rng) == 0 && se.LivingCount() == 2);
        T.True("Before you own a front: walking up or shooting nearby doesn't start a fight", !StreamRules.CrewProvoked(false, true, true, false));
        T.True("...but hurting one of them does", StreamRules.CrewProvoked(false, false, false, true));
        T.True("Once you're in the game, walking up does", StreamRules.CrewProvoked(true, true, false, false));

        T.Section("Rival soldiers: notice, warn, fight");
        const float R = 25f;
        Func<Watch, float, bool, Watch> walk = (now, d, inGame) => WatchRules.Next(now, d, true, false, false, false, inGame, 0, 8000, R);
        T.True("Far away they ignore you", walk(Watch.Calm, 70f, true) == Watch.Calm);
        T.True("On foot within 50 m they start watching", walk(Watch.Calm, 45f, true) == Watch.Watching);
        T.True("Within 25 m they warn you", walk(Watch.Watching, 20f, true) == Watch.Warning);
        T.True("A warning holds until you're 5 m past the line", walk(Watch.Warning, 28f, true) == Watch.Warning && walk(Watch.Warning, 32f, true) == Watch.Watching);
        T.True("Watching fades once you're well away", walk(Watch.Watching, 55f, true) == Watch.Watching && walk(Watch.Watching, 65f, true) == Watch.Calm);
        T.True("Walking right up to them starts the fight", walk(Watch.Warning, 6f, true) == Watch.Hostile);
        T.True("The fuse running out starts the fight", WatchRules.Next(Watch.Warning, 20f, true, false, false, false, true, 8000, 8000, R) == Watch.Hostile
            && WatchRules.Next(Watch.Warning, 20f, true, false, false, false, true, 7500, 8000, R) == Watch.Warning);
        T.True("Aiming at one of them starts the fight", WatchRules.Next(Watch.Watching, 40f, true, true, false, false, true, 0, 8000, R) == Watch.Hostile);
        T.True("Shooting nearby starts the fight", WatchRules.Next(Watch.Calm, 70f, true, false, true, false, true, 0, 8000, R) == Watch.Hostile);
        T.True("Hurting one always starts the fight", WatchRules.Next(Watch.Calm, 70f, false, false, false, true, false, 0, 8000, R) == Watch.Hostile);
        T.True("Driving past never bothers them", WatchRules.Next(Watch.Calm, 5f, false, false, false, false, true, 0, 8000, R) == Watch.Calm
            && WatchRules.Next(Watch.Warning, 10f, false, false, false, false, true, 99000, 8000, R) == Watch.Watching);
        T.True("Before your first front they warn but never start it", walk(Watch.Warning, 5f, false) == Watch.Warning
            && WatchRules.Next(Watch.Warning, 10f, true, true, true, false, false, 99000, 8000, R) == Watch.Warning);
        T.True("Once it's a fight it stays a fight", walk(Watch.Hostile, 200f, true) == Watch.Hostile);
        T.Check("Small front, cool streets: 8 s warning", WatchRules.WarnMs("CK", 0, 20), 8000, 0);
        T.Check("Bigger front: 5 s", WatchRules.WarnMs("CK", 2, 20), 5000, 0);
        T.Check("Hot streets: 3 s", WatchRules.WarnMs("CK", 0, 75), 3000, 0);
        T.Check("Blazing streets: no warning", WatchRules.WarnMs("VS", 0, 95), 0, 0);
        T.True("Hellfire MC have the shortest fuse, the Vinewood Elite the longest", WatchRules.WarnMs("IR", 0, 20) < WatchRules.WarnMs("CK", 0, 20) && WatchRules.WarnMs("CK", 0, 20) < WatchRules.WarnMs("VS", 0, 20));
        T.True("Blazing streets: walking into range starts the fight", WatchRules.Next(Watch.Watching, 20f, true, false, false, false, true, 0, 0, R) == Watch.Hostile);
        T.True("Last warning comes halfway", !WatchRules.LastWarningDue(3900, 8000) && WatchRules.LastWarningDue(4000, 8000) && !WatchRules.LastWarningDue(9000, 0));
        T.True("Calm and unseen, you can creep closer", WatchRules.Next(Watch.Calm, 12f, true, false, false, false, true, 0, 8000, R, false) == Watch.Calm);
        T.True("...but not right up behind them", WatchRules.Next(Watch.Calm, 2.5f, true, false, false, false, true, 0, 8000, R, false) == Watch.Hostile);
        T.True("Once they're watching, ducking out of sight doesn't reset them", WatchRules.Next(Watch.Watching, 20f, true, false, false, false, true, 0, 8000, R, false) == Watch.Warning);
        T.True("Crouched in stealth mode they notice you later", WatchRules.SightScale(true, 0, false) == 0.6f && WatchRules.SightScale(true, 100, false) < WatchRules.SightScale(true, 0, false));
        T.True("Darkness helps, and stacks with stealth", WatchRules.SightScale(false, 0, true) == 0.75f && WatchRules.SightScale(true, 100, true) < 0.31f && WatchRules.SightScale(false, 0, false) == 1f);
        T.True("Night is 9 pm to 5 am", WatchRules.IsNight(22) && WatchRules.IsNight(3) && !WatchRules.IsNight(5) && !WatchRules.IsNight(12));
        float half = WatchRules.SightScale(true, 50, false); // 0.5
        T.True("Sneaking: no warning at 20 m that would come walking", walk(Watch.Watching, 20f, true) == Watch.Warning
            && WatchRules.Next(Watch.Calm, 20f, true, false, false, false, true, 0, 8000, R, true, half) == Watch.Watching);
        T.True("Sneaking: walking right up means 4 m, not 8", WatchRules.Next(Watch.Calm, 6f, true, false, false, false, true, 0, 8000, R, true, half) == Watch.Warning);
        T.True("Once they've noticed you, crouching doesn't reset a warning", WatchRules.Next(Watch.Warning, 22f, true, false, false, false, true, 1000, 8000, R, true, half) == Watch.Warning);
        T.True("The fuse only runs out while you're in range", WatchRules.Next(Watch.Warning, 200f, true, false, false, false, true, 99000, 8000, R) == Watch.Calm
            && WatchRules.Next(Watch.Warning, 29f, true, false, false, false, true, 8000, 8000, R) == Watch.Hostile);
        T.True("Near the door with every soldier round the back: not heard", WatchRules.Next(Watch.Calm, 2f, true, false, false, false, true, 0, 8000, R, false, 1f, 20f) == Watch.Calm
            && WatchRules.Next(Watch.Calm, 2f, true, false, false, false, true, 0, 8000, R, false, 1f, 2f) == Watch.Hostile);
        T.True("Before your first front, aiming from far off is ignored", WatchRules.Next(Watch.Calm, 200f, true, true, false, false, false, 0, 8000, R) == Watch.Calm
            && WatchRules.Next(Watch.Calm, 40f, true, true, false, false, false, 0, 8000, R) == Watch.Warning);
        T.True("Night edges: 9 pm is night, 4 am too", WatchRules.IsNight(21) && WatchRules.IsNight(4) && !WatchRules.IsNight(20));
        T.True("Silenced shots are only heard up close", WatchRules.HeardShot(true, false, 70f) && !WatchRules.HeardShot(true, true, 30f) && WatchRules.HeardShot(true, true, 15f) && !WatchRules.HeardShot(false, false, 5f));
        T.True("Street crews have a shorter fuse than a front", WatchRules.StreetWarnMs("CK", 20) < WatchRules.WarnMs("CK", 0, 20) && WatchRules.StreetWarnMs("CK", 95) == 0);
        T.True("Street crews warn at 15 m", HangoutRules.AlertOnFoot == 15f);
        T.True("Every crew has warning lines", content.Lines.Pick(rng, "warnCK", null) != "" && content.Lines.Pick(rng, "warnIR", null) != "" && content.Lines.Pick(rng, "warnVS", null) != "" && content.Lines.Pick(rng, "lastWarning", null) != "");
        T.True("The Broker's fallback text waits ten minutes of free roam", !Economy.BrokerReady(9 * 60000L) && Economy.BrokerReady(11 * 60000L));

        T.Section("One-click front management");
        var qs = SaveService.NewEmpire();
        var qe = new Empire(qs, content);
        qe.SetOwner("carwash_handson", Owners.Player);
        GuardState q1 = qe.Recruit(rng), q2 = qe.Recruit(rng), q3 = qe.Recruit(rng);
        Stats.Set(q1, new[] { 20, 20, 20, 20 }); Stats.Set(q2, new[] { 80, 80, 80, 80 }); Stats.Set(q3, new[] { 50, 50, 50, 50 });
        int posts = content.Front("carwash_handson").Capacity;
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
        T.True("A ledge 2.5 m above the door's level is refused, a step of 1.5 m is fine", !AutoPosts.Accept(door, new List<PostDef>(), 100f, 205f, 31.5f) && AutoPosts.Accept(door, new List<PostDef>(), 100f, 205f, 30.5f));
        T.True("Wall and ledge rule: 0.7 m clear all round, ground within 0.6 m for 1 m out", AutoPosts.WallClearance == 0.7f && AutoPosts.LedgeCheck == 1.0f && AutoPosts.MaxStep == 0.6f);
        var ground = AutoPosts.GroundProbes(door);
        T.Check("Ground fallback: 32 probes (four rings of 8)", ground.Count, 32, 0);
        T.Check("...the first 3 m straight out front", ground[0][1] - door.Y, 3, 0.01);
        T.True("...all within 10 m of the door, so a soldier stays with the building", ground.All(g => Math.Sqrt((g[0] - door.X) * (g[0] - door.X) + (g[1] - door.Y) * (g[1] - door.Y)) <= 10.01));
        T.True("Ground fallback allows a spot 1.2 m from the door that the navmesh pass wouldn't", AutoPosts.Accept(door, new List<PostDef>(), 100f, 201.2f, 29f, 1.0f) && !AutoPosts.Accept(door, new List<PostDef>(), 100f, 201.2f, 29f));

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
        T.True("The Dead Kings hold the car wash at start", shipped.Front("carwash_handson").StartOwner == Owners.CrownKings);
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
        T.True("Fronts start with their doc owners", e.OwnerOf("autoshop_atomic") == Owners.CrownKings && e.OwnerOf("carwash_handson") == Owners.CrownKings);

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

        e.Assign(g2, "carwash_handson", 1);
        Stats.Set(g2, new[] { 50, 50, 50, 50 }); // average Stamina: the base recovery time
        e.GuardDown(g2, "x", false, 5000, 1000);
        T.True("Injury: keeps the post, status injured", g2.Status == GuardStatus.Injured && g2.Post == 1);
        T.Check("Not recovered before the time", e.Recover(5999), 0, 0);
        T.Check("Recovered after the time", e.Recover(6000), 1, 0);
        T.True("Back on duty at the same post", g2.Status == GuardStatus.Active && e.AtPost("carwash_handson", 1) == g2);

        // Save round trip keeps guards and fronts; ids keep counting up.
        string dir = Path.Combine(Path.GetTempPath(), "fr_emp_" + Guid.NewGuid().ToString("N"));
        try
        {
            var saves = new SaveService(dir, null);
            saves.Save(state);
            var back = saves.Load().State;
            T.Check("Guards survive save/load", back.Guards.Count, state.Guards.Count, 0);
            T.True("Front owner survives save/load", back.Fronts.Find(f => f.Id == "carwash_handson").Owner == Owners.Player);
            T.True("Post survives save/load", back.Guards.Find(g => g.Id == g2.Id).Post == 1);
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
        T.Equal("Every guard keeps watch: torch at night, whatever their trait", StreamRules.GuardScenario("WORLD_HUMAN_SMOKING", Trait.Sharpshooter, 23), StreamRules.Torch);
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
