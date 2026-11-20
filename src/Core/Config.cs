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
        // [Attacks]
        public AttackFrequency Frequency = AttackFrequency.Normal;
        public int WarningHours = 3;
        public bool PoliceRespond = true;
        public bool ArrivalCamera = true;
        // [Economy]
        public double IncomeMultiplier = 1.0;
        public double WageMultiplier = 1.0;
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
            c.Frequency = Enum(ini, "Attacks", "Frequency", c.Frequency, warn);
            c.WarningHours = Int(ini, "Attacks", "WarningHours", c.WarningHours, 1, 24, warn);
            c.PoliceRespond = Bool(ini, "Attacks", "PoliceRespond", c.PoliceRespond, warn);
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
