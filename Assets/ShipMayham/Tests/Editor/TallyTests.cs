using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class TallyTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;

        [SetUp]
        public void SetUp()
        {
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
                new StationDefinition(StationKind.Repair, new Vector2(4, -3)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
            sim.Join(Host);
        }

        // Interactions share a short cooldown.
        private void Wait() { for (int i = 0; i < 5; i++) sim.Tick(.1f); }

        private void BoardPassengers()
        {
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Wait();
            Assert.That(sim.Interact(Host, 0), Does.StartWith("Passengers aboard"));
            Wait();
        }

        [Test]
        public void ServingAnOrderCountsForTheServer()
        {
            BoardPassengers();
            sim.Crew[0].Position = new Vector2(-3, 0);
            Assert.That(sim.Interact(Host, 2), Is.Not.Empty);       // take the food for passenger 0
            Wait();
            sim.Crew[0].Position = new Vector2(-1.2f, -1f);
            Assert.That(sim.Interact(Host, sim.Stations.Length + 0), Does.StartWith("Order delivered"));
            Assert.AreEqual(1, sim.Tallies[0].Orders);
        }

        [Test]
        public void ThrowingAPassengerCounts()
        {
            BoardPassengers();
            sim.Crew[0].Position = new Vector2(-1.2f, -1f);
            sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0);
            Wait();
            Assert.That(sim.PassengerCommand(Host, -1, PassengerAction.Throw, 0), Does.StartWith("Wheee"));
            Assert.AreEqual(1, sim.Tallies[0].Throws);
        }

        [Test]
        public void CollectingAnUnpaidFareCountsButCheckingAloneDoesNot()
        {
            BoardPassengers();
            sim.Crew[0].Position = new Vector2(-1.2f, -1f);
            sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, 0);       // passenger 0 has not paid
            Wait();
            Assert.AreEqual(0, sim.Tallies[0].Fares);
            sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, 0);
            Assert.AreEqual(1, sim.Tallies[0].Fares);
        }

        [Test]
        public void PatchingTheLeakCountsEveryStep()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Leak = 1;
            sim.Crew[0].Position = new Vector2(3.5f, -3);
            for (int step = 0; step < 4; step++)
            {
                Wait();
                sim.Interact(Host, 3);
            }
            Assert.AreEqual(4, sim.Tallies[0].Repairs);
        }

        [Test]
        public void HelmTimeCountsOnlyWhileSailing()
        {
            sim.Crew[0].Position = new Vector2(1, 6.8f);
            sim.Interact(Host, 1);
            Assert.IsTrue(sim.Crew[0].AtHelm);
            for (int i = 0; i < 100; i++) sim.Tick(.1f);                     // in port: 10 s that do not count
            Assert.AreEqual(0, sim.Tallies[0].HelmSeconds);
            sim.State.Phase = VoyagePhase.Sailing;
            for (int i = 0; i < 100; i++) sim.Tick(.1f);
            Assert.That(sim.Tallies[0].HelmSeconds, Is.InRange(9, 10));
        }

        [Test]
        public void ANewVoyageStartsFromZeroAndLeavingClearsTheSlot()
        {
            sim.Tallies[0].Add(TallyKind.Orders, 5);
            sim.State.Phase = VoyagePhase.Arrived;
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Wait();
            sim.Interact(Host, 0);
            Assert.IsTrue(sim.Tallies[0].IsEmpty, "The next voyage starts with a clean slate.");

            sim.Tallies[0].Add(TallyKind.Hits, 2);
            sim.Leave(Host);
            Assert.IsTrue(sim.Tallies[0].IsEmpty, "A slot that is vacated forgets its tally.");
        }

        [Test]
        public void TalliesSurviveHostMigration()
        {
            sim.Tallies[0].Add(TallyKind.Throws, 3);
            sim.Tallies[0].HelmSeconds = 42;
            var restored = new VoyageSimulation(System.Array.Empty<StationDefinition>(), new Vector2[8]);
            restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.AreEqual(3, restored.Tallies[0].Throws);
            Assert.AreEqual(42, restored.Tallies[0].HelmSeconds);
        }
    }
}
