// Fronts & Rivals: spawns one guard at a post without ever blocking the game thread.
// The model is requested, then checked on later ticks, and given up after 5 seconds.
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;
using FrontsAndRivals.Core;

namespace FrontsAndRivals.Spawn
{
    public sealed class Post
    {
        public Vector3 Position;
        public float Heading;
        public string Scenario; // e.g. "WORLD_HUMAN_GUARD_STAND" (from DurtyFree scenariosCompact.json)
    }

    public sealed class GuardSpawner
    {
        private sealed class Pending
        {
            public Model Model;
            public Post Post;
            public int StartedAt;
            public System.Action<Ped> OnSpawned;
        }

        private readonly List<Pending> _pending = new List<Pending>();
        private readonly Registry _registry;

        public GuardSpawner(Registry registry) { _registry = registry; }

        public void Request(string modelName, Post post, System.Action<Ped> onSpawned)
        {
            var model = new Model(modelName);
            if (!model.IsValid || !model.IsPed) { Log.Write("Bad ped model: " + modelName); return; }
            model.Request();
            _pending.Add(new Pending { Model = model, Post = post, StartedAt = Game.GameTime, OnSpawned = onSpawned });
        }

        /// <summary>Call from the Streamer job (every 500 ms is enough).</summary>
        public void Update()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending p = _pending[i];
                if (!p.Model.IsLoaded)
                {
                    if (Game.GameTime - p.StartedAt > 5000) { p.Model.MarkAsNoLongerNeeded(); _pending.RemoveAt(i); }
                    continue;
                }
                if (!_registry.CanSpawnPed) continue; // entity budget full, try again next update

                Ped ped = World.CreatePed(p.Model, p.Post.Position, p.Post.Heading);
                p.Model.MarkAsNoLongerNeeded();
                _pending.RemoveAt(i);
                if (ped == null) continue;

                ped.IsPersistent = true;                       // keep it until we delete it
                ped.RelationshipGroup = Factions.Guards;
                ped.Weapons.Give(WeaponHash.Pistol, 250, false, true);
                ped.Armor = 0;

                // No aim-assist lock-on to your own guards.
                Function.Call(Hash.SET_PED_CAN_BE_TARGETTED_BY_PLAYER, ped, Game.Player, false);

                // Idle animation at the post. Signature (NativeDB): ped, name, x, y, z, heading, duration, sitting, teleport.
                Function.Call(Hash.TASK_START_SCENARIO_AT_POSITION, ped, p.Post.Scenario,
                    p.Post.Position.X, p.Post.Position.Y, p.Post.Position.Z, p.Post.Heading, 0, false, true);

                _registry.Track(ped);
                p.OnSpawned?.Invoke(ped);
            }
        }
    }
}
