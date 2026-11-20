// Fronts & Rivals: fronts and guards as data (the "Data" simulation tier). Pure: no game calls, unit-tested.
using System;
using System.Collections.Generic;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.Rules
{
    public sealed class Empire
    {
        public const int MaxReserve = 6;   // guards waiting at the HQ
        public const int MaxRoster = 60;   // enough to guard about 13 fronts fully; wages are the real limit

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

        public enum HireResult { Ok, NotEnoughCash, RosterFull, ReserveFull, Gone }

        /// <summary>Every trait a recruit can roll (every recruit has exactly one).</summary>
        public static readonly Trait[] RecruitTraits =
        {
            Trait.Sharpshooter, Trait.Tough, Trait.Hothead, Trait.NightOwl, Trait.Lookout, Trait.Medic, Trait.Loyal, Trait.Nervous
        };

        /// <summary>The Broker's offers for the day: new faces each day, unhired ones are gone.</summary>
        public void RefreshOffers(Random rng, int day)
        {
            _s.Offers.Clear();
            for (int i = 0; i < Economy.RecruitsPerDay; i++)
            {
                GuardState g = NewGuard(rng);
                g.Id = 0; // gets a real id when hired
                g.Trait = (int)RecruitTraits[rng.Next(RecruitTraits.Length)];
                Stats.Set(g, Stats.Seed((Trait)g.Trait, rng));
                _s.Offers.Add(g);
            }
            _s.OffersDay = day;
        }

        public int OfferPrice(GuardState offer) { return Economy.HirePrice((Trait)offer.Trait, Stats.Overall(offer)); }

        /// <summary>Hires one of today's offers. The caller takes the returned price from the player's cash.</summary>
        public HireResult Hire(GuardState offer, long cash, out int price)
        {
            price = 0;
            if (offer == null || !_s.Offers.Contains(offer)) return HireResult.Gone;
            price = OfferPrice(offer);
            if (cash < price) return HireResult.NotEnoughCash;
            if (LivingCount() >= MaxRoster) return HireResult.RosterFull;
            if (ReserveCount() >= MaxReserve) return HireResult.ReserveFull;
            _s.Offers.Remove(offer);
            offer.Id = _s.NextGuardId++;
            offer.Front = null;
            offer.Post = GuardState.Reserve;
            offer.Status = GuardStatus.Active;
            _s.Guards.Add(offer);
            return HireResult.Ok;
        }

        /// <summary>Lets a guard go. They leave the roster for good.</summary>
        public int EscortCount()
        {
            int n = 0;
            foreach (GuardState g in _s.Guards) if (g.Escort && g.Status != GuardStatus.Dead) n++;
            return n;
        }

        /// <summary>Calls a guard to follow you, or sends them back to their post. Returns false if not allowed.</summary>
        public bool SetEscort(GuardState g, bool on)
        {
            if (g == null) return false;
            if (!on) { g.Escort = false; return true; }
            if (!FrontsAndRivals.Spawn.EscortRules.CanCall(g.Status == GuardStatus.Active, g.Escort, EscortCount())) return false;
            g.Escort = true;
            return true;
        }

        /// <summary>A midnight on the payroll: every guard standing a post gets a little better at it.</summary>
        public void DayOnDuty()
        {
            foreach (GuardState g in _s.Guards)
                if (g.Status == GuardStatus.Active && g.IsPosted) Stats.Add(g, Stats.DayOnDuty);
        }

        /// <summary>Fired: off the roster for good, no severance.</summary>
        public void LetGo(GuardState g)
        {
            if (g != null) _s.Guards.Remove(g);
        }

        /// <summary>Price of the next weapon tier for this guard, or -1 if they already carry a rifle.</summary>
        public int WeaponUpgradePrice(GuardState g) { return g == null ? -1 : Economy.UpgradePrice((Weapon)g.Weapon); }

        public void UpgradeWeapon(GuardState g)
        {
            if (g != null && g.Weapon < (int)DefenseRating.TopWeapon) g.Weapon++;
        }

        /// <summary>Buying only works for fronts that are for sale (the starter car wash).</summary>
        public bool CanBuy(string frontId) { return OwnerOf(frontId) == Owners.ForSale; }

        /// <summary>A guard as the defense rules see them.</summary>
        public static GuardRecord Record(GuardState g)
        {
            var r = new GuardRecord { Name = g.First, Rank = (Rank)g.Rank, Weapon = (Weapon)g.Weapon, Trait = (Trait)g.Trait, Injured = g.Status == GuardStatus.Injured };
            if (Stats.HasSkills(g)) { r.Shooting = g.Shooting; r.Strength = g.Strength; r.Stamina = g.Stamina; r.Stealth = g.Stealth; }
            return r;
        }

        /// <summary>A front's defense rating right now (design doc formula, including the front's upgrades).</summary>
        public double Defense(string frontId, int hour)
        {
            var posted = new List<GuardRecord>();
            foreach (GuardState g in _s.Guards)
            {
                if (g.Front != frontId || g.Status == GuardStatus.Dead || g.Escort) continue; // a bodyguard is with you, not here
                posted.Add(Record(g));
            }
            FrontState fs = Find(frontId);
            var up = fs == null ? new FrontUpgrades() : new FrontUpgrades { Cameras = fs.Cameras, Doors = fs.Doors, Floodlights = fs.Floodlights, Armory = fs.Armory };
            return DefenseRating.Compute(posted, up, hour);
        }

        public int OwnedCount()
        {
            int n = 0;
            foreach (FrontState f in _s.Fronts) if (f.Owner == Owners.Player) n++;
            return n;
        }

        /// <summary>Daily income from owned fronts, before multipliers.</summary>
        public int DailyIncome()
        {
            int n = 0;
            foreach (FrontState f in _s.Fronts)
            {
                if (f.Owner != Owners.Player) continue;
                FrontDef def = _content.Front(f.Id);
                if (def != null) n += def.Income;
            }
            return n;
        }

        public int DailyWages()
        {
            int n = 0;
            foreach (GuardState g in _s.Guards) if (g.Status != GuardStatus.Dead) n += Economy.Wage((Rank)g.Rank);
            return n;
        }

        /// <summary>A new recruit with a name and a face. Returns null if the roster or reserve is full.</summary>
        public GuardState Recruit(Random rng)
        {
            if (LivingCount() >= MaxRoster || ReserveCount() >= MaxReserve) return null;
            GuardState g = NewGuard(rng);
            g.Id = _s.NextGuardId++;
            Stats.Set(g, Stats.Seed((Trait)g.Trait, rng));
            _s.Guards.Add(g);
            return g;
        }

        /// <summary>About a third of recruits go by a street name; the rest are just first and last names.</summary>
        public const double NickChance = 0.35;
        /// <summary>Recruits are women about 4 times in 10.</summary>
        public const double FemaleChance = 0.4;

        /// <summary>Someone on the roster, in today's offers or in a crew already has this first and last name.</summary>
        public bool NameTaken(string first, string last)
        {
            foreach (GuardState g in _s.Guards) if (g.First == first && g.Last == last) return true;
            foreach (GuardState g in _s.Offers) if (g.First == first && g.Last == last) return true;
            foreach (CrewState c in _s.Crews)
                if (c.Members != null) foreach (CrewMember m in c.Members) if (m.First == first && m.Last == last) return true;
            return false;
        }

        private GuardState NewGuard(Random rng)
        {
            NamesFile names = _content.Names;
            ModelsFile models = _content.Models;
            bool female = models.GuardsFemale.Count > 0 && (models.GuardsMale.Count == 0 || rng.NextDouble() < FemaleChance);
            string first, last;
            int tries = 0;
            do
            {
                first = Pick(rng, female ? names.FemaleNames : names.MaleNames);
                last = Pick(rng, names.Surnames);
            } while (NameTaken(first, last) && ++tries < 12);
            var g = new GuardState
            {
                Id = 0,
                First = first,
                Nick = rng.NextDouble() < NickChance ? Pick(rng, names.Nicknames) : "",
                Last = last,
                Model = Pick(rng, female ? models.GuardsFemale : models.GuardsMale),
                Rank = (int)Rank.Recruit,
                Weapon = (int)Weapon.Knife,
                Trait = (int)Trait.None,
                Status = GuardStatus.Active,
                Front = null,
                Post = GuardState.Reserve
            };
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

        /// <summary>Is permanent death on? The INI must allow it and the Office's switch must not have turned it off.</summary>
        public bool PermanentDeath(bool iniPermanentDeath) { return iniPermanentDeath && !State.GuardsRecover; }

        /// <summary>Permanent death (or an injury when PermanentDeath=false). The guard leaves the post either way.</summary>
        public void GuardDown(GuardState g, string where, bool permanentDeath, long nowActiveMs, long recoveryMs)
        {
            if (g == null || g.Status == GuardStatus.Dead) return;
            g.Escort = false;
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
                g.RecoverAtMs = nowActiveMs + (long)(recoveryMs * Stats.RecoveryFactor(Stats.HasSkills(g) ? g.Stamina : Stats.Average));
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
