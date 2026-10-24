using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class CareerProgressTests
    {
        [Test]
        public void CareerRoundTripRestoresOnlyPersistentProgress()
        {
            var source = new VoyageState
            {
                Wallet = 245,
                Reputation = 9,
                Livery = ShipLivery.Sunset,
                EngineLevel = 2,
                CapacityLevel = 1,
                ComfortLevel = 1,
                DockJobsCompleted = 4,
                Health = 23,
                Route = RouteProfile.StormPassage,
                Boarded = 6
            };
            string json = JsonUtility.ToJson(CareerProgress.Capture(source));
            Assert.IsTrue(CareerProgressStore.TryDecode(json, out CareerProgress decoded));
            var restored = new VoyageState { Health = 100, FoodStock = 16, DrinkStock = 16 };

            decoded.Apply(ref restored);

            Assert.AreEqual(245, restored.Wallet);
            Assert.AreEqual(9, restored.Reputation);
            Assert.GreaterOrEqual(restored.Reputation, VoyageSimulation.RouteReputation(RouteProfile.StormPassage));
            Assert.AreEqual(ShipLivery.Sunset, restored.Livery);
            Assert.AreEqual(2, restored.EngineLevel);
            Assert.AreEqual(1, restored.CapacityLevel);
            Assert.AreEqual(1, restored.ComfortLevel);
            Assert.AreEqual(4, restored.DockJobsCompleted);
            Assert.AreEqual(100, restored.Health);
            Assert.AreEqual(RouteProfile.CoastalFerry, restored.Route);
            Assert.AreEqual(0, restored.Boarded);
        }

        [Test]
        public void CareerDataIsClampedBeforeSavingAndApplying()
        {
            var source = new VoyageState
            {
                Wallet = -50,
                Reputation = -2,
                EngineLevel = 99,
                CapacityLevel = 99,
                ComfortLevel = 99,
                DockJobsCompleted = -1
            };
            CareerProgress captured = CareerProgress.Capture(source);
            var restored = new VoyageState();
            captured.Apply(ref restored);

            Assert.AreEqual(0, restored.Wallet);
            Assert.AreEqual(0, restored.Reputation);
            Assert.AreEqual(2, restored.EngineLevel);
            Assert.AreEqual(2, restored.CapacityLevel);
            Assert.AreEqual(1, restored.ComfortLevel);
            Assert.AreEqual(0, restored.DockJobsCompleted);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-json")]
        [TestCase("{\"Version\":3,\"Wallet\":999}")]
        public void InvalidOrUnknownCareerDataIsRejected(string json)
        {
            Assert.IsFalse(CareerProgressStore.TryDecode(json, out _));
        }
    }
}
