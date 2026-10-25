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
            if (Crew[slot].Swimming) return "You are in the water.";
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
                held.Motion = PassengerMotion.Standing;
                held.RecoveryRemaining = StandingSeconds;
                Vector3 ahead = Quaternion.Euler(0, passengerCarryYaw[slot], 0) * Vector3.forward * 1.3f;
                held.Position = SafePassengerPosition(carried, Crew[slot].Position + new Vector2(ahead.x, ahead.z));
                held.Velocity = Vector3.zero;
                React(carried, PassengerReaction.Complaint);
                bool placed = PassengerPlacedForEvent(carried);
                ClaimNearestFreeSeat(carried);
                if (placed) return "New seat found! +15 event bonus on arrival.";
                return "Passenger put down. They will walk to the nearest free seat.";
            }
            if (airborne) return "Land before interacting.";
            if (action == PassengerAction.Throw) return "Pick up a passenger first (G / B).";
            if (passenger < 0 || passenger >= MaxPassengers || !Passengers[passenger].Active) return "Look at a nearby passenger.";
            ref var target = ref Passengers[passenger];
            if (action == PassengerAction.CheckTicket && target.Motion != PassengerMotion.OnDeck) return "Passenger is busy. Wait until they are back in a seat.";
            if (!CanBeLifted(target.Motion)) return target.Motion == PassengerMotion.Landing ? "Passenger is lying dazed. Give them a moment." : "Passenger is being carried, flying or awaiting rescue.";
            if (Vector2.Distance(Crew[slot].Position, new Vector2(target.Position.x, target.Position.z)) > InteractionRange) return "Move closer to the passenger.";
            if (action == PassengerAction.CheckTicket)
            {
                target.WorkRemaining = 0;
                PausePassengerRoutine(ref target);
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
            target.WorkRemaining = 0;
            target.Routine = PassengerRoutine.Seated; target.RoamIn = 15;
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

        public const float LandingSeconds = 1.2f, DizzySeconds = 1f, StandingSeconds = .5f, PassengerWalkSpeed = 1.6f,
            ReturnTimeoutSeconds = 8f, SeatArrivalDistance = .12f;

        /// <summary>The seat a passenger sits in: their own unless they were put down elsewhere and claimed another.</summary>
        public int SeatOf(int passenger) => Passengers[passenger].SeatClaim == 0 ? passenger : Passengers[passenger].SeatClaim - 1;

        /// <summary>Passengers that are walking back or dazed can be picked up again; a passenger lying on the deck cannot.</summary>
        public static bool CanBeLifted(PassengerMotion motion) =>
            motion == PassengerMotion.OnDeck || motion == PassengerMotion.Standing || motion == PassengerMotion.Dizzy || motion == PassengerMotion.Returning;

        /// <summary>Seats that exist right now: the rest are folded away until the capacity upgrade unlocks them.</summary>
        public int OpenSeats => ShipProgression.UnlockedSeats(State.CapacityLevel);

        public bool IsSeatOpen(int seat) => seat >= 0 && seat < OpenSeats;

        /// <summary>Seats nobody else claims. A passenger's own seat is always free for them.</summary>
        private int NearestFreeSeat(int passenger, Vector2 from)
        {
            int best = SeatOf(passenger);
            float bestDistance = float.MaxValue;
            for (int seat = 0; seat < Mathf.Min(Seats.Length, OpenSeats); seat++)
            {
                bool taken = false;
                for (int other = 0; other < MaxPassengers && !taken; other++)
                    taken = other != passenger && Passengers[other].Active && SeatOf(other) == seat;
                if (taken) continue;
                float distance = Vector2.Distance(from, Seats[seat]);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = seat;
            }
            return best;
        }

        private void ClaimNearestFreeSeat(int passenger)
        {
            var p = Passengers[passenger].Position;
            int seat = NearestFreeSeat(passenger, new Vector2(p.x, p.z));
            Passengers[passenger].SeatClaim = (byte)(seat == passenger ? 0 : seat + 1);
        }

        private bool BlockedByStation(Vector2 point)
        {
            foreach (var station in Stations) if ((point - station.Position).sqrMagnitude < StationClearance * StationClearance) return true;
            return false;
        }

        private void BeginReturn(int passenger)
        {
            Passengers[passenger].Motion = PassengerMotion.Returning;
            Passengers[passenger].RecoveryRemaining = ReturnTimeoutSeconds;
        }

        /// <summary>Walks the passenger toward their seat, steering around stations. A stuck passenger is seated when the timeout runs out.</summary>
        private void TickReturn(int passenger, float dt)
        {
            ref var p = ref Passengers[passenger];
            Vector2 position = new Vector2(p.Position.x, p.Position.z), seat = Seats[SeatOf(passenger)];
            Vector2 goal = AisleGoalIn(position, seat);
            p.RecoveryRemaining -= dt;
            if (WalkAbandoned(p)) { ReturnPassengerToSeat(passenger); return; }
            bool atSeat = goal == seat;
            if (WalkToward(passenger, goal, PassengerWalkSpeed, dt, atSeat ? SeatArrivalDistance : .1f, WalkTimedOut(p)) && atSeat)
                ReturnPassengerToSeat(passenger);
        }

        private static readonly float[] SteerAngles = { 0, 50, -50, 100, -100 };

        private void ReturnPassengerToSeat(int passenger)
        {
            ref var p = ref Passengers[passenger];
            p.Motion = PassengerMotion.OnDeck;
            p.CarrierSlot = -1;
            int seat = SeatOf(passenger);
            p.Position = new Vector3(Seats[seat].x, .5f, Seats[seat].y);
            p.Velocity = Vector3.zero;
            p.RecoveryRemaining = 0;
            p.Routine = PassengerRoutine.Seated; p.RoamIn = 15 + passenger * 4;
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
            int seat = SeatOf(passenger);
            return new Vector3(Seats[seat].x, .5f, Seats[seat].y);
        }

        private void TickPassengers(float dt)
        {
            for (int i = 0; i < MaxPassengers; i++)
            {
                if (!Passengers[i].Active) continue;
                ref var p = ref Passengers[i];
                p.ReactionRemaining = Mathf.Max(0, p.ReactionRemaining - dt);
                TickPassengerLife(i, dt);
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
                        p.Position.y = SeaLevel + PassengerSwimDepth;
                        p.RecoveryRemaining = p.CanSwim ? SwimBackSeconds(new Vector2(p.Position.x, p.Position.z)) : PassengerNonSwimmerWaitSeconds;
                        p.Satisfaction = Mathf.Max(0, p.Satisfaction - 20);
                        State.RescueCount++;
                    }
                    else if (p.Position.y <= .5f)
                    {
                        Vector3 landing = SafePassengerPosition(i, new Vector2(p.Position.x, p.Position.z));
                        p.Motion = PassengerMotion.Landing;
                        p.RecoveryRemaining = LandingSeconds;
                        p.Position = landing;
                        p.Velocity = Vector3.zero;
                    }
                }
                else if (p.Motion == PassengerMotion.Landing)
                {
                    p.RecoveryRemaining -= dt;
                    if (p.RecoveryRemaining <= 0) { p.Motion = PassengerMotion.Dizzy; p.RecoveryRemaining = DizzySeconds; }
                }
                else if (p.Motion == PassengerMotion.Dizzy || p.Motion == PassengerMotion.Standing)
                {
                    p.RecoveryRemaining -= dt;
                    if (p.RecoveryRemaining <= 0) BeginReturn(i);
                }
                else if (p.Motion == PassengerMotion.Returning) TickReturn(i, dt);
                else if (p.Motion == PassengerMotion.Boarding) TickBoarding(i, dt);
                else if (p.Motion == PassengerMotion.Leaving) TickLeaving(i, dt);
                else if (p.Motion == PassengerMotion.Rescuing)
                {
                    p.RecoveryRemaining = Mathf.Max(0, p.RecoveryRemaining - dt);
                    Vector2 water = new Vector2(p.Position.x, p.Position.z);
                    if (p.CanSwim && p.RecoveryRemaining > 0)
                    {
                        // A swimmer paddles to the nearest ladder instead of waiting to be hauled out.
                        Vector2 goal = Ladders[NearestShipLadder(water)].Base;
                        Vector2 next = Vector2.MoveTowards(water, goal, PassengerSwimSpeed * dt);
                        p.Velocity = new Vector3(next.x - water.x, 0, next.y - water.y) / Mathf.Max(dt, .0001f);
                        p.Position = new Vector3(next.x, p.Position.y, next.y);
                    }
                    else p.Velocity = Vector3.zero;
                    if (p.RecoveryRemaining == 0)
                    {
                        // Out of the water at the ladder (a swimmer) or the nearest rail (hauled), then walks to the seat like everyone else.
                        var edge = p.CanSwim ? Ladders[NearestShipLadder(water)].Top
                            : new Vector2(Mathf.Clamp(p.Position.x, -DeckBounds.x, DeckBounds.x), Mathf.Clamp(p.Position.z, -DeckBounds.y, DeckBounds.y));
                        p.Velocity = Vector3.zero;
                        p.Position = SafePassengerPosition(i, edge);
                        p.Motion = PassengerMotion.Dizzy;
                        p.RecoveryRemaining = DizzySeconds;
                        React(i, PassengerReaction.Rescued);
                    }
                }
            }
        }
    }
}
