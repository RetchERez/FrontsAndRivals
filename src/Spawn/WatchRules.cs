// Fronts & Rivals: how rival crews react to you, like GTA V's own gangs on their turf.
//   Calm      ignoring you: smoking, patrolling, holding the door (and anyone driving past)
//   Watching  they've seen you on foot nearby: patrols stop and they turn to keep an eye on you
//   Warning   you're close: guns up, blips on, a warning in the crew's colour and a short fuse
//   Hostile   the full fight (the fuse ran out, you walked right up, aimed at one, shot nearby or hurt one)
// Back off during a warning and they lower their guns, then go back to work.
// Calm crews only notice what they can see in front of them, and crouching (better with a good Stealth skill) or
// darkness shrinks every distance, so a careful approach gets much closer. Silenced shots are only heard up close.
// Distances are to the nearest of the front's door and its soldiers, so walking up behind a patroller counts.
// Pure logic, no game calls (unit-tested); the Streamer and Hangouts feed it every half second or so.
using System;

namespace FrontsAndRivals.Spawn
{
    public enum Watch { Calm, Watching, Warning, Hostile }

    public static class WatchRules
    {
        /// <summary>Walking right up to them ends the warning on the spot (before the sight scale).</summary>
        public const float TrespassRange = 8f;
        /// <summary>Even unseen, someone this close is heard.</summary>
        public const float HearRange = 3f;
        public const float ShotHeard = 80f, SilencedShotHeard = 20f;
        public const int BlazingHeat = 90, HotHeat = 70;

        /// <summary>They start watching at twice the warning range.</summary>
        public static float WatchRange(float warnRange) { return warnRange * 2f; }
        /// <summary>A warning holds until you're 5 m past the warning range (no flicker at the edge).</summary>
        public static float BackOffRange(float warnRange) { return warnRange + 5f; }
        /// <summary>Watching holds until you're 10 m past the watch range.</summary>
        public static float CalmRange(float warnRange) { return WatchRange(warnRange) + 10f; }

        public static bool IsNight(int hour) { return hour >= 21 || hour < 5; }

        /// <summary>How far they pick you out, as a share of the usual distances: crouched in stealth mode 0.6 (down
        /// to 0.4 with a maxed Stealth skill), three quarters at night, and both together.</summary>
        public static float SightScale(bool stealth, int stealthSkill, bool night)
        {
            float s = 1f;
            if (stealth) s *= 0.6f - 0.2f * Math.Max(0, Math.Min(100, stealthSkill)) / 100f;
            if (night) s *= 0.75f;
            return s;
        }

        /// <summary>Gunfire within 80 m is heard; a silenced shot only within 20 m.</summary>
        public static bool HeardShot(bool shooting, bool silenced, float distance)
        {
            return shooting && distance < (silenced ? SilencedShotHeard : ShotHeard);
        }

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

        /// <summary>Street crews on the pavement: a shorter fuse than a front (three fifths of a small front's).</summary>
        public static int StreetWarnMs(string crewId, int heat) { return WarnMs(crewId, 0, heat) * 3 / 5; }

        /// <summary>The next state. warnedMs = how long the current warning has run; seen = someone can see you;
        /// scale = SightScale. Before your first front (inTheGame false) they still watch and warn, but only start a
        /// fight if you hurt one of them.</summary>
        public static Watch Next(Watch now, float distance, bool onFoot, bool aimingAtOne, bool shotHeard, bool hurtOne,
                                 bool inTheGame, int warnedMs, int warnLimitMs, float warnRange, bool seen = true, float scale = 1f)
        {
            if (now == Watch.Hostile || hurtOne) return Watch.Hostile;
            if (inTheGame && shotHeard) return Watch.Hostile;
            if (aimingAtOne) return inTheGame ? Watch.Hostile : Watch.Warning;

            float warn = warnRange * scale;
            if (!onFoot)
            {
                // Driving: never escalates. A warning drops back to watching, and watching fades once you're away.
                if (now == Watch.Calm) return Watch.Calm;
                return distance <= CalmRange(warn) ? Watch.Watching : Watch.Calm;
            }
            // Calm and nobody can see you: you can creep closer (but not right up behind them).
            if (now == Watch.Calm && !seen && distance > HearRange) return Watch.Calm;

            if (inTheGame)
            {
                if (distance <= TrespassRange * scale) return Watch.Hostile;
                if (distance <= warn && warnLimitMs <= 0) return Watch.Hostile;
                if (now == Watch.Warning && warnedMs >= warnLimitMs) return Watch.Hostile;
            }
            if (distance <= warn) return Watch.Warning;
            if (now == Watch.Warning && distance <= BackOffRange(warn)) return Watch.Warning;
            if (distance <= WatchRange(warn)) return Watch.Watching;
            if (now != Watch.Calm && distance <= CalmRange(warn)) return Watch.Watching;
            return Watch.Calm;
        }

        /// <summary>"Last warning" comes halfway through the fuse.</summary>
        public static bool LastWarningDue(int warnedMs, int warnLimitMs)
        {
            return warnLimitMs > 0 && warnedMs >= warnLimitMs / 2;
        }
    }
}
