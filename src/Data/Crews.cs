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

        /// <summary>Weak, normal and powerful rides (two models each), picked by how hard the crew is coming for you.</summary>
        [DataMember(Name = "vehicleTiers")] public List<VehicleTierDef> VehicleTiers;

        /// <summary>Revenge intensity, 0 weak / 1 normal / 2 powerful: grudge plus half the street heat. A crew that barely
        /// knows you sends its beaters; one you've hurt badly, on blazing streets, rolls up in its best. Pure, tested.</summary>
        public static int TierFor(int grudge, int heat)
        {
            int score = Math.Max(0, grudge) + Math.Max(0, heat) / 2;
            return score < 25 ? 0 : score < 60 ? 1 : 2;
        }

        public const int MaxConvoy = 6; // vehicles per attack, inside the 8-vehicle budget

        /// <summary>The convoy for a squad: the crew's two signature vehicles in turn until everyone has a seat.
        /// Hellfire MC rolls up on bikes, two to a bike. Pure, unit-tested.</summary>
        public List<string> ConvoyPlan(int squad) { return ConvoyPlan(squad, -1); }

        /// <summary>The convoy for a squad from one tier's two rides, alternating until everyone has a seat
        /// (tier -1: the crew's signature pair).</summary>
        public List<string> ConvoyPlan(int squad, int tier)
        {
            var plan = new List<string>();
            List<string> rides = RidesFor(tier);
            if (rides == null || rides.Count == 0) return plan;
            int seated = 0;
            for (int i = 0; seated < Math.Max(1, squad) && plan.Count < MaxConvoy; i++)
            {
                string m = rides[i % rides.Count];
                plan.Add(m);
                seated += SeatsOf(m);
            }
            return plan;
        }

        public List<string> RidesFor(int tier)
        {
            if (tier >= 0 && VehicleTiers != null && VehicleTiers.Count > 0)
            {
                VehicleTierDef t = VehicleTiers[Math.Min(tier, VehicleTiers.Count - 1)];
                if (t != null && t.Vehicles != null && t.Vehicles.Count > 0) return t.Vehicles;
            }
            return Vehicles;
        }

        /// <summary>Seats in a vehicle model, from the crew's own data (2 on a bike, 4 in a sedan; 4 if unknown).</summary>
        public int SeatsOf(string model)
        {
            if (VehicleTiers != null)
                foreach (VehicleTierDef t in VehicleTiers)
                    if (t != null && t.Vehicles != null)
                        for (int i = 0; i < t.Vehicles.Count; i++)
                            if (t.Vehicles[i] == model && t.Seats != null && i < t.Seats.Count && t.Seats[i] > 0) return t.Seats[i];
            if (Vehicles != null) { int k = Vehicles.IndexOf(model); if (k >= 0) return SeatsFor(k); }
            return 4;
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
        /// <summary>Heavier guns the crew brings to bigger fronts and grudge fights.</summary>
        [DataMember(Name = "heavyWeapons")] public List<string> HeavyWeapons;
        /// <summary>The rare explosive a crew brings to a hit: grenades, Molotovs, pipe bombs, a launcher.</summary>
        [DataMember(Name = "explosives")] public List<string> Explosives;

        /// <summary>Share of a crew's soldiers carrying a heavy gun: 10% at a small front, 35% mid-size, 60% big,
        /// plus up to 25% more at full grudge (never over 80%).</summary>
        public static double HeavyShare(int tier, int grudge)
        {
            return Math.Min(0.8, 0.1 + 0.25 * Math.Max(0, Math.Min(2, tier)) + Math.Max(0, Math.Min(100, grudge)) / 400.0);
        }

        private static uint Mix(int pick) { return unchecked((uint)pick * 2654435761u); }

        /// <summary>The gun for one soldier, from a stable number (the same soldier keeps the same gun every visit).
        /// Everyday guns at small fronts; more heavy ones at big fronts and when the crew holds a grudge. Pure, tested.</summary>
        public string WeaponFor(int pick, int tier, int grudge)
        {
            uint h = Mix(pick);
            bool heavy = HeavyWeapons != null && HeavyWeapons.Count > 0 && (h % 1000) / 1000.0 < HeavyShare(tier, grudge);
            List<string> from = heavy ? HeavyWeapons : Weapons;
            if (from == null || from.Count == 0) return "pistol";
            return from[(int)((h / 1000) % (uint)from.Count)];
        }

        /// <summary>The car wash crew: a couple of kids with pistols, bats, knives and crowbars, the easy way in.</summary>
        public static readonly string[] StarterWeapons = { "pistol", "bat", "pistol", "knife", "crowbar" };

        public static string StarterWeapon(int pick) { return StarterWeapons[(int)(Mix(pick) % (uint)StarterWeapons.Length)]; }

        /// <summary>Aim of a crew front's soldiers by size: 20 small, 27 mid-size, 34 big (street heat adds up to 20).</summary>
        public static int FrontAccuracy(int tier) { return 20 + 7 * Math.Max(0, Math.Min(2, tier)); }

        public const double ExplosiveChance = 0.05;

        /// <summary>About 1 attacker in 20 carries the crew's explosive (at most one per squad; the caller enforces that).
        /// Returns the explosive's name, or null.</summary>
        public string ExplosiveFor(int pick)
        {
            if (Explosives == null || Explosives.Count == 0) return null;
            uint h = Mix(pick ^ 0x5bd1e995);
            if ((h % 1000) / 1000.0 >= ExplosiveChance) return null;
            return Explosives[(int)((h / 1000) % (uint)Explosives.Count)];
        }

        /// <summary>The leader carries the crew's best gun.</summary>
        public string LeaderWeapon()
        {
            if (HeavyWeapons != null && HeavyWeapons.Count > 0) return HeavyWeapons[HeavyWeapons.Count - 1];
            return Weapons != null && Weapons.Count > 0 ? Weapons[Weapons.Count - 1] : "pistol";
        }

        /// <summary>Armour on soldiers holding a front: none at a small one, 25 mid-size, 50 big.</summary>
        public static int FrontArmor(int tier) { return tier >= 2 ? 50 : tier == 1 ? 25 : 0; }
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
    public sealed class VehicleTierDef
    {
        [DataMember(Name = "vehicles")] public List<string> Vehicles;
        [DataMember(Name = "seats")] public List<int> Seats;
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
 /// <summary>The crew this one is feuding with (its last turf fight, either way).</summary>
        [DataMember(Name = "feudWith", Order = 20)] public string FeudWith;
        [DataMember(Name = "lastMove", Order = 6)] public string LastMove;
        /// <summary>Grudge from street kills so far on EmpireState.StreetDay (capped per day).</summary>
        [DataMember(Name = "streetGrudgeToday", Order = 21)] public int StreetGrudgeToday;

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
        /// <summary>The hour the raid set off (the odds shown were worked out for it). Null in older saves.</summary>
        [DataMember(Name = "hour", Order = 5)] public int? Hour;
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
