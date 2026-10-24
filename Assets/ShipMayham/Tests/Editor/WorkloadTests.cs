using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class WorkloadTests
    {
        private static VoyageSimulation NewSim(int crew)
        {
            var sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4, 3)),
                new StationDefinition(StationKind.Cannon, new Vector2(4, 3)),
                new StationDefinition(StationKind.Ammo, new Vector2(4, 0)),
                new StationDefinition(StationKind.Power, new Vector2(4, -3)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
            for (int i = 0; i < crew; i++) sim.Join((ulong)(10 + i));
            return sim;
        }

        private static void Sail(VoyageSimulation sim)
        {
            sim.State.Route = RouteProfile.CoastalFerry;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Tick(.05f);
        }

        // ------------------------------------------------------------ the scale itself

        [TestCase(4, 1f)] [TestCase(3, .8f)] [TestCase(2, .6f)] [TestCase(1, .45f)] [TestCase(0, .45f)] [TestCase(7, 1f)]
        public void ScaleFollowsTheCrewSize(int crew, float expected) => Assert.AreEqual(expected, Workload.ScaleFor(crew), .0001f);

        [Test]
        public void SmallerCrewsGetMoreTimeNeverLess()
        {
            for (int crew = 1; crew < Workload.FullCrew; crew++) Assert.Less(Workload.ScaleFor(crew), Workload.ScaleFor(crew + 1));
            Assert.AreEqual(12f, Workload.Stretch(12f, 1f), .0001f);
            Assert.AreEqual(12f / .45f, Workload.Stretch(12f, .45f), .0001f);
            Assert.AreEqual(Workload.Stretch(12f, .1f), Workload.Stretch(12f, 0f), .0001f, "a zero scale cannot divide by zero");
        }

        // ------------------------------------------------------------ pirates and power

        [TestCase(4)] [TestCase(3)] [TestCase(2)] [TestCase(1)]
        public void PiratesArriveLaterForSmallerCrews(int crew)
        {
            var sim = NewSim(crew);
            Sail(sim);
            float scale = Workload.ScaleFor(crew);
            Assert.AreEqual(VoyageSimulation.BasePirateArrival / scale, sim.PirateAt, .001f);
            Assert.AreEqual(VoyageSimulation.BasePowerFault / scale, sim.PowerFaultAt, .001f);
            while (sim.Encounter.Pirate == PiratePhase.Waiting && sim.State.Elapsed < 60) sim.Tick(.05f);
            Assert.AreEqual(PiratePhase.Warning, sim.Encounter.Pirate);
            Assert.AreEqual(VoyageSimulation.BasePirateArrival / scale, sim.State.Elapsed, .2f);
        }

        [Test]
        public void SoloPiratesShootLessOften()
        {
            float Gap(int crew)
            {
                var sim = NewSim(crew);
                Sail(sim);
                while (sim.Encounter.Pirate != PiratePhase.Attacking && sim.State.Elapsed < 80) sim.Tick(.05f);
                Assert.AreEqual(PiratePhase.Attacking, sim.Encounter.Pirate);
                return sim.Encounter.EnemyShotRemaining;
            }
            Assert.AreEqual(8f, Gap(4), .2f);
            Assert.AreEqual(8f / .45f, Gap(1), .3f);
        }

        [Test]
        public void ThePowerFaultIsScheduledLaterForSmallerCrews()
        {
            var full = NewSim(4);
            var solo = NewSim(1);
            Sail(full); Sail(solo);
            Assert.AreEqual(VoyageSimulation.BasePowerFault, full.PowerFaultAt, .001f);
            Assert.AreEqual(VoyageSimulation.BasePowerFault / Workload.Solo, solo.PowerFaultAt, .001f);
        }

        // ------------------------------------------------------------ events

        [Test]
        public void ShipEventsComeLessOftenForSmallerCrews()
        {
            var full = NewSim(4);
            var solo = NewSim(1);
            Sail(full); Sail(solo);
            Assert.AreEqual(VoyageSimulation.FirstEventDelay, full.Event.NextIn, .2f);
            Assert.AreEqual(VoyageSimulation.FirstEventDelay / Workload.Solo, solo.Event.NextIn, .2f);
        }

        [Test]
        public void TheGapBetweenEventsStretchesToo()
        {
            var sim = NewSim(2);
            sim.Passengers[0] = new PassengerState { Active = true, Satisfaction = 100, CarrierSlot = -1, FarePaid = true, Position = new Vector3(-1.9f, .5f, -1.5f) };
            sim.Passengers[1] = new PassengerState { Active = true, Satisfaction = 100, CarrierSlot = -1, FarePaid = true, Position = new Vector3(1.9f, .5f, -1.5f) };
            sim.State.Boarded = 2;
            Sail(sim);
            while (sim.Event.Phase != VoyageEventPhase.Active && sim.State.Elapsed < 80) sim.Tick(.05f);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase, "an event starts");
            Assert.AreEqual(VoyageSimulation.FirstEventDelay / Workload.ScaleFor(2), sim.State.Elapsed, .4f);
        }

        // ------------------------------------------------------------ what must not change

        [Test]
        public void ConnectionChangesNeverRestartARunningTimetable()
        {
            var sim = NewSim(1);
            Sail(sim);
            float pirate = sim.PirateAt, power = sim.PowerFaultAt, nextEvent = sim.Event.NextIn;
            sim.Join(21); sim.Join(22); sim.Join(23);          // the crew grows to four after departure
            sim.Tick(.1f);
            Assert.AreEqual(pirate, sim.PirateAt);
            Assert.AreEqual(power, sim.PowerFaultAt);
            Assert.AreEqual(nextEvent - .1f, sim.Event.NextIn, .01f);
            Assert.AreEqual(Workload.Full, sim.WorkloadScale, "the live scale follows the crew, only future delays use it");
        }

        [Test]
        public void PassengerCountAndEconomyDoNotDependOnTheCrew()
        {
            for (int crew = 1; crew <= 4; crew++)
            {
                var sim = NewSim(crew);
                sim.Crew[0].Position = new Vector2(-3, -3.5f);
                for (int i = 0; i < 20; i++) sim.Tick(.02f);
                sim.Interact(10, 0);
                Assert.AreEqual(4, sim.State.Boarded, "boarding is unchanged for " + crew + " crew");
                Assert.AreEqual(16, sim.State.FoodStock);
                Assert.AreEqual(16, sim.State.DrinkStock);
            }
        }

        [Test]
        public void LeaksIgnoreTheCrewSize()
        {
            var sim = NewSim(1);
            Sail(sim);
            while (sim.State.Leak == 0 && sim.State.Elapsed < 60) sim.Tick(.05f);
            Assert.AreEqual(25f, sim.State.Elapsed, .3f);
        }

        [Test]
        public void ANewVoyageGetsAFreshTimetable()
        {
            var sim = NewSim(1);
            Sail(sim);
            Assert.AreNotEqual(VoyageSimulation.BasePirateArrival, sim.PirateAt);
            sim.State.Health = 0;
            sim.Tick(.1f);
            for (int i = 0; i < 10; i++) sim.Tick(.1f);
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            sim.Interact(10, 0);
            Assert.AreEqual(VoyageSimulation.BasePirateArrival, sim.PirateAt, "reset to the base until the next departure");
        }

        [Test]
        public void MigrationKeepsTheTimetable()
        {
            var sim = NewSim(2);
            Sail(sim);
            byte[] data = sim.ExportMigrationData();
            var restored = NewSim(4);
            restored.ImportMigrationData(data);
            Assert.AreEqual(sim.PirateAt, restored.PirateAt);
            Assert.AreEqual(sim.PowerFaultAt, restored.PowerFaultAt);
            restored.Tick(.1f);
            Assert.AreEqual(sim.PirateAt, restored.PirateAt, "the new host does not recompute a running voyage");
        }

        // ------------------------------------------------------------ shapes instead of colour

        [Test]
        public void EveryShapeHasAnInsideAndAnOutside()
        {
            foreach (IconShape shape in System.Enum.GetValues(typeof(IconShape)))
            {
                Assert.IsTrue(IconShapes.Contains(shape, 0, 0), shape + " centre");
                Assert.IsFalse(IconShapes.Contains(shape, 1.2f, 1.2f), shape + " far corner");
                Assert.IsFalse(IconShapes.Contains(shape, -1.2f, 1.2f), shape + " far corner");
            }
        }

        [Test]
        public void ShapesDifferFromEachOtherAtTheCorners()
        {
            Assert.IsTrue(IconShapes.Contains(IconShape.Square, .75f, .75f));
            Assert.IsFalse(IconShapes.Contains(IconShape.Circle, .75f, .75f));
            Assert.IsFalse(IconShapes.Contains(IconShape.Diamond, .75f, .75f));
            Assert.IsFalse(IconShapes.Contains(IconShape.Triangle, .75f, .75f));
            Assert.IsTrue(IconShapes.Contains(IconShape.Triangle, 0, .9f), "the triangle has a point up");
            Assert.IsTrue(IconShapes.Contains(IconShape.Triangle, .6f, -.7f), "and a wide base");
            Assert.IsFalse(IconShapes.Contains(IconShape.Triangle, .6f, .5f));
            Assert.IsTrue(IconShapes.Contains(IconShape.Diamond, 0, 1f));
            Assert.IsFalse(IconShapes.Contains(IconShape.Square, 0, 1f));
        }

        [Test]
        public void SeverityHasItsOwnShapeAndSpritesAreBuilt()
        {
            Assert.AreEqual(IconShape.Triangle, IconShapes.ForSeverity(HudKit.Danger));
            Assert.AreEqual(IconShape.Diamond, IconShapes.ForSeverity(HudKit.Warning));
            Assert.AreEqual(IconShape.Square, IconShapes.ForSeverity(HudKit.Good));
            Assert.AreEqual(IconShape.Circle, IconShapes.ForSeverity(HudKit.Info));
            foreach (IconShape shape in System.Enum.GetValues(typeof(IconShape)))
            {
                var sprite = IconShapes.Sprite(shape);
                Assert.IsNotNull(sprite, shape.ToString());
                Assert.AreSame(sprite, IconShapes.Sprite(shape), "cached");
                Assert.Greater(sprite.rect.width, 8f, shape + " has a usable size");
            }
        }

        [Test]
        public void ResourcePillsUseDistinctShapes()
        {
            var parent = new GameObject("PillParent", typeof(RectTransform));
            try
            {
                var food = new ResourcePill(parent.transform, "Food", "FOOD", HudKit.Food, IconShape.Square);
                var drink = new ResourcePill(parent.transform, "Drink", "DRINKS", HudKit.Drink, IconShape.Triangle);
                var foodIcon = food.Root.Find("Line/Icon").GetComponent<UnityEngine.UI.Image>();
                var drinkIcon = drink.Root.Find("Line/Icon").GetComponent<UnityEngine.UI.Image>();
                Assert.AreSame(IconShapes.Sprite(IconShape.Square), foodIcon.sprite);
                Assert.AreSame(IconShapes.Sprite(IconShape.Triangle), drinkIcon.sprite);
                Assert.AreNotSame(foodIcon.sprite, drinkIcon.sprite);
            }
            finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void AlertsRepeatTheirSeverityAsAShape()
        {
            var parent = new GameObject("AlertParent", typeof(RectTransform));
            try
            {
                var list = new AlertList(parent.transform, "Alerts", 3);
                list.Begin();
                list.Add(HudKit.Danger, "HULL LEAK", "Patch it");
                list.Add(HudKit.Warning, "POWER OUT", "Reset");
                list.Add(HudKit.Info, "SHIP EVENT", "Do it");
                list.End();
                Assert.AreSame(IconShapes.Sprite(IconShape.Triangle), parent.transform.Find("Alerts/Alert0/Icon").GetComponent<UnityEngine.UI.Image>().sprite);
                Assert.AreSame(IconShapes.Sprite(IconShape.Diamond), parent.transform.Find("Alerts/Alert1/Icon").GetComponent<UnityEngine.UI.Image>().sprite);
                Assert.AreSame(IconShapes.Sprite(IconShape.Circle), parent.transform.Find("Alerts/Alert2/Icon").GetComponent<UnityEngine.UI.Image>().sprite);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
