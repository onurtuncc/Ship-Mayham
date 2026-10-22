using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class ShipUpgradeTests : PrototypeSceneFixture
    {
        [TestCase(0, 4)] [TestCase(1, 6)] [TestCase(2, 8)] [TestCase(5, 8)] [TestCase(-1, 4)]
        public void SeatsUnlockWithCapacity(int level, int seats) => Assert.AreEqual(seats, ShipProgression.UnlockedSeats(level));

        [TestCase(0, 0, false)] [TestCase(1, 1, false)] [TestCase(2, 2, true)] [TestCase(9, 2, true)]
        public void EngineLevelsAddStacksThenBoosters(int level, int funnels, bool boosters)
        {
            Assert.AreEqual(funnels, ShipProgression.Funnels(level));
            Assert.AreEqual(boosters, ShipProgression.HasBoosters(level));
        }

        [TestCase(100f, 0)] [TestCase(60f, 0)] [TestCase(59f, 1)] [TestCase(30f, 1)] [TestCase(29f, 2)] [TestCase(0f, 2)]
        public void DamageStagesFollowHealth(float health, int stage) => Assert.AreEqual(stage, ShipProgression.DamageStage(health));

        private static Transform[] Make(Transform parent, int count)
        {
            var roots = new Transform[count];
            for (int i = 0; i < count; i++) { roots[i] = new GameObject("Module" + i).transform; roots[i].SetParent(parent); }
            return roots;
        }

        private static int CountActive(Transform[] roots)
        {
            int n = 0;
            foreach (var r in roots) if (r.gameObject.activeSelf) n++;
            return n;
        }

        [Test] public void ViewShowsOnlyTheModulesTheLevelsEarn()
        {
            var host = new GameObject("UpgradeHost", typeof(PrototypeView));
            try
            {
                var view = host.AddComponent<PrototypeShipUpgradeView>();
                view.Seats = Make(host.transform, 8); view.Funnels = Make(host.transform, 2);
                view.Boosters = Make(host.transform, 2); view.ComfortFittings = Make(host.transform, 3);
                view.Apply(engineLevel: 1, capacityLevel: 1, comfortLevel: 0, animate: false);
                Assert.AreEqual(6, CountActive(view.Seats));
                Assert.AreEqual(1, CountActive(view.Funnels));
                Assert.AreEqual(0, CountActive(view.Boosters));
                Assert.AreEqual(0, CountActive(view.ComfortFittings));
                view.Apply(2, 2, 1, animate: false);
                Assert.AreEqual(8, CountActive(view.Seats)); Assert.AreEqual(2, CountActive(view.Funnels));
                Assert.AreEqual(2, CountActive(view.Boosters)); Assert.AreEqual(3, CountActive(view.ComfortFittings));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test] public void AuthoredSceneStartsWithModulesHiddenAndWired()
        {
            var view = FindInScene<PrototypeView>();
            var upgrades = view.GetComponent<PrototypeShipUpgradeView>();
            var effects = view.GetComponent<PrototypeShipEffects>();
            Assert.IsNotNull(upgrades); Assert.IsNotNull(effects);
            Assert.AreEqual(VoyageSimulation.MaxPassengers, upgrades.Seats.Length);
            Assert.AreEqual(2, upgrades.Funnels.Length); Assert.AreEqual(2, upgrades.Boosters.Length); Assert.AreEqual(7, upgrades.ComfortFittings.Length);
            foreach (var module in upgrades.Funnels) Assert.IsFalse(module.gameObject.activeSelf);
            foreach (var module in upgrades.ComfortFittings) Assert.IsFalse(module.gameObject.activeSelf);
            Assert.AreEqual(2, effects.FunnelSmoke.Length); Assert.AreEqual(2, effects.BoosterNozzles.Length);
            foreach (var seat in upgrades.Seats) Assert.IsNotNull(seat, "Every passenger needs a SeatVisuals root.");
        }
    }
}
