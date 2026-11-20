// Fronts & Rivals: the two cleanup markers that need no memory patching.
//   Peds:     an FR_ relationship group. Group hashes come from the name (Jenkins one-at-a-time,
//             the game's string hash), so they are the same after a script reload.
//   Vehicles: a plate "FR" + 2-digit front number + 2-letter owner code, e.g. FR03CK.
// Pure: no game calls, unit-tested.
namespace FrontsAndRivals.Spawn
{
    public static class Markers
    {
        public const string GuardsGroup = "FR_GUARDS";
        public const string StaffGroup = "FR_STAFF";
        public const string CrownKingsGroup = "FR_CREW_CK";
        public const string IronsideGroup = "FR_CREW_IR";
        public const string VelvetGroup = "FR_CREW_VS";

        public static readonly string[] AllGroups = { GuardsGroup, StaffGroup, CrownKingsGroup, IronsideGroup, VelvetGroup };

        private static readonly int[] AllGroupHashes =
        {
            Hash(GuardsGroup), Hash(StaffGroup), Hash(CrownKingsGroup), Hash(IronsideGroup), Hash(VelvetGroup)
        };

        public static bool IsModGroup(int hash)
        {
            for (int i = 0; i < AllGroupHashes.Length; i++)
                if (AllGroupHashes[i] == hash) return true;
            return false;
        }

        /// <summary>The game's string hash (joaat), lower-cased like the game does. Same result as Game.GenerateHash.</summary>
        public static int Hash(string text)
        {
            uint h = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= 'A' && c <= 'Z') c = (char)(c + 32);
                h += c;
                h += h << 10;
                h ^= h >> 6;
            }
            h += h << 3;
            h ^= h >> 11;
            h += h << 15;
            return unchecked((int)h);
        }

        /// <summary>Owner codes used on plates.</summary>
        public const string OwnerPlayer = "YU", OwnerCrownKings = "CK", OwnerIronside = "IR", OwnerVelvet = "VS", OwnerDev = "DV";

        /// <summary>"FR" + front number (00-99) + owner code. 6 characters, inside the 8-character limit.</summary>
        public static string MakePlate(int number, string ownerCode)
        {
            if (number < 0) number = 0;
            if (number > 99) number = 99;
            return "FR" + (number < 10 ? "0" : "") + number + ownerCode;
        }

        /// <summary>True only for the exact plate shape the mod writes: FR, 2 digits, 2 capital letters.
        /// Strict on purpose, so a player's custom plate like "FRANKLIN" is never treated as ours.</summary>
        public static bool IsModPlate(string plate)
        {
            if (plate == null) return false;
            string p = plate.Trim();
            if (p.Length != 6) return false;
            if (p[0] != 'F' || p[1] != 'R') return false;
            if (p[2] < '0' || p[2] > '9' || p[3] < '0' || p[3] > '9') return false;
            if (p[4] < 'A' || p[4] > 'Z' || p[5] < 'A' || p[5] > 'Z') return false;
            return true;
        }
    }
}
