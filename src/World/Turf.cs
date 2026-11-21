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

        /// <summary>Chance per crew per night for a frequency name (Off/Low/Normal/High).</summary>
        public static double ChanceFor(string frequency)
        {
            switch ((frequency ?? "Normal").ToLowerInvariant())
            {
                case "off": return 0;
                case "low": return 0.10;
                case "high": return 0.40;
                default: return MoveChance;
            }
        }

        public static List<Move> Nightly(EmpireState s, ContentDb content, Random rng) { return Nightly(s, content, rng, MoveChance); }

        /// <summary>One night of crews pushing for territory. Returns every move, successful or not.</summary>
        public static List<Move> Nightly(EmpireState s, ContentDb content, Random rng, double moveChance)
        {
            var moves = new List<Move>();
            foreach (CrewState crew in s.Crews)
            {
                if (Nemesis.IsQuiet(crew, s.Day) || crew.Manpower < MinManpowerToMove) continue;
                if (moveChance <= 0 || rng.NextDouble() >= moveChance) continue;
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
                if (move.FromCrew != null)
                {
                    crew.FeudWith = move.FromCrew;
                    CrewState other = Nemesis.Crew(s, move.FromCrew);
                    if (other != null) other.FeudWith = crew.Id;
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

        public const int MarketTarget = 2;       // fronts the market tries to keep on offer
        public const double MarketChance = 0.15; // per night, while it's short: now and then, not a conveyor belt

        /// <summary>Keeps the market alive for a late-story buyer: while fewer than three fronts are for sale, the most
        /// stretched crew (the most fronts per soldier) sometimes sells the one furthest from its home. Returns the front
        /// put on sale, or null.</summary>
        public static FrontState Market(EmpireState s, ContentDb content, Random rng)
        {
            int forSale = 0;
            foreach (FrontState f in s.Fronts) if (f.Owner == Owners.ForSale) forSale++;
            if (forSale >= MarketTarget || rng.NextDouble() >= MarketChance) return null;
            CrewState stretched = null;
            double worst = double.MaxValue;
            foreach (CrewState c in s.Crews)
            {
                int held = 0;
                foreach (FrontState f in s.Fronts) if (f.Owner == c.Id) held++;
                if (held < 2) continue; // a crew keeps its last foothold
                double perFront = Math.Max(0, c.Manpower) / (double)held;
                if (perFront < worst) { worst = perFront; stretched = c; }
            }
            if (stretched == null) return null;
            CrewDef def = content.Crew(stretched.Id);
            FrontState sell = null;
            double far = -1;
            foreach (FrontState f in s.Fronts)
            {
                if (f.Owner != stretched.Id) continue;
                FrontDef fd = content.Front(f.Id);
                if (fd == null || fd.Starter) continue;
                double d = def != null ? Distance(def.Home, fd.Door) : 0;
                if (d > far) { far = d; sell = f; }
            }
            if (sell == null) return null;
            sell.Owner = Owners.ForSale;
            sell.TakenFrom = null;
            stretched.LastMove = "Sold the " + content.Front(sell.Id).ShortName + ", couldn't hold it";
            return sell;
        }

        private static double Distance(Spot a, Spot b)
        {
            if (a == null || b == null || !a.IsSet || !b.IsSet) return 0;
            float dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
