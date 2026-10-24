using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    public enum DeckAction : byte { CleanSpill, CalmPassenger, TakeExtinguisher, Extinguish, IsolateFire, SelectValve, TakePlug, WorkFare, HaulChest }
    [Serializable]
    public struct DeckConditionState : INetworkSerializable, IEquatable<DeckConditionState>
    {
        public bool Spill;
        public Vector2 SpillPosition;
        public int CleanSteps, Cleaned;
        public float NextSpill;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Spill); s.SerializeValue(ref SpillPosition); s.SerializeValue(ref CleanSteps);
            s.SerializeValue(ref Cleaned); s.SerializeValue(ref NextSpill);
        }
        public bool Equals(DeckConditionState other) => Spill == other.Spill && SpillPosition.Equals(other.SpillPosition) &&
            CleanSteps == other.CleanSteps && Cleaned == other.Cleaned && NextSpill.Equals(other.NextSpill);
    }
    public sealed partial class VoyageSimulation
    {
        public DeckConditionState Deck;
        private void ResetDeck() => Deck = new DeckConditionState { NextSpill = 35, SpillPosition = new Vector2(0, -3) };
        public static float PatienceFactor(PassengerPersonality personality) =>
            personality == PassengerPersonality.Impatient ? 1.6f : personality == PassengerPersonality.Patient ? .7f : 1;

        private static void PausePassengerRoutine(ref PassengerState passenger)
        {
            if (passenger.Routine == PassengerRoutine.Seated || passenger.WorkRemaining > 0) return;
            // Stop for service without playing a seated pose in the middle of the aisle.
            passenger.Routine = PassengerRoutine.Watching;
            passenger.RoutineTimer = 2;
            passenger.RoamIn = 15;
        }

        private void TickPassengerLife(int index, float dt)
        {
            ref var p = ref Passengers[index];
            p.CalmRemaining = Mathf.Max(0, p.CalmRemaining - dt);
            if (State.Phase != VoyagePhase.Sailing || p.Motion != PassengerMotion.OnDeck) return;
            if (p.WorkRemaining > 0) { TickPassengerWork(index, dt); return; }
            bool danger = Navigation.WaveActive || Encounter.Pirate == PiratePhase.Attacking;
            if (p.Personality == PassengerPersonality.Nervous && danger && p.CalmRemaining <= 0)
                p.Satisfaction = Mathf.Max(0, p.Satisfaction - dt * .2f);
            if (Event.Phase == VoyageEventPhase.Active && Event.TargetPassenger == index) return;
            if (p.Routine == PassengerRoutine.Seated)
            {
                p.RoamIn -= dt;
                if (p.RoamIn > 0 || State.Elapsed < 15) return;
                int moving = 0;
                foreach (var other in Passengers)
                    if (other.Active && other.Motion == PassengerMotion.OnDeck && other.Routine != PassengerRoutine.Seated) moving++;
                if (moving >= 2) return;
                p.Routine = PassengerRoutine.Strolling;
                p.RoutineTarget = new Vector2(index % 2 == 0 ? -.35f : .75f, p.Position.z);
                p.RoutineTimer = 8;
            }
            if (p.Routine == PassengerRoutine.Watching)
            {
                p.RoutineTimer -= dt;
                if (p.RoutineTimer > 0) return;
                p.Routine = PassengerRoutine.Returning; p.RoutineTarget = Seats[index]; p.RoutineTimer = 8;
            }
            p.RoutineTimer -= dt;
            Vector2 current = new Vector2(p.Position.x, p.Position.z);
            Vector2 next = Vector2.MoveTowards(current, p.RoutineTarget, dt * .85f);
            if (PassengerWalkable(index, next)) p.Position = new Vector3(next.x, .5f, next.y);
            if (p.RoutineTimer <= 0) { ReturnPassengerToSeat(index); return; }
            if (Vector2.Distance(new Vector2(p.Position.x, p.Position.z), p.RoutineTarget) > .08f) return;
            if (p.Routine == PassengerRoutine.Returning) ReturnPassengerToSeat(index);
            else { p.Routine = PassengerRoutine.Watching; p.RoutineTimer = 4; }
        }
        private bool PassengerWalkable(int index, Vector2 point)
        {
            if (Mathf.Abs(point.x) > DeckBounds.x || Mathf.Abs(point.y) > DeckBounds.y) return false;
            foreach (var station in Stations) if (Vector2.Distance(point, station.Position) < .8f) return false;
            for (int i = 0; i < MaxPassengers; i++)
            {
                if (i == index) continue;
                if (Vector2.Distance(point, Seats[i]) < .65f) return false;
                var p = Passengers[i];
                if (p.Active && p.Motion == PassengerMotion.OnDeck && Vector2.Distance(point, new Vector2(p.Position.x, p.Position.z)) < .65f) return false;
            }
            foreach (var crew in Crew)
                if (crew.Active && !crew.OnShore && Vector2.Distance(point, crew.Position) < .7f) return false;
            return true;
        }
        private void TickDeck(float dt)
        {
            if (State.Boarded == 0) return;
            if (!Deck.Spill)
            {
                Deck.NextSpill = Mathf.Max(0, Deck.NextSpill - dt);
                if (Deck.NextSpill == 0) { Deck.Spill = true; Deck.CleanSteps = 0; }
                return;
            }
            for (int i = 0; i < MaxPassengers; i++)
                if (Passengers[i].Active && Passengers[i].Motion == PassengerMotion.OnDeck &&
                    Vector2.Distance(Deck.SpillPosition, new Vector2(Passengers[i].Position.x, Passengers[i].Position.z)) < 3)
                    Passengers[i].Satisfaction = Mathf.Max(0, Passengers[i].Satisfaction - dt * .15f);
        }
        public string DeckCommand(ulong clientId, DeckAction action, int passenger = -1)
        {
            if (action >= DeckAction.TakeExtinguisher && action <= DeckAction.SelectValve) return SafetyCommand(clientId, action);
            if (action >= DeckAction.TakePlug && action <= DeckAction.HaulChest) return OpportunityCommand(clientId, action, passenger);
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby || (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing)) return "Deck tasks are closed.";
            var crew = Crew[slot];
            if (crew.OnShore || crew.AtHelm || crew.AtCannon || crew.AtCrane || crew.JumpHeight > 0 || crew.VerticalSpeed > 0 ||
                crew.Carrying != ServiceItem.None || CarriedPassenger(slot) >= 0) return "Stand on deck with empty hands and release your station first.";
            Vector2 target;
            if (action == DeckAction.CleanSpill)
            {
                if (!Deck.Spill) return "The floor is already clean.";
                target = Deck.SpillPosition;
            }
            else if (action == DeckAction.CalmPassenger)
            {
                if (passenger < 0 || passenger >= MaxPassengers || !Passengers[passenger].Active || Passengers[passenger].Motion != PassengerMotion.OnDeck) return "Look at a passenger on deck.";
                target = new Vector2(Passengers[passenger].Position.x, Passengers[passenger].Position.z);
            }
            else return "Unknown deck action.";
            if (Vector2.Distance(crew.Position, target) > InteractionRange) return "Move closer.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            if (action == DeckAction.CalmPassenger)
            {
                ref var p = ref Passengers[passenger];
                if (p.CalmRemaining > 0) return "They are already reassured.";
                p.CalmRemaining = 12; p.Satisfaction = Mathf.Min(100, p.Satisfaction + 3); p.RoamIn = 12;
                React(passenger, PassengerReaction.Thanks);
                return "Reassured for 12 seconds. Thanks for checking on me!";
            }
            Deck.CleanSteps++;
            if (Deck.CleanSteps < 3) return $"Mopping: {Deck.CleanSteps}/3";
            Deck.Spill = false; Deck.CleanSteps = 0; Deck.Cleaned++; Deck.NextSpill = 45;
            return "Floor clean! Nearby passengers can relax again.";
        }
    }
}
