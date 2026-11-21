// Fronts & Rivals: owns every entity the mod creates, enforces the 28 ped / 8 vehicle budget,
// and cleans up orphans without any memory patching (see Markers).
using System.Collections.Generic;
using GTA;
using FrontsAndRivals.Core;

namespace FrontsAndRivals.Spawn
{
    public sealed class Registry
    {
        public const int MaxPeds = 28;
        public const int MaxVehicles = 8;

        private readonly List<Ped> _peds = new List<Ped>();
        private readonly List<Vehicle> _vehicles = new List<Vehicle>();

        public int PedCount { get { return _peds.Count; } }
        public int VehicleCount { get { return _vehicles.Count; } }
        public bool CanSpawnPed { get { return _peds.Count < MaxPeds; } }
        public bool CanSpawnVehicle { get { return _vehicles.Count < MaxVehicles; } }

        /// <summary>Takes ownership of a game-group ped (the real police the mod sends to street crime): counted and cleaned
        /// up like the rest, but left in the game's own group so the game treats them as police.</summary>
        public void TrackKeepGroup(Ped ped)
        {
            if (ped == null) return;
            ped.IsPersistent = true;
            _peds.Add(ped);
        }

        /// <summary>Takes ownership of a ped. It must already be in an FR_ group, or it couldn't be swept later.</summary>
        public void Track(Ped ped)
        {
            if (ped == null) return;
            ped.IsPersistent = true;
            if (!Markers.IsModGroup(ped.RelationshipGroup.Hash))
            {
                Log.Write("WARNING: tracked ped without an FR_ group, moving it to FR_GUARDS");
                ped.RelationshipGroup = Factions.Guards;
            }
            _peds.Add(ped);
        }

        /// <summary>Takes ownership of a vehicle and gives it an FR plate (Markers.MakePlate).</summary>
        public void Track(Vehicle vehicle, string plate)
        {
            if (vehicle == null) return;
            if (!Markers.IsModPlate(plate))
            {
                Log.Write("WARNING: plate '" + plate + "' is not an FR plate, using FR99DV");
                plate = Markers.MakePlate(99, Markers.OwnerDev);
            }
            vehicle.IsPersistent = true;
            vehicle.Mods.LicensePlate = plate;
            _vehicles.Add(vehicle);
        }

        public bool Owns(Ped ped)
        {
            for (int i = 0; i < _peds.Count; i++) if (_peds[i] == ped) return true;
            return false;
        }

        /// <summary>Drops entries whose entity no longer exists (deleted by another mod or the game).
        /// A vanished ped is "not spawned", never "dead".</summary>
        public void Prune()
        {
            for (int i = _peds.Count - 1; i >= 0; i--)
                if (_peds[i] == null || !_peds[i].Exists()) _peds.RemoveAt(i);
            for (int i = _vehicles.Count - 1; i >= 0; i--)
                if (_vehicles[i] == null || !_vehicles[i].Exists()) _vehicles.RemoveAt(i);
        }

        public void Despawn(Ped ped)
        {
            if (ped != null && ped.Exists()) ped.Delete();
            _peds.Remove(ped);
        }

        public void Despawn(Vehicle vehicle)
        {
            if (vehicle != null && vehicle.Exists()) vehicle.Delete();
            _vehicles.Remove(vehicle);
        }

        /// <summary>Deletes everything we own. Each delete is guarded so one bad handle can't stop the rest.</summary>
        public int DeleteAll()
        {
            int n = 0;
            foreach (Ped p in _peds)
            {
                try { if (p != null && p.Exists()) { p.Delete(); n++; } } catch (System.Exception ex) { Log.Error("DeleteAll ped", ex); }
            }
            foreach (Vehicle v in _vehicles)
            {
                try { if (v != null && v.Exists()) { v.Delete(); n++; } } catch (System.Exception ex) { Log.Error("DeleteAll vehicle", ex); }
            }
            _peds.Clear();
            _vehicles.Clear();
            return n;
        }

        /// <summary>On load: remove peds in FR_ groups and vehicles with FR plates left from an earlier
        /// session or a script reload. Never touches the player, or a vehicle the player is in or last used.</summary>
        public static int SweepOrphans()
        {
            int peds = 0, vehicles = 0;
            Ped playerPed = Game.Player.Character;
            foreach (Ped p in World.GetAllPeds())
            {
                if (p == null || p == playerPed || p.IsPlayer) continue;
                if (Markers.IsModGroup(p.RelationshipGroup.Hash)) { p.Delete(); peds++; }
            }
            Vehicle current = playerPed.CurrentVehicle;
            Vehicle last = playerPed.LastVehicle;
            foreach (Vehicle v in World.GetAllVehicles())
            {
                if (v == null || v == current || v == last) continue;
                if (Markers.IsModPlate(v.Mods.LicensePlate)) { v.Delete(); vehicles++; }
            }
            Log.Write("Orphan sweep: removed " + peds + " peds, " + vehicles + " vehicles");
            return peds + vehicles;
        }
    }
}
