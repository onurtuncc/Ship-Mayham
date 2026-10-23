using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class UnstuckTests
    {
        private static VoyageSimulation NewSimulation()
        {
            var sim = new VoyageSimulation(System.Array.Empty<StationDefinition>(), new[]
            {
                new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 0), new Vector2(2, 0),
                new Vector2(-2, 4), new Vector2(2, 2), new Vector2(-2, 6), new Vector2(2, 4)
            });
            sim.Join(10, "a");
            sim.Join(11, "b");
            return sim;
        }

        [Test]
        public void TheCrewMemberIsPutBackOnTheDeckSpawn()
        {
            var sim = NewSimulation();
            sim.Crew[1].Position = new Vector2(4.9f, 7.9f);
            sim.Crew[1].Velocity = Vector2.right;
            sim.Crew[1].JumpHeight = 1f;
            Assert.AreEqual("Back on your feet.", sim.Unstuck(11));
            Assert.AreEqual(new Vector2(-1.5f + 1, -6), sim.Crew[1].Position);
            Assert.AreEqual(Vector2.zero, sim.Crew[1].Velocity);
            Assert.AreEqual(0f, sim.Crew[1].JumpHeight);
        }

        [Test]
        public void TheHelmAndHeldItemsAreReleased()
        {
            var sim = NewSimulation();
            sim.Crew[0].AtHelm = true;
            sim.Crew[0].Carrying = ServiceItem.Food;
            sim.Unstuck(10);
            Assert.IsFalse(sim.Crew[0].AtHelm);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
        }

        [Test]
        public void ItCoolsDownAndOnlyForRegisteredCrew()
        {
            var sim = NewSimulation();
            Assert.AreEqual("Crew not registered.", sim.Unstuck(99));
            sim.Unstuck(10);
            Assert.That(sim.Unstuck(10), Does.StartWith("You can use this again in"));
            Assert.AreEqual("Back on your feet.", sim.Unstuck(11), "The cooldown belongs to each crew member.");

            for (int i = 0; i < Mathf.CeilToInt(VoyageSimulation.UnstuckCooldown / .1f) + 2; i++) sim.Tick(.1f);
            Assert.AreEqual("Back on your feet.", sim.Unstuck(10));
        }
    }
}
