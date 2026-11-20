// Fronts & Rivals: content as data (fronts.json, names.json, models.json in scripts/FrontsAndRivals/).
// Balance and text live here so they can change without recompiling. Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FrontsAndRivals.Data
{
    [DataContract]
    public sealed class Spot
    {
        [DataMember(Name = "x", Order = 0)] public float X;
        [DataMember(Name = "y", Order = 1)] public float Y;
        [DataMember(Name = "z", Order = 2)] public float Z;
        [DataMember(Name = "h", Order = 3)] public float H;

        /// <summary>Unscouted spots are all zeros.</summary>
        public bool IsSet { get { return X != 0f || Y != 0f || Z != 0f; } }

        public static Spot At(float x, float y, float z, float h) { return new Spot { X = x, Y = y, Z = z, H = h }; }

        public float DistanceTo(float x, float y, float z)
        {
            float dx = X - x, dy = Y - y, dz = Z - z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    [DataContract]
    public sealed class PostDef
    {
        [DataMember(Name = "x", Order = 0)] public float X;
        [DataMember(Name = "y", Order = 1)] public float Y;
        [DataMember(Name = "z", Order = 2)] public float Z;
        [DataMember(Name = "h", Order = 3)] public float H;
        [DataMember(Name = "scenario", Order = 4)] public string Scenario;
    }

    [DataContract]
    public sealed class FrontDef
    {
        public const int MaxPosts = 4;

        [DataMember(Name = "id", Order = 0)] public string Id;
        [DataMember(Name = "name", Order = 1)] public string Name;
        [DataMember(Name = "area", Order = 2)] public string Area;
        [DataMember(Name = "number", Order = 3)] public int Number;
        [DataMember(Name = "tier", Order = 4)] public int Tier;
        [DataMember(Name = "income", Order = 5)] public int Income;
        [DataMember(Name = "startOwner", Order = 6)] public string StartOwner;
        [DataMember(Name = "door", Order = 7)] public Spot Door;
        [DataMember(Name = "ownerSpot", Order = 8)] public Spot OwnerSpot;
        [DataMember(Name = "posts", Order = 9)] public List<PostDef> Posts;
        [DataMember(Name = "convoySpawns", Order = 10)] public List<Spot> ConvoySpawns;
        /// <summary>Short label for tight spots in menus, e.g. "Car Wash".</summary>
        [DataMember(Name = "short", Order = 11)] public string Short;

        public string ShortName { get { return string.IsNullOrEmpty(Short) ? Name : Short; } }

        /// <summary>A front can be streamed once it has a door and at least one post.</summary>
        public bool IsScouted { get { return Door != null && Door.IsSet && Posts != null && Posts.Count > 0; } }

        public void Normalize()
        {
            if (Door == null) Door = new Spot();
            if (OwnerSpot == null) OwnerSpot = new Spot();
            if (Posts == null) Posts = new List<PostDef>();
            if (ConvoySpawns == null) ConvoySpawns = new List<Spot>();
            if (Posts.Count > MaxPosts) Posts.RemoveRange(MaxPosts, Posts.Count - MaxPosts);
            if (string.IsNullOrEmpty(StartOwner)) StartOwner = Owners.ForSale;
            if (string.IsNullOrEmpty(Name)) Name = Id;
        }
    }

    /// <summary>Owner codes used in fronts.json and the save.</summary>
    public static class Owners
    {
        public const string Player = "player";
        public const string ForSale = "forsale";
        public const string CrownKings = "CK";
        public const string Ironside = "IR";
        public const string Velvet = "VS";

        public static bool IsValid(string o)
        {
            return o == Player || o == ForSale || o == CrownKings || o == Ironside || o == Velvet;
        }
    }

    [DataContract]
    public sealed class NamesFile
    {
        [DataMember(Name = "maleNames")] public List<string> MaleNames;
        [DataMember(Name = "femaleNames")] public List<string> FemaleNames;
        [DataMember(Name = "nicknames")] public List<string> Nicknames;
        [DataMember(Name = "surnames")] public List<string> Surnames;
    }

    [DataContract]
    public sealed class ModelsFile
    {
        [DataMember(Name = "guardsMale")] public List<string> GuardsMale;
        [DataMember(Name = "guardsFemale")] public List<string> GuardsFemale;
    }

    public sealed class ContentDb
    {
        public List<FrontDef> Fronts = new List<FrontDef>();
        public NamesFile Names;
        public ModelsFile Models;

        private readonly string _dir;
        private readonly Action<string> _log;

        public ContentDb(string directory, Action<string> log)
        {
            _dir = directory;
            _log = log ?? (s => { });
        }

        public string FrontsPath { get { return Path.Combine(_dir, "fronts.json"); } }

        public FrontDef Front(string id)
        {
            for (int i = 0; i < Fronts.Count; i++) if (Fronts[i].Id == id) return Fronts[i];
            return null;
        }

        /// <summary>Loads all three files. Anything missing or broken falls back to built-in defaults and logs why.</summary>
        public void Load()
        {
            List<FrontDef> fronts = Read<List<FrontDef>>("fronts.json");
            if (fronts == null || fronts.Count == 0)
            {
                _log("Content: using built-in front list (no usable fronts.json)");
                fronts = DefaultFronts();
            }
            var seen = new HashSet<string>();
            Fronts = new List<FrontDef>();
            foreach (FrontDef f in fronts)
            {
                if (f == null || string.IsNullOrEmpty(f.Id)) { _log("Content: skipped a front with no id"); continue; }
                if (!seen.Add(f.Id)) { _log("Content: skipped duplicate front id " + f.Id); continue; }
                f.Normalize();
                if (!Owners.IsValid(f.StartOwner)) { _log("Content: " + f.Id + " has unknown startOwner " + f.StartOwner + ", using forsale"); f.StartOwner = Owners.ForSale; }
                Fronts.Add(f);
            }

            Names = Read<NamesFile>("names.json");
            if (Names == null || Empty(Names.MaleNames) || Empty(Names.FemaleNames) || Empty(Names.Nicknames) || Empty(Names.Surnames))
            {
                _log("Content: names.json missing or incomplete, using built-in names");
                Names = DefaultNames();
            }

            Models = Read<ModelsFile>("models.json");
            if (Models == null || Empty(Models.GuardsMale))
            {
                _log("Content: models.json missing or incomplete, using built-in models");
                Models = DefaultModels();
            }
            if (Models.GuardsFemale == null) Models.GuardsFemale = new List<string>();

            int scouted = 0;
            foreach (FrontDef f in Fronts) if (f.IsScouted) scouted++;
            _log("Content: " + Fronts.Count + " fronts (" + scouted + " scouted), "
                 + (Names.MaleNames.Count + Names.FemaleNames.Count) + " first names, "
                 + (Models.GuardsMale.Count + Models.GuardsFemale.Count) + " guard models");
        }

        /// <summary>Writes fronts.json (used by the in-game scouting tools). Same temp-and-swap safety as the save.</summary>
        public void SaveFronts()
        {
            Directory.CreateDirectory(_dir);
            string tmp = FrontsPath + ".tmp";
            File.WriteAllText(tmp, FrontsJson.Write(Fronts), new UTF8Encoding(false));
            if (File.Exists(FrontsPath)) File.Replace(tmp, FrontsPath, FrontsPath + ".bak", true);
            else File.Move(tmp, FrontsPath);
        }

        private T Read<T>(string name) where T : class
        {
            string path = Path.Combine(_dir, name);
            if (!File.Exists(path)) return null;
            try
            {
                // Notepad and PowerShell can add a UTF-8 byte-order mark, which the JSON reader rejects. Skip it.
                byte[] bytes = File.ReadAllBytes(path);
                int start = (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ? 3 : 0;
                var ser = new DataContractJsonSerializer(typeof(T));
                using (var ms = new MemoryStream(bytes, start, bytes.Length - start)) return (T)ser.ReadObject(ms);
            }
            catch (Exception ex)
            {
                _log("Content: " + name + " could not be read (" + ex.GetType().Name + ": " + ex.Message + ")");
                return null;
            }
        }

        private static bool Empty(List<string> l) { return l == null || l.Count == 0; }

        public static List<FrontDef> DefaultFronts()
        {
            // Real GTA V buildings; doors from published coordinates (see docs/locations_found.md).
            return new List<FrontDef>
            {
                Def("carwash_handson", "Hands On Car Wash", "Strawberry", 1, 0, 4000, Owners.ForSale, 25.20f, -1391.98f, 28.91f, 0f),
                Def("liquor_brewersdrop", "Brewer's Drop Liquor", "Strawberry", 2, 0, 5000, Owners.Velvet, 98.90f, -1309.50f, 29.30f, 122f),
                Def("jewelry_discountjewels", "Discount Jewels", "Davis", 3, 1, 6000, Owners.CrownKings, 130.90f, -1771.80f, 29.70f, 320f),
                Def("bar_mirrorpark", "Mirror Park Tavern", "Mirror Park", 4, 1, 7000, Owners.Velvet, 1217.94f, -416.80f, 67.78f, 78f),
                Def("autoshop_atomic", "Atomic Service Center", "Rancho", 5, 2, 9000, Owners.CrownKings, 487.00f, -1878.30f, 26.20f, 272f),
                Def("depot_fridgit", "Fridgit Cold Storage", "La Mesa", 6, 2, 12000, Owners.Ironside, 867.80f, -1639.70f, 30.20f, 103f)
            };
        }

        private static FrontDef Def(string id, string name, string area, int number, int tier, int income, string owner,
                                    float x, float y, float z, float h)
        {
            var f = new FrontDef { Id = id, Name = name, Short = ShortFor(id), Area = area, Number = number, Tier = tier, Income = income, StartOwner = owner, Door = Spot.At(x, y, z, h) };
            f.Normalize();
            return f;
        }

        private static string ShortFor(string id)
        {
            switch (id)
            {
                case "carwash_handson": return "Car Wash";
                case "liquor_brewersdrop": return "Liquor Store";
                case "jewelry_discountjewels": return "Jewelers";
                case "bar_mirrorpark": return "Tavern";
                case "autoshop_atomic": return "Auto Shop";
                case "depot_fridgit": return "Cold Storage";
                default: return null;
            }
        }

        public static NamesFile DefaultNames()
        {
            return new NamesFile
            {
                MaleNames = new List<string> { "Mateo", "Andre", "Curtis", "Rafael", "Darius", "Omar" },
                FemaleNames = new List<string> { "Nadia", "Keisha", "Marisol" },
                Nicknames = new List<string> { "Smoke", "Brick", "Slim", "Dice" },
                Surnames = new List<string> { "Alvarez", "Brooks", "Carter", "Delgado" }
            };
        }

        public static ModelsFile DefaultModels()
        {
            return new ModelsFile
            {
                GuardsMale = new List<string> { "s_m_m_security_01", "a_m_y_stbla_02", "a_m_m_eastsa_02" },
                GuardsFemale = new List<string> { "a_f_y_eastsa_03" }
            };
        }
    }

    /// <summary>Writes fronts.json in the same readable layout as the design doc, so hand edits and F9 snippets match.</summary>
    public static class FrontsJson
    {
        public static string Write(List<FrontDef> fronts)
        {
            var sb = new StringBuilder();
            sb.Append("[\r\n");
            for (int i = 0; i < fronts.Count; i++)
            {
                FrontDef f = fronts[i];
                sb.Append("  {\r\n");
                Field(sb, "id", Str(f.Id)); Field(sb, "name", Str(f.Name)); Field(sb, "short", Str(f.ShortName)); Field(sb, "area", Str(f.Area));
                Field(sb, "number", Int(f.Number)); Field(sb, "tier", Int(f.Tier)); Field(sb, "income", Int(f.Income));
                Field(sb, "startOwner", Str(f.StartOwner));
                Field(sb, "door", SpotJson(f.Door)); Field(sb, "ownerSpot", SpotJson(f.OwnerSpot));
                sb.Append("    \"posts\": [");
                for (int p = 0; p < f.Posts.Count; p++)
                {
                    PostDef post = f.Posts[p];
                    sb.Append(p == 0 ? "\r\n" : ",\r\n");
                    sb.Append("      { \"x\": ").Append(F2(post.X)).Append(", \"y\": ").Append(F2(post.Y))
                      .Append(", \"z\": ").Append(F2(post.Z)).Append(", \"h\": ").Append(F1(post.H))
                      .Append(", \"scenario\": ").Append(Str(post.Scenario)).Append(" }");
                }
                sb.Append(f.Posts.Count > 0 ? "\r\n    ],\r\n" : "],\r\n");
                sb.Append("    \"convoySpawns\": [");
                for (int c = 0; c < f.ConvoySpawns.Count; c++)
                {
                    sb.Append(c == 0 ? "\r\n      " : ",\r\n      ").Append(SpotJson(f.ConvoySpawns[c]));
                }
                sb.Append(f.ConvoySpawns.Count > 0 ? "\r\n    ]\r\n" : "]\r\n");
                sb.Append(i < fronts.Count - 1 ? "  },\r\n" : "  }\r\n");
            }
            sb.Append("]\r\n");
            return sb.ToString();
        }

        private static void Field(StringBuilder sb, string name, string value)
        {
            sb.Append("    \"").Append(name).Append("\": ").Append(value).Append(",\r\n");
        }

        public static string SpotJson(Spot s)
        {
            if (s == null) s = new Spot();
            return "{ \"x\": " + F2(s.X) + ", \"y\": " + F2(s.Y) + ", \"z\": " + F2(s.Z) + ", \"h\": " + F1(s.H) + " }";
        }

        private static string F2(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string F1(float v) { return v.ToString("0.0", CultureInfo.InvariantCulture); }
        private static string Int(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        private static string Str(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
