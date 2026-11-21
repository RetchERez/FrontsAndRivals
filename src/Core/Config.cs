// Fronts & Rivals: reads FrontsAndRivals.ini. Unknown or bad values fall back to the design-doc
// default and log a warning. Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FrontsAndRivals.Core
{
    /// <summary>Minimal INI reader: [Section], key=value, ';' or '#' comments. Case-insensitive.</summary>
    public sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static IniFile Parse(string text)
        {
            var ini = new IniFile();
            Dictionary<string, string> current = null;
            if (text == null) return ini;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    if (!ini._sections.TryGetValue(name, out current))
                    {
                        current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        ini._sections[name] = current;
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || current == null) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                int comment = value.IndexOf(" ;", StringComparison.Ordinal);
                if (comment >= 0) value = value.Substring(0, comment).Trim();
                current[key] = value;
            }
            return ini;
        }

        public string Get(string section, string key)
        {
            Dictionary<string, string> s;
            string v;
            if (_sections.TryGetValue(section, out s) && s.TryGetValue(key, out v)) return v;
            return null;
        }
    }

    public enum AttackFrequency { Off, Low, Normal, High }

    public sealed class Config
    {
        // [General]
        public bool Enabled = true;
        public string OfficeHotkey = "None";
        /// <summary>Controller: the Interaction Menu button (Back/View, M on keyboard) opens the Office.</summary>
        public bool OfficeButton = true; // hold Back/View; a tap still changes the camera
        // [Sound]
        public bool SoundEffects = true;
        // [Attacks]
        public AttackFrequency Frequency = AttackFrequency.Normal;
        public int HoldMinutes = 5; // how long your guards hold an attack (at even odds) before it's decided without you
        public bool PoliceRespond = true;
        /// <summary>False: attacks always resolve on their own, even when you're there.</summary>
        public bool LiveDefense = true;
        public bool EmpirePressure = true; // more fronts held: attacks come sooner and bigger
        /// <summary>Minutes between attacks. 0 = use Frequency (Low 75, Normal 45, High 25).</summary>
        public int CooldownMinutes = 0;
        /// <summary>No attacks in the first minutes after loading a game.</summary>
        public int FirstAttackDelayMinutes = 10;
        // [Turf]
        public AttackFrequency TurfFrequency = AttackFrequency.Normal;
        // [Notifications]
        public NoteLevel NotifyLevel = NoteLevel.All;
        public int MinSecondsBetweenTexts = 20;
        public bool Taunts = true;
        public bool StreetWord = true;
        public bool MidnightReport = true;
        // [Rivals]
        public int RivalAlertRange = 35;
        // [Map] (ShowRivalFronts below)
        public bool ShowForSaleFronts = true;
        public bool FrontStaff = true;
        public bool ArrivalCamera = true;
        // [Economy]
        public double IncomeMultiplier = 1.0;
        public double WageMultiplier = 1.0;
        /// <summary>A front for sale costs this many days of its income (the starter car wash is always $10,000).</summary>
        public int FrontPriceDays = 35; // = Economy.PriceDays: Story Mode property return, about 20 weeks to pay back
        // [Guards]
        public bool PermanentDeath = true;
        /// <summary>Fronts can't be lost: a failed defense damages the front (no income at the next midnight) and deaths become injuries.</summary>
        public bool ProtectedMode = false;
        /// <summary>Start with the car wash, 2 recruits and the Broker's message already done (for reinstalls).</summary>
        public bool SkipIntro = false;
        // [Map]
        public bool ShowRivalFronts = true;
        // [Debug]
        public bool DevTools = false;

        /// <summary>Loads the INI at path. A missing file gives all defaults.</summary>
        public static Config Load(string path, Action<string> warn)
        {
            string text = null;
            try
            {
                if (File.Exists(path)) text = File.ReadAllText(path);
                else if (warn != null) warn("Config: " + path + " not found, using defaults");
            }
            catch (Exception ex)
            {
                if (warn != null) warn("Config: could not read " + path + ": " + ex.Message);
            }
            return FromIni(IniFile.Parse(text), warn);
        }

        public static Config FromIni(IniFile ini, Action<string> warn)
        {
            var c = new Config();
            c.Enabled = Bool(ini, "General", "Enabled", c.Enabled, warn);
            c.OfficeHotkey = ini.Get("General", "OfficeHotkey") ?? c.OfficeHotkey;
            c.OfficeButton = Bool(ini, "General", "OfficeButton", c.OfficeButton, warn);
            c.SoundEffects = Bool(ini, "Sound", "Effects", c.SoundEffects, warn);
            c.Frequency = Enum(ini, "Attacks", "Frequency", c.Frequency, warn);
            c.HoldMinutes = Int(ini, "Attacks", "HoldMinutes", c.HoldMinutes, 2, 12, warn);
            c.PoliceRespond = Bool(ini, "Attacks", "PoliceRespond", c.PoliceRespond, warn);
            c.LiveDefense = Bool(ini, "Attacks", "LiveDefense", c.LiveDefense, warn);
            c.EmpirePressure = Bool(ini, "Attacks", "EmpirePressure", c.EmpirePressure, warn);
            c.CooldownMinutes = Int(ini, "Attacks", "CooldownMinutes", c.CooldownMinutes, 0, 600, warn);
            c.FirstAttackDelayMinutes = Int(ini, "Attacks", "FirstAttackDelayMinutes", c.FirstAttackDelayMinutes, 0, 120, warn);
            c.TurfFrequency = Enum(ini, "Turf", "Frequency", c.TurfFrequency, warn);
            c.NotifyLevel = Enum(ini, "Notifications", "Level", c.NotifyLevel, warn);
            c.MinSecondsBetweenTexts = Int(ini, "Notifications", "MinSecondsBetween", c.MinSecondsBetweenTexts, 0, 600, warn);
            c.Taunts = Bool(ini, "Notifications", "Taunts", c.Taunts, warn);
            c.StreetWord = Bool(ini, "Notifications", "StreetWord", c.StreetWord, warn);
            c.MidnightReport = Bool(ini, "Notifications", "MidnightReport", c.MidnightReport, warn);
            c.RivalAlertRange = Int(ini, "Rivals", "AlertRange", c.RivalAlertRange, 5, 150, warn);
            c.ShowForSaleFronts = Bool(ini, "Map", "ShowForSaleFronts", c.ShowForSaleFronts, warn);
            c.FrontStaff = Bool(ini, "Map", "FrontStaff", c.FrontStaff, warn);
            c.FrontPriceDays = Int(ini, "Economy", "FrontPriceDays", c.FrontPriceDays, 1, 100, warn);
            c.ArrivalCamera = Bool(ini, "Attacks", "ArrivalCamera", c.ArrivalCamera, warn);
            c.IncomeMultiplier = Double(ini, "Economy", "IncomeMultiplier", c.IncomeMultiplier, 0, 100, warn);
            c.WageMultiplier = Double(ini, "Economy", "WageMultiplier", c.WageMultiplier, 0, 100, warn);
            c.PermanentDeath = Bool(ini, "Guards", "PermanentDeath", c.PermanentDeath, warn);
            c.ProtectedMode = Bool(ini, "Guards", "ProtectedMode", c.ProtectedMode, warn);
            c.SkipIntro = Bool(ini, "General", "SkipIntro", c.SkipIntro, warn);
            c.ShowRivalFronts = Bool(ini, "Map", "ShowRivalFronts", c.ShowRivalFronts, warn);
            c.DevTools = Bool(ini, "Debug", "DevTools", c.DevTools, warn);
            return c;
        }

        private static bool Bool(IniFile ini, string s, string k, bool def, Action<string> warn)
        {
            string v = ini.Get(s, k);
            if (v == null) return def;
            if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1") return true;
            if (v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0") return false;
            Warn(warn, s, k, v, def.ToString());
            return def;
        }

        private static int Int(IniFile ini, string s, string k, int def, int min, int max, Action<string> warn)
        {
            string v = ini.Get(s, k);
            if (v == null) return def;
            int r;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) && r >= min && r <= max) return r;
            Warn(warn, s, k, v, def.ToString(CultureInfo.InvariantCulture));
            return def;
        }

        private static double Double(IniFile ini, string s, string k, double def, double min, double max, Action<string> warn)
        {
            string v = ini.Get(s, k);
            if (v == null) return def;
            double r;
            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r) && r >= min && r <= max) return r;
            Warn(warn, s, k, v, def.ToString(CultureInfo.InvariantCulture));
            return def;
        }

        private static T Enum<T>(IniFile ini, string s, string k, T def, Action<string> warn) where T : struct
        {
            string v = ini.Get(s, k);
            if (v == null) return def;
            T r;
            int ignored;
            if (!int.TryParse(v, out ignored) && System.Enum.TryParse(v, true, out r)) return r;
            Warn(warn, s, k, v, def.ToString());
            return def;
        }

        private static void Warn(Action<string> warn, string s, string k, string v, string def)
        {
            if (warn != null) warn("Config: [" + s + "] " + k + "=" + v + " is not valid, using " + def);
        }
    }
}
