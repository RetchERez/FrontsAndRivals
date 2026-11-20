// Fronts & Rivals: file log in scripts/FrontsAndRivals/FrontsAndRivals.log, rolled over at 1 MB.
// Pure: no game calls, so the unit tests can use it too.
using System;
using System.IO;

namespace FrontsAndRivals.Core
{
    public static class Log
    {
        public const long MaxBytes = 1024 * 1024;

        private static string _filePath;

        /// <summary>Call once at startup with the mod's data folder. Until then, Write does nothing.</summary>
        public static void Init(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                _filePath = Path.Combine(directory, "FrontsAndRivals.log");
            }
            catch
            {
                _filePath = null;
            }
        }

        public static string FilePath { get { return _filePath; } }

        public static void Write(string line)
        {
            string path = _filePath;
            if (path == null) return;
            try
            {
                var info = new FileInfo(path);
                bool append = true;
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Copy(path, path + ".old", true);
                    append = false; // start a fresh file
                }
                using (var w = new StreamWriter(path, append))
                    w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + line);
            }
            catch
            {
                // Logging must never throw into the game.
            }
        }

        public static void Error(string where, Exception ex)
        {
            Write("ERROR in " + where + ": " + ex);
        }
    }
}
