using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>What happens to a passenger after being thrown, put down or rescued: lying, dizzy, walking back, sitting.</summary>
    public sealed class PassengerReturnTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;

        [SetUp]
        public void Setup()
        {
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4, 3)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
            sim.Join(Host);
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Advance(.4f);
            sim.Interact(Host, 0);                    // board the passengers at the manifest
            Advance(.4f);
        }

        private void Advance(float seconds)
        {
            for (int n = 0; n < Mathf.CeilToInt(seconds / .02f); n++) sim.Tick(.02f);
        }

        private void Lift(int passenger)
        {
            var p = sim.Passengers[passenger];
            sim.Crew[0].Position = new Vector2(p.Position.x - 1, p.Position.z);
            Advance(.4f);
            sim.PassengerCommand(Host, passenger, PassengerAction.CarryOrDrop, 0);
        }

        private void PutDownAt(Vector2 crewPosition)
        {
            sim.Crew[0].Position = crewPosition;
            Advance(.4f);
            sim.PassengerCommand(Host, -1, PassengerAction.CarryOrDrop, 0);
        }

        private void ThrowOnDeck(int passenger)
        {
            Lift(passenger);
            sim.Crew[0].Position = new Vector2(0, -6);
            Advance(.4f);
            sim.PassengerCommand(Host, -1, PassengerAction.Throw, 0);
        }

        private static Vector2 Flat(PassengerState p) => new Vector2(p.Position.x, p.Position.z);

        // ------------------------------------------------------------ thrown

        [Test]
        public void ThrownPassengerLiesThenIsDizzyThenWalksBackAndSits()
        {
            ThrowOnDeck(0);
            Assert.AreEqual(PassengerMotion.Airborne, sim.Passengers[0].Motion);
            while (sim.Passengers[0].Motion == PassengerMotion.Airborne) Advance(.02f);
            Assert.AreEqual(PassengerMotion.Landing, sim.Passengers[0].Motion, "lies where they land");
            Vector2 landing = Flat(sim.Passengers[0]);
            Advance(VoyageSimulation.LandingSeconds - .2f);
            Assert.AreEqual(PassengerMotion.Landing, sim.Passengers[0].Motion);
            Assert.AreEqual(landing, Flat(sim.Passengers[0]), "no sliding while lying");
            Advance(.3f);
            Assert.AreEqual(PassengerMotion.Dizzy, sim.Passengers[0].Motion);
            Advance(VoyageSimulation.DizzySeconds + .1f);
            Assert.AreEqual(PassengerMotion.Returning, sim.Passengers[0].Motion);
            Assert.Greater(sim.Passengers[0].Velocity.magnitude, 1f, "walking has a velocity the body faces");
            Advance(VoyageSimulation.ReturnTimeoutSeconds);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.That(Flat(sim.Passengers[0]), Is.EqualTo(sim.Seats[0]).Using(Vector2EqualityComparer.Instance));
            Assert.AreEqual(Vector3.zero, sim.Passengers[0].Velocity);
        }

        [Test]
        public void WalkingBackTakesTheDistanceNotTheTimeout()
        {
            ThrowOnDeck(0);
            while (sim.Passengers[0].Motion != PassengerMotion.Returning) Advance(.02f);
            float distance = Vector2.Distance(Flat(sim.Passengers[0]), sim.Seats[0]);
            float elapsed = 0;
            while (sim.Passengers[0].Motion == PassengerMotion.Returning && elapsed < 10) { Advance(.02f); elapsed += .02f; }
            Assert.Less(elapsed, VoyageSimulation.ReturnTimeoutSeconds - .5f, "arrived by walking, not by the timeout teleport");
            Assert.AreEqual(distance / VoyageSimulation.PassengerWalkSpeed, elapsed, .6f);
        }

        // ------------------------------------------------------------ put down

        [Test]
        public void PutDownPassengerStandsAShortWhileThenWalksToTheNearestFreeSeat()
        {
            Lift(0);
            PutDownAt(new Vector2(1.9f, 3.1f));       // beside the free seats on the right-hand side
            Assert.AreEqual(PassengerMotion.Standing, sim.Passengers[0].Motion);
            Assert.AreNotEqual(0, sim.Passengers[0].SeatClaim, "a free seat nearer than their own was claimed");
            int claimed = sim.SeatOf(0);
            Assert.GreaterOrEqual(claimed, 4, "seats 0-3 belong to the other boarded passengers");
            Advance(VoyageSimulation.StandingSeconds + .1f);
            Assert.AreEqual(PassengerMotion.Returning, sim.Passengers[0].Motion);
            Advance(VoyageSimulation.ReturnTimeoutSeconds);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.That(Flat(sim.Passengers[0]), Is.EqualTo(sim.Seats[claimed]).Using(Vector2EqualityComparer.Instance));
        }

        [Test]
        public void PutDownNextToTheirOwnSeatKeepsTheirOwnSeat()
        {
            Lift(0);
            PutDownAt(new Vector2(-3.4f, -2.2f));
            Assert.AreEqual(0, sim.SeatOf(0));
            Assert.AreEqual(0, sim.Passengers[0].SeatClaim);
        }

        [Test]
        public void TwoPassengersNeverClaimTheSameSeat()
        {
            Lift(0);
            PutDownAt(new Vector2(1.9f, 3.1f));
            Lift(1);
            PutDownAt(new Vector2(1.9f, 3.1f));
            Assert.AreNotEqual(sim.SeatOf(0), sim.SeatOf(1));
            var seen = new HashSet<int>();
            for (int i = 0; i < VoyageSimulation.MaxPassengers; i++)
                if (sim.Passengers[i].Active) Assert.IsTrue(seen.Add(sim.SeatOf(i)), "seat " + sim.SeatOf(i) + " claimed twice");
        }

        [Test]
        public void ClaimedSeatIsReleasedForTheNextVoyage()
        {
            Lift(0);
            PutDownAt(new Vector2(1.9f, 3.1f));
            Assert.AreNotEqual(0, sim.Passengers[0].SeatClaim);
            sim.State.Phase = VoyagePhase.Boarding;
            sim.State.Boarded = 0;
            for (int i = 0; i < VoyageSimulation.MaxPassengers; i++) sim.Passengers[i] = default;
            sim.Interact(Host, 0);
            Assert.AreEqual(0, sim.Passengers[0].SeatClaim);
        }

        // ------------------------------------------------------------ busy passengers

        [Test]
        public void PassengersAreBusyWhileHandledButCanBeLiftedAgainExceptWhenLying()
        {
            ThrowOnDeck(0);
            while (sim.Passengers[0].Motion != PassengerMotion.Landing) Advance(.02f);
            Advance(.4f);
            var near = new Vector2(sim.Passengers[0].Position.x - 1, sim.Passengers[0].Position.z);
            sim.Crew[0].Position = near;
            Advance(.4f);
            Assert.That(sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0), Does.Contain("lying"));
            Advance(.4f);
            Assert.That(sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, 0), Does.Contain("busy"));

            while (sim.Passengers[0].Motion != PassengerMotion.Returning) Advance(.02f);
            sim.Crew[0].Position = new Vector2(sim.Passengers[0].Position.x - 1, sim.Passengers[0].Position.z);
            Advance(.4f);
            Assert.That(sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, 0), Does.Contain("busy"));
            Advance(.4f);
            sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0);
            Assert.AreEqual(PassengerMotion.Carried, sim.Passengers[0].Motion, "a walking passenger can be lifted: the walk is cancelled");
            Assert.AreEqual(0, sim.CarriedPassenger(0));
        }

        [Test]
        public void ServiceWaitsUntilThePassengerSitsAgain()
        {
            Lift(0);
            PutDownAt(new Vector2(-3.4f, -2.2f));
            sim.Passengers[0].Request = ServiceItem.Food;
            sim.Crew[0].Position = sim.Stations[2].Position + Vector2.right;
            Advance(.4f);
            sim.Interact(Host, 2);                    // take the food
            sim.Crew[0].Position = new Vector2(sim.Passengers[0].Position.x - 1, sim.Passengers[0].Position.z);
            Advance(.4f);
            Assert.That(sim.Interact(Host, sim.Stations.Length + 0), Does.Contain("back on deck"));
            Assert.AreEqual(0, sim.State.Served);
        }

        // ------------------------------------------------------------ rescue

        [Test]
        public void RescuedPassengerClimbsBackDizzyThenWalksToTheSeat()
        {
            Lift(0);
            sim.Crew[0].Position = new Vector2(-5, -1.5f);
            Advance(.4f);
            sim.PassengerCommand(Host, -1, PassengerAction.Throw, -90);
            Advance(.2f);
            Assert.AreEqual(PassengerMotion.Rescuing, sim.Passengers[0].Motion);
            Advance(8.1f);
            Assert.AreEqual(PassengerMotion.Dizzy, sim.Passengers[0].Motion);
            Assert.Greater(sim.Passengers[0].Position.y, 0f, "back on the deck, not in the water");
            Assert.LessOrEqual(Mathf.Abs(sim.Passengers[0].Position.x), VoyageSimulation.DeckBounds.x + .01f);
            Advance(VoyageSimulation.DizzySeconds + .2f);
            Assert.AreEqual(PassengerMotion.Returning, sim.Passengers[0].Motion);
            Advance(VoyageSimulation.ReturnTimeoutSeconds);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.That(Flat(sim.Passengers[0]), Is.EqualTo(sim.Seats[0]).Using(Vector2EqualityComparer.Instance));
            Assert.AreEqual(1, sim.State.RescueCount);
        }

        // ------------------------------------------------------------ edge cases

        [Test]
        public void AStuckWalkerIsSeatedWhenTheTimeoutRunsOut()
        {
            // Walled in by stations on every side, the passenger cannot walk; the timeout puts them in the seat.
            var walled = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Food, new Vector2(3.2f, 2f)),
                new StationDefinition(StationKind.Drink, new Vector2(4.4f, 2.8f)),
                new StationDefinition(StationKind.Repair, new Vector2(4.4f, 1.2f)),
                new StationDefinition(StationKind.Helm, new Vector2(5.3f, 2f)),
                new StationDefinition(StationKind.Power, new Vector2(3.2f, 3.6f)),
                new StationDefinition(StationKind.Ammo, new Vector2(3.2f, .4f)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
            walled.Join(Host);
            walled.Crew[0].Position = new Vector2(-3, -3.5f);
            for (int i = 0; i < 20; i++) walled.Tick(.02f);
            walled.Interact(Host, 0);
            walled.Passengers[0].Motion = PassengerMotion.Returning;
            walled.Passengers[0].RecoveryRemaining = VoyageSimulation.ReturnTimeoutSeconds;
            walled.Passengers[0].Position = new Vector3(4f, .5f, 2f);
            for (float t = 0; t < VoyageSimulation.ReturnTimeoutSeconds + .5f; t += .02f) walled.Tick(.02f);
            Assert.AreEqual(PassengerMotion.OnDeck, walled.Passengers[0].Motion);
            Assert.AreEqual(walled.Seats[0], new Vector2(walled.Passengers[0].Position.x, walled.Passengers[0].Position.z));
        }

        [Test]
        public void ArrivalWhileHandledSeatsEveryoneAtTheirClaimedSeat()
        {
            Lift(0);
            PutDownAt(new Vector2(1.9f, 3.1f));
            int claimed = sim.SeatOf(0);
            Lift(1);
            PutDownAt(new Vector2(0, -5));
            sim.Crew[0].Position = Vector2.zero;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = VoyageSimulation.Destination;
            sim.State.Speed = 0;
            sim.Crew[0].AtHelm = true;
            Advance(.4f);
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[1].Motion);
            Assert.AreEqual(sim.Seats[claimed], Flat(sim.Passengers[0]));
        }

        [Test]
        public void MigrationKeepsTheClaimedSeatAndTheWalk()
        {
            Lift(0);
            PutDownAt(new Vector2(1.9f, 3.1f));
            Advance(VoyageSimulation.StandingSeconds + .5f);
            Assert.AreEqual(PassengerMotion.Returning, sim.Passengers[0].Motion);
            byte[] data = sim.ExportMigrationData();

            var restored = new VoyageSimulation(sim.Stations, sim.Seats);
            restored.ImportMigrationData(data);
            Assert.AreEqual(PassengerMotion.Returning, restored.Passengers[0].Motion);
            Assert.AreEqual(sim.SeatOf(0), restored.SeatOf(0));
            for (float t = 0; t < VoyageSimulation.ReturnTimeoutSeconds; t += .02f) restored.Tick(.02f);
            Assert.AreEqual(PassengerMotion.OnDeck, restored.Passengers[0].Motion);
            Assert.AreEqual(restored.Seats[restored.SeatOf(0)], Flat(restored.Passengers[0]));
        }

        // ------------------------------------------------------------ presentation

        [TestCase(PassengerMotion.Landing, PassengerPose.Lying)]
        [TestCase(PassengerMotion.Dizzy, PassengerPose.Dizzy)]
        [TestCase(PassengerMotion.Standing, PassengerPose.Standing)]
        [TestCase(PassengerMotion.Returning, PassengerPose.Walking)]
        [TestCase(PassengerMotion.Carried, PassengerPose.Panicked)]
        [TestCase(PassengerMotion.Airborne, PassengerPose.Panicked)]
        [TestCase(PassengerMotion.Rescuing, PassengerPose.Panicked)]
        public void EachHandlingStateHasItsOwnPose(PassengerMotion motion, PassengerPose expected)
        {
            var p = new PassengerState { Active = true, Motion = motion, Reaction = PassengerReaction.Thanks, ReactionRemaining = 3 };
            Assert.AreEqual(expected, PassengerPresentation.PoseFor(p, PassengerCue.Relaxed), "a reaction never overrides the pose of a handled passenger");
        }

        [Test]
        public void ThePromptOffersALiftForAWalkingPassenger()
        {
            var p = new PassengerState { Active = true, Motion = PassengerMotion.Returning };
            var prompt = PromptCatalog.ForPassenger(2, p);
            Assert.AreEqual(HudControl.Carry, prompt.Key);
            Assert.AreEqual("Lift passenger", prompt.Action);
            Assert.That(prompt.Detail, Does.Contain("seat"));
        }

        [Test]
        public void LandingTriggersTheFeedbackOnce()
        {
            var before = FeedbackSnapshot.Create();
            var after = FeedbackSnapshot.Create();
            before.Valid = after.Valid = true;
            before.Passengers[3] = new PassengerSnapshot { Active = true, Motion = PassengerMotion.Airborne };
            after.Passengers[3] = new PassengerSnapshot { Active = true, Motion = PassengerMotion.Landing };
            var events = new List<FeedbackEvent>();
            FeedbackDetector.Detect(before, after, events);
            Assert.IsTrue(events.Exists(e => e.Kind == FeedbackKind.PassengerLanded && e.Slot == 3));
            events.Clear();
            FeedbackDetector.Detect(after, after, events);
            Assert.IsFalse(events.Exists(e => e.Kind == FeedbackKind.PassengerLanded));
        }

        private sealed class Vector2EqualityComparer : IEqualityComparer<Vector2>
        {
            public static readonly Vector2EqualityComparer Instance = new Vector2EqualityComparer();
            public bool Equals(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .01f;
            public int GetHashCode(Vector2 v) => v.GetHashCode();
        }
    }
}
