using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class EmergencyPhaseTests
    {
        private static void Wait(VoyageSimulation sim) { for (int i = 0; i < 25; i++) sim.Tick(.02f); }
        private static VoyageSimulation Make()
        {
            var original = NavigationPhaseTests.Create();
            var stations = new StationDefinition[original.Stations.Length + 1]; original.Stations.CopyTo(stations, 0);
            stations[stations.Length - 1] = new StationDefinition(StationKind.RepairSupply, new Vector2(3, -6));
            var sim = new VoyageSimulation(stations, original.Seats, true, 42);
            sim.Join(10); sim.State.Phase = VoyagePhase.Sailing; return sim;
        }
        [Test] public void FireWarnsBeforeBurningAndIsolationStopsDamageButCutsService()
        {
            var sim = Make(); sim.State.Elapsed = 32; sim.Tick(.02f);
            Assert.AreEqual(FirePhase.Warning, sim.Emergency.Fire);
            Assert.That(sim.State.Health, Is.EqualTo(100 - .65f * .02f).Within(.001f), "Only the existing leak damages during the fire warning.");
            sim.Emergency.Fire = FirePhase.Burning; sim.Emergency.Heat = 1;
            sim.Tick(.1f); Assert.Less(sim.State.Health, 100);
            sim.Crew[0].Position = new Vector2(3, 0); sim.DeckCommand(10, DeckAction.IsolateFire);
            float health = sim.State.Health; sim.Tick(.1f);
            Assert.That(sim.State.Health, Is.EqualTo(health - .65f * .1f).Within(.001f), "Isolation stops fire damage, not an unrelated leak.");
            Assert.IsTrue(sim.Encounter.PowerOut);
        }
        [Test] public void EquipmentAndRangeRequiredAndTwoSpraysConsumeTwoCharges()
        {
            var sim = Make(); sim.Emergency.Fire = FirePhase.Burning; sim.Emergency.Heat = 1;
            sim.Crew[0].Position = VoyageSimulation.FirePoint; sim.DeckCommand(10, DeckAction.Extinguish);
            Assert.AreEqual(4, sim.Emergency.ExtinguisherCharges);
            sim.Crew[0].Position = new Vector2(3, -6); Wait(sim); sim.DeckCommand(10, DeckAction.TakeExtinguisher);
            Assert.AreEqual(ServiceItem.Extinguisher, sim.Crew[0].Carrying);
            Wait(sim); sim.DeckCommand(10, DeckAction.Extinguish); Assert.AreEqual(4, sim.Emergency.ExtinguisherCharges);
            sim.Crew[0].Position = VoyageSimulation.FirePoint; Wait(sim); sim.DeckCommand(10, DeckAction.Extinguish);
            Wait(sim); sim.DeckCommand(10, DeckAction.Extinguish);
            Assert.AreEqual(FirePhase.Extinguished, sim.Emergency.Fire); Assert.AreEqual(2, sim.Emergency.ExtinguisherCharges);
            Assert.AreEqual(1, sim.Emergency.FiresPutOut);
            Wait(sim); sim.DeckCommand(10, DeckAction.Extinguish); Assert.AreEqual(2, sim.Emergency.ExtinguisherCharges);
        }
        [Test] public void ValveDiagnosisOffersCorrectAndWrongOutcomes()
        {
            var sim = Make(); sim.State.Leak = 1; sim.Crew[0].Position = new Vector2(3, -3);
            sim.Emergency.SelectedValve = sim.LeakingValve; sim.Emergency.ValveAdjusted = true;
            sim.Interact(10, 4); Assert.AreEqual(0, sim.State.Leak); Assert.AreEqual(3, sim.State.RepairKitStock);
            Wait(sim); sim.State.Leak = 1; sim.Emergency.SelectedValve = (sim.LeakingValve + 1) % 3;
            sim.Emergency.ValveAdjusted = true; float health = sim.State.Health; sim.Interact(10, 4);
            Assert.AreEqual(1, sim.State.Leak); Assert.AreEqual(1, sim.Emergency.WrongValves); Assert.AreEqual(health - 2, sim.State.Health);
        }
        [Test] public void MigrationPreservesIsolationHeatAndSupply()
        {
            var sim = Make(); sim.Emergency.Fire = FirePhase.Burning; sim.Emergency.Heat = .5f;
            sim.Emergency.Isolated = true; sim.Emergency.ExtinguisherCharges = 3;
            var target = Make(); target.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsTrue(sim.Emergency.Equals(target.Emergency));
        }
        [Test] public void RemoteAndUnknownPlayersCannotIsolateOrTakeEquipment()
        {
            var sim = Make(); sim.Emergency.Fire = FirePhase.Burning;
            sim.DeckCommand(10, DeckAction.IsolateFire); sim.DeckCommand(999, DeckAction.TakeExtinguisher);
            Assert.IsFalse(sim.Emergency.Isolated); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
        }
    }
}
