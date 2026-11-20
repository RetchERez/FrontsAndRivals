// Fronts & Rivals: who works at your fronts. Pure rules, no game calls, unit-tested.
// Staff make a business you own look open: a cashier on the phone, a janitor sweeping, a chef on a smoke break.
// They only exist at the one front you're standing near, and they always give way to guards and fights.
using FrontsAndRivals.Rules;

namespace FrontsAndRivals.Spawn
{
    public static class WorkerRules
    {
        public const float InRange = 120f;   // staff appear when you're this close to the door
        public const float OutRange = 170f;  // and leave past this
        public const int Headroom = 12;      // free ped slots kept for guards, crews and attackers

        /// <summary>Staff only when at least Headroom slots stay free for everything else.</summary>
        public static bool Fits(int otherPeds, int workersNow, int maxPeds)
        {
            return otherPeds + workersNow + 1 + Headroom <= maxPeds;
        }

        /// <summary>Must staff leave to free slots? (Something else is using the budget.)</summary>
        public static bool MustYield(int otherPeds, int workersNow, int maxPeds)
        {
            return workersNow > 0 && otherPeds + workersNow + Headroom > maxPeds;
        }

        /// <summary>Busy by day: one at a small front, two at a bigger one. One at night.</summary>
        public static int Count(int tier, int hour)
        {
            if (DefenseRating.IsNight(hour)) return 1;
            return tier >= 1 ? 2 : 1;
        }

        /// <summary>The kind of business, from the front's id ("bar_mirrorpark" is a bar).</summary>
        public static string Kind(string frontId)
        {
            if (string.IsNullOrEmpty(frontId)) return "";
            int cut = frontId.IndexOf('_');
            return cut > 0 ? frontId.Substring(0, cut) : frontId;
        }

        /// <summary>Ped models by kind of business, women and men mixed (names checked against the game's ped list).</summary>
        public static string[] Models(string frontId)
        {
            switch (Kind(frontId))
            {
                case "carwash": return new[] { "s_m_m_autoshop_01", "s_f_y_shop_low", "s_m_y_valet_01" };
                case "autoshop": return new[] { "s_m_m_autoshop_01", "s_f_y_shop_mid", "s_m_m_autoshop_02" };
                case "cafe": case "diner": case "restaurant": case "noodles": return new[] { "s_m_y_waiter_01", "s_f_y_shop_mid", "s_m_y_chef_01" };
                case "jewelry": return new[] { "s_f_m_shop_high", "s_m_y_shop_mask" };
                case "shop": case "liquor": case "market": case "mall": case "plaza": return new[] { "mp_m_shopkeep_01", "s_f_y_shop_low", "s_f_y_shop_mid" };
                case "motel": return new[] { "s_f_m_maid_01", "s_m_m_janitor" };
                case "bar": case "lounge": return new[] { "s_m_y_barman_01", "s_f_y_bartender_01" };
                case "club": return new[] { "s_m_m_bouncer_01", "s_f_y_bartender_01" };
                case "depot": case "factory": return new[] { "s_m_y_factory_01", "a_f_m_eastsa_01", "s_m_m_dockwork_01" };
                default: return new[] { "s_m_m_janitor", "s_f_y_shop_low" };
            }
        }

        /// <summary>Idle scenarios (names checked against the game's scenario list). Night is a smoke or a phone call.</summary>
        public static string Scenario(string frontId, int index, int hour)
        {
            if (DefenseRating.IsNight(hour)) return index % 2 == 0 ? "WORLD_HUMAN_SMOKING" : "WORLD_HUMAN_STAND_MOBILE";
            string[] list;
            switch (Kind(frontId))
            {
                case "carwash": list = new[] { "WORLD_HUMAN_CAR_PARK_ATTENDANT", "WORLD_HUMAN_JANITOR" }; break;
                case "autoshop": case "depot": case "factory": list = new[] { "WORLD_HUMAN_CLIPBOARD", "WORLD_HUMAN_SMOKING" }; break;
                case "cafe": case "diner": case "restaurant": case "noodles": list = new[] { "WORLD_HUMAN_AA_COFFEE", "WORLD_HUMAN_SMOKING" }; break;
                case "motel": list = new[] { "WORLD_HUMAN_MAID_CLEAN", "WORLD_HUMAN_JANITOR" }; break;
                case "bar": case "lounge": case "club": list = new[] { "WORLD_HUMAN_SMOKING", "WORLD_HUMAN_STAND_IMPATIENT" }; break;
                default: list = new[] { "WORLD_HUMAN_STAND_MOBILE", "WORLD_HUMAN_JANITOR" }; break;
            }
            return list[index % list.Length];
        }

        /// <summary>Where staff stand: 2.5-4 m out from the door, to either side, so they never block it or a guard post.</summary>
        public static float[] Offset(int index)
        {
            // angle (degrees from straight out of the door), distance
            switch (index % 3)
            {
                case 0: return new[] { -55f, 3.0f };
                case 1: return new[] { 55f, 3.5f };
                default: return new[] { 0f, 4.0f };
            }
        }
    }
}
