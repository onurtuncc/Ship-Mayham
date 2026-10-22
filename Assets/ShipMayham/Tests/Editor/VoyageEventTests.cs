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
        public void EventDeckOffersEightPlayableTasksBeforeRepeating()
        {
            Advance(VoyageSimulation.FirstEventDelay + .1f);
            var seen = new System.Collections.Generic.HashSet<VoyageEventKind>();
            for (int card = 0; card < 8; card++)
            {
                Assert.IsTrue(seen.Add(sim.Event.Kind), "Repeated a card before exhausting the deck.");
                var kind = sim.Event.Kind;
                int target = sim.Event.TargetPassenger;
                RepairShip();
                switch (kind)
                {
                    case VoyageEventKind.ContrabandInspection: CheckPassenger(target); break;
                    case VoyageEventKind.TicketAudit: CheckPassenger(0); CheckPassenger(1); break;
                    case VoyageEventKind.VipService: ServePassenger(target); break;
                    case VoyageEventKind.GalleyRush: ServeAnyOrder(); ServeAnyOrder(); break;
                    case VoyageEventKind.LooseCargo: for (int i = 0; i < 3; i++) UseStation(4); break;
                    case VoyageEventKind.EngineOverheat: UseStation(4); UseStation(4); break;
                    case VoyageEventKind.SeatShuffle:
                        sim.Crew[0].Position = sim.Seats[target] + Vector2.right;
                        Advance(.4f); sim.PassengerCommand(Host, target, PassengerAction.CarryOrDrop, 0);
                        sim.Crew[0].Position = new Vector2(0, -5);
                        Advance(.4f); sim.PassengerCommand(Host, target, PassengerAction.CarryOrDrop, 0);
                        break;
                    case VoyageEventKind.FuseOverload: break; // RepairShip uses the real power panel.
                    default: Assert.Fail("Unexpected card."); break;
                }
                Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase, kind.ToString());
                if (card < 7) Advance(VoyageSimulation.EventGap + .1f);
            }
            Assert.AreEqual(8, sim.Event.ResolvedCount);
            Assert.AreEqual(120, sim.Event.Reward);
        }

        [Test]
        public void IgnoredContrabandEventDamagesShipAndCutsPower()
        {
            StartSpecificEvent(VoyageEventKind.ContrabandInspection);
            int failures = sim.Event.FailedCount;
            Advance(VoyageSimulation.EventDuration + .2f);
            Assert.AreEqual(failures + 1, sim.Event.FailedCount);
            Assert.AreEqual(VoyageEventPhase.Failed, sim.Event.Phase);
            Assert.LessOrEqual(sim.State.Health, 95);
            Assert.IsTrue(sim.Encounter.PowerOut);
        }

        [Test]
        public void ContrabandTargetCanBeRecheckedAfterNormalTicketInspection()
        {
            StartSpecificEvent(VoyageEventKind.ContrabandInspection);
            int futureSuspect = sim.Event.TargetPassenger;
            var activeEvent = sim.Event;
            sim.Event.Phase = VoyageEventPhase.Waiting;
            sim.Event.NextIn = 100;
            CheckPassenger(futureSuspect);
            sim.Event = activeEvent;

            string result = CheckPassenger(futureSuspect);

            Assert.That(result, Does.Contain("Contraband found"));
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        [Test]
        public void EventStateAndDeckPositionSurviveHostMigration()
        {
            StartSpecificEvent(VoyageEventKind.TicketAudit);
            CheckPassenger(0);

            var restored = new VoyageSimulation(sim.Stations, sim.Seats);
            restored.ImportMigrationData(sim.ExportMigrationData());

            Assert.AreEqual(sim.Event, restored.Event);
            for (int card = 0; card < 8; card++)
            {
                sim.Event.Remaining = restored.Event.Remaining = .01f;
                TickBoth(restored, .1f);
                sim.State.Health = restored.State.Health = 100;
                TickBoth(restored, VoyageSimulation.EventGap + .1f);
                Assert.AreEqual(sim.Event, restored.Event);
            }
        }

        private void TickBoth(VoyageSimulation other, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) { sim.Tick(.05f); other.Tick(.05f); }
        }

        private void StartSpecificEvent(VoyageEventKind kind)
        {
            Advance(VoyageSimulation.FirstEventDelay + .1f);
            for (int i = 0; i < 8 && sim.Event.Kind != kind; i++)
            {
                sim.Event.Remaining = .01f; sim.Tick(.05f);
                sim.State.Health = 100;
                Advance(VoyageSimulation.EventGap + .1f);
            }
            Assert.AreEqual(kind, sim.Event.Kind);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase);
            sim.State.Health = 100;
        }

        private void RepairShip()
        {
            if (sim.State.Leak != 0) for (int i = 0; i < 4; i++) UseStation(4);
            if (sim.Encounter.PowerOut) for (int i = 0; i < 3; i++) UseStation(5);
        }

        private void ServePassenger(int passenger)
        {
            ServiceItem request = sim.Passengers[passenger].Request;
            Assert.AreNotEqual(ServiceItem.None, request);
            UseStation(request == ServiceItem.Food ? 2 : 3);
            sim.Crew[0].Position = new Vector2(sim.Passengers[passenger].Position.x - 1, sim.Passengers[passenger].Position.z);
            Advance(.4f);
            sim.Interact(Host, sim.Stations.Length + passenger);
        }

        [Test] public void AuditRequiresDifferentPassengers()
        {
            StartSpecificEvent(VoyageEventKind.TicketAudit);
            CheckPassenger(0); CheckPassenger(0);
            Assert.AreEqual(1, sim.Event.Progress);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase);
            CheckPassenger(1);
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        [Test] public void AuditDoesNotBlockCollectingAnUnpaidFare()
        {
            StartSpecificEvent(VoyageEventKind.TicketAudit);
            Assert.IsFalse(sim.Passengers[0].FarePaid);
            CheckPassenger(0);
            CheckPassenger(0);
            Assert.IsTrue(sim.Passengers[0].FarePaid);
            Assert.AreEqual(1, sim.State.CollectedFares);
            Assert.AreEqual(1, sim.Event.Progress);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase);
        }

        [Test] public void VipRequiresTheNamedPassengersOrder()
        {
            StartSpecificEvent(VoyageEventKind.VipService);
            RepairShip();
            int target = sim.Event.TargetPassenger;
            ServePassenger((target + 1) % sim.State.Boarded);
            Assert.AreEqual(0, sim.Event.Progress);
            ServePassenger(target);
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        [Test] public void EngineCanBeVentedOnlyAfterCaptainSlowsDown()
        {
            StartSpecificEvent(VoyageEventKind.EngineOverheat);
            RepairShip(); sim.State.Speed = 2;
            Assert.That(UseStation(4), Does.Contain("slow"));
            Assert.AreEqual(0, sim.Event.Progress);
            sim.State.Speed = 0;
            UseStation(4); UseStation(4);
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        [Test] public void EngineAlsoCoolsByStayingSlowWithoutRepairs()
        {
            StartSpecificEvent(VoyageEventKind.EngineOverheat);
            sim.State.Speed = 0; Advance(4);
            sim.State.Speed = 2; sim.Tick(.05f);
            Assert.AreEqual(0, sim.Event.CoolingTime);
            sim.State.Speed = 0; Advance(4);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase);
            Advance(4.2f);
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        private VoyageEventKind FirstCard(RouteProfile route, uint seed)
        {
            var voyage = new VoyageSimulation(sim.Stations, sim.Seats, eventSeed: seed);
            voyage.State.Phase = VoyagePhase.Sailing;
            voyage.State.Route = route;
            voyage.State.Boarded = sim.State.Boarded;
            System.Array.Copy(sim.Passengers, voyage.Passengers, sim.Passengers.Length);
            for (int i = 0; i < 365; i++) voyage.Tick(.05f);
            return voyage.Event.Kind;
        }

        [Test] public void RouteWeightsFavorTheirContractsAcrossSessionSeeds()
        {
            int coastalService = 0, marketService = 0, coastalTechnical = 0, stormTechnical = 0;
            for (uint seed = 1; seed <= 256; seed++)
            {
                var coast = FirstCard(RouteProfile.CoastalFerry, seed);
                var market = FirstCard(RouteProfile.MarketRun, seed);
                var storm = FirstCard(RouteProfile.StormPassage, seed);
                if (coast == VoyageEventKind.VipService || coast == VoyageEventKind.GalleyRush) coastalService++;
                if (market == VoyageEventKind.VipService || market == VoyageEventKind.GalleyRush) marketService++;
                if (coast == VoyageEventKind.LooseCargo || coast == VoyageEventKind.EngineOverheat || coast == VoyageEventKind.FuseOverload) coastalTechnical++;
                if (storm == VoyageEventKind.LooseCargo || storm == VoyageEventKind.EngineOverheat || storm == VoyageEventKind.FuseOverload) stormTechnical++;
            }
            Assert.Greater(marketService, coastalService + 30);
            Assert.Greater(stormTechnical, coastalTechnical + 30);
        }

        [Test] public void RushOrdersRespectTheAvailableMixOfSupplies()
        {
            sim.State.FoodStock = 1; sim.State.DrinkStock = 1;
            for (int p = 0; p < sim.State.Boarded; p++) sim.Passengers[p].Request = ServiceItem.None;
            StartSpecificEvent(VoyageEventKind.GalleyRush);
            Assert.AreEqual(ServiceItem.Food, sim.Passengers[0].Request);
            Assert.AreEqual(ServiceItem.Drink, sim.Passengers[1].Request);
            ServePassenger(0); ServePassenger(1);
            Assert.AreEqual(VoyageEventPhase.Resolved, sim.Event.Phase);
        }

        [Test] public void MissingStationsExcludeImpossibleTasks()
        {
            var limited = new VoyageSimulation(System.Array.Empty<StationDefinition>(), sim.Seats);
            limited.State.Phase = VoyagePhase.Sailing;
            limited.State.Boarded = 1;
            limited.Passengers[0] = new PassengerState { Active = true, Satisfaction = 100 };
            for (int card = 0; card < 9; card++)
            {
                for (int t = 0; t < 500; t++) limited.Tick(.05f);
                Assert.That(limited.Event.Kind, Is.EqualTo(VoyageEventKind.ContrabandInspection)
                    .Or.EqualTo(VoyageEventKind.TicketAudit).Or.EqualTo(VoyageEventKind.SeatShuffle));
                limited.Event.Remaining = .01f; limited.Tick(.05f); limited.State.Health = 100;
            }
        }
    }
}
