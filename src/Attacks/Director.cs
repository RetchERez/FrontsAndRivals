// Fronts & Rivals: attack planning (Build plan milestone 4).
// In M1 this is only the hook the dev tools call, so the "Force attack" option exists from day one.
using FrontsAndRivals.Core;

namespace FrontsAndRivals.Attacks
{
    public sealed class Director
    {
        private bool _forceRequested;

        /// <summary>Dev tools: plan an attack on the next Director update, skipping the cooldown.</summary>
        public void RequestForcedAttack()
        {
            _forceRequested = true;
            Log.Write("Director: forced attack requested");
        }

        public bool ForceRequested { get { return _forceRequested; } }

        /// <summary>Runs every 60 real seconds while the pause gate is open.</summary>
        public void Update()
        {
            if (_forceRequested)
            {
                _forceRequested = false;
                // M4 replaces this with real planning: pick crew, pick front, send the warning.
                Log.Write("Director: forced attack consumed. No attack system yet (milestone 4).");
                GTA.UI.Notification.Show("Fronts & Rivals dev: forced attack reached the Director. Attacks arrive in milestone 4.");
            }
        }
    }
}
