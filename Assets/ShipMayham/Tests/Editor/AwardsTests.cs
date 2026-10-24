using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class AwardsTests
    {
        private static readonly bool[] Everyone = { true, true, true, true };

        private static CrewTally[] Tallies(params CrewTally[] given)
        {
            var result = new CrewTally[4];
            for (int i = 0; i < given.Length; i++) result[i] = given[i];
            return result;
        }

        [Test]
        public void NothingSpecialMeansNoAwards()
        {
            Assert.IsEmpty(Awards.Compute(Tallies(new CrewTally { Orders = 2, Throws = 1, Repairs = 2 }), Everyone));
            Assert.IsEmpty(Awards.Compute(Tallies(), Everyone));
        }

        [Test]
        public void TheLeaderWinsOnceThresholdIsReached()
        {
            var awards = Awards.Compute(Tallies(new CrewTally { Throws = 1 }, new CrewTally { Throws = 3 }, new CrewTally { Throws = 2 }), Everyone);
            Assert.AreEqual(1, awards.Count);
            Assert.AreEqual("Passenger Hurler", awards[0].Title);
            Assert.AreEqual(3, awards[0].Value);
            CollectionAssert.AreEqual(new[] { 1 }, awards[0].Slots);
        }

        [Test]
        public void TiesShareTheAward()
        {
            var awards = Awards.Compute(Tallies(new CrewTally { Orders = 4 }, new CrewTally { Orders = 4 }, new CrewTally { Orders = 1 }), Everyone);
            Assert.AreEqual("Tray Master", awards.Single().Title);
            CollectionAssert.AreEqual(new[] { 0, 1 }, awards.Single().Slots);
        }

        [Test]
        public void CrewWhoLeftDoNotWin()
        {
            var active = new[] { true, false, true, false };
            var awards = Awards.Compute(Tallies(new CrewTally { Hits = 2 }, new CrewTally { Hits = 9 }, new CrewTally { Hits = 3 }), active);
            CollectionAssert.AreEqual(new[] { 2 }, awards.Single().Slots);
        }

        [Test]
        public void AtMostFourAwardsAreShownFunniestFirst()
        {
            var everything = new CrewTally { Throws = 5, Hits = 5, Repairs = 5, Orders = 5, Fares = 5, Deliveries = 5, HelmSeconds = 300 };
            var awards = Awards.Compute(Tallies(everything), Everyone);
            Assert.AreEqual(Awards.MaxShown, awards.Count);
            CollectionAssert.AreEqual(new[] { "Passenger Hurler", "Sharpshooter", "Fix-It Hero", "Tray Master" }, awards.Select(a => a.Title));
        }

        [Test]
        public void ASoloCrewStillGetsHighlights()
        {
            var awards = Awards.Compute(Tallies(new CrewTally { HelmSeconds = 95, Fares = 2 }), new[] { true, false, false, false });
            CollectionAssert.AreEqual(new[] { "Fare Collector", "Steady Hand" }, awards.Select(a => a.Title));
        }

        [Test]
        public void TheTallyReadsAndWritesEachKind()
        {
            var tally = new CrewTally();
            foreach (TallyKind kind in System.Enum.GetValues(typeof(TallyKind))) tally.Add(kind, 2);
            foreach (TallyKind kind in System.Enum.GetValues(typeof(TallyKind))) Assert.AreEqual(2, tally.Get(kind), kind.ToString());
            Assert.IsFalse(tally.IsEmpty);
            Assert.IsTrue(default(CrewTally).IsEmpty);
            Assert.IsTrue(tally.Equals(tally));
            Assert.IsFalse(tally.Equals(default));
        }
    }
}
