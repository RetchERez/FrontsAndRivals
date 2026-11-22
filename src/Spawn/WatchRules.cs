// Fronts & Rivals: how a rival front's soldiers react to you, like GTA V's own gangs on their turf.
//   Calm      ignoring you: smoking, patrolling, holding the door (and anyone driving past)
//   Watching  you're on foot nearby: patrols stop and they turn to keep an eye on you
//   Warning   you're close: guns up, blips on, a warning in the crew's colour and a short fuse
//   Hostile   the full fight (the fuse ran out, you walked right up, aimed at one, shot nearby or hurt one)
// Back off during a warning and they lower their guns, then go back to work.
// Pure logic, no game calls (unit-tested); the Streamer feeds it every half second.
using System;

namespace FrontsAndRivals.Spawn
{
    public enum Watch { Calm, Watching, Warning, Hostile }

    public static class WatchRules
    {
        /// <summary>Walking right up to them ends the warning on the spot.</summary>
        public const float TrespassRange = 8f;
        public const int BlazingHeat = 90, HotHeat = 70;

        /// <summary>They start watching at twice the warning range.</summary>
        public static float WatchRange(float warnRange) { return warnRange * 2f; }
        /// <summary>A warning holds until you're 5 m past the warning range (no flicker at the edge).</summary>
        public static float BackOffRange(float warnRange) { return warnRange + 5f; }
        /// <summary>Watching holds until you're 10 m past the watch range.</summary>
        public static float CalmRange(float warnRange) { return WatchRange(warnRange) + 10f; }

        /// <summary>How long the warning lasts before they open fire, in ms. 0 = no warning at all.
        /// Small fronts give a full warning and bigger ones a shorter one; hot streets cut it short and blazing streets
        /// skip it. Hellfire MC have a short fuse, the Vinewood Elite talk longest.</summary>
        public static int WarnMs(string crewId, int tier, int heat)
        {
            if (heat >= BlazingHeat) return 0;
            int ms = tier <= 0 ? 8000 : 5000;
            if (heat >= HotHeat) ms = Math.Min(ms, 3000);
            if (crewId == "IR") ms = ms * 3 / 4;
            else if (crewId == "VS") ms = ms * 5 / 4;
            return ms;
        }

        /// <summary>The next state. warnedMs = how long the current warning has run. Before your first front
        /// (inTheGame false) they still watch and warn, but only start a fight if you hurt one of them.</summary>
        public static Watch Next(Watch now, float distance, bool onFoot, bool aimingAtOne, bool shootingNear, bool hurtOne,
                                 bool inTheGame, int warnedMs, int warnLimitMs, float warnRange)
        {
            if (now == Watch.Hostile || hurtOne) return Watch.Hostile;
            if (inTheGame && shootingNear) return Watch.Hostile;
            if (aimingAtOne) return inTheGame ? Watch.Hostile : Watch.Warning;

            if (!onFoot)
            {
                // Driving: never escalates. A warning drops back to watching, and watching fades once you're away.
                if (now == Watch.Calm) return Watch.Calm;
                return distance <= CalmRange(warnRange) ? Watch.Watching : Watch.Calm;
            }

            if (inTheGame)
            {
                if (distance <= TrespassRange) return Watch.Hostile;
                if (distance <= warnRange && warnLimitMs <= 0) return Watch.Hostile;
                if (now == Watch.Warning && warnedMs >= warnLimitMs) return Watch.Hostile;
            }
            if (distance <= warnRange) return Watch.Warning;
            if (now == Watch.Warning && distance <= BackOffRange(warnRange)) return Watch.Warning;
            if (distance <= WatchRange(warnRange)) return Watch.Watching;
            if (now != Watch.Calm && distance <= CalmRange(warnRange)) return Watch.Watching;
            return Watch.Calm;
        }

        /// <summary>"Last warning" comes halfway through the fuse.</summary>
        public static bool LastWarningDue(int warnedMs, int warnLimitMs)
        {
            return warnLimitMs > 0 && warnedMs >= warnLimitMs / 2;
        }
    }
}
