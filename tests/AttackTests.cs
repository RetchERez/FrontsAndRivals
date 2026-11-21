using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

static class AttackTests
{
    static void PressureTests()
    {
        T.Section("Empire-size pressure");
        T.True("1-3 fronts: base timing", Attacks.PressureCooldownFactor(1) == 1.0 && Attacks.PressureCooldownFactor(3) == 1.0);
        T.True("10 fronts: Normal ~35 min", Math.Abs(45 * Attacks.PressureCooldownFactor(10) - 35.2) < 0.5);
        T.True("15 fronts: Normal ~30 min", Math.Abs(45 * Attacks.PressureCooldownFactor(15) - 30.4) < 0.5);
        T.True("Never below 55%", Attacks.PressureCooldownFactor(29) == Attacks.PressureMinFactor);
        T.True("Factor never rises with more fronts", Attacks.PressureCooldownFactor(12) <= Attacks.PressureCooldownFactor(11));
        T.True("Squad bonus 0/1/2 at 9/10/22", Attacks.PressureSquadBonus(9) == 0 && Attacks.PressureSquadBonus(10) == 1 && Attacks.PressureSquadBonus(21) == 1 && Attacks.PressureSquadBonus(22) == 2);
        T.True("Bonus still capped at 9", Resolver.SquadSize(3, 100, 30, 3) == 9);
        T.True("Bonus still capped by manpower", Resolver.SquadSize(1, 0, 4, 3) == 4);
        T.True("No bonus = old squad size", Resolver.SquadSize(2, 60, 20, 0) == Resolver.SquadSize(2, 60, 20));

        var s = new EmpireState();
        for (int i = 0; i < 15; i++) s.Fronts.Add(new FrontState { Id = "f" + i, Owner = Owners.Player });
        long now = 100 * 60000L;
        s.LastAttackMs = now - 42 * 60000L; // 42 min since the last attack
        T.True("15 fronts, pressure on: 42 min is long enough on Normal", Attacks.CanPlan(s, now, now, "Normal", 0, 10, true));
        T.True("15 fronts, pressure off: still waits 60 min", !Attacks.CanPlan(s, now, now, "Normal", 0, 10, false));
        T.True("Exact CooldownMinutes is kept as set", !Attacks.CanPlan(s, now, now, "Normal", 50, 10, true));
        var g2s = new EmpireState();
        Attacks.GracePeriod(g2s, "x", 1000);
        T.True("A new front gets a grace period", g2s.FrontAttackedMs["x"] == 1000 && Attacks.SameFrontDelayMs >= 2 * 24 * Attacks.GameHourMs);
        var mcontent = new ContentDb(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Content")), null);
        mcontent.Load();
        var ms = SaveService.NewEmpire();
        var me2 = new Empire(ms, mcontent);
        me2.EnsureFronts();
        Nemesis.EnsureCrews(ms, mcontent, new Random(4));
        foreach (FrontState f in ms.Fronts) if (f.Owner == Owners.ForSale && f.Id != "carwash_handson") f.Owner = "CK";
        int sold = 0;
        for (int n = 0; n < 40; n++) if (Turf.Market(ms, mcontent, new Random(n)) != null) sold++;
        int onSale = ms.Fronts.Count(f => f.Owner == Owners.ForSale);
        T.True("The market refills when it runs dry, up to two (" + onSale + " on sale)", sold > 0 && onSale <= Turf.MarketTarget);
        CrewDef ir = mcontent.Crew("IR"), ck = mcontent.Crew("CK");
        T.True("Hellfire MC rides bikes, two up: 9 attackers on 5 bikes", ir.ConvoyPlan(9).Count == 5 && ir.ConvoyPlan(9).TrueForAll(v => v == "daemon" || v == "hexer"));
        T.True("Dead Kings: 9 in 3 cars, alternating their two rides", ck.ConvoyPlan(9).Count == 3 && ck.ConvoyPlan(9)[0] != ck.ConvoyPlan(9)[1]);
        T.True("A convoy always seats the whole squad", ir.ConvoySeats(9) >= 9 && ck.ConvoySeats(5) >= 5 && ir.ConvoyPlan(1).Count == 1);
        T.True("Heavy guns: 10% at a small front, 60% at a big one, more with grudge", Math.Abs(CrewDef.HeavyShare(0, 0) - 0.1) < 1e-9 && Math.Abs(CrewDef.HeavyShare(2, 0) - 0.6) < 1e-9 && CrewDef.HeavyShare(2, 100) <= 0.8);
        int heavySmall = 0, heavyBig = 0, booms = 0;
        for (int i = 0; i < 2000; i++)
        {
            if (ck.HeavyWeapons.Contains(ck.WeaponFor(i, 0, 0))) heavySmall++;
            if (ck.HeavyWeapons.Contains(ck.WeaponFor(i, 2, 0))) heavyBig++;
            if (ck.ExplosiveFor(i) != null) booms++;
        }
        T.True("A big front's crew carries far more heavy guns (" + heavyBig + " vs " + heavySmall + " per 2,000)", heavyBig > heavySmall * 3);
        T.True("About 1 attacker in 20 brings an explosive (" + booms + " per 2,000)", booms > 60 && booms < 140);
        T.True("Same soldier, same gun every visit", ck.WeaponFor(42, 1, 30) == ck.WeaponFor(42, 1, 30));
        T.True("Each crew fights with its own kit", mcontent.Crew("VS").LeaderWeapon() == "carbinerifle" && ir.Explosives.Contains("molotov") && CrewDef.FrontArmor(2) == 50);
        T.True("New names", ck.Name == "Dead Kings" && ir.Name == "Hellfire MC" && mcontent.Crew("VS").Name == "Vinewood Outfit");
        T.True("Labels: Unknown at 0, Marked at 20", Attacks.PressureLabel(0) == "Unknown" && Attacks.PressureLabel(20) == "Marked");
    }

    static ContentDb Content()
    {
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Content"));
        var c = new ContentDb(repo, null);
        c.Load();
        return c;
    }

    public static void Run()
    {
        T.Section("Crews (content and setup)");
        var content = Content();
        T.Check("3 crews in crews.json", content.Crews.Count, 3, 0);
        T.True("Dead Kings boss is Darnell \"King D\" Hayes", content.Crew("CK").Boss.First == "Darnell" && content.Crew("CK").Boss.Nick == "King D");
        T.True("Every crew has 3 lieutenants", content.Crews.TrueForAll(c => c.Lieutenants.Count == 3));
        T.True("lines.json loaded", content.Lines.Count >= 10);

        var rng = new Rng(2024);
        var s = SaveService.NewEmpire();
        var e = new Empire(s, content);
        Nemesis.EnsureCrews(s, content, rng);
        T.Check("Save gets 3 crews", s.Crews.Count, 3, 0);
        CrewState ck = Nemesis.Crew(s, "CK");
        T.True("Crew has a boss and 3 lieutenants", ck.Boss != null && ck.Lieutenants().Count == 3);
        T.True("Members get unique ids", ck.Members[0].Id != ck.Members[1].Id);
        T.True("Traits parsed (King D is Ruthless)", ck.Boss.LeaderTrait == LeaderTrait.Ruthless);

        T.Section("Attack planning");
        T.Check("Normal cooldown 60 min", Attacks.CooldownMs("Normal"), 60 * 60000, 0);
        T.Check("Low cooldown 90 min", Attacks.CooldownMs("Low"), 90 * 60000, 0);
        T.Check("High cooldown 35 min", Attacks.CooldownMs("High"), 35 * 60000, 0);
        T.Check("Off disables attacks", Attacks.CooldownMs("Off"), -1, 0);
        long now = 100 * 60000;
        T.True("No fronts owned: no attack", !Attacks.CanPlan(s, now, now, "Normal"));
        e.SetOwner("carwash_handson", Owners.Player);
        T.True("Own a front after the cooldown: can plan", Attacks.CanPlan(s, now, now, "Normal"));
        T.True("Never in the first 10 minutes after loading", !Attacks.CanPlan(s, now, 5 * 60000, "Normal"));
        T.True("Off: never", !Attacks.CanPlan(s, now, now, "Off"));
        PressureTests();
        s.LastLossMs = now - 10 * 60000;
        T.True("Never within 30 minutes of losing a front", !Attacks.CanPlan(s, now, now, "Normal"));
        s.LastLossMs = -10000000;

        T.Check("Even odds: guards hold 5 minutes", Attacks.HoldMinutes(5, 100, 100, false, "CK"), 5, 0);
        T.True("Outgunned about 3, strong up to 7", Attacks.HoldMinutes(5, 0, 100, false, "CK") == 3 && Attacks.HoldMinutes(5, 1000, 1, false, "CK") == 7);
        T.Check("Cameras add 2 minutes", Attacks.HoldMinutes(5, 100, 100, true, "CK"), 7, 0);
        T.Check("The Vinewood Outfit takes a minute", Attacks.HoldMinutes(5, 100, 100, false, "VS"), 4, 0);
        T.True("Always 2 to 12 minutes", Attacks.HoldMinutes(2, 0, 500, false, "VS") == 2 && Attacks.HoldMinutes(12, 999, 1, true, "CK") == 12);
        var dug = new EmpireState { Day = 4 };
        var lostFront = new FrontState { Id = "x" };
        lostFront.DugInUntilDay = dug.Day + 1;
        T.True("A lost front can't be claimed back before midnight", Attacks.DugIn(dug, lostFront));
        dug.Day = 5;
        T.True("...and can after it", !Attacks.DugIn(dug, lostFront));
        T.Check("Near the crew's home: d = 1.0", Attacks.DistanceFactor(Spot.At(10, 10, 0, 0), Spot.At(500, 10, 0, 0)), 1.0, 0.0001);
        T.Check("Far away: d = 0.6", Attacks.DistanceFactor(Spot.At(10, 10, 0, 0), Spot.At(5010, 10, 0, 0)), 0.6, 0.0001);

        // Crew weights: aggression + grudge/25; a quiet crew never attacks.
        int ckPicks = 0, irPicks = 0, vsPicks = 0;
        Nemesis.Crew(s, "VS").QuietUntilDay = s.Day + 3;
        for (int i = 0; i < 3000; i++)
        {
            CrewState c = Attacks.PickCrew(s, content, rng);
            if (c.Id == "CK") ckPicks++; else if (c.Id == "IR") irPicks++; else vsPicks++;
        }
        T.Check("Quiet crews sit out", vsPicks, 0, 0);
        T.Check("Dead Kings (2.0) picked ~80% vs Ironside (0.5)", ckPicks / 3000.0, 0.8, 0.04);
        Nemesis.Crew(s, "VS").QuietUntilDay = -1;

        AttackState plan = Attacks.Plan(s, content, e, rng, 5, 12, now);
        T.True("Plan: targets your only front", plan != null && plan.Front == "carwash_handson");
        T.True("Plan: no warning, already underway, 2-12 minutes to get there", plan.Phase == AttackPhase.Window && plan.WarnEnds == now
               && plan.WindowEnds - now >= 2 * Attacks.Minute && plan.WindowEnds - now <= 12 * Attacks.Minute);
        T.True("Plan: squad of at least 1 with a named leader", plan.Squad >= 1 && Nemesis.Crew(s, plan.Crew).Member(plan.Leader) != null);

        T.Section("Attack results");
        var guards = new List<GuardState>();
        FrontDef wash = content.Front("carwash_handson");
        for (int p = 0; p < wash.Posts.Count; p++) { GuardState g = e.Recruit(rng); e.Assign(g, "carwash_handson", p); guards.Add(g); }

        var a1 = new AttackState { Crew = "CK", Front = "carwash_handson", Leader = ck.Lieutenants()[0].Id, Squad = 3, Strength = 1 };
        int grudgeBefore = ck.Grudge;
        Attacks.Outcome win = Attacks.Resolve(s, content, e, a1, rng, 12, now, true, false, true, 2, 1000);
        T.True("Defended: front still yours", win.DefendersWon && e.PlayerOwns("carwash_handson"));
        T.Check("Defended: grudge +8", ck.Grudge - grudgeBefore, 8, 0);
        T.True("Defended: survivors gain XP", guards[0].Xp == 1 && guards[0].Defenses == 1);
        T.True("Attack cleared and cooldown starts", s.Attack == null && s.LastAttackMs == now);

        var a2 = new AttackState { Crew = "CK", Front = "carwash_handson", Leader = ck.Lieutenants()[0].Id, Squad = 3, Strength = 1000 };
        Attacks.Outcome loss = Attacks.Resolve(s, content, e, a2, rng, 12, now + 1, true, false, false, 0, 1000);
        T.True("Lost: the crew owns the front", loss.FrontLost && e.OwnerOf("carwash_handson") == "CK");
        T.True("Lost: our guards went back to the reserve", guards.TrueForAll(g => !g.IsPosted));
        T.True("Lost: no new attack for 30 minutes", s.LastLossMs == now + 1);

        e.SetOwner("carwash_handson", Owners.Player);
        var a3 = new AttackState { Crew = "IR", Front = "carwash_handson", Leader = Nemesis.Crew(s, "IR").Boss.Id, Squad = 3, Strength = 1000 };
        Attacks.Outcome prot = Attacks.Resolve(s, content, e, a3, rng, 12, now + 2, true, true, false, 0, 1000);
        T.True("Protected mode: front damaged, not lost", prot.FrontDamaged && e.PlayerOwns("carwash_handson") && e.Find("carwash_handson").Damaged);

        // Auto-resolve keeps the design-doc win rate at D = A.
        var mc = SaveService.NewEmpire();
        var me = new Empire(mc, content);
        Nemesis.EnsureCrews(mc, content, rng);
        me.SetOwner("carwash_handson", Owners.Player);
        GuardState lone = me.Recruit(rng); me.Assign(lone, "carwash_handson", 0);
        Stats.Set(lone, new[] { 50, 50, 50, 50 });
        double d = me.Defense("carwash_handson", 12);
        int wins = 0;
        for (int i = 0; i < 4000; i++)
        {
            lone.Status = GuardStatus.Active; lone.Front = "carwash_handson"; lone.Post = 0; lone.Xp = 0; lone.Rank = 0;
            Stats.Set(lone, new[] { 50, 50, 50, 50 }); // fights teach skills; keep this guard the same for every trial
            me.SetOwner("carwash_handson", Owners.Player);
            var aa = new AttackState { Crew = "CK", Front = "carwash_handson", Leader = 0, Squad = 3, Strength = d };
            if (Attacks.Resolve(mc, content, me, aa, rng, 12, i, true, false, null, 0, 1000).DefendersWon) wins++;
            if (!mc.Guards.Contains(lone)) mc.Guards.Add(lone);
        }
        T.Check("Auto-resolve at D = A wins ~50% (4,000 fights)", wins / 4000.0, 0.5, 0.03);

        T.Section("Nemesis loop");
        var leader = new CrewMember { Id = 999, First = "Dre", Nick = "Ledger", Last = "Collins", Role = MemberRole.Lieutenant, Trait = (int)LeaderTrait.Showboat, Alive = true };
        int escapes = 0;
        for (int i = 0; i < 4000; i++)
        {
            leader.Alive = true; leader.Scars.Clear();
            if (!Nemesis.LeaderLost(leader, "Car Wash", rng, 0)) escapes++;
        }
        T.Check("Leaders escape a lost fight ~70% of the time", escapes / 4000.0, 0.7, 0.03);
        leader.Alive = true; leader.Scars.Clear(); leader.Rep = 1;
        var always = new Rng(1);
        CrewMember cautious = new CrewMember { Id = 998, Role = MemberRole.Lieutenant, Trait = (int)LeaderTrait.Cautious, Alive = true };
        int cEsc = 0;
        for (int i = 0; i < 4000; i++) { cautious.Alive = true; if (!Nemesis.LeaderLost(cautious, "x", always, 0)) cEsc++; }
        T.Check("Cautious leaders escape ~90%", cEsc / 4000.0, 0.9, 0.03);
        bool died = Nemesis.LeaderLost(leader, "Car Wash", new FixedRandom(0.1), 5000);
        T.True("Escaped leader: +1 rep, a scar, a return text due later", !died && leader.Rep == 2 && leader.Scars[0] == "put me down at the Car Wash" && leader.ReturnTextAt > 5000);

        // A lieutenant dies: grudge +15 and a survivor is promoted.
        CrewState ir = Nemesis.Crew(s, "IR");
        CrewMember lt = ir.Lieutenants()[0];
        int g0 = ir.Grudge;
        lt.Alive = false;
        var evs = Nemesis.OnMemberDied(s, content, ir, lt, "Car Wash", rng, s.Day);
        T.Check("Killing a lieutenant: grudge +15", ir.Grudge - g0, 15, 0);
        T.True("A survivor is promoted to keep 3 lieutenants", ir.Lieutenants().Count == 3 && evs.Exists(x => x.Moment == "promotion"));

        // The boss dies: the highest-rep lieutenant takes over.
        CrewMember oldBoss = ir.Boss;
        CrewMember favourite = ir.Lieutenants()[1];
        favourite.Rep = 50;
        oldBoss.Alive = false;
        Nemesis.OnMemberDied(s, content, ir, oldBoss, "Car Wash", rng, s.Day);
        T.True("Boss dies: highest-rep lieutenant takes over", ir.Boss == favourite);

        // Collapse and rebuild.
        CrewState vs = Nemesis.Crew(s, "VS");
        foreach (CrewMember m in vs.Members) if (m.Role == MemberRole.Lieutenant) m.Alive = false;
        CrewMember vb = vs.Boss;
        vb.Alive = false;
        var col = Nemesis.OnMemberDied(s, content, vs, vb, "Tavern", rng, 10);
        T.True("Boss with no lieutenants: the crew collapses for 3 days", col.Exists(x => x.Moment == "collapsed") && vs.QuietUntilDay == 13);
        T.True("Still quiet on day 12", Nemesis.Rebuild(s, content, vs, rng, 12) == null);
        var rb = Nemesis.Rebuild(s, content, vs, rng, 13);
        T.True("Day 13: rebuilt with a new boss and one lieutenant", rb != null && vs.Boss != null && vs.Lieutenants().Count == 1 && vs.QuietUntilDay == -1);
        T.True("Velvet promotions have no nickname", string.IsNullOrEmpty(vs.Boss.Nick));

        // Daily: manpower +1 per held front (max 20), grudge -5 without contact.
        var ds = SaveService.NewEmpire();
        var de = new Empire(ds, content);
        Nemesis.EnsureCrews(ds, content, rng);
        CrewState dck = Nemesis.Crew(ds, "CK");
        dck.Manpower = 10; dck.Grudge = 20; dck.LastContactDay = 0; ds.Day = 1;
        int ckHeld = ds.Fronts.FindAll(x => x.Owner == "CK").Count;
        Nemesis.Daily(ds, 1);
        T.Check("Manpower grows by 2 plus 1 per front the crew holds", dck.Manpower, 10 + 2 + ckHeld, 0);
        T.True("A crew with no fronts recruits 4 a night to fight back", Nemesis.StreetRecruits(0) == 4 && Nemesis.StreetRecruits(3) == 5);
        T.Check("Defending in person pays a day of takings", Economy.DefendBonus(7000, 1.0), 7000, 0);
        T.Check("...scaled by the income multiplier", Economy.DefendBonus(7000, 0.5), 3500, 0);
        T.True("The crew waits for you once if you're under 1 km away", Attacks.OnTheWayRange == 1000f && Attacks.OnTheWayMs == 90000);
        T.Check("No contact today: grudge -8", dck.Grudge, 12, 0);
        dck.Manpower = 19; Nemesis.Daily(ds, 2);
        T.Check("Manpower caps at 20", dck.Manpower, 20, 0);

        T.Section("Turf (the city moves without you)");
        var ts = SaveService.NewEmpire();
        var te = new Empire(ts, content);
        Nemesis.EnsureCrews(ts, content, rng);
        foreach (CrewState c in ts.Crews) c.Manpower = 20;
        te.SetOwner("cafe_tacolibre", Owners.Player);
        int grabs = 0, wars = 0;
        for (int night = 0; night < 200; night++)
        {
            ts.Day = night;
            foreach (CrewState c in ts.Crews) c.Manpower = 20;
            foreach (Turf.Move m in Turf.Nightly(ts, content, rng))
            {
                if (!m.Success) continue;
                if (m.FromCrew == null) grabs++; else wars++;
            }
        }
        T.True("Crews grab fronts that are for sale", grabs > 0);
        T.True("Crews take fronts from each other", wars > 0);
        T.True("Your fronts are never taken by turf moves", te.OwnerOf("cafe_tacolibre") == Owners.Player);
        T.True("The starter car wash is never grabbed", te.OwnerOf("carwash_handson") == Owners.ForSale);
        var weak = SaveService.NewEmpire();
        new Empire(weak, content);
        Nemesis.EnsureCrews(weak, content, rng);
        foreach (CrewState c in weak.Crews) c.Manpower = 3;
        bool none = true;
        for (int i = 0; i < 50; i++) if (Turf.Nightly(weak, content, rng).Count > 0) none = false;
        T.True("Crews with fewer than 8 soldiers stay home", none);
        T.Check("Starter price is $10,000", Economy.FrontPrice(content.Front("carwash_handson")), 10000, 0);
        T.Check("Other fronts are priced like Story Mode property (35 design days)", Economy.FrontPrice(content.Front("motel_alta")), 4500 * 35, 0);
        double weeksBack = Economy.FrontPrice(content.Front("motel_alta")) / (Economy.BaseIncome(content.Front("motel_alta")) * 7.0);
        T.True("...about 20 weeks to pay back, like Smoke on the Water's 22 (" + weeksBack.ToString("0") + ")", weeksBack >= 18 && weeksBack <= 22);

        T.Section("Text lines");
        var tokens = new Dictionary<string, string> { { "crew", "Dead Kings" }, { "front", "Car Wash" } };
        string w = content.Lines.Pick(rng, "warning", tokens);
        T.True("Warning lines fill {crew} and {front}", w.Contains("Car Wash") && !w.Contains("{"));
        T.Equal("Fill replaces every token", LineBook.Fill("{a} and {a}", new Dictionary<string, string> { { "a", "x" } }), "x and x");
        T.True("Unknown moment gives an empty line, never a crash", content.Lines.Pick(rng, "nope", tokens) == "");
    }

    /// <summary>Random that always returns the same double (for deterministic branches).</summary>
    sealed class FixedRandom : Random
    {
        private readonly double _v;
        public FixedRandom(double v) { _v = v; }
        protected override double Sample() { return _v; }
        public override double NextDouble() { return _v; }
        public override int Next(int maxValue) { return 0; }
    }
}
