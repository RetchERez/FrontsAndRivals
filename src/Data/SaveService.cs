// Fronts & Rivals: crash-safe save. Write to .tmp, keep 3 backups, then swap in.
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;

namespace FrontsAndRivals.Data
{
    [DataContract]
    public sealed class EmpireState
    {
        [DataMember] public int SchemaVersion = 1;
        [DataMember] public int RngSeed;
        [DataMember] public List<string> OwnedFronts = new List<string>();
        [DataMember] public Dictionary<string, int> Grudge = new Dictionary<string, int>();
    }

    public static class SaveService
    {
        private static readonly string Dir = Path.Combine("scripts", "FrontsAndRivals");
        private static readonly string Main = Path.Combine(Dir, "empire.json");

        public static void Save(EmpireState state)
        {
            Directory.CreateDirectory(Dir);
            string tmp = Main + ".tmp";
            var ser = new DataContractJsonSerializer(typeof(EmpireState));
            using (var fs = File.Create(tmp)) ser.WriteObject(fs, state);

            // Rotate backups: bak2 -> bak3, bak1 -> bak2, main -> bak1.
            for (int i = 2; i >= 1; i--)
            {
                string from = Main + ".bak" + i, to = Main + ".bak" + (i + 1);
                if (File.Exists(from)) File.Copy(from, to, true);
            }
            if (File.Exists(Main)) File.Copy(Main, Main + ".bak1", true);

            // Swap in the new file.
            if (File.Exists(Main)) File.Delete(Main);
            File.Move(tmp, Main);
        }

        public static EmpireState Load()
        {
            foreach (string path in new[] { Main, Main + ".bak1", Main + ".bak2", Main + ".bak3" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var ser = new DataContractJsonSerializer(typeof(EmpireState));
                    using (var fs = File.OpenRead(path)) return (EmpireState)ser.ReadObject(fs);
                }
                catch (SerializationException) { /* corrupt: try the next backup */ }
            }
            return new EmpireState { RngSeed = System.Environment.TickCount };
        }
    }
}
