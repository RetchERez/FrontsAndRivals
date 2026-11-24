// Fronts & Rivals dev tools: "Auto-scout all fronts". One button, run once by the developer:
// teleports to every front that has a door, waits for that area's navmesh to stream in,
// places its 4 guard posts automatically (up to 3 tries, keeping the best), saves fronts.json,
// and returns you where you started. The resulting fronts.json is copied into src/Content and shipped.
// A state machine stepped from the Streamer job: no Wait loops on the game thread.
using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Core;
using FrontsAndRivals.Data;
using FrontsAndRivals.Spawn;

namespace FrontsAndRivals.Dev
{
    public sealed class AutoScout
    {
        private const int NavmeshTimeoutMs = 15000;
        private const int SettleMs = 4000;      // let the ground and props stream in after the navmesh reports loaded
        private const int MaxAttempts = 3;
        private const float NavBox = 20f;       // half-size of the area that must have navmesh

        private readonly ContentDb _content;
        private readonly AutoPostPlacer _placer;
        private readonly Action _worldChanged;

        private readonly Queue<FrontDef> _queue = new Queue<FrontDef>();
        private FrontDef _current;
        private int _arrivedAt;
        private int _loadedAt;
        private int _attempt;
        private bool _overwrite;
        private Vector3 _returnPos;
        private float _returnHeading;
        private int _done, _failed;
        private readonly List<string> _report = new List<string>();

        public bool Running { get { return _current != null || _queue.Count > 0; } }

        public AutoScout(ContentDb content, AutoPostPlacer placer, Action worldChanged)
        {
            _content = content;
            _placer = placer;
            _worldChanged = worldChanged;
        }

        /// <param name="overwrite">Also redo fronts that already have posts.</param>
        public void Start(bool overwrite)
        {
            if (Running) { DevTools.Notify("Auto-scout is already running."); return; }
            foreach (FrontDef def in _content.Fronts)
                if (def.Door.IsSet && (overwrite || def.Posts.Count < AutoPosts.Wanted)) _queue.Enqueue(def);
            if (_queue.Count == 0) { DevTools.Notify("Every front already has 4 posts. Use \"redo posts\" to place them again."); return; }

            _overwrite = overwrite;
            Ped p = Game.Player.Character;
            _returnPos = p.Position;
            _returnHeading = p.Heading;
            _done = _failed = 0;
            _report.Clear();
            Log.Write("AutoScout: starting, " + _queue.Count + " fronts");
            DevTools.Notify("Auto-scouting " + _queue.Count + " fronts, about 10 seconds each. Hands off.");
            Next();
        }

        private void Next()
        {
            Ped p = Game.Player.Character;
            if (_queue.Count == 0)
            {
                _current = null;
                p.IsPositionFrozen = false;
                p.Position = _returnPos;
                p.Heading = _returnHeading;
                _content.SaveFronts();
                _worldChanged();
                string summary = "Auto-scout done: " + _done + " fronts placed, " + _failed + " failed.";
                Log.Write("AutoScout: " + summary + " " + string.Join("; ", _report.ToArray()));
                DevTools.Notify(summary + (_failed > 0 ? " See log." : ""));
                return;
            }
            _current = _queue.Dequeue();
            // Stand on the door and hold still while the area streams in.
            p.IsPositionFrozen = true;
            p.Position = new Vector3(_current.Door.X, _current.Door.Y, _current.Door.Z);
            _arrivedAt = Game.GameTime;
            _loadedAt = 0;
            _attempt = 0;
        }

        /// <summary>Step every update (0.5 s).</summary>
        public void Update()
        {
            if (_current == null) return;
            FrontDef f = _current;
            int now = Game.GameTime;

            if (_loadedAt == 0)
            {
                bool loaded = Function.Call<bool>(Hash.IS_NAVMESH_LOADED_IN_AREA,
                    f.Door.X - NavBox, f.Door.Y - NavBox, f.Door.Z - 10f, f.Door.X + NavBox, f.Door.Y + NavBox, f.Door.Z + 10f);
                if (loaded) _loadedAt = now;
                else if (now - _arrivedAt > NavmeshTimeoutMs)
                {
                    int g = f.Posts.Count >= AutoPosts.Wanted ? f.Posts.Count : _placer.PlaceFromGround(f);
                    Log.Write("AutoScout: " + f.Id + " navmesh never loaded: " + _placer.LastReport);
                    Finish(f, g, null);
                    return;
                }
                else return;
            }
            if (now - _loadedAt < SettleMs) return;

            // First try replaces old posts when redoing; later tries only keep a better result.
            int n = _placer.Place(f, _attempt == 0 && _overwrite);
            _attempt++;
            Log.Write("AutoScout: " + f.Id + " try " + _attempt + ": " + _placer.LastReport);
            if (n < AutoPosts.Wanted && _attempt < MaxAttempts)
            {
                _loadedAt = now; // more of the area streams in every second; look again after another settle
                return;
            }
            if (n < AutoPosts.Wanted) { n = _placer.PlaceFromGround(f); Log.Write("AutoScout: " + f.Id + " top-up: " + _placer.LastReport); }
            Finish(f, n, null);
        }

        private void Finish(FrontDef f, int posts, string why)
        {
            if (posts > 0) { _done++; _report.Add(f.Id + ": " + posts + " posts"); }
            else { _failed++; _report.Add(f.Id + ": FAILED (" + why + ")"); }
            Log.Write("AutoScout: " + f.Id + (posts > 0 ? " -> " + posts + " posts" : " failed: " + why));
            foreach (PostDef p in f.Posts) // every post, so each location can be checked from the log
                Log.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "AutoScout:   post {0:0.0} m from the door, {1:+0.0;-0.0} m up, at {2:0.00}, {3:0.00}, {4:0.00}",
                    f.Door.DistanceTo(p.X, p.Y, p.Z), p.Z - f.Door.Z, p.X, p.Y, p.Z));
            Next();
        }

        /// <summary>Abort (e.g. a mission started): unfreeze where you stand.</summary>
        public void Cancel()
        {
            if (!Running) return;
            _queue.Clear();
            _current = null;
            Game.Player.Character.IsPositionFrozen = false;
            Log.Write("AutoScout: cancelled");
        }
    }
}
