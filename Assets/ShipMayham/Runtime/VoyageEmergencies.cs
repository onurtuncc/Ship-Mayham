using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    public enum FirePhase : byte { Dormant, Warning, Burning, Extinguished }
    [Serializable]
    public struct ShipEmergencyState : INetworkSerializable, IEquatable<ShipEmergencyState>
    {
        public FirePhase Fire;
        public float Warning, Heat;
        public bool Isolated, ValveAdjusted;
        public int ExtinguisherCharges, SelectedValve, FiresPutOut, WrongValves;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Fire); s.SerializeValue(ref Warning); s.SerializeValue(ref Heat);
            s.SerializeValue(ref Isolated); s.SerializeValue(ref ValveAdjusted); s.SerializeValue(ref ExtinguisherCharges);
            s.SerializeValue(ref SelectedValve); s.SerializeValue(ref FiresPutOut); s.SerializeValue(ref WrongValves);
        }
        public bool Equals(ShipEmergencyState other) => Fire == other.Fire && Warning.Equals(other.Warning) && Heat.Equals(other.Heat) &&
            Isolated == other.Isolated && ValveAdjusted == other.ValveAdjusted && ExtinguisherCharges == other.ExtinguisherCharges &&
            SelectedValve == other.SelectedValve && FiresPutOut == other.FiresPutOut && WrongValves == other.WrongValves;
    }
    public sealed partial class VoyageSimulation
    {
        public ShipEmergencyState Emergency;
        public static readonly Vector2 FirePoint = new Vector2(3.8f, 1);
        public int LeakingValve => (int)(Hazards.Seed % 3);
        private void ResetEmergencies() => Emergency = new ShipEmergencyState { ExtinguisherCharges = 4 };
        private void TickEmergencies(float dt)
        {
            if (Emergency.Fire == FirePhase.Dormant && State.Elapsed >= 32 && HasEventStation(StationKind.RepairSupply))
            { Emergency.Fire = FirePhase.Warning; Emergency.Warning = 6; return; }
            if (Emergency.Fire == FirePhase.Warning)
            {
                Emergency.Warning = Mathf.Max(0, Emergency.Warning - dt);
                if (Emergency.Warning == 0) { Emergency.Fire = FirePhase.Burning; Emergency.Heat = 1; }
            }
            if (Emergency.Fire == FirePhase.Burning && !Emergency.Isolated) DamageHull(dt * .35f);
        }
        private string SafetyCommand(ulong clientId, DeckAction action)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby || (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing)) return "Safety tasks are closed.";
            var crew = Crew[slot];
            if (crew.OnShore || crew.AtHelm || crew.AtCannon || crew.AtCrane || crew.JumpHeight > 0 || crew.VerticalSpeed > 0 || CarriedPassenger(slot) >= 0)
                return "Stand on deck and release your station first.";
            StationKind kind = action == DeckAction.TakeExtinguisher ? StationKind.RepairSupply :
                action == DeckAction.SelectValve ? StationKind.Repair : StationKind.Power;
            int station = Array.FindIndex(Stations, s => s.Kind == kind);
            Vector2 target = action == DeckAction.Extinguish ? FirePoint : station < 0 ? new Vector2(999, 999) : Stations[station].Position;
            if (Vector2.Distance(crew.Position, target) > InteractionRange) return "Move closer to the safety equipment.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            if (action == DeckAction.TakeExtinguisher)
            {
                if (crew.Carrying == ServiceItem.Extinguisher) { Crew[slot].Carrying = ServiceItem.None; return "Extinguisher returned."; }
                if (crew.Carrying != ServiceItem.None) return "Return your item first.";
                Crew[slot].Carrying = ServiceItem.Extinguisher;
                return "Extinguisher equipped. E / A at the fire sprays; R at KITS returns it.";
            }
            if (action == DeckAction.Extinguish)
            {
                if (Emergency.Fire != FirePhase.Burning) return "No active fire.";
                if (crew.Carrying != ServiceItem.Extinguisher) return "Get an extinguisher with R / D-pad down at KITS.";
                if (Emergency.ExtinguisherCharges <= 0) return "Extinguisher empty.";
                Emergency.ExtinguisherCharges--; Emergency.Heat = Mathf.Max(0, Emergency.Heat - .5f);
                if (Emergency.Heat > 0) return "Fire shrinking. Spray once more.";
                Emergency.Fire = FirePhase.Extinguished; Emergency.FiresPutOut++; Tally(slot, TallyKind.Repairs);
                return "Fire extinguished. Reset the POWER breakers if the circuit was isolated.";
            }
            if (crew.Carrying != ServiceItem.None) return "Free your hands first.";
            if (action == DeckAction.IsolateFire)
            {
                if (Emergency.Fire != FirePhase.Burning) return "No fire to isolate.";
                if (Emergency.Isolated) return "Circuit already isolated. Use the extinguisher to finish.";
                Emergency.Isolated = true; Encounter.PowerOut = true; Encounter.PowerRepairSteps = 0;
                return "Circuit isolated: fire damage stopped, but food/drink counters stay off until the fire is out.";
            }
            if (action == DeckAction.SelectValve)
            {
                if (State.Leak == 0) return "No leak to diagnose.";
                Emergency.SelectedValve = (Emergency.SelectedValve + 1) % 3; Emergency.ValveAdjusted = true;
                return $"Valve {(char)('A' + Emergency.SelectedValve)} selected. Pressure clue: {(char)('A' + LeakingValve)}. E applies.";
            }
            return "Unknown safety action.";
        }
        private string ApplyLeakValve()
        {
            Emergency.ValveAdjusted = false;
            if (Emergency.SelectedValve != LeakingValve)
            {
                Emergency.WrongValves++; DamageHull(2);
                return $"Wrong valve: +2 damage. Hot pipe is {(char)('A' + LeakingValve)}; R selects the next valve.";
            }
            FinishLeakRepair();
            return "Correct valve closed. Leak stopped without spending a patch kit.";
        }
    }
}
