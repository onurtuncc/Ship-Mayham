using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Diving: a swimmer holds the dive key to sink and lets go to float up. Below <see cref="SurfaceBand"/> they hold their
    /// breath; running out forces them to the surface. The bay is a wide round swim zone around the ship (marked by buoys), the
    /// seabed lies a few metres down, and coins and treasure rest on it.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>Depth of the seabed under the harbor bay; at sea the water is too deep to reach the bottom.</summary>
        public const float BaySeabedDepth = 6f, OpenSeaDepth = 14f;
        public const float DiveSpeed = 1.7f, AscendSpeed = 2.4f;
        public const float BreathSeconds = 22f, BreathRecoverPerSecond = 3f, MinDiveBreathSeconds = 10f;
        /// <summary>Under this depth the head is still in the air or the spray: no breath is used.</summary>
        public const float SurfaceBand = .45f;
        /// <summary>How far from the ship one may swim: far enough to reach every neighbouring island. There are no buoys or walls.</summary>
        public const float SwimZoneRadius = 700f;
        /// <summary>How far a swimmer's depth may differ from a sunken coin's for them to pick it up.</summary>
        public const float CoinDepthTolerance = 1.4f;

        /// <summary>The depth of the floor below a crew member in the water.</summary>
        public float SeabedDepthAt(Vector2 position)
        {
            if (!HarborOpen) return OpenSeaDepth;
            // The floor follows the island's beach: shallow near the shore, down to the bay's depth further out.
            float ground = IslandTerrain.For(DockedIsland(State)).Height(position - new Vector2(QuayOffsetX, 0));
            return Mathf.Clamp(IslandTerrain.SeaLevel - ground, .3f, BaySeabedDepth);
        }

        /// <summary>
        /// Wading: a walker who steps into deep water starts to swim, a swimmer who reaches ground shallow enough stands up. The
        /// coordinates change with them: the island's own, or the ship's.
        /// </summary>
        private void TickShoreline(int i, Vector2 previousShore)
        {
            if (!HarborOpen || InLobby) return;
            ref var crew = ref Crew[i];
            var terrain = IslandTerrain.For(DockedIsland(State));
            Vector2 offset = new Vector2(QuayOffsetX, 0);
            if (crew.OnShore)
            {
                if (!terrain.DeepEnoughToSwim(crew.Position)) return;
                bool carrying = crew.Carrying != ServiceItem.None || CarriedPassenger(i) >= 0 || crew.AtCrane || InMiniGame(i);
                Vector2 water = crew.Position + offset;
                if (carrying || !WaterWalkable(water, DockedIsland(State)))
                {
                    crew.Position = previousShore;
                    crew.Velocity = Vector2.zero;
                    return;
                }
                crew.OnShore = false;
                crew.Swimming = true;
                crew.Position = water;
                crew.JumpHeight = crew.VerticalSpeed = 0;
                overboardClock[i] = 0;
                ResetDive(i);
            }
            else if (crew.Swimming && crew.Depth <= LadderClimbDepth)
            {
                Vector2 land = crew.Position - offset;
                if (!terrain.ShallowEnoughToStand(land) || !ShoreStandable(DockedIsland(State), land)) return;
                crew.Swimming = false;
                crew.OnShore = true;
                crew.Position = land;
                crew.JumpHeight = crew.VerticalSpeed = 0;
                ResetDive(i);
            }
        }

        /// <summary>Seconds of breath a swimmer has left.</summary>
        public static float BreathLeft(in CrewState crew) => Mathf.Max(0, BreathSeconds - crew.BreathUsed);

        /// <summary>Whether the swimmer's head is below the surface.</summary>
        public static bool Submerged(in CrewState crew) => crew.Swimming && crew.Depth > SurfaceBand;

        /// <summary>
        /// Moves a swimmer up or down. Someone out of breath rises whatever they press, and cannot dive again until they have
        /// caught enough breath.
        /// </summary>
        private void StepDive(int i, bool dive, float dt)
        {
            ref var crew = ref Crew[i];
            if (!crew.Swimming) { ResetDive(i); return; }
            bool exhausted = crew.BreathUsed >= BreathSeconds;
            bool winded = crew.Depth <= SurfaceBand && crew.BreathUsed > BreathSeconds - MinDiveBreathSeconds;
            bool down = dive && !exhausted && !winded && !InMiniGame(i);
            float floor = SeabedDepthAt(crew.Position);
            crew.Depth = Mathf.Clamp(crew.Depth + (down ? DiveSpeed : -AscendSpeed) * dt, 0, floor);
            if (crew.Depth > SurfaceBand)
            {
                crew.BreathUsed = Mathf.Min(BreathSeconds, crew.BreathUsed + dt);
                if (crew.BreathUsed >= BreathSeconds && !exhausted) CrewNotice?.Invoke(i, "Out of breath! Swim up for air.");
            }
            else crew.BreathUsed = Mathf.Max(0, crew.BreathUsed - BreathRecoverPerSecond * dt);
        }

        private void ResetDive(int i)
        {
            Crew[i].Depth = 0;
            Crew[i].BreathUsed = 0;
        }

        /// <summary>The depth a sunken coin rests at: a little above the bay floor, varying from coin to coin.</summary>
        public static float CoinDepth(int coin) => 3.2f + coin % 3 * .9f;
    }
}
