using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class PreparationPhaseTests
    {
        private static VoyageSimulation Make()
        {
            var sim = NavigationPhaseTests.Create(); sim.State.Phase = VoyagePhase.Boarding;
            sim.Crew[0].Position = new Vector2(-3, -4); sim.State.Wallet = 500; return sim;
        }
        private static void Wait(VoyageSimulation sim) { for (int i = 0; i < 25; i++) sim.Tick(.02f); }
        [Test] public void EquipmentBuysOnceAndSwitchingCannotFarmKits()
        {
            var sim = Make(); sim.PrepareVoyage(10, PreparationAction.Workshop);
            Assert.AreEqual(410, sim.State.Wallet); Assert.AreEqual(5, sim.State.RepairKitStock);
            Wait(sim); sim.PrepareVoyage(10, PreparationAction.ServiceTray);
            Wait(sim); sim.PrepareVoyage(10, PreparationAction.Workshop);
            Assert.AreEqual(320, sim.State.Wallet); Assert.AreEqual(5, sim.State.RepairKitStock);
            Assert.AreEqual(3, VoyageSimulation.RepairBufferLimit(sim.State));
        }
        [Test] public void TrayCarriesTwoServesOneAndReturnsTheRemainder()
        {
            var sim = Make(); sim.PrepareVoyage(10, PreparationAction.ServiceTray); Wait(sim); sim.Interact(10, 0);
            sim.Crew[0].Position = new Vector2(-3, 0); Wait(sim); sim.Interact(10, 2);
            Assert.AreEqual(14, sim.State.FoodStock); Assert.AreEqual(2, sim.Crew[0].ServiceUnits);
            sim.Crew[0].Position = new Vector2(-3, -1.5f); Wait(sim); sim.Interact(10, sim.Stations.Length);
            Assert.AreEqual(1, sim.State.Served); Assert.AreEqual(1, sim.Crew[0].ServiceUnits);
            Assert.AreEqual(ServiceItem.Food, sim.Crew[0].Carrying);
            sim.Leave(10); Assert.AreEqual(15, sim.State.FoodStock);
        }
        [Test] public void NewVoyageClearsTheOldTrayQuantity()
        {
            var sim = Make(); sim.State.Phase = VoyagePhase.Arrived;
            sim.Crew[0].Carrying = ServiceItem.Food; sim.Crew[0].ServiceUnits = 2;
            sim.Interact(10, 0);
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            Assert.AreEqual(0, sim.Crew[0].ServiceUnits);
        }
        [Test] public void PennantCostsOnceAndHasNoMechanicalEffect()
        {
            var sim = Make(); sim.PrepareVoyage(10, PreparationAction.Pennant);
            Assert.AreEqual(440, sim.State.Wallet); Assert.IsTrue(sim.State.PennantVisible);
            Wait(sim); sim.PrepareVoyage(10, PreparationAction.Pennant);
            Assert.AreEqual(440, sim.State.Wallet); Assert.IsFalse(sim.State.PennantVisible);
            Assert.AreEqual(100, sim.State.Health); Assert.AreEqual(0, sim.State.EngineLevel);
        }
        [Test] public void RemotePoorOrSailingPlayersCannotPrepare()
        {
            var sim = Make(); sim.Crew[0].Position = Vector2.zero; sim.PrepareVoyage(10, PreparationAction.ServiceTray);
            Assert.AreEqual(500, sim.State.Wallet);
            sim.Crew[0].Position = new Vector2(-3,-4); sim.State.Wallet = 0; Wait(sim); sim.PrepareVoyage(10, PreparationAction.Pennant);
            Assert.IsFalse(sim.State.OwnedPennant); sim.State.Phase = VoyagePhase.Sailing;
            Wait(sim); sim.PrepareVoyage(10, PreparationAction.SafeCourse); Assert.AreEqual(PassageCourse.Direct, sim.Journey.Course);
        }
        [Test] public void PhysicalGatesMustBeVisitedBeforeBonusAndDocking()
        {
            var sim = Make(); sim.PrepareVoyage(10, PreparationAction.SafeCourse);
            sim.State.Phase = VoyagePhase.Sailing; sim.Crew[0].AtHelm = true; sim.State.Position = VoyageSimulation.Destination;
            sim.HelmAction(10); Assert.AreEqual(VoyagePhase.Sailing, sim.State.Phase);
            sim.State.Position = VoyageSimulation.CourseGate(PassageCourse.Safe, 0); sim.Tick(.02f);
            sim.State.Position = VoyageSimulation.CourseGate(PassageCourse.Safe, 1); sim.Tick(.02f);
            Assert.AreEqual(2, sim.Journey.Checkpoints);
            sim.State.Position = VoyageSimulation.Destination; sim.HelmAction(10);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase); Assert.AreEqual(12, sim.State.LastRouteBonus);
        }
        [Test] public void SafePassageLegsAvoidShoalsAcrossSeeds()
        {
            var points = new[] { Vector2.zero, VoyageSimulation.CourseGate(PassageCourse.Safe, 0),
                VoyageSimulation.CourseGate(PassageCourse.Safe, 1), VoyageSimulation.Destination };
            for (uint seed = 1; seed <= 128; seed++)
                for (int leg = 1; leg < points.Length; leg++)
                    for (int step = 0; step <= 100; step++)
                    {
                        Vector2 point = Vector2.Lerp(points[leg - 1], points[leg], step / 100f);
                        for (int shoal = 0; shoal < VoyageSimulation.ShoalCount; shoal++)
                            Assert.Greater(Vector2.Distance(point, VoyageSimulation.ShoalPosition(seed, shoal)),
                                VoyageSimulation.ShoalRadius + 2, "Safe passage must not lead directly through a shoal.");
                    }
        }
        [Test] public void CareerV1StillLoadsAndV2KeepsPurchasedChoices()
        {
            Assert.IsTrue(CareerProgressStore.TryDecode("{\"Version\":1,\"Wallet\":55}", out var legacy));
            var state = new VoyageState(); legacy.Apply(ref state); Assert.AreEqual(55, state.Wallet);
            var sim = Make(); sim.PrepareVoyage(10, PreparationAction.ServiceTray); Wait(sim); sim.PrepareVoyage(10, PreparationAction.Pennant);
            var captured = CareerProgress.Capture(sim.State);
            captured.Apply(ref state); Assert.AreEqual(ShipEquipment.ServiceTray, state.Equipment); Assert.IsTrue(state.OwnedPennant);
        }
        [Test] public void MigrationKeepsCourseInventoryAndOneTimeGrant()
        {
            var sim = Make(); sim.PrepareVoyage(10, PreparationAction.Workshop); Wait(sim); sim.PrepareVoyage(10, PreparationAction.ShortcutCourse);
            sim.Journey.Checkpoints = 1; var target = Make(); target.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsTrue(sim.Journey.Equals(target.Journey)); Assert.AreEqual(5, target.State.RepairKitStock);
            target.Tick(.02f); Assert.AreEqual(5, target.State.RepairKitStock);
        }
    }
}
