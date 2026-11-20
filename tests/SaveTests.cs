using System;
using System.IO;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;

static class SaveTests
{
    public static void Run()
    {
        T.Section("SaveService (crash-safe save)");
        string dir = Path.Combine(Path.GetTempPath(), "fr_save_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            Log.Init(dir);
            var log = new System.Collections.Generic.List<string>();
            var saves = new SaveService(dir, log.Add);

            var fresh = saves.Load();
            T.Equal("No file: a new empire", fresh.Source, "new");
            T.True("New empire gets an RNG state", fresh.State.RngState != 0);
            T.True("New empire has lists ready", fresh.State.OwnedFronts != null && fresh.State.Grudge != null);

            // Round trip
            var s = fresh.State;
            s.OwnedFronts.Add("carwash_davis");
            s.Grudge["CK"] = 40;
            s.RngState = 0xDEADBEEFCAFEUL;
            s.ActiveMs = 123456;
            saves.Save(s);
            var back = saves.Load();
            T.Equal("Round trip: loads from empire.json", back.Source, "empire.json");
            T.Equal("Round trip: owned front", back.State.OwnedFronts.Count == 1 ? back.State.OwnedFronts[0] : "?", "carwash_davis");
            T.Check("Round trip: grudge", back.State.Grudge["CK"], 40, 0);
            T.True("Round trip: 64-bit RNG state exact", back.State.RngState == 0xDEADBEEFCAFEUL);
            T.Check("Round trip: active time", back.State.ActiveMs, 123456, 0);
            T.True("Uses schemaVersion field name", File.ReadAllText(saves.MainPath).Contains("\"schemaVersion\":1"));

            // Backups rotate: after 4 more saves we have bak1..bak3 and no temp file.
            for (int i = 0; i < 4; i++) saves.Save(s);
            T.True("bak1, bak2, bak3 exist", File.Exists(saves.BackupPath(1)) && File.Exists(saves.BackupPath(2)) && File.Exists(saves.BackupPath(3)));
            T.True("No empire.tmp left behind", !File.Exists(saves.TempPath));
            T.True("Save is under 50 KB", new FileInfo(saves.MainPath).Length < 50 * 1024);
            int mainCount = saves.Load().State.SaveCount;
            T.True("bak1 is the previous save", ReadCount(saves.BackupPath(1)) == mainCount - 1);
            T.True("bak3 is three saves back", ReadCount(saves.BackupPath(3)) == mainCount - 3);

            // Crash mid-write: a half-written temp file is left and empire.json is truncated garbage.
            File.WriteAllText(saves.TempPath, "{\"schemaVer");
            File.WriteAllText(saves.MainPath, "{\"schemaVersion\":1,\"own");
            var recovered = saves.Load();
            T.Equal("Corrupt main: falls back to bak1", recovered.Source, "empire.bak1");
            T.Check("Recovered the previous save", recovered.State.SaveCount, mainCount - 1, 0);
            T.True("Corrupt main moved aside, not left to rotate into the backups",
                !File.Exists(saves.MainPath) && Directory.GetFiles(dir, "empire.corrupt-*.json").Length == 1);
            saves.Save(recovered.State);
            T.Equal("Next save is healthy again", saves.Load().Source, "empire.json");

            // A file missing fields (hand-edited) gets defaults instead of nulls.
            File.WriteAllText(saves.MainPath, "{\"schemaVersion\":1}");
            var partial = saves.Load().State;
            T.True("Missing lists become empty, not null", partial.OwnedFronts != null && partial.Grudge != null);

            // A save from a newer mod version is never overwritten silently: it's skipped, backups used.
            File.WriteAllText(saves.MainPath, "{\"schemaVersion\":99}");
            var newer = saves.Load();
            T.Equal("Newer-schema file is not loaded", newer.Source, "locked");
            saves.Save(newer.State);
            T.Equal("...and is never overwritten", File.ReadAllText(saves.MainPath), "{\"schemaVersion\":99}");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    static int ReadCount(string path)
    {
        var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(EmpireState));
        using (var fs = File.OpenRead(path)) return ((EmpireState)ser.ReadObject(fs)).SaveCount;
    }
}
