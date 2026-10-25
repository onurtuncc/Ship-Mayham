using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Passengers come aboard along the pier and the gangway and leave the same way, instead of appearing in and vanishing
    /// from their seats. Only ships with a harbor use the walk; harborless test ships keep the instant seating.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>Where the boarding queue starts on the pier, in ship-local deck coordinates.</summary>
        public static readonly Vector2 PierLaneStart = new Vector2(-9.2f, -6f);
        /// <summary>The first deck square behind the gangway gate.</summary>
        public static readonly Vector2 GateInside = new Vector2(-4.3f, -6f);
        public const float QueueSpacing = .75f, GangwayWalkSpeed = 2.4f, PierLaneEnd = -15f, FlowTimeoutSeconds = 14f;

        /// <summary>
        /// When false, passengers appear in and vanish from their seats instead of walking the gangway. Tests that are about
        /// something else switch it off so they do not have to wait for the queue.
        /// </summary>
        public static bool WalkingPassengers = true;

        private bool PassengersWalk => HarborEnabled && WalkingPassengers;

        public bool AnyPassengerBoarding
        {
            get { foreach (var p in Passengers) if (p.Active && p.Motion == PassengerMotion.Boarding) return true; return false; }
        }

        public bool AnyPassengerLeaving
        {
            get { foreach (var p in Passengers) if (p.Active && p.Motion == PassengerMotion.Leaving) return true; return false; }
        }

        /// <summary>The place a passenger first appears when boarding: in line on the pier, or already in the seat without a harbor.</summary>
        private Vector3 BoardingStart(int index) => PassengersWalk
            ? new Vector3(PierLaneStart.x - QueueSpacing * index, .5f, PierLaneStart.y)
            : new Vector3(Seats[index].x, .5f, Seats[index].y);

        private PassengerMotion BoardingMotion => PassengersWalk ? PassengerMotion.Boarding : PassengerMotion.OnDeck;

        /// <summary>Everyone walks off the ship after a successful or failed voyage; carried passengers are put down first.</summary>
        private void BeginDisembarking()
        {
            for (int p = 0; p < MaxPassengers; p++)
            {
                if (!Passengers[p].Active || Passengers[p].Motion == PassengerMotion.Rescuing) continue;
                if (!PassengersWalk) { ReturnPassengerToSeat(p); continue; }
                ref var passenger = ref Passengers[p];
                passenger.CarrierSlot = -1;
                passenger.WorkRemaining = 0;
                passenger.Routine = PassengerRoutine.Seated;
                passenger.Motion = PassengerMotion.Leaving;
                passenger.RecoveryRemaining = FlowTimeoutSeconds;
                passenger.Velocity = Vector3.zero;
            }
        }

        public const float AisleHalfWidth = .45f, SeatRowTolerance = .6f, BackRowY = -6f;

        /// <summary>
        /// The next stop on the way to a seat: along the back of the deck into the central aisle, up the aisle to the seat's
        /// row, then sideways to the seat. Keeps walkers out of the benches and the stations along the rails.
        /// </summary>
        private static Vector2 AisleGoalIn(Vector2 position, Vector2 seat)
        {
            if (Mathf.Abs(position.y - seat.y) <= SeatRowTolerance) return seat;
            return Mathf.Abs(position.x) > AisleHalfWidth ? new Vector2(0, position.y) : new Vector2(0, seat.y);
        }

        /// <summary>The way out: sideways into the aisle, down to the back of the deck, then to the gate.</summary>
        private static Vector2 AisleGoalOut(Vector2 position)
        {
            if (position.y > BackRowY + .4f)
                return Mathf.Abs(position.x) > AisleHalfWidth ? new Vector2(0, position.y) : new Vector2(0, BackRowY);
            return GateInside;
        }

        /// <summary>Metres per second squared a walking passenger speeds up with, so a walk starts with a step, not at full pace.</summary>
        public const float WalkerAcceleration = 14f;
        /// <summary>
        /// Seconds after a walk's timeout during which a stuck passenger keeps walking, ignoring obstacles, before the last-resort
        /// snap to the goal. Walking through a counter for a moment reads far better than vanishing and reappearing in a seat.
        /// </summary>
        public const float StuckGraceSeconds = 6f;

        /// <summary>
        /// One step toward <paramref name="goal"/>, steering around stations. The pace ramps up from the current velocity, and when
        /// an obstacle forces a detour the passenger keeps to the side they were already passing on instead of zig-zagging.
        /// <paramref name="ghost"/> ignores obstacles (a stuck walker whose timeout ran out). Returns true on arrival.
        /// </summary>
        private bool WalkToward(int passenger, Vector2 goal, float speed, float dt, float arrival, bool ghost = false)
        {
            ref var p = ref Passengers[passenger];
            Vector2 position = new Vector2(p.Position.x, p.Position.z);
            float distance = Vector2.Distance(position, goal);
            if (distance <= arrival) return true;
            Vector2 direction = (goal - position) / distance;
            Vector2 previous = new Vector2(p.Velocity.x, p.Velocity.z);
            float pace = Mathf.MoveTowards(Mathf.Min(previous.magnitude, speed), speed, WalkerAcceleration * dt);
            float step = Mathf.Min(distance, pace * dt);
            // Positive steer angles turn counter-clockwise: try the side the walker is already heading toward first.
            float side = previous.sqrMagnitude > .01f && direction.x * previous.y - direction.y * previous.x < 0 ? -1 : 1;
            foreach (float angle in SteerAngles)
            {
                Vector2 heading = Quaternion.Euler(0, 0, angle * side) * direction;
                Vector2 candidate = position + heading * step;
                if (!ghost && BlockedByStation(candidate)) continue;
                p.Position = new Vector3(candidate.x, .5f, candidate.y);
                p.Velocity = new Vector3(heading.x, 0, heading.y) * pace;
                return false;
            }
            p.Velocity = Vector3.zero;
            return false;
        }

        /// <summary>A walk whose timeout ran out keeps going through obstacles; only past the grace period does it give up.</summary>
        private static bool WalkTimedOut(in PassengerState p) => p.RecoveryRemaining <= 0;
        private static bool WalkAbandoned(in PassengerState p) => p.RecoveryRemaining <= -StuckGraceSeconds;

        private void TickBoarding(int passenger, float dt)
        {
            ref var p = ref Passengers[passenger];
            p.RecoveryRemaining -= dt;
            Vector2 position = new Vector2(p.Position.x, p.Position.z), seat = Seats[SeatOf(passenger)];
            Vector2 goal = position.x < GateInside.x - .4f ? GateInside : AisleGoalIn(position, seat);
            if (WalkAbandoned(p)) { ReturnPassengerToSeat(passenger); return; }
            bool atSeat = goal == seat;
            if (WalkToward(passenger, goal, GangwayWalkSpeed, dt, atSeat ? SeatArrivalDistance : .15f, WalkTimedOut(p)) && atSeat)
                ReturnPassengerToSeat(passenger);
        }

        private void TickLeaving(int passenger, float dt)
        {
            ref var p = ref Passengers[passenger];
            p.RecoveryRemaining -= dt;
            Vector2 position = new Vector2(p.Position.x, p.Position.z);
            bool onDeck = position.x > GateInside.x + .4f;
            Vector2 goal = onDeck ? AisleGoalOut(position) : new Vector2(PierLaneEnd, PierLaneStart.y);
            bool arrived = WalkAbandoned(p) || WalkToward(passenger, goal, GangwayWalkSpeed, dt, onDeck ? .15f : .3f, WalkTimedOut(p)) && !onDeck;
            if (!arrived) return;
            p.Active = false;
            p.Velocity = Vector3.zero;
            p.Motion = PassengerMotion.OnDeck;
        }
    }
}
