using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The gangway as walkable ground. While the gate is open a narrow lane runs from the ship's rail through the gate, along
    /// the plank and onto the quay. Crew simply walk it; halfway along the plank their position is re-expressed in the other
    /// area's coordinates (ship deck or quay). The conversion is exact while the ship lies at its berth, so the body neither
    /// jumps nor slides, and a small gap between the two handover lines keeps someone standing on the plank from flickering
    /// between areas. Pure and shared by the server and client prediction.
    /// </summary>
    public static class GangwayPassage
    {
        /// <summary>Half the width of the gate opening and the plank, in metres.</summary>
        public const float HalfWidth = .6f;
        /// <summary>Ship-local x past which a walker heading for the quay counts as ashore.</summary>
        public const float ToShoreX = -6.9f;
        /// <summary>Ship-local x past which a walker heading for the deck counts as aboard again.</summary>
        public const float ToShipX = -6.5f;
        /// <summary>How far the lane reaches past each end so the clamp at a world boundary never stops someone mid-step.</summary>
        private const float Overlap = .3f;

        /// <summary>The lane's centre line (ship-local y), through the gate.</summary>
        public static float LaneY => VoyageSimulation.ShipGangway.y;
        /// <summary>Ship-local x of the rail, where the deck ends and the plank begins.</summary>
        public static float DeckEdgeX => -VoyageSimulation.DeckBounds.x;
        /// <summary>Ship-local x of the quay edge, where the plank ends.</summary>
        public static float QuayEdgeX => HarborTownLayout.Bounds.xMax + VoyageSimulation.QuayOffsetX;

        public static Vector2 ShipToShore(Vector2 shipLocal) => shipLocal - new Vector2(VoyageSimulation.QuayOffsetX, 0);
        public static Vector2 ShoreToShip(Vector2 shore) => shore + new Vector2(VoyageSimulation.QuayOffsetX, 0);

        /// <summary>True for a ship-local point on the gate lane (the gate opening, the plank, and a little of either end).</summary>
        public static bool InLane(Vector2 shipLocal) =>
            Mathf.Abs(shipLocal.y - LaneY) <= HalfWidth && shipLocal.x >= QuayEdgeX - Overlap && shipLocal.x <= DeckEdgeX + Overlap;

        /// <summary>True while a crew member stands on the plank itself, outside the ship's rail (ship-local point).</summary>
        public static bool OnPlank(Vector2 shipLocal) => InLane(shipLocal) && shipLocal.x < DeckEdgeX - .01f;

        /// <summary>The lane in a walker's own coordinates.</summary>
        public static bool InLane(Vector2 position, bool onShore) => InLane(onShore ? ShoreToShip(position) : position);

        /// <summary>
        /// Switches the walker to the other area once they are past the handover line on the lane. Returns true when it did.
        /// The velocity is kept: walking on is one continuous movement.
        /// </summary>
        public static bool Handover(ref CrewMotionState state)
        {
            if (!state.OnShore)
            {
                if (state.Position.x >= ToShoreX || !InLane(state.Position)) return false;
                state.OnShore = true;
                state.Position = ShipToShore(state.Position);
                return true;
            }
            Vector2 ship = ShoreToShip(state.Position);
            if (ship.x <= ToShipX || !InLane(ship)) return false;
            state.OnShore = false;
            state.Position = ship;
            return true;
        }

        /// <summary>Walking height on the plank: the deck (0.5 m) at the rail sloping to the quay (0.3 m) at its edge.</summary>
        public static float PlankHeight(Vector2 shipLocal, float deckHeight, float quayHeight) =>
            Mathf.Lerp(quayHeight, deckHeight, Mathf.InverseLerp(QuayEdgeX, DeckEdgeX, shipLocal.x));
    }

    /// <summary>
    /// Wraps the deck or the quay so the open gangway lane is walkable from it as well: bounds reach out to cover the lane and
    /// points on the lane pass regardless of the inner world. Reused, mutable and allocation-free; set it up before each step.
    /// </summary>
    public sealed class GangwayWorld : ILocomotionWorld
    {
        private ILocomotionWorld inner;
        private bool open, shore;

        public GangwayWorld Use(ILocomotionWorld world, bool gateOpen, bool onShore)
        {
            inner = world; open = gateOpen; shore = onShore;
            return this;
        }

        public Vector2 Min => open && !shore ? new Vector2(Mathf.Min(inner.Min.x, GangwayPassage.QuayEdgeX - .5f), inner.Min.y) : inner.Min;
        public Vector2 Max => open && shore ? new Vector2(Mathf.Max(inner.Max.x, GangwayPassage.ShipToShore(new Vector2(GangwayPassage.DeckEdgeX, 0)).x + .5f), inner.Max.y) : inner.Max;

        public bool Walkable(Vector2 position)
        {
            if (open && GangwayPassage.InLane(position, shore)) return true;
            Vector2 min = inner.Min, max = inner.Max;
            if (position.x < min.x || position.x > max.x || position.y < min.y || position.y > max.y) return false;
            return inner.Walkable(position);
        }
    }
}
