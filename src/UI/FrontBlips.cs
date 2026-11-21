// Fronts & Rivals: map blips. The game's own icon for each kind of business (car wash, bar, store, nightclub...):
//   yours: green, short range on the minimap        threatened (warning text sent): yellow, flashing
//   under attack: red, flashing, with a red radius ring     rival-held: the crew's colour (Map > ShowRivalFronts)
// A threatened or attacked front also gets a GPS route, unless Office > Settings > Route to attacks is off.
// States never rely on colour alone: threatened and under attack also flash, and under attack gets the ring.
using System.Collections.Generic;
using GTA;
using GTA.Math;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

namespace FrontsAndRivals.UI
{
    public sealed class FrontBlips
    {
        private sealed class Entry
        {
            public Blip Blip;
            public Blip Ring;
            public string Shown; // what the blip currently shows, so nothing changes when nothing changed
            public string Owner; // who held it last time, so a change of hands can flash
            public int FlashUntil;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        public bool ShowRivalFronts = true;
        public bool ShowForSaleFronts = true;

        /// <summary>Flashes one front's map icon for ten seconds (the first-front introduction).</summary>
        public void Flash(string frontId)
        {
            Entry en;
            if (!_entries.TryGetValue(frontId, out en) || en.Blip == null || !en.Blip.Exists()) return;
            en.Blip.IsFlashing = true;
            en.FlashUntil = GTA.Game.GameTime + 10000;
        }

        public void Refresh(Empire empire, ContentDb content)
        {
            if (empire == null) return;
            AttackState attack = empire.State.Attack;
            foreach (FrontDef def in content.Fronts)
            {
                Entry en;
                if (!_entries.TryGetValue(def.Id, out en)) { en = new Entry(); _entries[def.Id] = en; }
                string owner = empire.OwnerOf(def.Id);
                string want = Want(empire, def, owner, attack);

                if (en.FlashUntil > 0 && GTA.Game.GameTime > en.FlashUntil && en.Blip != null && en.Blip.Exists() && !en.Shown.StartsWith("attack"))
                {
                    en.Blip.IsFlashing = false;
                    en.FlashUntil = 0;
                }
                bool changedHands = en.Owner != null && en.Owner != owner;
                en.Owner = owner;
                if (want == en.Shown && en.Blip != null && en.Blip.Exists()) continue;
                Remove(en);
                en.Shown = want;
                if (want == null) continue;

                var pos = new Vector3(def.Door.X, def.Door.Y, def.Door.Z);
                Blip b = GTA.World.CreateBlip(pos);
                // The game's own icon for this kind of business (car wash, bar, store, nightclub...).
                b.Sprite = (BlipSprite)FrontsAndRivals.Spawn.WorkerRules.MapIcon(def.Id, owner == Owners.ForSale);
                b.Name = def.Name;
                b.IsShortRange = want == "yours" || want.StartsWith("crew");
                bool route = want.EndsWith("+route");
                switch (route ? want.Substring(0, want.Length - 6) : want)
                {
                    case "yours": b.Color = (BlipColor)CrewColors.Get(empire.State.CrewColor ?? CrewColors.DefaultPlayer).Blip; break;
                    case "threat": b.Color = BlipColor.Yellow; b.IsFlashing = true; break;
                    case "attack":
                        b.Color = BlipColor.Red; b.IsFlashing = true;
                        en.Ring = GTA.World.CreateBlip(pos, 60f);
                        en.Ring.Color = BlipColor.Red;
                        en.Ring.Alpha = 90;
                        break;
                    case "forsale": b.Color = BlipColor.White; b.Name = def.Name + " (For Sale)"; break;
                    default:
                        CrewDef crew = content.Crew(owner);
                        b.Color = crew != null ? (BlipColor)crew.BlipColor : BlipColor.Grey;
                        b.Name = def.Name + (crew != null ? " (" + crew.Name + ")" : "");
                        break;
                }
                if (route) b.ShowRoute = true;
                b.Scale = owner == Owners.Player ? 0.9f : 0.8f;
                if (changedHands && !want.StartsWith("attack") && !want.StartsWith("threat"))
                {
                    // Changed hands (a turf move overnight, a raid, a claim): flash a few seconds so you notice.
                    b.IsFlashing = true;
                    en.FlashUntil = GTA.Game.GameTime + 10000;
                }
                en.Blip = b;
            }
        }

        private string Want(Empire empire, FrontDef def, string owner, AttackState attack)
        {
            if (!def.Door.IsSet) return null;
            if (owner == Owners.Player)
            {
                if (empire.State.HideBlips) return null;
                if (attack != null && attack.Front == def.Id && (attack.Warned || attack.Phase != AttackPhase.Warning))
                    return (attack.Phase == AttackPhase.Warning ? "threat" : "attack") + (empire.State.RouteOff ? "" : "+route");
                return "yours";
            }
            if (empire.State.HideBlips) return null;
            if (owner == Owners.ForSale) return ShowForSaleFronts ? "forsale" : null;
            if (!ShowRivalFronts) return null;
            return "crew:" + owner;
        }

        private static void Remove(Entry en)
        {
            if (en.Blip != null && en.Blip.Exists()) en.Blip.Delete();
            if (en.Ring != null && en.Ring.Exists()) en.Ring.Delete();
            en.Blip = null;
            en.Ring = null;
            en.Shown = null;
        }

        public void DeleteAll()
        {
            foreach (Entry en in _entries.Values)
            {
                try { Remove(en); } catch { }
            }
            _entries.Clear();
        }
    }
}
