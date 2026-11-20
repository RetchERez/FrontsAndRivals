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
        /// <summary>Free-roam milliseconds played with the mod active. Attack cooldowns build on this later.</summary>
        [DataMember(Name = "activeMs", Order = 4)] public long ActiveMs;
        [DataMember(Name = "ownedFronts", Order = 5)] public List<string> OwnedFronts = new List<string>();
        [DataMember(Name = "grudge", Order = 6)] public Dictionary<string, int> Grudge = new Dictionary<string, int>();
    }
}
