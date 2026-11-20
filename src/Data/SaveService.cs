// Fronts & Rivals: crash-safe save in scripts/FrontsAndRivals/.
//   1. Write empire.tmp and flush it to disk.
//   2. Rotate backups: bak2 -> bak3, bak1 -> bak2 (copies, so a crash here loses nothing).
//   3. File.Replace(tmp, empire.json, bak1): the old save becomes bak1 and the new one goes live in one step.
// Load tries empire.json, then bak1..bak3. A corrupt file is moved aside so it never pushes good backups out.
// Pure: no game calls, unit-tested.
using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace FrontsAndRivals.Data
{
    public sealed class SaveService
    {
        public sealed class LoadResult
        {
            public EmpireState State;
            /// <summary>"empire.json", "empire.bak1".. or "new".</summary>
            public string Source;
        }

        private readonly string _dir;
        private readonly Action<string> _log;

        public SaveService(string directory, Action<string> log)
        {
            _dir = directory;
            _log = log ?? (s => { });
        }

        public string MainPath { get { return Path.Combine(_dir, "empire.json"); } }
        public string TempPath { get { return Path.Combine(_dir, "empire.tmp"); } }
        public string BackupPath(int i) { return Path.Combine(_dir, "empire.bak" + i); }

        /// <summary>True when the save on disk is from a newer mod version. Save() then writes nothing.</summary>
        public bool Locked { get; private set; }

        public void Save(EmpireState state)
        {
            if (Locked) return;
            Directory.CreateDirectory(_dir);
            state.SaveCount++;
            string main = MainPath, tmp = TempPath;

            var ser = new DataContractJsonSerializer(typeof(EmpireState));
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ser.WriteObject(fs, state);
                fs.Flush(true); // force to disk before the swap
            }

            if (File.Exists(BackupPath(2))) File.Copy(BackupPath(2), BackupPath(3), true);
            if (File.Exists(BackupPath(1))) File.Copy(BackupPath(1), BackupPath(2), true);

            if (File.Exists(main))
            {
                File.Replace(tmp, main, BackupPath(1), true);
            }
            else
            {
                File.Move(tmp, main);
            }
        }

        public LoadResult Load()
        {
            string[] names = { "empire.json", "empire.bak1", "empire.bak2", "empire.bak3" };
            foreach (string name in names)
            {
                string path = Path.Combine(_dir, name);
                if (!File.Exists(path)) continue;
                try
                {
                    EmpireState s = Read(path);
                    if (s == null) throw new InvalidDataException("empty file");
                    if (!Migrations.Upgrade(s))
                    {
                        // Written by a newer Fronts & Rivals. Never overwrite it: run with a blank empire, save nothing.
                        _log("Save " + name + " is schema " + s.SchemaVersion + ", newer than this mod (" +
                             EmpireState.CurrentSchema + "). Saving is locked so it is never overwritten.");
                        Locked = true;
                        return new LoadResult { State = NewEmpire(), Source = "locked" };
                    }
                    if (name != "empire.json") _log("Loaded the save from backup " + name);
                    return new LoadResult { State = s, Source = name };
                }
                catch (Exception ex)
                {
                    _log("Save " + name + " could not be read (" + ex.GetType().Name + ": " + ex.Message + ")");
                    if (name == "empire.json") MoveAside(path);
                }
            }

            return new LoadResult { State = NewEmpire(), Source = "new" };
        }

        public static EmpireState NewEmpire()
        {
            return new EmpireState
            {
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                RngState = Core.Rng.NewState()
            };
        }

        private static EmpireState Read(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(EmpireState));
            using (var fs = File.OpenRead(path)) return (EmpireState)ser.ReadObject(fs);
        }

        private void MoveAside(string path)
        {
            try
            {
                string dest = Path.Combine(_dir, "empire.corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                File.Move(path, dest);
                _log("Moved the unreadable save to " + Path.GetFileName(dest));
            }
            catch (Exception ex)
            {
                _log("Could not move the unreadable save aside: " + ex.Message);
            }
        }
    }
}
