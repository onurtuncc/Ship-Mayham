using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class VoyageEventTests
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
                new StationDefinition(StationKind.Drink, new Vector2(-4, 3)),
                new StationDefinition(StationKind.Repair, new Vector2(4, -3)),
                new StationDefinition(StationKind.Power, new Vector2(4, 0))
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
            sim.Join(Host);
            UseStation(0);
            UseStation(1);
            sim.HelmAction(Host);
            Advance(.4f);
            sim.Interact(Host, 1);
        }

        private void Advance(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) sim.Tick(.05f);
        }

        private string UseStation(int station)
        {
            sim.Crew[0].Position = sim.Stations[station].Position + Vector2.right;
            Advance(.4f);
            return sim.Interact(Host, station);
        }

        private string CheckPassenger(int passenger)
        {
            Vector3 position = sim.Passengers[passenger].Position;
            sim.Crew[0].Position = new Vector2(position.x - 1, position.z);
            Advance(.4f);
            return sim.PassengerCommand(Host, passenger, PassengerAction.CheckTicket, 0);
        }

        private void ServeAnyOrder()
        {
            for (int passenger = 0; passenger < sim.State.Boarded; passenger++)
            {
                ServiceItem request = sim.Passengers[passenger].Request;
                if (request == ServiceItem.None) continue;
                UseStation(request == ServiceItem.Food ? 2 : 3);
                sim.Crew[0].Position = new Vector2(sim.Passengers[passenger].Position.x - 1, sim.Passengers[passenger].Position.z);
                Advance(.4f);
                sim.Interact(Host, sim.Stations.Length + passenger);
                return;
            }
            Assert.Fail("Expected at least one passenger order.");
        }

        [Test]
        public void EventDeckCyclesThroughThreeCooperativeTasks()
        {
            Advance(VoyageSimulation.FirstEventDelay + .1f);
            Assert.AreEqual(VoyageEventKind.ContrabandInspection, sim.Event.Kind);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase);

            int suspect = sim.Event.TargetPassenger;
            string result = CheckPassenger(suspect);
            Assert.That(result, Does.Contain("Contraband found"));
            Assert.AreEqual(1, sim.Event.ResolvedCount);
            Assert.AreEqual(15, sim.Event.Reward);

            Advance(VoyageSimulation.EventGap + .1f);
            Assert.AreEqual(VoyageEventKind.GalleyRush, sim.Event.Kind);
            ServeAnyOrder();
            ServeAnyOrder();
            Assert.AreEqual(2, sim.Event.Progress);
            Assert.AreEqual(2, sim.Event.ResolvedCount);

            Advance(VoyageSimulation.EventGap + .1f);
            Assert.AreEqual(VoyageEventKind.LooseCargo, sim.Event.Kind);
            if (sim.State.Leak != 0) for (int i = 0; i < 4; i++) UseStation(4);
            for (int i = 0; i < 3; i++) UseStation(4);
            Assert.AreEqual(3, sim.Event.Progress);
            Assert.AreEqual(3, sim.Event.ResolvedCount);
            Assert.AreEqual(45, sim.Event.Reward);
        }

        [Test]
        public void IgnoredContrabandEventDamagesShipAndCutsPower()
        {
            Advance(VoyageSimulation.FirstEventDelay + VoyageSimulation.EventDuration + .2f);
            Assert.AreEqual(1, sim.Event.FailedCount);
            Assert.AreEqual(VoyageEventPhase.Failed, sim.Event.Phase);
            Assert.LessOrEqual(sim.State.Health, 95);
            Assert.IsTrue(sim.Encounter.PowerOut);
        }

        [Test]
        public void ContrabandTargetCanBeRecheckedAfterNormalTicketInspection()
        {
            int futureSuspect = 2;
            CheckPassenger(futureSuspect);
            Advance(VoyageSimulation.FirstEventDelay + .1f);

            string result = CheckPassenger(futureSuspect);

            Assert.That(result, Does.Contain("Contraband found"));
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        [Test]
        public void EventStateAndDeckPositionSurviveHostMigration()
        {
            Advance(VoyageSimulation.FirstEventDelay + .1f);
            int suspect = sim.Event.TargetPassenger;
            CheckPassenger(suspect);
            Advance(VoyageSimulation.EventGap + .1f);

            var restored = new VoyageSimulation(sim.Stations, sim.Seats);
            restored.ImportMigrationData(sim.ExportMigrationData());

            Assert.AreEqual(sim.Event, restored.Event);
            Assert.AreEqual(VoyageEventKind.GalleyRush, restored.Event.Kind);
            Assert.AreEqual(VoyageEventPhase.Active, restored.Event.Phase);
        }
    }
}
