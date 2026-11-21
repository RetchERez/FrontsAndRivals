// Fronts & Rivals: rival crews (content + save state), the active attack, and the text-line book.
// Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace FrontsAndRivals.Data
{
    public enum LeaderTrait { Ruthless, Cautious, Showboat, Tactician }

    [DataContract]
    public sealed class PersonDef
    {
        [DataMember(Name = "first")] public string First;
        [DataMember(Name = "nick")] public string Nick;
        [DataMember(Name = "last")] public string Last;
        [DataMember(Name = "trait")] public string Trait;
    }

    [DataContract]
    public sealed class CrewDef
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "style")] public string Style;
        [DataMember(Name = "soldierPower")] public double SoldierPower;
        [DataMember(Name = "aggression")] public double Aggression;
        [DataMember(Name = "home")] public Spot Home;
        [DataMember(Name = "blipColor")] public int BlipColor;
        [DataMember(Name = "paint")] public int Paint;
        [DataMember(Name = "boss")] public PersonDef Boss;
        [DataMember(Name = "lieutenants")] public List<PersonDef> Lieutenants;
        [DataMember(Name = "models")] public List<string> Models;
        [DataMember(Name = "vehicles")] public List<string> Vehicles;
        /// <summary>Seats in each of those vehicles (the game's own vehicle data): 2 on a motorcycle, 4 in a sedan.</summary>
        [DataMember(Name = "vehicleSeats")] public List<int> VehicleSeats;

        public const int MaxConvoy = 6; // vehicles per attack, inside the 8-vehicle budget

        /// <summary>The convoy for a squad: the crew's two signature vehicles in turn until everyone has a seat.
        /// Ironside MC rolls up on bikes, two to a bike. Pure, unit-tested.</summary>
        public List<string> ConvoyPlan(int squad)
        {
            var plan = new List<string>();
            if (Vehicles == null || Vehicles.Count == 0) return plan;
            int seated = 0;
            for (int i = 0; seated < Math.Max(1, squad) && plan.Count < MaxConvoy; i++)
            {
                plan.Add(Vehicles[i % Vehicles.Count]);
                seated += SeatsFor(i % Vehicles.Count);
            }
            return plan;
        }

        public int SeatsFor(int index)
        {
            return VehicleSeats != null && index < VehicleSeats.Count && VehicleSeats[index] > 0 ? VehicleSeats[index] : 4;
        }

        /// <summary>Seats the convoy plan gives (the squad is trimmed to this if the budget ran short).</summary>
        public int ConvoySeats(int squad)
        {
            int seats = 0;
            List<string> plan = ConvoyPlan(squad);
            for (int i = 0; i < plan.Count; i++) seats += SeatsFor(i % Vehicles.Count);
            return seats;
        }
        [DataMember(Name = "weapons")] public List<string> Weapons;
    }

    [DataContract]
    public sealed class CrewsFile
    {
        [DataMember(Name = "crews")] public List<CrewDef> Crews;
    }

    public static class MemberRole
    {
        public const string Boss = "boss";
        public const string Lieutenant = "lieutenant";
    }

    [DataContract]
    public sealed class CrewMember
    {
        [DataMember(Name = "id", Order = 0)] public int Id;
        [DataMember(Name = "first", Order = 1)] public string First;
        [DataMember(Name = "nick", Order = 2)] public string Nick;
        [DataMember(Name = "last", Order = 3)] public string Last;
        [DataMember(Name = "trait", Order = 4)] public int Trait;
        [DataMember(Name = "role", Order = 5)] public string Role;
        [DataMember(Name = "rep", Order = 6)] public int Rep;
        [DataMember(Name = "alive", Order = 7)] public bool Alive = true;
        [DataMember(Name = "model", Order = 8)] public string Model;
        /// <summary>Memorable things that happened between this member and the outfit, newest last (max 8).</summary>
        [DataMember(Name = "log", Order = 9)] public List<string> Log = new List<string>();
        /// <summary>Scars, e.g. "put me down at the Car Wash". Quoted in their texts.</summary>
        [DataMember(Name = "scars", Order = 10)] public List<string> Scars = new List<string>();
        /// <summary>Free-roam time after which a "I got back up" text is due (0 = none pending).</summary>
        [DataMember(Name = "returnTextAt", Order = 11)] public long ReturnTextAt;

        public LeaderTrait LeaderTrait { get { return (LeaderTrait)Trait; } }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(Nick) ? First + " " + Last : First + " \"" + Nick + "\" " + Last; }
        }

        public void AddLog(string line)
        {
            if (Log == null) Log = new List<string>();
            Log.Add(line);
            while (Log.Count > 8) Log.RemoveAt(0);
        }
    }

    [DataContract]
    public sealed class CrewState
    {
        [DataMember(Name = "id", Order = 0)] public string Id;
        [DataMember(Name = "grudge", Order = 1)] public int Grudge;
        [DataMember(Name = "manpower", Order = 2)] public int Manpower = 10;
        [DataMember(Name = "members", Order = 3)] public List<CrewMember> Members = new List<CrewMember>();
        /// <summary>After a collapse the crew goes quiet until this in-game day.</summary>
        [DataMember(Name = "quietUntilDay", Order = 4)] public int QuietUntilDay = -1;
        [DataMember(Name = "lastContactDay", Order = 5)] public int LastContactDay;
        [DataMember(Name = "lastMove", Order = 6)] public string LastMove;

        public CrewMember Boss
        {
            get { foreach (CrewMember m in Members) if (m.Alive && m.Role == MemberRole.Boss) return m; return null; }
        }

        public List<CrewMember> Lieutenants()
        {
            var l = new List<CrewMember>();
            foreach (CrewMember m in Members) if (m.Alive && m.Role == MemberRole.Lieutenant) l.Add(m);
            return l;
        }

        public CrewMember Member(int id)
        {
            foreach (CrewMember m in Members) if (m.Id == id) return m;
            return null;
        }
    }

    public static class AttackPhase
    {
        public const string Warning = "warning";
        public const string Window = "window";
        public const string Live = "live";
    }

    /// <summary>Your guards out on a raid against a rival front.</summary>
    [DataContract]
    public sealed class RaidState
    {
        [DataMember(Name = "front", Order = 0)] public string Front;
        [DataMember(Name = "crew", Order = 1)] public string Crew;
        [DataMember(Name = "guards", Order = 2)] public List<int> GuardIds = new List<int>();
        /// <summary>Free-roam ms when word comes back.</summary>
        [DataMember(Name = "endsMs", Order = 3)] public long EndsMs;
        [DataMember(Name = "odds", Order = 4)] public double Odds;
    }

    [DataContract]
    public sealed class AttackState
    {
        [DataMember(Name = "crew", Order = 0)] public string Crew;
        [DataMember(Name = "front", Order = 1)] public string Front;
        [DataMember(Name = "leader", Order = 2)] public int Leader;
        [DataMember(Name = "squad", Order = 3)] public int Squad;
        [DataMember(Name = "strength", Order = 4)] public double Strength;
        [DataMember(Name = "phase", Order = 5)] public string Phase = AttackPhase.Warning;
        /// <summary>Free-roam ms when the warning ends and the attack window opens.</summary>
        [DataMember(Name = "warnEnds", Order = 6)] public long WarnEnds;
        /// <summary>Free-roam ms when the window closes and the attack auto-resolves.</summary>
        [DataMember(Name = "windowEnds", Order = 7)] public long WindowEnds;
        /// <summary>The crew already waited 90 s for you once (saved, so a reload can't repeat it).</summary>
        [DataMember(Name = "waited", Order = 8)] public bool Waited;
        /// <summary>Free-roam ms when word of the attack firms up into a warning (the rumour comes at planning).</summary>
        [DataMember(Name = "warnAt", Order = 9)] public long WarnAt;
        [DataMember(Name = "warned", Order = 10)] public bool Warned;
    }

    /// <summary>Text templates with tokens like {front} and {crew}. Built-in fallbacks if lines.json is missing.</summary>
    public sealed class LineBook
    {
        private readonly Dictionary<string, List<string>> _lines = new Dictionary<string, List<string>>();

        public static LineBook Load(string path, Action<string> log)
        {
            var book = new LineBook();
            try
            {
                if (File.Exists(path))
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    int start = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
                    var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                    var ser = new DataContractJsonSerializer(typeof(Dictionary<string, List<string>>), settings);
                    using (var ms = new MemoryStream(bytes, start, bytes.Length - start))
                    {
                        var d = (Dictionary<string, List<string>>)ser.ReadObject(ms);
                        foreach (var kv in d) if (kv.Value != null && kv.Value.Count > 0) book._lines[kv.Key] = kv.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("Content: lines.json could not be read (" + ex.Message + "), using built-in lines");
            }
            return book;
        }

        public int Count { get { return _lines.Count; } }

        /// <summary>A random line for the moment, tokens replaced. Unknown moments get a plain fallback.</summary>
        public string Pick(Random rng, string moment, IDictionary<string, string> tokens)
        {
            List<string> options;
            string line = _lines.TryGetValue(moment, out options) ? options[rng.Next(options.Count)] : Fallback(moment);
            return Fill(line, tokens);
        }

        public static string Fill(string line, IDictionary<string, string> tokens)
        {
            if (tokens == null) return line;
            foreach (var kv in tokens) line = line.Replace("{" + kv.Key + "}", kv.Value ?? "");
            return line;
        }

        private static string Fallback(string moment)
        {
            switch (moment)
            {
                case "warning": return "Word is the {crew} want the {front}. Tonight.";
                case "held": return "{guard} and the others held the {front}.";
                case "lost": return "We lost the {front}. {leader} is running it now.";
                case "guardDied": return "{guard} didn't make it out of the {front}.";
                case "frontTaken": return "The {front} is yours.";
                default: return "";
            }
        }
    }
}
