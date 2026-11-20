// Fronts & Rivals: fronts and guards as data (the "Data" simulation tier). Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public sealed class Empire
    {
        public const int MaxReserve = 6;   // guards waiting at the HQ
        public const int MaxRoster = 30;   // 6 fronts x 4 posts + 6 reserve

        private readonly EmpireState _s;
        private readonly ContentDb _content;

        public Empire(EmpireState state, ContentDb content)
        {
            _s = state;
            _content = content;
            EnsureFronts();
        }

        public EmpireState State { get { return _s; } }

        /// <summary>Adds a FrontState for every front in content that the save doesn't know yet.</summary>
        public void EnsureFronts()
        {
            foreach (FrontDef def in _content.Fronts)
            {
                if (Find(def.Id) == null) _s.Fronts.Add(new FrontState { Id = def.Id, Owner = def.StartOwner });
            }
        }

        public FrontState Find(string frontId)
        {
            for (int i = 0; i < _s.Fronts.Count; i++) if (_s.Fronts[i].Id == frontId) return _s.Fronts[i];
            return null;
        }

        public string OwnerOf(string frontId)
        {
            FrontState f = Find(frontId);
            return f == null ? null : f.Owner;
        }

        public bool PlayerOwns(string frontId) { return OwnerOf(frontId) == Owners.Player; }

        /// <summary>Changes hands. When the player loses a front, its guards walk back to the HQ reserve.</summary>
        public void SetOwner(string frontId, string owner)
        {
            if (!Owners.IsValid(owner)) throw new ArgumentException("bad owner " + owner);
            FrontState f = Find(frontId);
            if (f == null) return;
            f.Owner = owner;
            if (owner != Owners.Player)
            {
                foreach (GuardState g in _s.Guards)
                    if (g.Front == frontId) { g.Front = null; g.Post = GuardState.Reserve; }
            }
        }

        public int LivingCount()
        {
            int n = 0;
            foreach (GuardState g in _s.Guards) if (g.Status != GuardStatus.Dead) n++;
            return n;
        }

        public int ReserveCount()
        {
            int n = 0;
            foreach (GuardState g in _s.Guards) if (g.Status != GuardStatus.Dead && !g.IsPosted) n++;
            return n;
        }

        public GuardState Guard(int id)
        {
            foreach (GuardState g in _s.Guards) if (g.Id == id) return g;
            return null;
        }

        /// <summary>Who stands at a front's post, or null.</summary>
        public GuardState AtPost(string frontId, int post)
        {
            foreach (GuardState g in _s.Guards)
                if (g.Front == frontId && g.Post == post && g.Status != GuardStatus.Dead) return g;
            return null;
        }

        /// <summary>A new recruit with a name and a face. Returns null if the roster or reserve is full.</summary>
        public GuardState Recruit(Random rng)
        {
            if (LivingCount() >= MaxRoster || ReserveCount() >= MaxReserve) return null;
            NamesFile names = _content.Names;
            ModelsFile models = _content.Models;
            bool female = models.GuardsFemale.Count > 0
                && rng.Next(models.GuardsMale.Count + models.GuardsFemale.Count) >= models.GuardsMale.Count;
            var g = new GuardState
            {
                Id = _s.NextGuardId++,
                First = Pick(rng, female ? names.FemaleNames : names.MaleNames),
                Nick = Pick(rng, names.Nicknames),
                Last = Pick(rng, names.Surnames),
                Model = Pick(rng, female ? models.GuardsFemale : models.GuardsMale),
                Rank = (int)Rank.Recruit,
                Weapon = (int)Weapon.Pistol,
                Trait = (int)Trait.None,
                Status = GuardStatus.Active,
                Front = null,
                Post = GuardState.Reserve
            };
            _s.Guards.Add(g);
            return g;
        }

        /// <summary>Posts a guard. Only at fronts you own, only at posts the front has.
        /// Whoever stood there goes back to the reserve (the swap is allowed even when the reserve is full).</summary>
        public bool Assign(GuardState g, string frontId, int post)
        {
            if (g == null || g.Status == GuardStatus.Dead) return false;
            FrontDef def = _content.Front(frontId);
            if (def == null || !PlayerOwns(frontId)) return false;
            if (post < 0 || post >= def.Posts.Count || post >= FrontDef.MaxPosts) return false;
            GuardState current = AtPost(frontId, post);
            if (current == g) return true;
            if (current != null) { current.Front = g.Front; current.Post = g.Post; } // swap places
            g.Front = frontId;
            g.Post = post;
            return true;
        }

        public void SendToReserve(GuardState g)
        {
            if (g == null) return;
            g.Front = null;
            g.Post = GuardState.Reserve;
        }

        /// <summary>Permanent death (or an injury when PermanentDeath=false). The guard leaves the post either way.</summary>
        public void GuardDown(GuardState g, string where, bool permanentDeath, long nowActiveMs, long recoveryMs)
        {
            if (g == null || g.Status == GuardStatus.Dead) return;
            if (permanentDeath)
            {
                g.Status = GuardStatus.Dead;
                g.FellAt = where;
                g.Front = null;
                g.Post = GuardState.Reserve;
            }
            else
            {
                g.Status = GuardStatus.Injured;
                g.RecoverAtMs = nowActiveMs + recoveryMs;
                // Injured guards keep their post and return to it by themselves.
            }
        }

        /// <summary>Back on duty once recovered. Returns how many came back.</summary>
        public int Recover(long nowActiveMs)
        {
            int n = 0;
            foreach (GuardState g in _s.Guards)
                if (g.Status == GuardStatus.Injured && nowActiveMs >= g.RecoverAtMs) { g.Status = GuardStatus.Active; n++; }
            return n;
        }

        private static string Pick(Random rng, List<string> list) { return list[rng.Next(list.Count)]; }
    }
}
