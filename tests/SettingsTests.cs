using System;
using System.IO;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

static class SettingsTests
{
    public static void Run()
    {
        T.Section("Settings (FrontsAndRivals.ini)");
        string repoIni = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FrontsAndRivals.ini"));
        int warnings = 0;
        Config shipped = Config.Load(repoIni, w => warnings++);
        T.Check("Shipped INI parses with no warnings", warnings, 0, 0);
        T.True("Shipped INI: dev tools off, sounds on, controller combo on, O hotkey", shipped.OfficeHotkey == "O" && !shipped.DevTools && shipped.SoundEffects && shipped.OfficeButton && shipped.EmpirePressure && shipped.FrontStaff && shipped.Raids && shipped.Bodyguards && shipped.PurchaseShot && shipped.Tips && shipped.Hangouts && shipped.Difficulty == "Normal");
        T.True("Shipped INI: defaults match the code", shipped.MinSecondsBetweenTexts == 20 && shipped.RivalAlertRange == 25
            && shipped.LiveDefense && shipped.TurfFrequency == AttackFrequency.Normal && shipped.NotifyLevel == NoteLevel.All);

        var c = Config.FromIni(IniFile.Parse("[General]\nOfficeButton=false\n[Sound]\nEffects=false\n[Attacks]\nCooldownMinutes=90\nLiveDefense=false\nFirstAttackDelayMinutes=0\n"
            + "[Turf]\nFrequency=Off\n[Notifications]\nLevel=Minimal\nMinSecondsBetween=45\nTaunts=false\n[Rivals]\nAlertRange=500\n"), null);
        T.True("Controller button and sound settings read", !c.OfficeButton && !c.SoundEffects);
        T.True("Attack settings read", c.CooldownMinutes == 90 && !c.LiveDefense && c.FirstAttackDelayMinutes == 0);
        T.True("Turf and notification settings read", c.TurfFrequency == AttackFrequency.Off && c.NotifyLevel == NoteLevel.Minimal && c.MinSecondsBetweenTexts == 45 && !c.Taunts);
        T.Check("Out-of-range alert range falls back to 25", c.RivalAlertRange, 25, 0);

        var s = SaveService.NewEmpire();
        s.Fronts.Add(new FrontState { Id = "x", Owner = Owners.Player });
        s.Heat = Heat.Neutral; // steady streets: the plain frequency
        long now = 200 * 60000L;
        s.LastAttackMs = now - 60 * 60000L;
        T.True("Cooldown 90 min: no attack after 60 min", !Attacks.CanPlan(s, now, now, "Normal", 90, 10));
        T.True("Cooldown 0 uses Frequency (60 min): attack allowed after 61 min", Attacks.CanPlan(s, now + 60000, now, "Normal", 0, 10));
        T.True("Frequency Off still blocks a custom cooldown", !Attacks.CanPlan(s, now, now, "Off", 5, 10));
        T.True("First attack delay 0: attacks right after loading", Attacks.CanPlan(s, now, 1000, "Normal", 0, 0));
        T.Check("Turf Off: no moves", Turf.ChanceFor("Off"), 0, 0);
        T.Check("Turf High: 40% a night per crew", Turf.ChanceFor("High"), 0.4, 0.0001);

        T.Section("Notification queue (no spam)");
        var set = new NoteSettings { MinSecondsBetween = 20 };
        var q = new NotifyQueue();
        q.Enqueue(N(NoteKind.StreetWord, "gossip"), set, 0);
        q.Enqueue(N(NoteKind.Taunt, "taunt"), set, 0);
        q.Enqueue(N(NoteKind.AttackWarning, "warning"), set, 0);
        T.Equal("Important messages jump the queue", q.Next(set, 0).Message, "warning");
        T.True("Nothing else for 20 seconds", q.Next(set, 19999) == null);
        T.Equal("Then story before gossip", q.Next(set, 20000).Message, "taunt");
        T.Equal("Then the gossip", q.Next(set, 40000).Message, "gossip");

        q.Enqueue(N(NoteKind.StreetWord, "stale"), set, 50000);
        T.True("Gossip waiting over 5 minutes is dropped", q.Next(set, 50000 + 6 * 60000) == null);

        var minimal = new NoteSettings { Level = NoteLevel.Minimal };
        T.True("Minimal: attack warnings get through", NotifyQueue.Allowed(NoteKind.AttackWarning, minimal));
        T.True("Minimal: no midnight report", !NotifyQueue.Allowed(NoteKind.Midnight, minimal));
        var important = new NoteSettings { Level = NoteLevel.Important };
        T.True("Important: guard deaths get through", NotifyQueue.Allowed(NoteKind.GuardDeath, important));
        T.True("Important: no taunts or gossip", !NotifyQueue.Allowed(NoteKind.Taunt, important) && !NotifyQueue.Allowed(NoteKind.StreetWord, important));
        T.True("Switches: taunts off blocks taunts", !NotifyQueue.Allowed(NoteKind.Taunt, new NoteSettings { Taunts = false }));
        T.True("Switches: midnight report off", !NotifyQueue.Allowed(NoteKind.Midnight, new NoteSettings { MidnightReport = false }));

        var flood = new NotifyQueue();
        for (int i = 0; i < 30; i++) flood.Enqueue(N(i == 29 ? NoteKind.AttackResult : NoteKind.StreetWord, "m" + i), set, i);
        T.Check("A flood is capped at 12 queued", flood.Count, NotifyQueue.MaxQueued, 0);
        string longText = "~h~Car Wash under attack~h~~n~The ~r~Dead Kings~s~ are hitting the Car Wash right now. Your people can hold about 4 minutes. Get there and fight beside them, or leave it to them. Route's on your GPS.";
        var pieces = NotifyQueue.Pieces(longText, 99, 10);
        T.True("Long alerts are sent whole, in pieces of 99 characters or less", string.Concat(pieces) == longText && pieces.TrueForAll(x => x.Length <= 99) && pieces.Count >= 2);
        T.True("...never split inside a colour code", pieces.TrueForAll(x => (x.Length - x.Replace("~", "").Length) % 2 == 0));
        T.True("A short alert is one piece", NotifyQueue.Pieces("Waypoint set.", 99, 10).Count == 1);
        T.Equal("...and the important one survived the cap", flood.Next(set, 100).Message, "m29");

        var nogap = new NoteSettings { MinSecondsBetween = 0 };
        var q2 = new NotifyQueue();
        q2.Enqueue(N(NoteKind.Midnight, "a"), nogap, 0);
        q2.Enqueue(N(NoteKind.Midnight, "b"), nogap, 0);
        T.True("Gap 0: back to back", q2.Next(nogap, 0) != null && q2.Next(nogap, 0) != null);

    }

    static Note N(NoteKind k, string msg) { return new Note { Kind = k, Message = msg, Sender = "s", Subject = "t" }; }
}
