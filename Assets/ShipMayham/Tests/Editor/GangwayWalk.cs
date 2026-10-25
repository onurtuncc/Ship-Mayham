using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>
    /// Test helper for changing area the way players do: walking the gangway lane through the gate. The gate rules apply
    /// (empty hands, harbor open, no station), so a closed gate leaves the crew member where they were.
    /// </summary>
    public static class GangwayWalk
    {
        private const float Step = .02f, MaxSeconds = 4f;

        /// <summary>
        /// Walks the crew member through the gate. On success they end on the gate point of the new area (the old
        /// <see cref="VoyageSimulation.GatePoint"/> spots), standing still with no held input. Returns true when the area changed.
        /// </summary>
        public static bool Cross(VoyageSimulation sim, ulong client, bool toShore)
        {
            int slot = sim.FindCrew(client);
            if (slot < 0 || sim.Crew[slot].Swimming) return false;
            if (sim.Crew[slot].OnShore == toShore) return false;
            sim.Crew[slot].Position = toShore ? new Vector2(-4.6f, GangwayPassage.LaneY) : VoyageSimulation.GatePoint(true);
            sim.Crew[slot].Velocity = Vector2.zero;
            Vector2 push = toShore ? Vector2.left : Vector2.right;
            for (float t = 0; t < MaxSeconds && sim.Crew[slot].OnShore != toShore; t += Step) { sim.SetInput(client, push); sim.Tick(Step); }
            sim.SetInput(client, Vector2.zero);
            if (sim.Crew[slot].OnShore != toShore) return false;
            sim.Crew[slot].Position = VoyageSimulation.GatePoint(toShore);
            sim.Crew[slot].Velocity = Vector2.zero;
            return true;
        }

        /// <summary>For scene tests: puts the crew member straight on the gate point of an area.</summary>
        public static void Place(VoyageSimulation sim, int slot, bool onShore)
        {
            sim.Crew[slot].OnShore = onShore;
            sim.Crew[slot].Position = VoyageSimulation.GatePoint(onShore);
            sim.Crew[slot].Velocity = Vector2.zero;
        }
    }
}
