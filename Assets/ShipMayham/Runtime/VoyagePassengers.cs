using UnityEngine;

namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        private readonly float[] passengerCarryYaw = new float[MaxCrew];

        public int CarriedPassenger(int crewSlot)
        {
            for (int p = 0; p < MaxPassengers; p++)
                if (Passengers[p].Active && Passengers[p].Motion == PassengerMotion.Carried && Passengers[p].CarrierSlot == crewSlot) return p;
            return -1;
        }

        public void AimPassenger(ulong clientId, float yaw)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || CarriedPassenger(slot) < 0 || !Finite(yaw)) return;
            passengerCarryYaw[slot] = Mathf.Repeat(yaw, 360);
        }

        private Vector3 HeldPassengerPosition(int slot)
        {
            var crew = Crew[slot];
            return new Vector3(crew.Position.x, 1.1f + crew.JumpHeight, crew.Position.y) +
                Quaternion.Euler(0, passengerCarryYaw[slot], 0) * new Vector3(.6f, 0, 1.1f);
        }

        public string PassengerCommand(ulong clientId, int passenger, PassengerAction action, float yaw)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !Finite(yaw)) return "Invalid passenger action.";
            if (Crew[slot].OnShore) return "Board the ship before interacting with passengers.";
            if (action != PassengerAction.CheckTicket && action != PassengerAction.CarryOrDrop && action != PassengerAction.Throw) return "Invalid passenger action.";
            if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing) return "Prepare a new voyage first.";
            if (Crew[slot].AtHelm || Crew[slot].AtCannon) return "Release your station first.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            int carried = CarriedPassenger(slot);
            bool airborne = Crew[slot].JumpHeight > 0 || Crew[slot].VerticalSpeed > 0;
            if (carried >= 0)
            {
                if (action == PassengerAction.CheckTicket) return "Put the passenger down to check their ticket.";
                if (action == PassengerAction.CarryOrDrop && airborne) return "Land before putting the passenger down.";
                passengerCarryYaw[slot] = Mathf.Repeat(yaw, 360);
                ref var held = ref Passengers[carried];
                held.CarrierSlot = -1;
                if (action == PassengerAction.Throw)
                {
                    held.Motion = PassengerMotion.Airborne;
                    held.Position = HeldPassengerPosition(slot);
                    held.Velocity = Quaternion.Euler(0, passengerCarryYaw[slot], 0) * new Vector3(0, 5, 8);
                    held.Satisfaction = Mathf.Max(0, held.Satisfaction - 5);
                    React(carried, PassengerReaction.Complaint);
                    Tally(slot, TallyKind.Throws);
                    return "Wheee! Overboard means a rescue fee and unhappy passengers.";
                }
                held.Motion = PassengerMotion.OnDeck;
                Vector3 ahead = Quaternion.Euler(0, passengerCarryYaw[slot], 0) * Vector3.forward * 1.3f;
                held.Position = SafePassengerPosition(carried, Crew[slot].Position + new Vector2(ahead.x, ahead.z));
                held.Velocity = Vector3.zero;
                React(carried, PassengerReaction.Complaint);
                if (PassengerPlacedForEvent(carried)) return "New seat found! +15 event bonus on arrival.";
                return "Passenger put down. They can be served at their new position.";
            }
            if (airborne) return "Land before interacting.";
            if (action == PassengerAction.Throw) return "Pick up a passenger first (G / B).";
            if (passenger < 0 || passenger >= MaxPassengers || !Passengers[passenger].Active) return "Look at a nearby passenger.";
            ref var target = ref Passengers[passenger];
            if (target.Motion != PassengerMotion.OnDeck) return "Passenger is being carried, flying or awaiting rescue.";
            if (Vector2.Distance(Crew[slot].Position, new Vector2(target.Position.x, target.Position.z)) > InteractionRange) return "Move closer to the passenger.";
            if (action == PassengerAction.CheckTicket)
            {
                string inspection = InspectPassengerForEvent(passenger);
                if (!target.TicketChecked)
                {
                    target.TicketChecked = true;
                    State.CheckedTickets++;
                    React(passenger, target.FarePaid ? PassengerReaction.Thanks : PassengerReaction.TicketDue);
                    string ticket = target.FarePaid ? "Ticket valid. Destination: North Port." : "No payment! F / Y again to collect 40 coins (paid on arrival).";
                    return ticket + inspection;
                }
                if (target.FarePaid) return "Already paid. No duplicate charge." + inspection;
                target.FarePaid = true;
                State.CollectedFares++;
                Tally(slot, TallyKind.Fares);
                React(passenger, PassengerReaction.FareCollected);
                return "Fare collected: +40 ticket income on successful arrival." + inspection;
            }
            if (Crew[slot].Carrying != ServiceItem.None) return "Serve or return your item before lifting a passenger.";
            target.Motion = PassengerMotion.Carried;
            target.CarrierSlot = slot;
            passengerCarryYaw[slot] = Mathf.Repeat(yaw, 360);
            target.Position = HeldPassengerPosition(slot);
            return "G / B: put down | LMB / RT: throw. Carrying slows walking.";
        }

        private void React(int passenger, PassengerReaction reaction)
        {
            Passengers[passenger].Reaction = reaction;
            Passengers[passenger].ReactionRemaining = 4;
        }

        private void ReturnPassengerToSeat(int passenger)
        {
            ref var p = ref Passengers[passenger];
            p.Motion = PassengerMotion.OnDeck;
            p.CarrierSlot = -1;
            p.Position = new Vector3(Seats[passenger].x, .5f, Seats[passenger].y);
            p.Velocity = Vector3.zero;
            p.RecoveryRemaining = 0;
        }

        private Vector3 SafePassengerPosition(int passenger, Vector2 desired)
        {
            // Keep dropped NPCs out of counters/chairs. Original seat is a guaranteed fallback.
            for (int ring = 0; ring < 3; ring++)
                for (int direction = 0; direction < 8; direction++)
                {
                    float angle = direction * Mathf.PI / 4;
                    Vector2 candidate = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * .8f);
                    bool crewClear = true;
                    foreach (var crew in Crew) if (crew.Active && Vector2.Distance(crew.Position, candidate) < .85f) crewClear = false;
                    if (crewClear && Mathf.Abs(candidate.x) <= DeckBounds.x && Mathf.Abs(candidate.y) <= DeckBounds.y && Walkable(candidate, passenger))
                        return new Vector3(candidate.x, .5f, candidate.y);
                }
            return new Vector3(Seats[passenger].x, .5f, Seats[passenger].y);
        }

        private void TickPassengers(float dt)
        {
            for (int i = 0; i < MaxPassengers; i++)
            {
                if (!Passengers[i].Active) continue;
                ref var p = ref Passengers[i];
                p.ReactionRemaining = Mathf.Max(0, p.ReactionRemaining - dt);
                if (p.Motion == PassengerMotion.Carried)
                {
                    if (p.CarrierSlot < 0 || p.CarrierSlot >= MaxCrew || !Crew[p.CarrierSlot].Active) ReturnPassengerToSeat(i);
                    else p.Position = HeldPassengerPosition(p.CarrierSlot);
                }
                else if (p.Motion == PassengerMotion.Airborne)
                {
                    p.Position += p.Velocity * dt + Vector3.down * (9 * dt * dt);
                    p.Velocity += Vector3.down * (18 * dt);
                    if (Mathf.Abs(p.Position.x) > 5.9f || Mathf.Abs(p.Position.z) > 9.9f)
                    {
                        p.Motion = PassengerMotion.Rescuing;
                        p.Velocity = Vector3.zero;
                        p.Position.y = -1.25f;
                        p.RecoveryRemaining = 8;
                        p.Satisfaction = Mathf.Max(0, p.Satisfaction - 20);
                        State.RescueCount++;
                    }
                    else if (p.Position.y <= .5f)
                    {
                        Vector3 landing = SafePassengerPosition(i, new Vector2(p.Position.x, p.Position.z));
                        p.Motion = PassengerMotion.OnDeck;
                        p.Position = landing;
                        p.Velocity = Vector3.zero;
                    }
                }
                else if (p.Motion == PassengerMotion.Rescuing)
                {
                    p.RecoveryRemaining = Mathf.Max(0, p.RecoveryRemaining - dt);
                    if (p.RecoveryRemaining == 0)
                    {
                        ReturnPassengerToSeat(i);
                        React(i, PassengerReaction.Rescued);
                    }
                }
            }
        }
    }
}
