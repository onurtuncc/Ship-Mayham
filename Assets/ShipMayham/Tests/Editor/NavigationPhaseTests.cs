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

        [Test] public void PressureCountsConnectedCrewWithoutChangingPassengersOrWallet()
        {
            var sim = Create(); Assert.AreEqual(.45f, sim.CrewPressure);
            sim.Join(11); Assert.AreEqual(.6f, sim.CrewPressure);
            sim.Join(12); Assert.AreEqual(.8f, sim.CrewPressure);
            sim.Join(13); Assert.AreEqual(1, sim.CrewPressure);
            sim.SuspendConnection(13); Assert.AreEqual(.8f, sim.CrewPressure);
            Assert.AreEqual(0, sim.State.Wallet); Assert.AreEqual(0, sim.State.Boarded);
        }
        [Test] public void FewerCrewDelayWavesWithoutExtendingAnActiveWarning()
        {
            var solo = Create(); solo.State.Route = RouteProfile.StormPassage; solo.Tick(.02f);
            var full = Create(); full.Join(11); full.Join(12); full.Join(13);
            full.State.Route = RouteProfile.StormPassage; full.Tick(.02f);
            solo.Tick(.1f); full.Tick(.1f);
            Assert.Greater(solo.Hazards.NextWaveAt, full.Hazards.NextWaveAt);
            solo.Hazards.NextWaveAt = solo.State.Elapsed; solo.Tick(.02f);
            float remaining = solo.Hazards.WaveRemaining;
            solo.Tick(.1f); Assert.AreEqual(remaining - .1f, solo.Hazards.WaveRemaining, .001f);
        }
    }
}
