// Fronts & Rivals: the city moves without you. Each in-game midnight a crew may grab a front that's for sale
// or take one from another crew, mostly near its home turf. Your fronts are only ever taken through attacks,
// and the starter front is never grabbed, so a new empire can always buy in. Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public static class Turf
    {
        public const double MoveChance = 0.25;     // per crew per night
        public const int MinManpowerToMove = 8;
        public const double GrabNeutralChance = 0.8;
        public const double MaxReach = 2500;        // metres from the crew's home

        public sealed class Move
        {
            public string Crew;
            public string Front;
            public string FromCrew;   // null when the front was for sale
            public bool Success;
        }

        /// <summary>One night of crews pushing for territory. Returns every move, successful or not.</summary>
        public static List<Move> Nightly(EmpireState s, ContentDb content, Random rng)
        {
            var moves = new List<Move>();
            foreach (CrewState crew in s.Crews)
            {
                if (Nemesis.IsQuiet(crew, s.Day) || crew.Manpower < MinManpowerToMove) continue;
                if (rng.NextDouble() >= MoveChance) continue;
                CrewDef def = content.Crew(crew.Id);
                if (def == null) continue;

                FrontState target = PickTarget(s, content, crew, def, rng);
                if (target == null) continue;
                var move = new Move { Crew = crew.Id, Front = target.Id, FromCrew = target.Owner == Owners.ForSale ? null : target.Owner };
                if (move.FromCrew == null)
                {
                    move.Success = rng.NextDouble() < GrabNeutralChance;
                    if (move.Success) crew.Manpower -= 1;
                }
                else
                {
                    CrewState defender = Nemesis.Crew(s, move.FromCrew);
                    int dm = defender != null ? defender.Manpower : 0;
                    double chance = crew.Manpower / (double)Math.Max(1, crew.Manpower + dm);
                    move.Success = rng.NextDouble() < chance;
                    crew.Manpower = Math.Max(0, crew.Manpower - (move.Success ? 1 : 2));
                    if (defender != null) defender.Manpower = Math.Max(0, defender.Manpower - (move.Success ? 2 : 1));
                }
                if (move.Success)
                {
                    target.Owner = crew.Id;
                    target.TakenFrom = null;
                    crew.LastMove = "Took the " + content.Front(target.Id).ShortName + (move.FromCrew != null ? " from the " + content.Crew(move.FromCrew).Name : "");
                }
                moves.Add(move);
            }
            return moves;
        }

        /// <summary>The nearest worthwhile front that isn't the player's or the crew's own: neutral fronts first,
        /// then weakly held rival fronts, weighted by closeness to home and income.</summary>
        private static FrontState PickTarget(EmpireState s, ContentDb content, CrewState crew, CrewDef def, Random rng)
        {
            var pool = new List<FrontState>();
            var weights = new List<double>();
            foreach (FrontState f in s.Fronts)
            {
                if (f.Owner == Owners.Player || f.Owner == crew.Id) continue;
                FrontDef fd = content.Front(f.Id);
                if (fd == null || fd.Starter) continue;
                double dist = Distance(def.Home, fd.Door);
                if (dist > MaxReach) continue;
                double w = Attacks.DistanceFactor(def.Home, fd.Door) * (fd.Income / 1000.0);
                if (f.Owner == Owners.ForSale) w *= 2.0;
                else
                {
                    CrewState other = Nemesis.Crew(s, f.Owner);
                    if (other != null && other.Manpower > crew.Manpower) w *= 0.3;
                }
                pool.Add(f);
                weights.Add(w);
            }
            if (pool.Count == 0) return null;
            double total = 0;
            foreach (double w in weights) total += w;
            double roll = rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++) { roll -= weights[i]; if (roll <= 0) return pool[i]; }
            return pool[pool.Count - 1];
        }

        private static double Distance(Spot a, Spot b)
        {
            if (a == null || b == null || !a.IsSet || !b.IsSet) return 0;
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
