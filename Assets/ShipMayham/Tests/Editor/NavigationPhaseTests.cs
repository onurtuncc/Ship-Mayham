using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class NavigationPhaseTests
    {
        internal static VoyageSimulation Create()
        {
            var seats = new Vector2[8];
            for (int i = 0; i < 8; i++) seats[i] = new Vector2(i % 2 == 0 ? -1.9f : 1.9f, -1.5f + i / 2 * 2.1f);
            var sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
                new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4,0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4,3)),
                new StationDefinition(StationKind.Repair, new Vector2(4,-3)),
                new StationDefinition(StationKind.Power, new Vector2(4,0)),
                new StationDefinition(StationKind.Cannon, new Vector2(4,6)) }, seats, true, 42);
            sim.Join(10, "captain");
            sim.State.Phase = VoyagePhase.Sailing;
            return sim;
        }
        [Test] public void LayoutIsSeededAndClearOfPortsAndCentreCorridor()
        {
            for (int i = 0; i < 3; i++)
            {
                var p = VoyageSimulation.ShoalPosition(42, i);
                Assert.AreEqual(p, VoyageSimulation.ShoalPosition(42, i));
                Assert.Greater(Mathf.Abs(p.x), VoyageSimulation.ShoalRadius);
                Assert.That(p.y, Is.InRange(40, 180));
            }
        }
        [Test] public void FastImpactSlowsAndDamagesOnlyOnceDuringCooldown()
        {
            var sim = Create(); sim.State.Position = VoyageSimulation.ShoalPosition(sim.Navigation.Seed, 0); sim.State.Speed = 4;
            sim.Tick(.02f);
            Assert.AreEqual(1, sim.Navigation.Impacts); Assert.Less(sim.State.Health, 100);
            float health = sim.State.Health;
            sim.Tick(.02f); Assert.AreEqual(health, sim.State.Health);
        }
        [Test] public void SlowShipDoesNotTakeShoalDamage()
        {
            var sim = Create(); sim.State.Position = VoyageSimulation.ShoalPosition(sim.Navigation.Seed, 0); sim.State.Speed = .8f;
            sim.Tick(.02f); Assert.AreEqual(100, sim.State.Health);
        }
        [Test] public void WaveWarnsThenRewardsFacingAndPenalizesBroadside()
        {
            var sim = Create(); sim.State.Route = RouteProfile.StormPassage; sim.Navigation.WaveIn = 0;
            sim.Tick(.02f); Assert.IsTrue(sim.Navigation.WaveActive); Assert.AreEqual(100, sim.State.Health);
            sim.State.Heading = sim.Navigation.WaveHeading; sim.Navigation.Warning = .01f; sim.Tick(.02f);
            Assert.AreEqual(1, sim.Navigation.WavesAvoided);
            sim.Navigation.WaveActive = true; sim.Navigation.Warning = .01f; sim.State.Heading += 90; sim.Tick(.02f);
            Assert.AreEqual(1, sim.Navigation.WavesHit); Assert.AreEqual(88, sim.State.Health);
        }
        [Test] public void PressureCountsConnectedCrewWithoutChangingPassengersOrWallet()
        {
            var sim = Create(); Assert.AreEqual(.45f, sim.CrewPressure);
            sim.Join(11); sim.Join(12); sim.Join(13); Assert.AreEqual(1, sim.CrewPressure);
            sim.SuspendConnection(13); Assert.AreEqual(.8f, sim.CrewPressure);
            Assert.AreEqual(0, sim.State.Wallet); Assert.AreEqual(0, sim.State.Boarded);
        }
        [Test] public void MigrationKeepsWarningAndSeedWithoutReroll()
        {
            var sim = Create(); sim.Navigation.WaveActive = true; sim.Navigation.Warning = 2.7f; sim.Navigation.WaveHeading = 120;
            var restored = Create(); restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsTrue(sim.Navigation.Equals(restored.Navigation));
        }
    }
}
