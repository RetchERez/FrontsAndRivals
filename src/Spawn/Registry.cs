// Fronts & Rivals: owns every entity the mod creates, enforces the budget,
// and cleans up orphans without any memory patching.
using System.Collections.Generic;
using GTA;

namespace FrontsAndRivals.Spawn
{
    public sealed class Registry
    {
        public const int MaxPeds = 28;
        public const int MaxVehicles = 8;
        public const string PlatePrefix = "FR";

        private readonly List<Ped> _peds = new List<Ped>();
        private readonly List<Vehicle> _vehicles = new List<Vehicle>();

        public bool CanSpawnPed => _peds.Count < MaxPeds;
        public bool CanSpawnVehicle => _vehicles.Count < MaxVehicles;

        public void Track(Ped ped) { _peds.Add(ped); }

        public void Track(Vehicle vehicle, string plate)
        {
            vehicle.IsPersistent = true;
            vehicle.Mods.LicensePlate = plate; // 8 characters max, e.g. "FR03CK"
            _vehicles.Add(vehicle);
        }

        /// <summary>Drops entries whose entity no longer exists (deleted by another mod or the game).
        /// A vanished ped is "not spawned", never "dead".</summary>
        public void Prune()
        {
            _peds.RemoveAll(p => p == null || !p.Exists());
            _vehicles.RemoveAll(v => v == null || !v.Exists());
        }

        public void Despawn(Ped ped)
        {
            if (ped != null && ped.Exists()) ped.Delete();
            _peds.Remove(ped);
        }

        /// <summary>Deletes everything we own. Called from Script.Aborted.</summary>
        public void DeleteAll()
        {
            foreach (Ped p in _peds) if (p != null && p.Exists()) p.Delete();
            foreach (Vehicle v in _vehicles) if (v != null && v.Exists()) v.Delete();
            _peds.Clear();
            _vehicles.Clear();
        }

        /// <summary>On load: remove peds in FR_ groups and vehicles with FR plates left over
        /// from an earlier session or a script reload.</summary>
        public static void SweepOrphans()
        {
            Ped playerPed = Game.Player.Character;
            foreach (Ped p in World.GetAllPeds())
            {
                if (p == null || p == playerPed || p.IsPlayer) continue;
                if (Factions.IsOwnGroup(p.RelationshipGroup.Hash)) p.Delete();
            }
            Vehicle current = playerPed.CurrentVehicle;
            foreach (Vehicle v in World.GetAllVehicles())
            {
                if (v == null || v == current) continue;
                string plate = v.Mods.LicensePlate;
                if (plate != null && plate.Trim().StartsWith(PlatePrefix)) v.Delete();
            }
        }
    }
}
