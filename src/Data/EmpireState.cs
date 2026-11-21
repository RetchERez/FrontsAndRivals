// Fronts & Rivals: everything the save file holds. Pure data, no game calls.
// DataContract deserialization skips field initializers, so Migrations.Normalize fills any nulls.
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace FrontsAndRivals.Data
{
    [DataContract]
    public sealed class EmpireState
    {
        public const int CurrentSchema = 3; // 2: seven-step weapon ladder; 3: five steps (balance pass)

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
        // ---- v1.0: crews and attacks ----
        [DataMember(Name = "crews", Order = 17)] public List<CrewState> Crews = new List<CrewState>();
        [DataMember(Name = "attack", Order = 18)] public AttackState Attack;
        [DataMember(Name = "lastAttackMs", Order = 19)] public long LastAttackMs;
        [DataMember(Name = "lastLossMs", Order = 20)] public long LastLossMs = -10000000;
        [DataMember(Name = "frontAttackedMs", Order = 21)] public Dictionary<string, long> FrontAttackedMs = new Dictionary<string, long>();
        [DataMember(Name = "nextMemberId", Order = 22)] public int NextMemberId = 1;
        [DataMember(Name = "introSent", Order = 23)] public bool IntroSent;
        [DataMember(Name = "memorial", Order = 24)] public List<string> Memorial = new List<string>();
        // Office Settings overrides (null/false = use the INI)
        [DataMember(Name = "turfFrequency", Order = 25)] public string TurfFrequency;
        [DataMember(Name = "notifyLevel", Order = 26)] public string NotifyLevel;
        [DataMember(Name = "liveDefenseOff", Order = 27)] public bool LiveDefenseOff;
        [DataMember(Name = "soundOff", Order = 28)] public bool SoundOff;
        [DataMember(Name = "guardsRecover", Order = 29)] public bool GuardsRecover; // Office: deaths become injuries
        [DataMember(Name = "routeOff", Order = 30)] public bool RouteOff;           // Office: no GPS route to attacks
        [DataMember(Name = "ruledAll", Order = 31)] public bool RuledAll;           // held every front at least once
        [DataMember(Name = "fearUntilDay", Order = 32)] public int FearUntilDay = -1; // you killed one of your own: recruits want danger money
        [DataMember(Name = "tipsSeen", Order = 33)] public int TipsSeen;              // first-time tips already shown (UI.Tip flags)
    }

    [DataContract]
    public sealed class FrontState
    {
        [DataMember(Name = "id", Order = 0)] public string Id;
        /// <summary>Owners.Player, Owners.ForSale or a crew code.</summary>
        [DataMember(Name = "owner", Order = 1)] public string Owner;
        /// <summary>Crew this front was taken from (they want it back: x1.5 target score).</summary>
        [DataMember(Name = "takenFrom", Order = 2)] public string TakenFrom;
        // Upgrades (permanent while held; a lost front keeps them at half strength, see Attacks).
        [DataMember(Name = "cameras", Order = 3)] public bool Cameras;
        [DataMember(Name = "doors", Order = 4)] public bool Doors;
        [DataMember(Name = "floodlights", Order = 5)] public bool Floodlights;
        [DataMember(Name = "armory", Order = 6)] public bool Armory;
        /// <summary>Protected mode: a failed defense damages the front, no income at the next midnight.</summary>
        [DataMember(Name = "damaged", Order = 7)] public bool Damaged;
        /// <summary>After you repel this crew, their nearest front is weakened (-25% defense) until this day.</summary>
        [DataMember(Name = "weakUntilDay", Order = 8)] public int WeakUntilDay = -1;
        /// <summary>A crew that just took this front from you holds it until this day (the next midnight).</summary>
        [DataMember(Name = "dugInUntilDay", Order = 9)] public int DugInUntilDay;
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
        /// <summary>Skills, 0-100, like the player's (Rules.Stats). They grow as the guard works for you.</summary>
        [DataMember(Name = "shooting", Order = 16)] public int Shooting;
        [DataMember(Name = "strength", Order = 17)] public int Strength;
        [DataMember(Name = "stamina", Order = 18)] public int Stamina;
        [DataMember(Name = "stealth", Order = 19)] public int Stealth;
        /// <summary>Following you as a bodyguard. They keep their post and go back to it when sent back.</summary>
        [DataMember(Name = "escort", Order = 20)] public bool Escort;

        /// <summary>Mateo "Smoke" Alvarez</summary>
        public string FullName { get { return string.IsNullOrEmpty(Nick) ? First + " " + Last : First + " \"" + Nick + "\" " + Last; } }
        /// <summary>How the Office and texts name a guard: Andre "Smoke", or Andre Brooks when there's no street name.</summary>
        public string ShortName { get { return string.IsNullOrEmpty(Nick) ? First + " " + Last : First + " \"" + Nick + "\""; } }
        public bool IsPosted { get { return Front != null && Post >= 0; } }
    }
}
