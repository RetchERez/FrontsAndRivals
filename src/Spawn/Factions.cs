// Fronts & Rivals: relationship groups. Guards respect you and the police, hate rival crews.
using GTA;
using GTA.Native;

namespace FrontsAndRivals.Spawn
{
    public static class Factions
    {
        // Relationship values from NativeDB: 0 Companion, 1 Respect, 2 Like, 3 Neutral, 4 Dislike, 5 Hate.
        private const int Respect = 1, Neutral = 3, Hate = 5;

        public static RelationshipGroup Guards;
        public static RelationshipGroup Staff;
        public static RelationshipGroup CrownKings, Ironside, Velvet;

        public static void Setup()
        {
            Guards = World.AddRelationshipGroup("FR_GUARDS");
            Staff = World.AddRelationshipGroup("FR_STAFF");
            CrownKings = World.AddRelationshipGroup("FR_CREW_CK");
            Ironside = World.AddRelationshipGroup("FR_CREW_IR");
            Velvet = World.AddRelationshipGroup("FR_CREW_VS");

            RelationshipGroup player = Game.Player.Character.RelationshipGroup;

            // Guards and the player respect each other.
            Guards.SetRelationshipBetweenGroups(player, Relationship.Respect, true);

            // Guards never fight the police or security.
            foreach (string name in new[] { "COP", "ARMY", "SECURITY_GUARD", "PRIVATE_SECURITY" })
            {
                int other = Game.GenerateHash(name);
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, Respect, Guards.Hash, other);
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, Respect, other, Guards.Hash);
            }

            // Staff are neutral to everyone they might meet.
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, Neutral, Staff.Hash, player.Hash);
            Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, Neutral, player.Hash, Staff.Hash);

            // Every crew hates you and your guards, both ways.
            foreach (RelationshipGroup crew in new[] { CrownKings, Ironside, Velvet })
            {
                crew.SetRelationshipBetweenGroups(Guards, Relationship.Hate, true);
                crew.SetRelationshipBetweenGroups(player, Relationship.Hate, true);
            }
        }

        public static bool IsOwnGroup(int hash)
        {
            return hash == Guards.Hash || hash == Staff.Hash || hash == CrownKings.Hash
                || hash == Ironside.Hash || hash == Velvet.Hash;
        }
    }
}
