// Fronts & Rivals: map blips for the fronts you own (business icon, green, short range on the minimap).
// Threatened / under attack colours arrive with attacks in milestone 4.
using System.Collections.Generic;
using GTA;
using GTA.Math;
using FrontsAndRivals.Data;
using FrontsAndRivals.Rules;

namespace FrontsAndRivals.UI
{
    public sealed class FrontBlips
    {
        private readonly Dictionary<string, Blip> _blips = new Dictionary<string, Blip>();
        private readonly List<string> _remove = new List<string>();

        /// <summary>Makes the blips match ownership. Cheap: six fronts, no allocations when nothing changed.</summary>
        public void Refresh(Empire empire, ContentDb content)
        {
            if (empire == null) return;
            foreach (FrontDef def in content.Fronts)
            {
                bool show = def.Door.IsSet && empire.PlayerOwns(def.Id) && !empire.State.HideBlips;
                Blip blip;
                bool has = _blips.TryGetValue(def.Id, out blip) && blip != null && blip.Exists();
                if (show && !has)
                {
                    blip = GTA.World.CreateBlip(new Vector3(def.Door.X, def.Door.Y, def.Door.Z));
                    blip.Sprite = BlipSprite.Business;
                    blip.Color = BlipColor.Green;
                    blip.IsShortRange = true;
                    blip.Name = def.Name;
                    _blips[def.Id] = blip;
                }
                else if (show && has && blip.Position.DistanceTo(new Vector3(def.Door.X, def.Door.Y, def.Door.Z)) > 1f)
                {
                    blip.Position = new Vector3(def.Door.X, def.Door.Y, def.Door.Z); // door moved by scouting
                }
                else if (!show && has)
                {
                    blip.Delete();
                    _blips.Remove(def.Id);
                }
            }
            // Fronts removed from content
            _remove.Clear();
            foreach (KeyValuePair<string, Blip> kv in _blips) if (content.Front(kv.Key) == null) _remove.Add(kv.Key);
            foreach (string id in _remove) { if (_blips[id] != null && _blips[id].Exists()) _blips[id].Delete(); _blips.Remove(id); }
        }

        public void DeleteAll()
        {
            foreach (Blip b in _blips.Values)
            {
                try { if (b != null && b.Exists()) b.Delete(); } catch { }
            }
            _blips.Clear();
        }
    }
}
