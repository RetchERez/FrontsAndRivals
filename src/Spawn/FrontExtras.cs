// Fronts & Rivals: ownership car, laying low when wanted, police response, arrival camera.
using GTA;
using GTA.Math;
using GTA.Native;

namespace FrontsAndRivals.Spawn
{
    public static class FrontExtras
    {
        /// <summary>Paints and locks the parked ownership car.
        /// Paint indexes from DurtyFree vehicleColors.json: 53 Metallic Green, 38 Metallic Orange,
        /// 40 Matte Dark Red, 54 Metallic Gasoline Blue Green.</summary>
        public static void StyleOwnershipCar(Registry registry, Vehicle car, int paintIndex, string plate)
        {
            Function.Call(Hash.SET_VEHICLE_COLOURS, car, paintIndex, paintIndex);
            Function.Call(Hash.SET_VEHICLE_DOORS_LOCKED, car, 2); // 2 = VEHICLELOCK_LOCKED
            registry.Track(car, plate);
        }

        /// <summary>True when guards at a front should go inside: player wanted within 250 m.</summary>
        public static bool ShouldLayLow(Vector3 front)
        {
            Player p = Game.Player;
            if (p.WantedLevel == 0) return false;
            return p.Character.Position.DistanceTo(front) < 250f;
        }

        /// <summary>Sends police after one attacker (dispatch service 1 = police, per community docs).
        /// Returns the incident id so it can be deleted with DELETE_INCIDENT later.</summary>
        public static int SendPoliceAfter(Ped attacker, int units)
        {
            var outId = new OutputArgument();
            bool ok = Function.Call<bool>(Hash.CREATE_INCIDENT_WITH_ENTITY, 1, attacker, units, 0f, outId, 0, 0);
            return ok ? outId.GetResult<int>() : 0;
        }

        /// <summary>3-second convoy shot. Always restores the gameplay camera, even if it throws.</summary>
        public static void PlayArrivalShot(Vehicle leadCar, Vector3 camPos)
        {
            Camera cam = null;
            Player player = Game.Player;
            try
            {
                cam = World.CreateCamera(camPos, Vector3.Zero, 50f);
                cam.PointAt(leadCar);
                World.RenderingCamera = cam;
                player.CanControlCharacter = false;
                int end = Game.GameTime + 3000;
                while (Game.GameTime < end)
                {
                    // Any button skips (control 24 = INPUT_ATTACK, 22 = INPUT_JUMP).
                    if (Game.IsControlJustPressed(Control.Attack) || Game.IsControlJustPressed(Control.Jump)) break;
                    Script.Yield();
                }
            }
            finally
            {
                World.RenderingCamera = null; // back to the gameplay camera
                if (cam != null) cam.Delete();
                player.CanControlCharacter = true;
            }
        }
    }
}
