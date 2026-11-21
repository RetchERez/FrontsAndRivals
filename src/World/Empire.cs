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
            // Posts now depend on a front's size: anyone standing on a post that no longer exists goes to the reserve.
            foreach (GuardState g in _s.Guards)
            {
                if (!g.IsPosted) continue;
                FrontDef def = _content.Front(g.Front);
                if (def == null || g.Post >= def.Capacity) { g.Front = null; g.Post = GuardState.Reserve; }
            }
        }

        public FrontState FindFront(string frontId) { return Find(frontId); }

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
            if (owner == Owners.Player && f.Owner != Owners.Player) f.HeldSinceDay = _s.Day; // a fresh start on income growth
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
            Trait.Sharpshooter, Trait.Tough, Trait.Hothead, Trait.NightOwl, Trait.Medic, Trait.Loyal, Trait.Nervous
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

        public int OfferPrice(GuardState offer)
        {
            int price = Economy.HirePrice((Trait)offer.Trait, Stats.Overall(offer));
            return _s.Day <= _s.FearUntilDay ? (int)(Math.Round(price * DangerMoney / 50.0) * 50) : price;
        }

        /// <summary>Recruits ask this much more for a day after you kill one of your own guards.</summary>
        public const double DangerMoney = 1.25;

        /// <summary>You killed one of your own guards (or a bodyguard). Word gets around: hiring costs more until tomorrow.</summary>
        public void KilledByBoss(GuardState g)
        {
            _s.FearUntilDay = _s.Day + 1;
        }

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
        // ---- one-click front management ----

        /// <summary>A guard's power on their own, for picking the best free guards.</summary>
        public static double Power(GuardState g) { return DefenseRating.GuardPower(Record(g), new FrontUpgrades(), 12); }

        /// <summary>Healthy guards waiting in the reserve (not posted, not a bodyguard).</summary>
        public List<GuardState> FreeGuards()
        {
            var free = new List<GuardState>();
            foreach (GuardState g in _s.Guards) if (g.Status == GuardStatus.Active && !g.IsPosted && !g.Escort && !OnRaid(g)) free.Add(g);
            free.Sort((a, b) => Power(b).CompareTo(Power(a)));
            return free;
        }

        public int EmptyPosts(string frontId)
        {
            FrontDef def = _content.Front(frontId);
            if (def == null) return 0;
            int n = 0;
            for (int p = 0; p < def.Capacity; p++) if (AtPost(frontId, p) == null) n++;
            return n;
        }

        /// <summary>Puts the strongest free guards on this front's empty posts. Returns how many were posted.</summary>
        public int FillPosts(string frontId)
        {
            FrontDef def = _content.Front(frontId);
            if (def == null || !PlayerOwns(frontId)) return 0;
            List<GuardState> free = FreeGuards();
            int placed = 0;
            for (int p = 0; p < def.Capacity && placed < free.Count; p++)
                if (AtPost(frontId, p) == null && Assign(free[placed], frontId, p)) placed++;
            return placed;
        }

        private List<GuardState> AtFront(string frontId)
        {
            var list = new List<GuardState>();
            foreach (GuardState g in _s.Guards) if (g.Front == frontId && g.Status != GuardStatus.Dead) list.Add(g);
            return list;
        }

        /// <summary>What one step up the weapon ladder costs for every guard posted here.</summary>
        public int ArmFrontPrice(string frontId)
        {
            int total = 0;
            foreach (GuardState g in AtFront(frontId)) { int p = WeaponUpgradePrice(g); if (p > 0 && !WeaponLocked(g)) total += p; }
            return total;
        }

        /// <summary>Every guard posted here gets their next gun. Returns how many were upgraded.</summary>
        public int ArmFront(string frontId)
        {
            int n = 0;
            foreach (GuardState g in AtFront(frontId)) if (WeaponUpgradePrice(g) > 0 && !WeaponLocked(g)) { UpgradeWeapon(g); n++; }
            return n;
        }

        // ---- raids ----

        public bool OnRaid(GuardState g) { return g != null && _s.Raid != null && _s.Raid.GuardIds.Contains(g.Id); }

        /// <summary>Your strongest healthy guards waiting in the reserve, up to four. Guards standing a post never go:
        /// a raid squad is something you choose to keep, at the cost of posting fewer people.</summary>
        public List<GuardState> RaidTeam()
        {
            var team = new List<GuardState>();
            foreach (GuardState g in _s.Guards) if (g.Status == GuardStatus.Active && !g.IsPosted && !g.Escort && !OnRaid(g)) team.Add(g);
            team.Sort((a, b) => Power(b).CompareTo(Power(a)));
            if (team.Count > Raids.MaxGuards) team.RemoveRange(Raids.MaxGuards, team.Count - Raids.MaxGuards);
            return team;
        }

        /// <summary>Why a raid on this front can't go ahead, or null if it can.</summary>
        public string RaidBlocked(FrontDef def)
        {
            if (def == null) return "Unknown front.";
            string owner = OwnerOf(def.Id);
            if (!Owners.IsCrew(owner)) return "Only fronts held by a rival crew can be raided.";
            if (_s.Raid != null) return "A raid is already under way.";
            if (_s.Day <= _s.LastRaidDay) return "Your people are regrouping after today's raid. One raid a day: next after midnight.";
            if (Attacks.DugIn(_s, Find(def.Id))) return "They only just took it and they're dug in. After midnight.";
            if (!Trusted(def)) return "Too big to take yet: hold " + Economy.TrustNeeded(def.Tier) + " fronts first.";
            if (RaidTeam().Count == 0) return "Nobody in the reserve to send. Raids use guards who aren't standing a post.";
            return null;
        }

        /// <summary>The odds of taking this front with the team you'd send right now.</summary>
        public double RaidOdds(FrontDef def, int hour)
        {
            CrewState crew = Nemesis.Crew(_s, OwnerOf(def.Id));
            CrewDef cd = _content.Crew(OwnerOf(def.Id));
            var records = new List<GuardRecord>();
            foreach (GuardState g in RaidTeam()) records.Add(Record(g));
            int soldiers = Raids.Soldiers(def, Find(def.Id), crew, _s.Day);
            if (soldiers == 0) return 0.95;
            return Raids.WinChance(Raids.TeamPower(records, hour), Raids.CrewDefense(cd, soldiers) * Difficulty.Strength(_s.Difficulty));
        }

        /// <summary>The Broker's starter deal: the car wash comes with two of the old owner's people, already on their posts
        /// (knives only), so a broke early-story character isn't left with a front nobody guards. Only on a first front.</summary>
        public int StarterCrew(FrontDef def, Random rng)
        {
            if (def == null || !def.Starter || LivingCount() > 0) return 0;
            int n = 0;
            for (int post = 0; post < def.Capacity && n < 2; post++)
            {
                GuardState g = Recruit(rng);
                if (g == null) break;
                Assign(g, def.Id, post);
                n++;
            }
            return n;
        }

        public const int RescueAfterDays = 2;

        /// <summary>Safety net for a player who lost everything: after two in-game days with no fronts, the Broker buys
        /// the car wash back from whoever took it and offers it again (with its starter crew if you have no guards).
        /// Call once a midnight. Returns true when the Broker stepped in.</summary>
        public bool BrokerRescue()
        {
            if (OwnedCount() > 0) { _s.LastHeldDay = _s.Day; return false; }
            if (_s.Day - _s.LastHeldDay < RescueAfterDays) return false;
            foreach (FrontDef def in _content.Fronts)
            {
                if (!def.Starter) continue;
                if (OwnerOf(def.Id) == Owners.ForSale) return false; // already on offer
                SetOwner(def.Id, Owners.ForSale);
                _s.LastHeldDay = _s.Day; // don't repeat every night
                return true;
            }
            return false;
        }

        /// <summary>Calls the crew back before word comes in: nobody fights, everyone goes home.</summary>
        public void CancelRaid() { _s.Raid = null; }

        /// <summary>Reset the city: every front goes back to where it started and the crews come back at full strength,
        /// but you keep your people (ranks, skills, guns) for another run. Money stays with your character.</summary>
        public void ResetCity(System.Random rng, long nowMs)
        {
            foreach (FrontState f in _s.Fronts)
            {
                FrontDef def = _content.Front(f.Id);
                f.Owner = def != null ? def.StartOwner : Owners.ForSale;
                f.TakenFrom = null;
                f.Cameras = f.Doors = f.Floodlights = f.Armory = false;
                f.Damaged = false;
                f.WeakUntilDay = -1;
                f.DugInUntilDay = 0;
                f.HeldSinceDay = _s.Day;
            }
            foreach (GuardState g in _s.Guards) { g.Front = null; g.Post = GuardState.Reserve; g.Escort = false; }
            _s.Crews.Clear();
            Nemesis.EnsureCrews(_s, _content, rng);
            _s.Attack = null;
            _s.Raid = null;
            _s.RuledAll = false;
            _s.FrontAttackedMs.Clear();
            _s.LastAttackMs = nowMs; // a normal wait before the first attack of the new run
            _s.CityResets++;
        }

        /// <summary>Sends the team. They keep their posts (their fronts are a guard short until they're back).</summary>
        public bool StartRaid(FrontDef def, long nowMs, int hour)
        {
            if (RaidBlocked(def) != null) return false;
            var raid = new RaidState { Front = def.Id, Crew = OwnerOf(def.Id), EndsMs = nowMs + Raids.RaidMs, Odds = RaidOdds(def, hour) };
            foreach (GuardState g in RaidTeam()) raid.GuardIds.Add(g.Id);
            _s.Raid = raid;
            _s.LastRaidDay = _s.Day;
            return true;
        }

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
            if (OwnedCount() == 0) return false; // crews would fight them before you're even in the game
            if (OnRaid(g)) return false;
            if (!FrontsAndRivals.Spawn.EscortRules.CanCall(g.Status == GuardStatus.Active, g.Escort, EscortCount())) return false;
            g.Escort = true;
            return true;
        }

        /// <summary>A midnight on the payroll: every guard standing a post gets a little better at it.</summary>
        /// <summary>Also 1 XP a night on a post, so ranks (and the guns they unlock) come with time served, not only
        /// from the rare attack on their own front. Returns the guards who were promoted tonight.</summary>
        public List<GuardState> DayOnDuty()
        {
            var promoted = new List<GuardState>();
            foreach (GuardState g in _s.Guards)
            {
                if (g.Status != GuardStatus.Active || !g.IsPosted) continue;
                Stats.Add(g, Stats.DayOnDuty);
                int before = g.Rank;
                g.Xp++;
                Attacks.Promote(g);
                if (g.Rank > before) promoted.Add(g);
            }
            return promoted;
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
            if (g != null && g.Weapon < (int)DefenseRating.TopWeapon && !WeaponLocked(g)) g.Weapon++;
        }

        /// <summary>Their next gun needs a higher rank than they have (Economy.MaxWeapon).</summary>
        public static bool WeaponLocked(GuardState g)
        {
            return g != null && g.Weapon < (int)DefenseRating.TopWeapon && (Weapon)(g.Weapon + 1) > Economy.MaxWeapon((Rank)g.Rank);
        }

        /// <summary>Does the Broker trust you with this front yet? (Economy.TrustNeeded)</summary>
        public bool Trusted(FrontDef def) { return def == null || OwnedCount() >= Economy.TrustNeeded(def.Tier); }

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
                if (g.Front != frontId || g.Status == GuardStatus.Dead || g.Escort || OnRaid(g)) continue; // with you, or out on a raid
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
                n += Economy.FrontIncome(_content.Front(f.Id), f, _s.Day, OwnedCount(), 1.0);
            }
            return n;
        }

        /// <summary>Tonight's take from one front, with its established and network bonuses.</summary>
        public int FrontIncome(string frontId)
        {
            FrontState f = Find(frontId);
            return f == null || f.Owner != Owners.Player ? 0 : Economy.FrontIncome(_content.Front(frontId), f, _s.Day, OwnedCount(), 1.0);
        }

        public int DailyWages()
        {
            int n = 0;
            int living = LivingCount();
            foreach (GuardState g in _s.Guards) if (g.Status != GuardStatus.Dead) n += Economy.WageFor((Rank)g.Rank, living, 1.0);
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
            if (post < 0 || post >= def.Capacity) return false;
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
