// Fronts & Rivals balance simulator: plays many games with a simple AI player on the mod's real rules code
// (Empire, Economy, Attacks, Raids, Nemesis, Turf) and reports how the game unfolds. No GTA, no randomness outside
// the seeded Random. Run: dotnet run --project tools/BalanceSim -c Release -- [runs] [days]
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

static class Sim
{
    sealed class Policy
    {
        public string Name;
        public double PresentAtAttack;   // chance you're there to fight a live defense
        public double LiveWinBoost;      // how much you add when you are
        public double RaidMinOdds;       // raids only at or above these odds
        public int AssaultEveryHours;    // you take a crew front in person every this many in-game hours (0 = never)
        public double AssaultWin;        // chance your in-person assault works
        public int RaidSquad;            // guards kept in the reserve for raids instead of posted
    }

    sealed class Stats
    {
        public int[] FrontsByDay;
        public long[] NetByDay, CashByDay;
        public int AttFull, AttFullWon, AttPartial, AttPartialWon, AttEmpty, AttEmptyWon;
        public int Attacks, AttacksWon, FrontsLost, Raids, RaidsWon, GuardsDead, Quits, Assaults;
        public int Day3 = -1, Day8 = -1, Day15 = -1, Day22 = -1;
        public long UpgradeSpend, GunSpend, HireSpend, FrontSpend;
    }

    static ContentDb _content;
    static bool Trace;

    static int Main(string[] args)
    {
        int runs = args.Length > 0 ? int.Parse(args[0]) : 200;
        int days = args.Length > 1 ? int.Parse(args[1]) : 30;
        string repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _content = new ContentDb(Path.Combine(repo, "src", "Content"), null);
        _content.Load();

        var policies = new[]
        {
            new Policy { Name = "Casual", PresentAtAttack = 0.10, LiveWinBoost = 0.25, RaidMinOdds = 0.70, AssaultEveryHours = 0, AssaultWin = 0.0, RaidSquad = 0 },
            new Policy { Name = "Normal", PresentAtAttack = 0.30, LiveWinBoost = 0.25, RaidMinOdds = 0.55, AssaultEveryHours = 48, AssaultWin = 0.8, RaidSquad = 2 },
            new Policy { Name = "Aggressive", PresentAtAttack = 0.50, LiveWinBoost = 0.30, RaidMinOdds = 0.45, AssaultEveryHours = 24, AssaultWin = 0.85, RaidSquad = 4 },
        };
        if (args.Length > 2 && args[2] == "trace") { Trace = true; Play(policies[1], 0, days, 1); return 0; }
        foreach (int diff in new[] { -2, 0, 2 })
            foreach (Policy p in policies)
                Report(p, diff, days, Enumerable.Range(0, runs).Select(seed => Play(p, diff, days, seed)).ToList());
        return 0;
    }

    static Stats Play(Policy pol, int difficulty, int days, int seed)
    {
        var rng = new Random(seed * 7919 + difficulty * 31 + pol.Name.Length);
        EmpireState s = SaveService.NewEmpire();
        s.Difficulty = difficulty;
        var e = new Empire(s, _content);
        e.EnsureFronts();
        Nemesis.EnsureCrews(s, _content, rng);
        long cash = 50000; // a mid-story character
        var st = new Stats { FrontsByDay = new int[days + 1], NetByDay = new long[days + 1], CashByDay = new long[days + 1] };
        const long Min = 60000, Hour = 2 * Min;
        long now = 0;
        int hour = 8; // free roam starts mid-morning
        e.RefreshOffers(rng, s.Day);
        long nextAssault = pol.AssaultEveryHours > 0 ? pol.AssaultEveryHours * Hour : long.MaxValue;
        AttackState attackSeen = null;

        for (int minute = 0; s.Day < days; minute++)
        {
            now = minute * Min;
            int newHour = (8 + (int)(now / Hour)) % 24;
            bool hourTick = newHour != hour || minute == 0;
            if (newHour == 0 && hour == 23) Midnight(s, e, rng, ref cash, st);
            hour = newHour;
            if (hourTick && hour == 6 && s.OffersDay != s.Day) e.RefreshOffers(rng, s.Day);
            e.Recover(now);

            if (hourTick) PlayerTurn(pol, s, e, rng, ref cash, st, now, hour, ref nextAssault);

            // The Director: one roll a minute, the same rules as the game.
            if (s.Attack == null)
            {
                if (Attacks.CanPlan(s, now, now, "Normal", 0, 10, true) && rng.NextDouble() < Attacks.PlanChancePerMinute)
                {
                    int bonus = Attacks.PressureSquadBonus(Attacks.HeldCount(s));
                    s.Attack = Attacks.Plan(s, _content, e, rng, Attacks.DefaultHoldMinutes, hour, now, bonus);
                }
            }
            else if (now >= s.Attack.WindowEnds)
            {
                AttackState a = s.Attack;
                if (a != attackSeen) { attackSeen = a; st.Attacks++; }
                bool? live = null; int lost = 0;
                if (rng.NextDouble() < pol.PresentAtAttack)
                {
                    double d = e.Defense(a.Front, hour);
                    double p = Math.Min(0.95, Resolver.WinChance(d, a.Strength) + pol.LiveWinBoost);
                    live = rng.NextDouble() < p;
                    lost = live.Value ? a.Squad : a.Squad / 2;
                }
                int before = s.Guards.Count(g => g.Status == GuardStatus.Dead);
                FrontDef fd = _content.Front(a.Front);
                int there = s.Guards.Count(g => g.Front == a.Front && g.Status == GuardStatus.Active && !g.Escort && !e.OnRaid(g));
                int kind = there == 0 ? 0 : there >= fd.Capacity ? 2 : 1;
                if (Trace) Console.WriteLine("  day " + s.Day + " attack on " + a.Front + " (cap " + fd.Capacity + ") guards there " + there
                    + " | held " + e.OwnedCount() + " roster " + e.LivingCount() + " free " + e.FreeGuards().Count + " posted " + s.Guards.Count(g => g.IsPosted && g.Status == GuardStatus.Active)
                    + " injured " + s.Guards.Count(g => g.Status == GuardStatus.Injured) + " offers " + s.Offers.Count + " cash " + cash);
                Attacks.Outcome o = Attacks.Resolve(s, _content, e, a, rng, hour, now, true, false, live, lost, 96 * Min);
                if (kind == 0) { st.AttEmpty++; if (o.DefendersWon) st.AttEmptyWon++; }
                else if (kind == 1) { st.AttPartial++; if (o.DefendersWon) st.AttPartialWon++; }
                else { st.AttFull++; if (o.DefendersWon) st.AttFullWon++; }
                if (o.DefendersWon) { st.AttacksWon++; if (live == true) cash += Economy.DefendBonus(e.FrontIncome(a.Front), 1.0); }
                if (o.FrontLost) st.FrontsLost++;
                st.GuardsDead += s.Guards.Count(g => g.Status == GuardStatus.Dead) - before;
            }

            if (s.Raid != null && now >= s.Raid.EndsMs)
            {
                int before = s.Guards.Count(g => g.Status == GuardStatus.Dead);
                Raids.Outcome ro = Raids.Resolve(s, _content, e, rng, hour, now, true, 96 * Min);
                st.Raids++;
                if (ro.Won) st.RaidsWon++;
                st.GuardsDead += s.Guards.Count(g => g.Status == GuardStatus.Dead) - before;
            }

            int held = e.OwnedCount();
            if (held >= 3 && st.Day3 < 0) st.Day3 = s.Day;
            if (held >= 8 && st.Day8 < 0) st.Day8 = s.Day;
            if (held >= 15 && st.Day15 < 0) st.Day15 = s.Day;
            if (held >= 22 && st.Day22 < 0) st.Day22 = s.Day;
        }
        return st;
    }

    static void Midnight(EmpireState s, Empire e, Random rng, ref long cash, Stats st)
    {
        s.Day++;
        Nemesis.Daily(s, s.Day);
        foreach (CrewState crew in s.Crews) Nemesis.Rebuild(s, _content, crew, rng, s.Day);
        Turf.Nightly(s, _content, rng, Turf.ChanceFor("Normal"));
        Economy.Payout pay = Economy.Midnight(s, _content, cash, 1.0, 1.0);
        e.DayOnDuty();
        st.Quits += pay.Quit.Count;
        cash += pay.Net;
        int d = Math.Min(s.Day, st.FrontsByDay.Length - 1);
        st.FrontsByDay[d] = e.OwnedCount();
        st.NetByDay[d] = pay.Net;
        st.CashByDay[d] = cash;
        e.EnsureFronts();
    }

    /// <summary>Once an in-game hour: what a sensible player would do in the Office.</summary>
    static void PlayerTurn(Policy pol, EmpireState s, Empire e, Random rng, ref long cash, Stats st, long now, int hour, ref long nextAssault)
    {
        long wagesReserve = e.DailyWages() * 2;

        // Buy the cheapest front for sale the Broker trusts you with.
        FrontDef buy = _content.Fronts.Where(f => e.CanBuy(f.Id) && e.Trusted(f)).OrderBy(f => Economy.FrontPrice(f, 5)).FirstOrDefault();
        if (buy != null && cash - Economy.FrontPrice(buy, 5) > wagesReserve + 3000)
        {
            int price = Economy.FrontPrice(buy, 5);
            cash -= price; st.FrontSpend += price;
            e.SetOwner(buy.Id, Owners.Player);
            Attacks.GracePeriod(s, buy.Id, now);
        }

        // Hire when there are posts to fill.
        int posts = _content.Fronts.Where(f => e.PlayerOwns(f.Id)).Sum(f => e.EmptyPosts(f.Id));
        foreach (GuardState offer in s.Offers.ToList())
        {
            if (e.FreeGuards().Count >= posts + pol.RaidSquad) break;
            int price;
            if (cash - e.OfferPrice(offer) < wagesReserve) break;
            if (e.Hire(offer, cash, out price) == Empire.HireResult.Ok) { cash -= price; st.HireSpend += price; }
        }
        // Post everyone except the raid squad (the weakest free guards get posted, the strongest stay ready to raid).
        foreach (FrontDef f in _content.Fronts.Where(f => e.PlayerOwns(f.Id)).OrderByDescending(f => f.Tier))
        {
            List<GuardState> free = e.FreeGuards();
            int canPost = Math.Max(0, free.Count - pol.RaidSquad);
            for (int post = 0; post < f.Capacity && canPost > 0; post++)
            {
                if (e.AtPost(f.Id, post) != null) continue;
                GuardState g = free[free.Count - 1]; free.RemoveAt(free.Count - 1);
                e.Assign(g, f.Id, post); canPost--;
            }
        }

        // Arm fronts, then buy upgrades, while there's comfortably enough money.
        foreach (FrontDef f in _content.Fronts.Where(f => e.PlayerOwns(f.Id)))
        {
            int cost = e.ArmFrontPrice(f.Id);
            if (cost > 0 && cash - cost > wagesReserve * 2) { cash -= cost; st.GunSpend += cost; e.ArmFront(f.Id); }
        }
        foreach (FrontDef f in _content.Fronts.Where(f => e.PlayerOwns(f.Id)).OrderByDescending(f => f.Tier))
        {
            FrontState fs = e.Find(f.Id);
            TryUpgrade(ref cash, st, f, wagesReserve, "Floodlights", 10000, fs.Floodlights, () => fs.Floodlights = true);
            TryUpgrade(ref cash, st, f, wagesReserve, "Cameras", 15000, fs.Cameras, () => fs.Cameras = true);
            TryUpgrade(ref cash, st, f, wagesReserve, "Reinforced Doors", 20000, fs.Doors, () => fs.Doors = true);
            TryUpgrade(ref cash, st, f, wagesReserve, "Armory", 30000, fs.Armory, () => fs.Armory = true);
        }

        // Raids on the best-odds rival front.
        if (s.Raid == null)
        {
            FrontDef target = _content.Fronts.Where(f => e.RaidBlocked(f) == null).OrderByDescending(f => e.RaidOdds(f, hour)).FirstOrDefault();
            if (target != null && e.RaidOdds(target, hour) >= pol.RaidMinOdds) e.StartRaid(target, now, hour);
        }

        // Going in yourself now and then.
        if (now >= nextAssault)
        {
            nextAssault = now + pol.AssaultEveryHours * 2 * 60000L;
            FrontDef t = _content.Fronts.Where(f => Owners.IsCrew(e.OwnerOf(f.Id)) && e.Trusted(f) && !Attacks.DugIn(s, e.Find(f.Id)))
                                        .OrderBy(f => f.Tier).FirstOrDefault();
            if (t != null && rng.NextDouble() < pol.AssaultWin)
            {
                string crew = e.OwnerOf(t.Id);
                CrewState cs = Nemesis.Crew(s, crew);
                cs.Manpower = Math.Max(0, cs.Manpower - FrontDef.PostsForTier(t.Tier));
                Attacks.TakeFront(s, e, e.Find(t.Id), crew);
                Attacks.GracePeriod(s, t.Id, now);
                st.Assaults++;
            }
        }
    }

    static void TryUpgrade(ref long cash, Stats st, FrontDef f, long reserve, string name, int basePrice, bool owned, Action install)
    {
        if (owned || !Economy.CanInstall(name, f.Tier)) return;
        int price = Economy.UpgradePrice(basePrice, f.Tier);
        if (cash - price < reserve * 3 + 20000) return;
        cash -= price; st.UpgradeSpend += price; install();
    }

    static string Pct(double v) { return (v * 100).ToString("0") + "%"; }

    static void Report(Policy p, int diff, int days, List<Stats> all)
    {
        Func<Func<Stats, int>, string> median = f =>
        {
            var v = all.Select(f).Where(x => x >= 0).OrderBy(x => x).ToList();
            return v.Count == 0 ? "never" : "day " + v[v.Count / 2] + (v.Count < all.Count ? " (" + Pct(v.Count / (double)all.Count) + " reach)" : "");
        };
        Func<int, double> frontsAt = d => all.Average(x => x.FrontsByDay[Math.Min(d, days)]);
        Func<int, double> netAt = d => all.Average(x => (double)x.NetByDay[Math.Min(d, days)]);
        Func<int, double> cashAt = d => all.Average(x => (double)x.CashByDay[Math.Min(d, days)]);
        double att = all.Sum(x => x.Attacks), won = all.Sum(x => x.AttacksWon), raids = all.Sum(x => x.Raids), rwon = all.Sum(x => x.RaidsWon);
        Console.WriteLine("== " + Difficulty.Name(diff) + " / " + p.Name + " (" + all.Count + " games, " + days + " in-game days)");
        Console.WriteLine("   fronts held   day 3: " + frontsAt(3).ToString("0.0") + "  day 7: " + frontsAt(7).ToString("0.0") + "  day 15: " + frontsAt(15).ToString("0.0") + "  day 30: " + frontsAt(days).ToString("0.0"));
        Console.WriteLine("   milestones    3 fronts " + median(x => x.Day3) + ", 8 fronts " + median(x => x.Day8) + ", 15 fronts " + median(x => x.Day15) + ", 22 fronts " + median(x => x.Day22));
        Console.WriteLine("   net/night     day 3: $" + netAt(3).ToString("N0") + "  day 7: $" + netAt(7).ToString("N0") + "  day 15: $" + netAt(15).ToString("N0") + "  day 30: $" + netAt(days).ToString("N0"));
        Console.WriteLine("   cash          day 7: $" + cashAt(7).ToString("N0") + "  day 15: $" + cashAt(15).ToString("N0") + "  day 30: $" + cashAt(days).ToString("N0"));
        Console.WriteLine("   attacks/game  " + (att / all.Count).ToString("0.0") + ", defended " + Pct(att > 0 ? won / att : 0) + ", fronts lost " + (all.Average(x => x.FrontsLost)).ToString("0.0")
                          + "   raids/game " + (raids / all.Count).ToString("0.0") + ", won " + Pct(raids > 0 ? rwon / raids : 0) + "   assaults " + all.Average(x => x.Assaults).ToString("0.0"));
        Func<Func<Stats,int>,Func<Stats,int>,string> rate = (n, w) => { double a = all.Sum(n), b = all.Sum(w); return (a / all.Count).ToString("0.0") + "/game won " + Pct(a > 0 ? b / a : 0); };
        Console.WriteLine("   by staffing   full " + rate(x => x.AttFull, x => x.AttFullWon) + ", partial " + rate(x => x.AttPartial, x => x.AttPartialWon) + ", empty " + rate(x => x.AttEmpty, x => x.AttEmptyWon));
        Console.WriteLine("   guards dead   " + all.Average(x => x.GuardsDead).ToString("0.0") + "/game, quits " + all.Average(x => x.Quits).ToString("0.0")
                          + "   spend: fronts $" + all.Average(x => x.FrontSpend).ToString("N0") + ", hires $" + all.Average(x => x.HireSpend).ToString("N0")
                          + ", guns $" + all.Average(x => x.GunSpend).ToString("N0") + ", upgrades $" + all.Average(x => x.UpgradeSpend).ToString("N0"));
    }
}
