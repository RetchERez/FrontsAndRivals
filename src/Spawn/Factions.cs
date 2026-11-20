// Fronts & Rivals: relationship groups. All prefixed FR_ so they never clash with other mods.
// Guards respect you and the police and hate the rival crews. Staff are neutral to everyone.
// Every FR_ group is neutral to the game's ambient gangs, so Grove Street locals don't start fights.
// Game group names verified against SHVDN's RelationshipGroupHash source (main branch).
using GTA;
using GTA.Native;
using FrontsAndRivals.Core;

namespace FrontsAndRivals.Spawn
{
    public static class Factions
    {
        // SET_RELATIONSHIP_BETWEEN_GROUPS values (NativeDB): 0 Companion, 1 Respect, 2 Like, 3 Neutral, 4 Dislike, 5 Hate.
        private const int Respect = 1, Like = 2, Neutral = 3, Hate = 5;

        public static RelationshipGroup Guards = Markers.Hash(Markers.GuardsGroup);
        public static RelationshipGroup Staff = Markers.Hash(Markers.StaffGroup);
        public static RelationshipGroup CrownKings = Markers.Hash(Markers.CrownKingsGroup);
        public static RelationshipGroup Ironside = Markers.Hash(Markers.IronsideGroup);
        public static RelationshipGroup Velvet = Markers.Hash(Markers.VelvetGroup);

        private static readonly string[] LawGroups = { "COP", "ARMY", "SECURITY_GUARD", "PRIVATE_SECURITY" };
        private static readonly string[] AmbientGangs =
        {
            "AMBIENT_GANG_LOST", "AMBIENT_GANG_MEXICAN", "AMBIENT_GANG_FAMILY", "AMBIENT_GANG_BALLAS",
            "AMBIENT_GANG_MARABUNTE", "AMBIENT_GANG_CULT", "AMBIENT_GANG_SALVA", "AMBIENT_GANG_WEICHENG",
            "AMBIENT_GANG_HILLBILLY"
        };

        public static void Setup()
        {
            // Create the groups. Their hashes come from the name, so a reload finds the same groups.
            foreach (string name in Markers.AllGroups)
            {
                var outHash = new OutputArgument();
                bool created = Function.Call<bool>(Hash.ADD_RELATIONSHIP_GROUP, name, outHash);
                int got = outHash.GetResult<int>();
                int expected = Markers.Hash(name);
                if (created && got != expected)
                    Log.Write("WARNING: group " + name + " hash " + got + " != expected " + expected);
                Log.Write("Group " + name + (created ? " created" : " already existed") + ", hash " + expected);
            }

            int player = Markers.Hash("PLAYER");
            int guards = Guards.Hash, staff = Staff.Hash;
            int[] crews = { CrownKings.Hash, Ironside.Hash, Velvet.Hash };

            Both(Respect, guards, player);
            Both(Like, guards, staff);
            foreach (string law in LawGroups) Both(Respect, guards, Markers.Hash(law));

            Both(Neutral, staff, player);
            foreach (string law in LawGroups) Both(Neutral, staff, Markers.Hash(law));

            foreach (int crew in crews)
            {
                Both(Hate, crew, guards);
                Both(Hate, crew, player);
                Both(Neutral, crew, staff);
            }

            foreach (string name in Markers.AllGroups)
                foreach (string gang in AmbientGangs)
                    Both(Neutral, Markers.Hash(name), Markers.Hash(gang));
        }

        private static void Both(int relationship, int a, int b)
        {
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, relationship, a, b);
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, relationship, b, a);
        }

        public static bool IsOwnGroup(int hash)
        {
            return Markers.IsModGroup(hash);
        }
    }
}
