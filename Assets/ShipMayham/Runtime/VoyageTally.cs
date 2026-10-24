using System;

namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        private readonly CrewTally[] tallies = new CrewTally[MaxCrew];
        private readonly float[] helmClock = new float[MaxCrew];

        /// <summary>What each crew slot did this voyage (read-only view for publishing).</summary>
        public CrewTally[] Tallies => tallies;

        private void Tally(int slot, TallyKind kind)
        {
            if (slot >= 0 && slot < MaxCrew) tallies[slot].Add(kind);
        }

        private void TickHelmTime(int slot, float dt)
        {
            helmClock[slot] += dt;
            tallies[slot].HelmSeconds = (int)helmClock[slot];
        }

        private void ClearTally(int slot)
        {
            tallies[slot] = default;
            helmClock[slot] = 0;
        }

        private void ResetTallies()
        {
            Array.Clear(tallies, 0, tallies.Length);
            Array.Clear(helmClock, 0, helmClock.Length);
        }
    }
}
