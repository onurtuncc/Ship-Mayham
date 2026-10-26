using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    public struct VoyageOpportunityState : INetworkSerializable, IEquatable<VoyageOpportunityState>
    {
        public bool ChestAvailable, ChestClaimed;
        public Vector2 ChestPosition;
        public float PlugRemaining;
        public int PlugsAvailable, PlugsUsed, ChestReward, WorkedFares;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ChestAvailable); s.SerializeValue(ref ChestClaimed); s.SerializeValue(ref ChestPosition);
            s.SerializeValue(ref PlugRemaining); s.SerializeValue(ref PlugsAvailable); s.SerializeValue(ref PlugsUsed);
            s.SerializeValue(ref ChestReward); s.SerializeValue(ref WorkedFares);
        }
        public bool Equals(VoyageOpportunityState other) => ChestAvailable == other.ChestAvailable && ChestClaimed == other.ChestClaimed &&
            ChestPosition.Equals(other.ChestPosition) && PlugRemaining.Equals(other.PlugRemaining) && PlugsAvailable == other.PlugsAvailable &&
            PlugsUsed == other.PlugsUsed && ChestReward == other.ChestReward && WorkedFares == other.WorkedFares;
    }
    public sealed partial class VoyageSimulation
    {
        public VoyageOpportunityState Opportunity;
        public static readonly Vector2 PlugLocker = new Vector2(2.4f, -6), RescueRope = new Vector2(-5, -1);
        private void ResetOpportunities() => Opportunity = new VoyageOpportunityState { PlugsAvailable = 2, ChestPosition = RouteChestPosition() };
        private void TickOpportunities(float dt)
        {
            Opportunity.PlugRemaining = Mathf.Max(0, Opportunity.PlugRemaining - dt);
            if (State.Elapsed >= 18 && !Opportunity.ChestClaimed) Opportunity.ChestAvailable = true;
        }
        private string OpportunityCommand(ulong clientId, DeckAction action, int passenger)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby || (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing)) return "Deck tasks are closed.";
            var crew = Crew[slot];
            if (crew.OnShore || crew.AtHelm || crew.AtCannon || crew.AtCrane || crew.JumpHeight > 0 || crew.VerticalSpeed > 0 || CarriedPassenger(slot) >= 0)
                return "Stand on deck and release your station first.";
            Vector2 point = action == DeckAction.TakePlug ? PlugLocker : RescueRope;
            if (action == DeckAction.WorkFare)
            {
                if (passenger < 0 || passenger >= MaxPassengers || !Passengers[passenger].Active || Passengers[passenger].Motion != PassengerMotion.OnDeck)
                    return "Look at a passenger on deck.";
                point = new Vector2(Passengers[passenger].Position.x, Passengers[passenger].Position.z);
            }
            if (Vector2.Distance(crew.Position, point) > InteractionRange) return "Move closer.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            if (action == DeckAction.TakePlug)
            {
                if (crew.Carrying == ServiceItem.TemporaryPlug) { ReturnItem(slot); return "Temporary plug returned."; }
                if (crew.Carrying != ServiceItem.None) return "Return your item first.";
                if (Opportunity.PlugsAvailable <= 0) return "No temporary plugs left. Use valves, manual repair or patch kits.";
                Opportunity.PlugsAvailable--; Crew[slot].Carrying = ServiceItem.TemporaryPlug;
                return "Carry the plug to REPAIR. It buys 20 seconds; a permanent repair is still needed.";
            }
            if (crew.Carrying != ServiceItem.None) return "Empty your hands first.";
            if (action == DeckAction.WorkFare)
            {
                ref var p = ref Passengers[passenger];
                if (!p.TicketChecked || p.FarePaid || p.WorkRemaining > 0) return "Inspect an unpaid passenger first; an agreement can only be made once.";
                if (!Deck.Spill) return "No cleaning job is available. Collect the fare or return when there is a spill.";
                p.WorkRemaining = 6; p.Routine = PassengerRoutine.Working; p.RoutineTarget = Deck.SpillPosition; p.RoutineTimer = 15;
                return "Cleaning job agreed: no 40-coin ticket income, but this passenger will mop the deck.";
            }
            if (action == DeckAction.HaulChest)
            {
                if (State.Phase != VoyagePhase.Sailing || !Opportunity.ChestAvailable || Opportunity.ChestClaimed) return "No sea chest to haul.";
                if (Mathf.Abs(State.Speed) > 1 || Vector2.Distance(State.Position, Opportunity.ChestPosition) > 15) return "Bring the ship within 15 m and slow below 1 m/s.";
                Opportunity.ChestAvailable = false; Opportunity.ChestClaimed = true; Opportunity.ChestReward = 30;
                return "Sea chest recovered! +30 bonus on successful arrival.";
            }
            return "Unknown opportunity.";
        }
        private void TickPassengerWork(int index, float dt)
        {
            ref var p = ref Passengers[index];
            if (!Deck.Spill) { p.WorkRemaining = 0; ReturnPassengerToSeat(index); return; }
            p.RoutineTimer -= dt;
            Vector2 current = new Vector2(p.Position.x, p.Position.z);
            Vector2 next = Vector2.MoveTowards(current, Deck.SpillPosition, dt * .85f);
            if (PassengerWalkable(index, next)) p.Position = new Vector3(next.x, .5f, next.y);
            if (p.RoutineTimer <= 0) { p.WorkRemaining = 0; ReturnPassengerToSeat(index); return; }
            if (Vector2.Distance(new Vector2(p.Position.x, p.Position.z), Deck.SpillPosition) > 1) return;
            p.WorkRemaining = Mathf.Max(0, p.WorkRemaining - dt);
            if (p.WorkRemaining > 0) return;
            Deck.Spill = false; Deck.Cleaned++; Deck.CleanSteps = 0; Deck.NextSpill = 45;
            p.FarePaid = true; p.WorkedFare = true; Opportunity.WorkedFares++;
            ReturnPassengerToSeat(index); React(index, PassengerReaction.Thanks);
        }
    }
}
