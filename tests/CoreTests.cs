using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using FrontsAndRivals.Core;
using FrontsAndRivals.Dev;
using FrontsAndRivals.Spawn;

static class CoreTests
{
    public static void Run()
    {
        FailureWindowTests();
        FreeRoamTests();
        SchedulerTests();
        RngTests();
        ConfigTests();
        MarkerTests();
        DevFormatTests();
    }

    static void FreeRoamTests()
    {
        T.Section("Free roam only");
        var block = new System.Collections.Generic.HashSet<string>(FreeRoamRules.Blocking);
        T.True("Story missions, heists and Strangers and Freaks pause the mod", block.Contains("franklin1") && block.Contains("jewelry_heist") && block.Contains("rural_bank_prep1") && block.Contains("nigel1") && block.Contains("bailbond1") && block.Contains("assassin_valet"));
        T.True("Side jobs and minigames pause it", block.Contains("taxiservice") && block.Contains("towing") && block.Contains("golf") && block.Contains("darts") && block.Contains("triathlonsp") && block.Contains("rampage3") && block.Contains("friendactivity") && block.Contains("director_mode"));
        T.True("Always-on controllers, launchers and ambient watchers never do (or the mod would never run)",
            FreeRoamRules.Blocking.All(n => !n.StartsWith("launcher_") && !n.StartsWith("controller_") && !n.EndsWith("_controller") && !n.EndsWith("amb") && !n.EndsWith("_ambient") && n != "main" && n != "blip_controller"));
        T.True("No name twice", block.Count == FreeRoamRules.Blocking.Length);
        T.True("Only Michael, Franklin and Trevor count as you", FreeRoamRules.Protagonists.Length == 3 && FreeRoamRules.Protagonists.Contains("player_one"));
        T.True("Franklin's mission letters and Strangers and Freaks mark mission starts; a safehouse doesn't", FreeRoamRules.IsMissionStartSprite(212) && FreeRoamRules.IsMissionStartSprite(66) && !FreeRoamRules.IsMissionStartSprite(40));
    }

    static void FailureWindowTests()
    {
        T.Section("FailureWindow (3 failures in 60 s)");
        var w = new FailureWindow();
        T.True("1st failure doesn't trip", !w.Record(0));
        T.True("2nd failure doesn't trip", !w.Record(10000));
        T.True("3rd failure within 60 s trips", w.Record(59000));

        var spread = new FailureWindow();
        spread.Record(0); spread.Record(40000);
        T.True("3 failures over 61 s don't trip", !spread.Record(61000));
        T.True("...but the next one 20 s later does (40, 61, 61+20 within 60 s)", spread.Record(81000));
    }

    static void SchedulerTests()
    {
        T.Section("Scheduler");
        int runs = 0, uiRuns = 0, failures = 0, disabled = 0;
        var s = new Scheduler((job, display, ex, off) => { failures++; if (off) disabled++; });
        s.Add("Every500", "x", 500, () => runs++);
        s.Add("UI", "ui", 0, () => uiRuns++, runWhilePaused: true);

        // 10 s of open gate at 60 fps -> 1 run at t=0, then every 500 ms.
        int now = 1000;
        for (int i = 0; i < 600; i++) { s.Update(now, true); now += 16; }
        T.Check("500 ms job runs ~20 times in ~9.6 s", runs, 20, 1);
        T.Check("UI job runs every tick", uiRuns, 600, 0);

        // 10 s paused: gated job stops, clock freezes, UI keeps going.
        int runsBefore = runs; long activeBefore = s.ActiveMs;
        for (int i = 0; i < 600; i++) { s.Update(now, false); now += 16; }
        T.Check("Gated job doesn't run while paused", runs - runsBefore, 0, 0);
        T.Check("Free-roam clock frozen while paused", s.ActiveMs - activeBefore, 0, 0);
        T.Check("UI job still runs while paused", uiRuns, 1200, 0);

        // Resume: no catch-up burst after the pause.
        s.Update(now, true); now += 16;
        s.Update(now, true);
        T.True("No burst of runs right after resuming", runs - runsBefore <= 1);

        // Big real-time jump (e.g. pause menu) advances the free-roam clock by at most 1 s.
        long a0 = s.ActiveMs;
        s.Update(now + 300000, true);
        T.Check("Clock step capped at 1 s", s.ActiveMs - a0, Scheduler.MaxStepMs, 0);

        // A throwing job: 3 throws in 60 s disables it; others keep running.
        int good = 0, bad = 0;
        var s2 = new Scheduler((job, display, ex, off) => { failures++; if (off) disabled++; });
        s2.Add("Bad", "bad", 0, () => { bad++; throw new InvalidOperationException("boom"); });
        s2.Add("Good", "good", 0, () => good++);
        int t = 0;
        for (int i = 0; i < 10; i++) { s2.Update(t, true); t += 16; }
        T.Check("Throwing job ran exactly 3 times before switching off", bad, 3, 0);
        T.True("Throwing job is disabled", s2.IsDisabled("Bad"));
        T.Check("Other job kept running every tick", good, 10, 0);
        T.Check("Failure handler saw one disable", disabled, 1, 0);
    }

    static void RngTests()
    {
        T.Section("Rng (one seeded generator per save)");
        var a = new Rng(12345UL);
        var b = new Rng(12345UL);
        bool same = true;
        for (int i = 0; i < 1000; i++) if (a.NextDouble() != b.NextDouble()) same = false;
        T.True("Same state gives the same sequence", same);

        // Save mid-sequence, restore, continue identically.
        ulong saved = a.State;
        double next1 = a.NextDouble(), next2 = a.NextDouble();
        var restored = new Rng(saved);
        T.True("Restored state continues the exact sequence", restored.NextDouble() == next1 && restored.NextDouble() == next2);

        var r = new Rng(99UL);
        bool inRange = true; double sum = 0;
        for (int i = 0; i < 100000; i++)
        {
            double d = r.NextDouble(); sum += d;
            if (d < 0 || d >= 1) inRange = false;
            int n = r.Next(5, 10);
            if (n < 5 || n >= 10) inRange = false;
        }
        T.True("NextDouble in [0,1) and Next(5,10) in [5,10)", inRange);
        T.Check("NextDouble mean ~0.5", sum / 100000, 0.5, 0.01);
        T.True("Different seeds differ", new Rng(1).NextDouble() != new Rng(2).NextDouble());
    }

    static void ConfigTests()
    {
        T.Section("Config (FrontsAndRivals.ini)");
        var defaults = Config.FromIni(IniFile.Parse(""), null);
        T.True("Empty INI: Enabled defaults true", defaults.Enabled);
        T.True("Empty INI: DevTools defaults false", !defaults.DevTools);
        T.True("Empty INI: Frequency defaults Normal", defaults.Frequency == AttackFrequency.Normal);
        T.Check("Empty INI: HoldMinutes defaults 5", defaults.HoldMinutes, 5, 0);

        string ini = "; comment\r\n[General]\r\nEnabled = false\r\n[attacks]\r\nfrequency=high ; inline comment\r\nHoldMinutes=99\r\n"
                   + "[Economy]\r\nIncomeMultiplier=1.5\r\nWageMultiplier=abc\r\n[Debug]\r\nDevTools=TRUE\r\n";
        int warnings = 0;
        var c = Config.FromIni(IniFile.Parse(ini), w => warnings++);
        T.True("Enabled=false read", !c.Enabled);
        T.True("Section and key names are case-insensitive", c.Frequency == AttackFrequency.High);
        T.True("DevTools=TRUE read", c.DevTools);
        T.Check("Out-of-range HoldMinutes falls back to 5", c.HoldMinutes, 5, 0);
        T.Check("IncomeMultiplier 1.5", c.IncomeMultiplier, 1.5, 0);
        T.Check("Bad WageMultiplier falls back to 1.0", c.WageMultiplier, 1.0, 0);
        T.Check("Two warnings logged", warnings, 2, 0);

        // A comma-decimal locale must not change how the INI is read.
        CultureInfo old = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
        var de = Config.FromIni(IniFile.Parse("[Economy]\nIncomeMultiplier=2.5\n"), null);
        Thread.CurrentThread.CurrentCulture = old;
        T.Check("INI decimals read the same on a German locale", de.IncomeMultiplier, 2.5, 0);
    }

    static void MarkerTests()
    {
        T.Section("Cleanup markers");
        // Known game hashes (SHVDN RelationshipGroupHash source) prove Markers.Hash is the game's hash.
        T.True("Hash(\"PLAYER\") = 0x6F0783F5", (uint)Markers.Hash("PLAYER") == 0x6F0783F5u);
        T.True("Hash(\"COP\") = 0xA49E591C", (uint)Markers.Hash("COP") == 0xA49E591Cu);
        T.True("Hash(\"AMBIENT_GANG_FAMILY\") = 0x45897C40", (uint)Markers.Hash("AMBIENT_GANG_FAMILY") == 0x45897C40u);
        T.True("Hash is case-insensitive like the game's", Markers.Hash("fr_guards") == Markers.Hash("FR_GUARDS"));
        T.True("FR_GUARDS is a mod group", Markers.IsModGroup(Markers.Hash("FR_GUARDS")));
        T.True("PLAYER is not a mod group", !Markers.IsModGroup(Markers.Hash("PLAYER")));

        T.Equal("Pawn shop held by Dead Kings plate", Markers.MakePlate(3, Markers.OwnerCrownKings), "FR03CK");
        T.Equal("Front 12 held by you", Markers.MakePlate(12, Markers.OwnerPlayer), "FR12YU");
        T.True("FR03CK is a mod plate", Markers.IsModPlate("FR03CK"));
        T.True("Padded plate ' FR03CK ' still matches", Markers.IsModPlate(" FR03CK "));
        T.True("FRANKLIN is not a mod plate", !Markers.IsModPlate("FRANKLIN"));
        T.True("FR1 is not a mod plate", !Markers.IsModPlate("FR1"));
        T.True("FR03ck (lowercase) is not a mod plate", !Markers.IsModPlate("FR03ck"));
        T.True("46EEK572 (random plate) is not a mod plate", !Markers.IsModPlate("46EEK572"));
        T.True("null plate is not a mod plate", !Markers.IsModPlate(null));
    }

    static void DevFormatTests()
    {
        T.Section("F9 position snippet");
        T.Equal("Snippet format", DevFormat.PositionJson(123.456f, -45.6f, 30.04f, 90f),
            "{ \"x\": 123.46, \"y\": -45.60, \"z\": 30.04, \"h\": 90.0 }");
        T.Equal("Negative heading wraps to 0-360", DevFormat.PositionJson(0, 0, 0, -90f),
            "{ \"x\": 0.00, \"y\": 0.00, \"z\": 0.00, \"h\": 270.0 }");
        CultureInfo old = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-FR");
        string fr = DevFormat.PositionJson(1.5f, 2.5f, 3.5f, 4.5f);
        Thread.CurrentThread.CurrentCulture = old;
        T.Equal("Dot decimals on a French locale", fr, "{ \"x\": 1.50, \"y\": 2.50, \"z\": 3.50, \"h\": 4.5 }");
    }
}
