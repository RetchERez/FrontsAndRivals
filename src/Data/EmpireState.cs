// Fronts & Rivals: everything the save file holds. Pure data, no game calls.
// DataContract deserialization skips field initializers, so Migrations.Normalize fills any nulls.
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FrontsAndRivals.Data
{
    [DataContract]
    public sealed class EmpireState
    {
        public const int CurrentSchema = 1;

        [DataMember(Name = "schemaVersion", Order = 0)] public int SchemaVersion = CurrentSchema;
        [DataMember(Name = "createdUtc", Order = 1)] public string CreatedUtc;
        [DataMember(Name = "saveCount", Order = 2)] public int SaveCount;
        /// <summary>The save's random generator state (Core.Rng). One seeded generator per save.</summary>
        [DataMember(Name = "rngState", Order = 3)] public ulong RngState;
        /// <summary>Free-roam milliseconds played with the mod active.</summary>
        [DataMember(Name = "activeMs", Order = 4)] public long ActiveMs;
        [DataMember(Name = "ownedFronts", Order = 5)] public List<string> OwnedFronts = new List<string>(); // unused since M2; kept so old saves load
        [DataMember(Name = "grudge", Order = 6)] public Dictionary<string, int> Grudge = new Dictionary<string, int>();
        [DataMember(Name = "fronts", Order = 7)] public List<FrontState> Fronts = new List<FrontState>();
        [DataMember(Name = "guards", Order = 8)] public List<GuardState> Guards = new List<GuardState>();
        [DataMember(Name = "nextGuardId", Order = 9)] public int NextGuardId = 1;
        /// <summary>In-game days counted since the empire started.</summary>
        [DataMember(Name = "day", Order = 10)] public int Day;
        /// <summary>Game calendar date (yyyy-MM-dd) when midnights were last processed.</summary>
        [DataMember(Name = "lastGameDate", Order = 11)] public string LastGameDate;
        /// <summary>Today's recruits from the Broker (not on the roster until hired).</summary>
        [DataMember(Name = "offers", Order = 12)] public List<GuardState> Offers = new List<GuardState>();
        [DataMember(Name = "offersDay", Order = 13)] public int OffersDay = -1;
        /// <summary>Settings screen: attack frequency override (null = use the INI) and map blips.</summary>
        [DataMember(Name = "frequency", Order = 14)] public string Frequency;
        [DataMember(Name = "hideBlips", Order = 15)] public bool HideBlips;
        [DataMember(Name = "lastIncome", Order = 16)] public int LastNet;
    }

    [DataContract]
    public sealed class FrontState
    {
        [DataMember(Name = "id", Order = 0)] public string Id;
        /// <summary>Owners.Player, Owners.ForSale or a crew code.</summary>
        [DataMember(Name = "owner", Order = 1)] public string Owner;
    }

    public static class GuardStatus
    {
        public const string Active = "active";
        public const string Injured = "injured";
        public const string Dead = "dead";
    }

    [DataContract]
    public sealed class GuardState
    {
        public const int Reserve = -1;

        [DataMember(Name = "id", Order = 0)] public int Id;
        [DataMember(Name = "first", Order = 1)] public string First;
        [DataMember(Name = "nick", Order = 2)] public string Nick;
        [DataMember(Name = "last", Order = 3)] public string Last;
        [DataMember(Name = "model", Order = 4)] public string Model;
        /// <summary>Rules.Rank, Rules.Weapon and Rules.Trait as ints.</summary>
        [DataMember(Name = "rank", Order = 5)] public int Rank;
        [DataMember(Name = "weapon", Order = 6)] public int Weapon;
        [DataMember(Name = "trait", Order = 7)] public int Trait;
        [DataMember(Name = "xp", Order = 8)] public int Xp;
        [DataMember(Name = "kills", Order = 9)] public int Kills;
        [DataMember(Name = "defenses", Order = 10)] public int Defenses;
        [DataMember(Name = "status", Order = 11)] public string Status = GuardStatus.Active;
        /// <summary>Front the guard is posted at, or null for the HQ reserve.</summary>
        [DataMember(Name = "front", Order = 12)] public string Front;
        [DataMember(Name = "post", Order = 13)] public int Post = Reserve;
        /// <summary>Free-roam time (EmpireState.ActiveMs) when an injured guard is back on duty.</summary>
        [DataMember(Name = "recoverAtMs", Order = 14)] public long RecoverAtMs;
        /// <summary>Memorial line, e.g. where they fell.</summary>
        [DataMember(Name = "fellAt", Order = 15)] public string FellAt;

        /// <summary>Mateo "Smoke" Alvarez</summary>
        public string FullName { get { return First + " \"" + Nick + "\" " + Last; } }
        public bool IsPosted { get { return Front != null && Post >= 0; } }
    }
}
