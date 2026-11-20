// Fronts & Rivals: file log in scripts/FrontsAndRivals/FrontsAndRivals.log, rolled over at 1 MB.
using System;
using System.IO;

namespace FrontsAndRivals.Core
{
    public static class Log
    {
        private static readonly string Dir = Path.Combine("scripts", "FrontsAndRivals");
        private static readonly string FilePath = Path.Combine(Dir, "FrontsAndRivals.log");

        public static void Write(string line)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1024 * 1024)
                    File.Copy(FilePath, FilePath + ".old", true);
                using (var w = new StreamWriter(FilePath, info.Exists && info.Length <= 1024 * 1024))
                    w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + line);
            }
            catch
            {
                // Logging must never throw into the game.
            }
        }
    }
}
