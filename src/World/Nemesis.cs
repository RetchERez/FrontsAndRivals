// Fronts & Rivals: the rival crews and the nemesis loop. Pure: no game calls, unit-tested.
// Design doc: every attack has a named leader; leaders who survive gain rep and scars and come back;
// dead lieutenants are replaced by promoted survivors; a crew with no leaders goes quiet, then rebuilds.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Nemesis
    {
        public const int GrudgeTakeFront = 12, GrudgeKillBoss = 30, GrudgeKillLieutenant = 15, GrudgeRepelled = 8, GrudgeDailyDecay = 8; // balance pass: was 25 / 10 / 5
        public const int BossLeadsAtGrudge = 70;
        public const int LieutenantBonus = 10, BossBonus = 30; // balance pass: was 25/45, which swamped small squads
        public const int MaxManpower = 20;
        public const int QuietDays = 3;
        public const double EscapeChance = 0.7, CautiousEscapeChance = 0.9;
        public const long ReturnTextDelayMs = 2L * 48 * 60000; // two in-game days of free roam

        public sealed class Event
        {
            public string Moment;   // a lines.json key
            public Dictionary<string, string> Tokens = new Dictionary<string, string>();
        }

        public static LeaderTrait ParseTrait(string t)
        {
            LeaderTrait r;
            return Enum.TryParse(t ?? "", true, out r) ? r : LeaderTrait.Cautious;
        }

        /// <summary>Adds a CrewState for every crew in content that the save doesn't have yet.</summary>
        public static void EnsureCrews(EmpireState s, ContentDb content, Random rng)
        {
            foreach (CrewDef def in content.Crews)
            {
                bool known = false;
                foreach (CrewState c in s.Crews) if (c.Id == def.Id) { known = true; break; }
                if (known) continue;
                var crew = new CrewState { Id = def.Id, Grudge = 0, Manpower = 10, QuietUntilDay = -1 };
                crew.Members.Add(NewMember(s, def, def.Boss, MemberRole.Boss, rng));
                foreach (PersonDef p in def.Lieutenants) crew.Members.Add(NewMember(s, def, p, MemberRole.Lieutenant, rng));
                s.Crews.Add(crew);
            }
        }

        public static CrewState Crew(EmpireState s, string id)
        {
            foreach (CrewState c in s.Crews) if (c.Id == id) return c;
            return null;
        }

        private static CrewMember NewMember(EmpireState s, CrewDef def, PersonDef p, string role, Random rng)
        {
            return new CrewMember
            {
                Id = s.NextMemberId++,
                First = p.First, Nick = p.Nick, Last = p.Last,
                Trait = (int)ParseTrait(p.Trait),
                Role = role,
                Rep = role == MemberRole.Boss ? 5 : 1,
                Alive = true,
                Model = def.Models[rng.Next(def.Models.Count)]
            };
        }

        public static void AddGrudge(CrewState c, int amount)
        {
            c.Grudge = Math.Max(0, Math.Min(100, c.Grudge + amount));
        }

        public static bool IsQuiet(CrewState c, int day) { return c.QuietUntilDay >= 0 && day < c.QuietUntilDay; }

        /// <summary>Who leads the next attack: the boss when grudge is 70+, otherwise a random living lieutenant.</summary>
        public static CrewMember PickLeader(CrewState c, Random rng)
        {
            CrewMember boss = c.Boss;
            List<CrewMember> lts = c.Lieutenants();
            if (boss != null && (c.Grudge >= BossLeadsAtGrudge || lts.Count == 0)) return boss;
            if (lts.Count == 0) return null;
            return lts[rng.Next(lts.Count)];
        }

        public static int LeaderBonus(CrewMember m) { return m != null && m.Role == MemberRole.Boss ? BossBonus : LieutenantBonus; }

        /// <summary>The crew won: the leader gains 2 rep.</summary>
        public static void LeaderWon(CrewMember leader, string frontName)
        {
            if (leader == null) return;
            leader.Rep += 2;
            leader.AddLog("Took the " + frontName + " from your outfit.");
        }

        /// <summary>The crew lost an auto-resolved fight: the leader escapes (70%, Cautious 90%) with +1 rep and a scar,
        /// or dies. Returns true if they died.</summary>
        public static bool LeaderLost(CrewMember leader, string frontName, Random rng, long nowMs)
        {
            if (leader == null) return false;
            double escape = leader.LeaderTrait == LeaderTrait.Cautious ? CautiousEscapeChance : EscapeChance;
            if (rng.NextDouble() < escape)
            {
                leader.Rep += 1;
                string scar = "put me down at the " + frontName;
                leader.Scars.Add(scar);
                leader.AddLog("Escaped a beating at the " + frontName + ".");
                leader.ReturnTextAt = nowMs + ReturnTextDelayMs;
                return false;
            }
            leader.Alive = false;
            leader.AddLog("Died at the " + frontName + ".");
            return true;
        }

        /// <summary>After a member dies: grudge goes up, the empty chair is filled, or the crew collapses.
        /// Returns the story events to text the player about.</summary>
        public static List<Event> OnMemberDied(EmpireState s, ContentDb content, CrewState crew, CrewMember dead, string frontName, Random rng, int day)
        {
            var events = new List<Event>();
            CrewDef def = content.Crew(crew.Id);
            string crewName = def != null ? def.Name : crew.Id;
            AddGrudge(crew, dead.Role == MemberRole.Boss ? GrudgeKillBoss : GrudgeKillLieutenant);
            events.Add(Ev("leaderDied", "leader", dead.DisplayName, "front", frontName, "crew", crewName));

            if (dead.Role == MemberRole.Boss)
            {
                CrewMember heir = null;
                foreach (CrewMember m in crew.Lieutenants()) if (heir == null || m.Rep > heir.Rep) heir = m;
                if (heir == null)
                {
                    crew.QuietUntilDay = day + QuietDays;
                    events.Add(Ev("collapsed", "crew", crewName));
                    return events;
                }
                heir.Role = MemberRole.Boss;
                heir.AddLog("Took over the " + crewName + ".");
                events.Add(Ev("newBoss", "leader", heir.DisplayName, "crew", crewName));
            }
            if (def != null && crew.Lieutenants().Count < 3)
            {
                CrewMember promoted = Promote(s, def, crew, rng, frontName);
                events.Add(Ev("promotion", "guard_new", promoted.DisplayName, "front", frontName, "crew", crewName));
            }
            return events;
        }

        /// <summary>The shared name pool (names.json), set when content loads. Null in tests that don't load it.</summary>
        public static NamesFile Names;
        public const double CrewNickChance = 0.5;
        private static readonly string[] CrewNicks = { "Two-Step", "Lockjaw", "Keys", "Static", "Rook", "Saint", "Flick", "Grit", "Dusty", "Echo",
            "Razor", "Blue", "Sixes", "Mute", "Cinder", "Patch", "Tex", "Wire", "Ledge", "Crow" };
        private static readonly string[] FallbackFirsts = { "Marcus", "Kendra", "Tyrone", "Dee", "Rico", "Jada", "Wes", "Cole", "Nina", "Boone", "Silas", "Mara" };
        private static readonly string[] FallbackLasts = { "Reyes", "Banks", "Holloway", "Pike", "Navarro", "Crane", "Mercer", "Okafor", "Lund", "Sharp" };

        private static string[] Combine(List<string> a, List<string> b)
        {
            var all = new List<string>();
            if (a != null) all.AddRange(a);
            if (b != null) all.AddRange(b);
            return all.Count > 0 ? all.ToArray() : FallbackFirsts;
        }

        public static bool NameTaken(EmpireState s, string first, string last)
        {
            foreach (CrewState c in s.Crews)
                if (c.Members != null) foreach (CrewMember x in c.Members) if (x.First == first && x.Last == last) return true;
            if (s.Guards != null) foreach (GuardState g in s.Guards) if (g.First == first && g.Last == last) return true;
            if (s.Offers != null) foreach (GuardState g in s.Offers) if (g.First == first && g.Last == last) return true;
            return false;
        }

        /// <summary>A soldier who survived the last fight becomes a lieutenant, with a generated name.</summary>
        public static CrewMember Promote(EmpireState s, CrewDef def, CrewState crew, Random rng, string frontName)
        {
            var m = new CrewMember
            {
                Id = s.NextMemberId++,
                Role = MemberRole.Lieutenant,
                Trait = rng.Next(4),
                Rep = 1,
                Alive = true,
                Model = def.Models[rng.Next(def.Models.Count)]
            };
            string[] firsts = Names != null ? Combine(Names.MaleNames, Names.FemaleNames) : FallbackFirsts;
            string[] lasts = Names != null && Names.Surnames != null && Names.Surnames.Count > 0 ? Names.Surnames.ToArray() : FallbackLasts;
            int tries = 0;
            do
            {
                m.First = firsts[rng.Next(firsts.Length)];
                m.Last = lasts[rng.Next(lasts.Length)];
            } while (NameTaken(s, m.First, m.Last) && ++tries < 12);
            // Half go by a street name. the Vinewood Elite never do.
            m.Nick = def.Id == "VS" || rng.NextDouble() >= CrewNickChance ? "" : CrewNicks[rng.Next(CrewNicks.Length)];
            m.AddLog("Walked away from the " + frontName + " and got promoted.");
            crew.Members.Add(m);
            return m;
        }

        /// <summary>After the quiet days, a collapsed crew rebuilds with a new boss and one lieutenant.</summary>
        public static Event Rebuild(EmpireState s, ContentDb content, CrewState crew, Random rng, int day)
        {
            if (crew.Boss != null || crew.QuietUntilDay < 0 || day < crew.QuietUntilDay) return null;
            CrewDef def = content.Crew(crew.Id);
            if (def == null) return null;
            CrewMember boss = Promote(s, def, crew, rng, "streets");
            boss.Role = MemberRole.Boss;
            boss.Rep = 3;
            boss.Log.Clear();
            boss.AddLog("Rebuilt the " + def.Name + ".");
            Promote(s, def, crew, rng, "streets");
            crew.QuietUntilDay = -1;
            crew.Manpower = Math.Max(crew.Manpower, 6);
            return Ev("rebuilt", "crew", def.Name, "leader", boss.DisplayName);
        }

        /// <summary>New soldiers a crew finds each midnight: 2 off the street plus 1 per front it holds.
        /// A crew with no fronts recruits hard (4 a night) to fight its way back, so the city never goes quiet.</summary>
        public static int StreetRecruits(int frontsHeld) { return frontsHeld <= 0 ? 4 : 2 + frontsHeld; }

        /// <summary>One in-game midnight for the crews: manpower regrows (StreetRecruits, max 20);
        /// grudge decays by 5 for crews that had no contact today.</summary>
        public static void Daily(EmpireState s, int day)
        {
            Heat.Nightly(s); // the streets cool off overnight
            foreach (CrewState c in s.Crews)
            {
                int held = 0;
                foreach (FrontState f in s.Fronts) if (f.Owner == c.Id) held++;
                c.Manpower = Math.Min(MaxManpower, c.Manpower + StreetRecruits(held));
                if (c.LastContactDay < day) AddGrudge(c, -GrudgeDailyDecay);
            }
        }

        public static Event Ev(string moment, params string[] kv)
        {
            var e = new Event { Moment = moment };
            for (int i = 0; i + 1 < kv.Length; i += 2) e.Tokens[kv[i]] = kv[i + 1];
            return e;
        }
    }
}
