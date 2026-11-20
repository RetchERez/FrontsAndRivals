// Fronts & Rivals dev tools: "Auto-scout all fronts". One button, run once by the developer:
// teleports to every front that has a door, waits for that area's navmesh to stream in,
// places its 4 guard posts automatically, saves fronts.json, and returns you where you started.
// The resulting fronts.json is copied into src/Content and shipped, so players get exact posts.
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
        private const int SettleMs = 1500;      // let the ground and props settle after the navmesh reports loaded
        private const float NavBox = 20f;       // half-size of the area that must have navmesh

        private readonly ContentDb _content;
        private readonly AutoPostPlacer _placer;
        private readonly Action _worldChanged;

        private readonly Queue<FrontDef> _queue = new Queue<FrontDef>();
        private FrontDef _current;
        private int _arrivedAt;
        private int _loadedAt;
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
                if (def.Door.IsSet && (overwrite || def.Posts.Count == 0)) _queue.Enqueue(def);
            if (_queue.Count == 0) { DevTools.Notify("No fronts need posts (each needs a door first)."); return; }

            Ped p = Game.Player.Character;
            _returnPos = p.Position;
            _returnHeading = p.Heading;
            _done = _failed = 0;
            _report.Clear();
            Log.Write("AutoScout: starting, " + _queue.Count + " fronts");
            DevTools.Notify("Auto-scouting " + _queue.Count + " fronts. Hands off for a moment.");
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
                else if (now - _arrivedAt > NavmeshTimeoutMs) { Finish(f, 0, "navmesh never loaded"); return; }
                else return;
            }
            if (now - _loadedAt < SettleMs) return;

            int n = _placer.Place(f);
            Finish(f, n, n == 0 ? "no usable ground near the door" : null);
        }

        private void Finish(FrontDef f, int posts, string why)
        {
            if (posts > 0) { _done++; _report.Add(f.Id + ": " + posts + " posts"); }
            else { _failed++; _report.Add(f.Id + ": FAILED (" + why + ")"); }
            Log.Write("AutoScout: " + f.Id + (posts > 0 ? " -> " + posts + " posts" : " failed: " + why));
            Next();
        }

        /// <summary>Abort (e.g. a mission started): unfreeze and go back.</summary>
        public void Cancel()
        {
            if (!Running) return;
            _queue.Clear();
            _current = null;
            Ped p = Game.Player.Character;
            p.IsPositionFrozen = false;
            Log.Write("AutoScout: cancelled");
        }
    }
}
