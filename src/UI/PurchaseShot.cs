// Fronts & Rivals: the short establishing shot when you buy a front, like buying a property in Story Mode.
// A scripted camera frames the building for 3.5 seconds with the banner on screen, HUD hidden. Any button skips it.
// Never blocks the game thread: it's a state machine stepped every tick, and it always hands the camera back
// (time's up, a skip, a mission starting, the world being cleared, or the script stopping).
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Data;

namespace FrontsAndRivals.UI
{
    public sealed class PurchaseShot
    {
        private const int LengthMs = 3500;
        private const int SkipAfterMs = 400; // the press that bought the front mustn't also skip the shot

        private Camera _cam;
        private int _startedAt, _endsAt;

        public bool Active { get { return _cam != null; } }

        public void Play(FrontDef def)
        {
            End();
            if (def == null || !def.Door.IsSet) return;
            Ped me = Game.Player.Character;
            var door = new Vector3(def.Door.X, def.Door.Y, def.Door.Z);
            Vector3 look = door + new Vector3(0f, 0f, 1.5f);

            // Back out from the door towards where you're standing, a little to one side and up, so the shot frames the
            // building rather than the back of your head.
            Vector3 dir = me.Position - door;
            dir.Z = 0f;
            if (dir.Length() < 0.5f) dir = new Vector3(0f, 1f, 0f);
            dir.Normalize();
            var side = new Vector3(-dir.Y, dir.X, 0f);
            Vector3 want = door + dir * 11f + side * 4f + new Vector3(0f, 0f, 3.5f);

            // Never put the camera inside a wall: stop short of anything solid between the door and the spot.
            RaycastResult hit = GTA.World.Raycast(look, want, IntersectFlags.Map, me);
            Vector3 pos = want;
            if (hit.DidHit)
            {
                Vector3 back = want - look;
                back.Normalize();
                pos = hit.HitPosition - back * 1.0f;
            }

            _cam = GTA.World.CreateCamera(pos, Vector3.Zero, 45f);
            if (_cam == null) return;
            _cam.PointAt(look);
            GTA.World.RenderingCamera = _cam;
            _startedAt = Game.GameTime;
            _endsAt = _startedAt + LengthMs;
        }

        /// <summary>Every tick, also while paused (so a mission starting mid-shot ends it at once).</summary>
        public void Update(bool gateOpen)
        {
            if (_cam == null) return;
            bool skip = Game.GameTime - _startedAt > SkipAfterMs
                && (Game.IsControlJustPressed(Control.FrontendAccept) || Game.IsControlJustPressed(Control.FrontendCancel)
                    || Game.IsControlJustPressed(Control.Context) || Game.IsControlJustPressed(Control.Attack)
                    || Game.IsControlJustPressed(Control.Jump) || Game.IsControlJustPressed(Control.Sprint));
            if (!gateOpen || skip || Game.GameTime >= _endsAt) { End(); return; }
            Function.Call(Hash.HIDE_HUD_AND_RADAR_THIS_FRAME);
            Game.DisableAllControlsThisFrame();
        }

        /// <summary>Back to the gameplay camera. Safe to call any time, any number of times.</summary>
        public void End()
        {
            if (_cam == null) return;
            try { GTA.World.RenderingCamera = null; }
            finally
            {
                if (_cam.Exists()) _cam.Delete();
                _cam = null;
            }
        }
    }
}
