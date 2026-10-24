using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The gangway gate: walking into it while it is open carries a crew member across, in either direction, without a button.
    /// The view glides the body along the plank, so the change of area is a short walk rather than a jump.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        public const float GateTriggerRadius = 1f, GateCooldownSeconds = .8f, GateInputThreshold = .25f;
        private readonly float[] gateReady = new float[MaxCrew];

        /// <summary>The gate's trigger point for a crew member in the given area, in that area's own coordinates.</summary>
        public static Vector2 GatePoint(bool ashore) => ashore ? HarborPoint(HarborAction.BoardShip) : ShipGangway;

        /// <summary>True when the crew member could pass the gate right now if they walked into it (hands free).</summary>
        public bool GateIsOpenFor(int slot)
        {
            if (slot < 0 || !HarborOpen) return false;
            var crew = Crew[slot];
            if (crew.AtHelm || crew.AtCannon || crew.AtCrane || crew.Swimming) return false;
            return crew.Carrying == ServiceItem.None && CarriedPassenger(slot) < 0;
        }

        private string CrossGangway(int slot, bool toShore)
        {
            ref var crew = ref Crew[slot];
            crew.OnShore = toShore;
            crew.Position = GatePoint(toShore);
            crew.Velocity = Vector2.zero;
            inputs[slot] = Vector2.zero;
            gateReady[slot] = clock + GateCooldownSeconds;
            return toShore ? "Welcome ashore! Supplies and hull protection use the shared wallet." : "Back aboard. All crew must board before departure.";
        }

        /// <summary>Called after a walking step: opens the gate for someone who walks into it with free hands.</summary>
        private void TryGangway(int i, Vector2 input)
        {
            if (clock < gateReady[i] || input.sqrMagnitude < GateInputThreshold * GateInputThreshold) return;
            if (Crew[i].JumpHeight > 0 || Crew[i].VerticalSpeed > 0 || !GateIsOpenFor(i)) return;
            bool toShore = !Crew[i].OnShore;
            if (Vector2.Distance(Crew[i].Position, GatePoint(Crew[i].OnShore)) > GateTriggerRadius) return;
            // Walking toward the other area: west off the ship, east back onto it.
            if (toShore ? input.x > -GateInputThreshold : input.x < GateInputThreshold) return;
            CrossGangway(i, toShore);
        }
    }
}
