using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>Passengers walk aboard along the pier and the gangway and walk off again.</summary>
    public sealed class PassengerFlowTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;
        private bool walkingBefore;
        private const float DeckGateEdge = 5.0f;

        [SetUp]
        public void SetUp()
        {
            walkingBefore = VoyageSimulation.WalkingPassengers;
            VoyageSimulation.WalkingPassengers = true;
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            }, true);
            sim.Join(Host);
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        private void Advance(float seconds)
        {
            for (int n = 0; n < Mathf.CeilToInt(seconds / .02f); n++) sim.Tick(.02f);
        }

        private void Board()
        {
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Advance(.4f);
            Assert.That(sim.Interact(Host, 0), Does.Contain("coming aboard"));
        }

        private bool AllSeated()
        {
            for (int i = 0; i < sim.State.Boarded; i++) if (sim.Passengers[i].Motion != PassengerMotion.OnDeck) return false;
            return true;
        }

        private float WaitUntilSeated(float limit = 20f)
        {
            float elapsed = 0;
            while (!AllSeated() && elapsed < limit) { Advance(.1f); elapsed += .1f; }
            return elapsed;
        }

        // ------------------------------------------------------------ boarding

        [Test]
        public void PassengersQueueOnThePierInsteadOfAppearingInTheirSeats()
        {
            Board();
            for (int i = 0; i < sim.State.Boarded; i++)
            {
                var p = sim.Passengers[i];
                Assert.AreEqual(PassengerMotion.Boarding, p.Motion, "passenger " + i);
                Assert.Less(p.Position.x, VoyageSimulation.PierLaneStart.x + .01f, "passenger " + i + " starts beyond the deck edge");
                Assert.AreEqual(VoyageSimulation.PierLaneStart.y, p.Position.z, .01f, "in a line on the lane");
                Assert.Greater(Vector2.Distance(new Vector2(p.Position.x, p.Position.z), sim.Seats[i]), 5f);
            }
            Assert.Greater(sim.Passengers[0].Position.x, sim.Passengers[3].Position.x, "the queue is ordered");
        }

        [Test]
        public void PassengersWalkThroughTheGateAndSitWithoutAJump()
        {
            Board();
            Vector3[] last = new Vector3[sim.State.Boarded];
            for (int i = 0; i < last.Length; i++) last[i] = sim.Passengers[i].Position;
            float biggestStep = 0, elapsed = 0;
            string worst = string.Empty;
            bool crossedGate = false;
            var gateSeen = new bool[sim.State.Boarded];
            while (!AllSeated() && elapsed < 20f)
            {
                Advance(.02f); elapsed += .02f;
                for (int i = 0; i < last.Length; i++)
                {
                    var p = sim.Passengers[i];
                    float step = Vector3.Distance(last[i], p.Position);
                    if (step > biggestStep) { biggestStep = step; worst = $"passenger {i} at {elapsed:0.00}s {last[i]} -> {p.Position} motion {p.Motion}"; }
                    last[i] = p.Position;
                    if (!gateSeen[i] && p.Position.x >= -DeckGateEdge)
                    {
                        gateSeen[i] = true;
                        crossedGate = true;
                        Assert.AreEqual(VoyageSimulation.GateInside.y, p.Position.z, .3f, "they step onto the deck at the gangway, not through the rail");
                    }
                }
            }
            Assert.IsTrue(crossedGate);
            Assert.IsTrue(AllSeated(), "everyone is seated");
            Assert.Less(biggestStep, VoyageSimulation.GangwayWalkSpeed * .02f + VoyageSimulation.SeatArrivalDistance + .001f, "no frame moves a passenger further than walking speed allows: no teleports. " + worst);
            for (int i = 0; i < sim.State.Boarded; i++)
                Assert.That(Vector2.Distance(new Vector2(sim.Passengers[i].Position.x, sim.Passengers[i].Position.z), sim.Seats[i]), Is.LessThan(.01f));
            Assert.Less(elapsed, 12f, "boarding a full ship takes about ten seconds at most");
        }

        [Test]
        public void TheShipCannotDepartWhileThePassengersAreStillBoarding()
        {
            Board();
            sim.Crew[0].Position = sim.Stations[1].Position + Vector2.right;
            Advance(.4f);
            sim.Interact(Host, 1);
            Assert.That(sim.HelmAction(Host), Does.Contain("still boarding"));
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
            WaitUntilSeated();
            Assert.That(sim.HelmAction(Host), Does.Contain("Under way"));
        }

        [Test]
        public void BoardingPassengersCannotBeCheckedServedOrLifted()
        {
            Board();
            Advance(.5f);
            sim.Crew[0].Position = new Vector2(sim.Passengers[0].Position.x - 1, sim.Passengers[0].Position.z);
            Advance(.4f);
            Assert.That(sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, 0), Does.Contain("busy"));
            Advance(.4f);
            sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0);
            Assert.AreNotEqual(PassengerMotion.Carried, sim.Passengers[0].Motion);
            Assert.False(VoyageSimulation.CanBeLifted(PassengerMotion.Boarding));
            Assert.False(VoyageSimulation.CanBeLifted(PassengerMotion.Leaving));
        }

        [Test]
        public void ShipsWithoutAHarborStillSeatPassengersInstantly()
        {
            var plain = new VoyageSimulation(sim.Stations, sim.Seats, false);
            plain.Join(Host);
            plain.Crew[0].Position = new Vector2(-3, -3.5f);
            for (int i = 0; i < 20; i++) plain.Tick(.02f);
            plain.Interact(Host, 0);
            Assert.AreEqual(PassengerMotion.OnDeck, plain.Passengers[0].Motion);
            Assert.AreEqual(plain.Seats[0], new Vector2(plain.Passengers[0].Position.x, plain.Passengers[0].Position.z));
        }

        // ------------------------------------------------------------ arriving

        private void Sail()
        {
            Board();
            WaitUntilSeated();
            sim.Crew[0].Position = sim.Stations[1].Position + Vector2.right;
            Advance(.4f);
            sim.Interact(Host, 1);
            sim.HelmAction(Host);
            sim.State.Position = VoyageSimulation.Destination;
            sim.State.Speed = 0;
            Advance(.4f);
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase);
        }

        [Test]
        public void PassengersWalkOffTheShipAfterArrivalAndOnlyThenCanANewVoyageStart()
        {
            Sail();
            for (int i = 0; i < sim.State.Boarded; i++) Assert.AreEqual(PassengerMotion.Leaving, sim.Passengers[i].Motion);
            Assert.IsTrue(sim.AnyPassengerLeaving);
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Advance(.4f);
            Assert.That(sim.Interact(Host, 0), Does.Contain("left the ship"));

            float elapsed = 0;
            Vector3[] last = new Vector3[sim.State.Boarded];
            for (int i = 0; i < last.Length; i++) last[i] = sim.Passengers[i].Position;
            float biggestStep = 0;
            while (sim.AnyPassengerLeaving && elapsed < 25f)
            {
                Advance(.02f); elapsed += .02f;
                for (int i = 0; i < last.Length; i++)
                {
                    if (!sim.Passengers[i].Active) continue;
                    biggestStep = Mathf.Max(biggestStep, Vector3.Distance(last[i], sim.Passengers[i].Position));
                    last[i] = sim.Passengers[i].Position;
                }
            }
            Assert.IsFalse(sim.AnyPassengerLeaving);
            Assert.Less(biggestStep, VoyageSimulation.GangwayWalkSpeed * .02f + VoyageSimulation.SeatArrivalDistance + .001f, "they walk off, they do not vanish from their seats");
            for (int i = 0; i < sim.State.Boarded; i++) Assert.IsFalse(sim.Passengers[i].Active);
            Advance(.4f);
            Assert.That(sim.Interact(Host, 0), Does.Contain("Back at the departure port"));
        }

        [Test]
        public void ThePaidTicketsAndTheReportDoNotDependOnWhoIsStillOnDeck()
        {
            sim.State.Voyage = 2;                                      // a voyage in which every passenger has paid
            Board();
            WaitUntilSeated();
            for (int i = 0; i < sim.State.Boarded; i++) sim.Passengers[i].FarePaid = true;
            sim.Crew[0].Position = sim.Stations[1].Position + Vector2.right;
            Advance(.4f);
            sim.Interact(Host, 1);
            sim.HelmAction(Host);
            sim.State.Position = VoyageSimulation.Destination;
            sim.State.Speed = 0;
            Advance(.4f);
            sim.HelmAction(Host);
            int tickets = sim.State.LastTickets;
            Advance(20f);
            Assert.AreEqual(tickets, sim.State.LastTickets);
            Assert.AreEqual(4 * 40, sim.State.LastTickets);
        }

        [Test]
        public void ACarriedPassengerIsPutDownAtArrivalAndLeavesWithTheOthers()
        {
            Board();
            WaitUntilSeated();
            sim.Crew[0].Position = new Vector2(sim.Passengers[0].Position.x - 1, sim.Passengers[0].Position.z);
            Advance(.4f);
            sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0);
            Assert.AreEqual(PassengerMotion.Carried, sim.Passengers[0].Motion);
            sim.Crew[0].Position = sim.Stations[1].Position + Vector2.right;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = VoyageSimulation.Destination;
            sim.State.Speed = 0;
            sim.Crew[0].AtHelm = true;
            Advance(.4f);
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase);
            Assert.AreEqual(PassengerMotion.Leaving, sim.Passengers[0].Motion);
            Assert.AreEqual(-1, sim.Passengers[0].CarrierSlot);
            Assert.AreEqual(-1, sim.CarriedPassenger(0));
        }

        [Test]
        public void WalkingStatesSurviveMigration()
        {
            Board();
            Advance(1f);
            byte[] data = sim.ExportMigrationData();
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true);
            restored.ImportMigrationData(data);
            Assert.AreEqual(PassengerMotion.Boarding, restored.Passengers[3].Motion);
            for (float t = 0; t < 15f; t += .1f) restored.Tick(.1f);
            for (int i = 0; i < restored.State.Boarded; i++) Assert.AreEqual(PassengerMotion.OnDeck, restored.Passengers[i].Motion);
        }

        // ------------------------------------------------------------ presentation

        [TestCase(PassengerMotion.Boarding)] [TestCase(PassengerMotion.Leaving)]
        public void WalkingOnAndOffUsesTheWalkingPose(PassengerMotion motion)
        {
            var p = new PassengerState { Active = true, Motion = motion };
            Assert.AreEqual(PassengerPose.Walking, PassengerPresentation.PoseFor(p, PassengerCue.Relaxed));
            Assert.IsNotEmpty(PassengerPresentation.ReactionText(p));
        }
    }
}
