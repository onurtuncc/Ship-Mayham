using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The gangway gate. While it is open a crew member with free hands simply walks through it, along the plank and onto the
    /// quay, and back the same way; there is no button and no jump (see <see cref="GangwayPassage"/>).
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>The gate's point for a crew member in the given area, in that area's own coordinates.</summary>
        public static Vector2 GatePoint(bool ashore) => ashore ? HarborPoint(HarborAction.BoardShip) : ShipGangway;

        /// <summary>The rule both ends use: the harbor is open and the crew member walks with empty hands, at no station.</summary>
        public static bool GateOpen(bool harborOpen, in CrewState crew, bool carryingPassenger) =>
            harborOpen && !crew.AtHelm && !crew.AtCannon && !crew.AtCrane && !crew.Swimming &&
            crew.Carrying == ServiceItem.None && !carryingPassenger;

        /// <summary>True when the crew member could walk through the gate right now.</summary>
        public bool GateIsOpenFor(int slot) => slot >= 0 && slot < MaxCrew && GateOpen(HarborOpen, Crew[slot], CarriedPassenger(slot) >= 0);

        /// <summary>Crew standing on the plank, between the ship's rail and the quay: neither aboard nor ashore yet.</summary>
        public bool CrewOnGangway
        {
            get
            {
                foreach (var crew in Crew)
                    if (crew.Active && !crew.OnShore && !crew.Swimming && GangwayPassage.OnPlank(crew.Position)) return true;
                return false;
            }
        }

        public const string WalkThroughGate = "Walk through the gangway gate to go ashore or board.";
    }
}
