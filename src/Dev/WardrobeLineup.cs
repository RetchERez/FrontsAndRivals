// Fronts & Rivals dev tools: "Wardrobe Lineup". Four rows of people in front of you, one per crew and one for your own:
// three everyday looks, an attacker with the mask up and a leader at the end of each row, so every look can be checked
// from one screenshot. They stand still, can't be hurt and leave with "Clear Lineup" (or any world clear).
using System.Collections.Generic;
using GTA;
using GTA.Math;
using FrontsAndRivals.Data;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Dev
{
    public sealed class WardrobeLineup
    {
        private readonly Registry _registry;
        private readonly List<Ped> _peds = new List<Ped>();

        public WardrobeLineup(Registry registry) { _registry = registry; }

        public void Show()
        {
            Clear();
            if (!Wardrobe.Enabled) { DevTools.Notify("Outfits aren't loaded."); return; }
            Ped me = Game.Player.Character;
            Vector3 fwd = me.ForwardVector, side = me.RightVector;
            string[] rows = { "CK", "IR", "VS", "player" };
            int made = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                for (int k = 0; k < 5; k++)
                {
                    if (!_registry.CanSpawnPed) break;
                    bool masked = k == 3, lead = k == 4;
                    int seed = 9100 + r * 37 + k * 11;
                    Dressing d = rows[r] == "player"
                        ? OutfitRules.Choose(Wardrobe.Data, CrewColors.StyleKey(Wardrobe.PlayerStyle()), Wardrobe.PlayerColour(), seed, null, lead)
                        : Wardrobe.ForCrew(rows[r], seed, lead);
                    if (d == null) continue;
                    Model m = Wardrobe.ModelOf(d);
                    if (!m.IsLoaded) { m.Request(); continue; }
                    Vector3 at = me.Position + fwd * (4f + r * 2.2f) + side * ((k - 2) * 1.4f);
                    Ped p = GTA.World.CreatePed(m, at, me.Heading + 180f);
                    if (p == null) continue;
                    p.RelationshipGroup = Factions.Staff;
                    _registry.Track(p);
                    Wardrobe.Apply(p, d, masked);
                    p.IsInvincible = true;
                    p.BlockPermanentEvents = true;
                    p.Task.StandStill(-1);
                    _peds.Add(p);
                    made++;
                }
            }
            DevTools.Notify(made + " in the lineup. Rows, front to back: Dead Kings, Hellfire MC, Vinewood Elite, your crew. In each row: three everyday looks, an attacker with the mask up, then a leader.");
        }

        public void Clear()
        {
            foreach (Ped p in _peds) _registry.Despawn(p);
            _peds.Clear();
        }
    }
}
