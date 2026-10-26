using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    public enum ShipEquipment : byte { Standard, ServiceTray, Workshop }
    public enum PassageCourse : byte { Direct, Safe, Shortcut }
    public enum PreparationAction : byte { ServiceTray, Workshop, Pennant, SafeCourse, ShortcutCourse, DirectCourse }
    [Serializable]
    public struct VoyageJourneyState : INetworkSerializable, IEquatable<VoyageJourneyState>
    {
        public PassageCourse Course;
        public int Checkpoints, KitGrantVoyage;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Course); s.SerializeValue(ref Checkpoints); s.SerializeValue(ref KitGrantVoyage); }
        public bool Equals(VoyageJourneyState other) => Course == other.Course && Checkpoints == other.Checkpoints && KitGrantVoyage == other.KitGrantVoyage;
    }
    public sealed partial class VoyageSimulation
    {
        public const int EquipmentPrice = 90, PennantPrice = 60;
        public VoyageJourneyState Journey;
        public static int RepairBufferLimit(VoyageState state) => state.Equipment == ShipEquipment.Workshop ? 3 : RepairKitBufferCapacity;
        public static int RepairKitLimit(VoyageState state) => state.Equipment == ShipEquipment.Workshop ? 5 : VoyageRepairKits;
        public static Vector2 CourseGate(PassageCourse course, int index) => CourseGate(course, index, Vector2.zero, Destination);

        /// <summary>
        /// A passage gate between two islands. The safe course swings 70 m to the left of the route (the shoals lie on the straight
        /// line), the shortcut 20 m to the right; the gates sit at fixed fractions of the route's length.
        /// </summary>
        public static Vector2 CourseGate(PassageCourse course, int index, Vector2 from, Vector2 to)
        {
            if (index < 0 || index > 1) throw new ArgumentOutOfRangeException(nameof(index));
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 1f) return from;
            Vector2 direction = along / length, side = new Vector2(direction.y, -direction.x);
            bool safe = course == PassageCourse.Safe;
            float fraction = safe ? (index == 0 ? 20f : 200f) / 220f : (index == 0 ? 70f : 150f) / 220f;
            return from + direction * (length * fraction) + side * (safe ? -70f : 20f);
        }
        private void ResetJourney() => Journey = default;
        private void TickJourney()
        {
            GrantWorkshopKits();
            if (State.Phase != VoyagePhase.Sailing || Journey.Course == PassageCourse.Direct || Journey.Checkpoints >= 2) return;
            if (Vector2.Distance(State.Position, CourseGate(Journey.Course, Journey.Checkpoints, OriginPoint(), DestinationPoint())) <= 18) Journey.Checkpoints++;
        }
        private void GrantWorkshopKits()
        {
            if (State.Phase != VoyagePhase.Boarding || State.Equipment != ShipEquipment.Workshop || Journey.KitGrantVoyage == State.Voyage) return;
            Journey.KitGrantVoyage = State.Voyage; State.RepairKitStock += 2;
        }
        public string PrepareVoyage(ulong clientId, PreparationAction action)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby || action > PreparationAction.DirectCourse ||
                (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Arrived)) return "Preparation is available at port.";
            var crew = Crew[slot];
            int desk = Array.FindIndex(Stations, s => s.Kind == StationKind.Manifest);
            if (desk < 0 || crew.OnShore || crew.AtHelm || crew.AtCannon || crew.AtCrane || crew.JumpHeight > 0 || crew.VerticalSpeed > 0 ||
                crew.Carrying != ServiceItem.None || CarriedPassenger(slot) >= 0 || Vector2.Distance(crew.Position, Stations[desk].Position) > InteractionRange)
                return "Talk to the purser with empty hands to prepare the ship.";
            if (State.Phase == VoyagePhase.Boarding && State.Boarded > 0) return "Choose equipment and passage before boarding passengers.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            if (action == PreparationAction.ServiceTray || action == PreparationAction.Workshop)
            {
                ShipEquipment choice = action == PreparationAction.ServiceTray ? ShipEquipment.ServiceTray : ShipEquipment.Workshop;
                int bit = 1 << (int)choice;
                if ((State.OwnedEquipment & bit) == 0)
                {
                    if (State.Wallet < EquipmentPrice) return "Equipment costs 90 coins.";
                    State.Wallet -= EquipmentPrice; State.OwnedEquipment |= bit;
                }
                State.Equipment = choice; GrantWorkshopKits();
                return choice == ShipEquipment.ServiceTray ? "Service tray fitted: carry two matching orders. Switch owned equipment free at port." :
                    "Workshop fitted: +2 kits once per voyage and a three-kit bench. Switch owned equipment free at port.";
            }
            if (action == PreparationAction.Pennant)
            {
                if (!State.OwnedPennant)
                {
                    if (State.Wallet < PennantPrice) return "The decorative pennant costs 60 coins.";
                    State.Wallet -= PennantPrice; State.OwnedPennant = true; State.PennantVisible = true;
                }
                else State.PennantVisible = !State.PennantVisible;
                return "Pennant changed. Cosmetic only.";
            }
            if (State.Phase != VoyagePhase.Boarding) return "Choose the passage after starting the next voyage.";
            Journey.Course = action == PreparationAction.SafeCourse ? PassageCourse.Safe :
                action == PreparationAction.ShortcutCourse ? PassageCourse.Shortcut : PassageCourse.Direct;
            Journey.Checkpoints = 0;
            return Journey.Course == PassageCourse.Safe ? "Safe passage: visit both western gates, then dock. +12 on arrival." :
                Journey.Course == PassageCourse.Shortcut ? "Risky shortcut: visit both eastern gates through the shoals. +25 on arrival." : "Direct practice passage selected.";
        }
        private int PassageBonus() => Journey.Checkpoints < 2 ? 0 : Journey.Course == PassageCourse.Safe ? 12 : Journey.Course == PassageCourse.Shortcut ? 25 : 0;
    }
}
