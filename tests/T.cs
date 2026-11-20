using System;

/// <summary>Tiny test helper. Exit code of the test run = number of failures.</summary>
static class T
{
    public static int Failures;

    public static void Check(string name, double got, double want, double tol = 0.05)
    {
        bool ok = Math.Abs(got - want) <= tol;
        Report(ok, name, got.ToString("0.###"), want.ToString());
    }

    public static void True(string name, bool condition)
    {
        Report(condition, name, condition.ToString(), "True");
    }

    public static void Equal(string name, string got, string want)
    {
        Report(got == want, name, got ?? "null", want ?? "null");
    }

    private static void Report(bool ok, string name, string got, string want)
    {
        if (!ok) Failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name + (ok ? "" : ": got " + got + ", want " + want));
    }

    public static void Section(string name)
    {
        Console.WriteLine();
        Console.WriteLine("== " + name);
    }
}

static class Program
{
    static int Main()
    {
        RulesTests.Run();
        CoreTests.Run();
        SaveTests.Run();
        FrontTests.Run();
        EconomyTests.Run();
        AttackTests.Run();
        SettingsTests.Run();
        Console.WriteLine();
        Console.WriteLine(T.Failures == 0 ? "ALL PASS" : T.Failures + " FAILURES");
        return T.Failures;
    }
}
