using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>
    /// Climbing out of the water must leave the crew member free to walk. The ship's ladder tops sit right beside counters
    /// (DRINK, AMMO, the seat upgrade), so these tests use the station layout of the real scene.
    /// </summary>
    public sealed class LadderExitTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;

        /// <summary>The deck stations as the prototype scene places them.</summary>
        private static StationDefinition[] SceneStations() => new[]
        {
            new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
            new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
            new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
            new StationDefinition(StationKind.Drink, new Vector2(-4, 3)),
            new StationDefinition(StationKind.Repair, new Vector2(4, -3)),
            new StationDefinition(StationKind.EngineUpgrade, new Vector2(3.8f, -7.8f)),
            new StationDefinition(StationKind.CapacityUpgrade, new Vector2(0, -7.8f)),
            new StationDefinition(StationKind.ComfortUpgrade, new Vector2(-3.8f, -7.8f)),
            new StationDefinition(StationKind.Cannon, new Vector2(4, 6)),
            new StationDefinition(StationKind.Power, new Vector2(4, 0)),
            new StationDefinition(StationKind.Ammo, new Vector2(4, 3)),
            new StationDefinition(StationKind.RepairSupply, new Vector2(4, -5.5f)),
        };

        [SetUp]
        public void SetUp()
        {
            var seats = new Vector2[VoyageSimulation.MaxPassengers];
            for (int i = 0; i < seats.Length; i++) seats[i] = new Vector2(i % 2 == 0 ? -1.9f : 1.9f, -1.5f + i / 2 * 2.1f);
            sim = new VoyageSimulation(SceneStations(), seats, true);
            sim.Join(Host);
        }

        private void Walk(Vector2 input, float seconds)
        {
            for (float t = 0; t < seconds; t += .02f) { sim.SetInput(Host, input); sim.Tick(.02f); }
        }

        private void ClimbOut(int ladder)
        {
            sim.Crew[0].Swimming = true;
            sim.Crew[0].OnShore = false;
            sim.Crew[0].Position = VoyageSimulation.Ladders[ladder].Base;
            Walk(Vector2.zero, .8f);                                    // past the ladder cooldown
            Assert.That(sim.LadderCommand(Host, ladder), Does.StartWith("Up you go"));
            Assert.IsFalse(sim.Crew[0].Swimming);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ClimbingOutLandsClearOfEveryCounter(int ladder)
        {
            ClimbOut(ladder);
            if (sim.Crew[0].OnShore) return;
            foreach (var station in sim.Stations)
                Assert.GreaterOrEqual(Vector2.Distance(sim.Crew[0].Position, station.Position), VoyageSimulation.StationClearance,
                    $"landed inside the clearance of {station.Kind}");
            Assert.Less(Vector2.Distance(sim.Crew[0].Position, VoyageSimulation.Ladders[ladder].Top), 1.3f, "still beside the ladder");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AfterClimbingOutTheCrewCanWalkAway(int ladder)
        {
            ClimbOut(ladder);
            Vector2 start = sim.Crew[0].Position;
            // Toward the middle of the deck (or into town from the quay ladder).
            Vector2 inward = sim.Crew[0].OnShore ? Vector2.left : (Vector2.zero - start).normalized;
            Walk(inward, .6f);
            Assert.Greater(Vector2.Distance(start, sim.Crew[0].Position), 1f, "walks off normally");
        }

        [Test]
        public void SomeoneStandingInsideACounterCanAlwaysStepOut()
        {
            sim.Crew[0].Position = new Vector2(-4.2f, 3f);              // inside DRINK's clearance
            Walk(Vector2.right, .6f);
            Assert.Greater(sim.Crew[0].Position.x, -3f);
        }

        [Test]
        public void OutsideACounterItIsStillSolid()
        {
            sim.Crew[0].Position = new Vector2(-2.5f, 3f);
            Walk(Vector2.left, 1f);
            Assert.GreaterOrEqual(Vector2.Distance(sim.Crew[0].Position, new Vector2(-4, 3)), VoyageSimulation.StationClearance - .01f);
        }
    }
}
