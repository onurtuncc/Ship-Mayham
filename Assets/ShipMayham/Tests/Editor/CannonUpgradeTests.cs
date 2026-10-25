using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>The two cannon upgrades: gunnery training (reload, rack, damage) and extra barrels (a fanned-out volley).</summary>
    public sealed class CannonUpgradeTests
    {
        private static readonly Vector2 Desk = new Vector2(-4, -4);
        private const ulong Gunner = 10;

        private static VoyageSimulation NewSimulation()
        {
            var sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, Desk),
                new StationDefinition(StationKind.Cannon, Vector2.zero),
                new StationDefinition(StationKind.Power, new Vector2(4, 0)),
            }, new Vector2[8]);
            sim.Join(Gunner, "gunner");
            sim.Crew[0].Position = Desk + new Vector2(.8f, 0);
            sim.State.Wallet = 5000;
            return sim;
        }

        private static void ManTheCannon(VoyageSimulation sim)
        {
            sim.Crew[0].AtCannon = true;
            sim.Crew[0].Position = Vector2.zero;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Encounter.Pirate = PiratePhase.Attacking;
            sim.Encounter.PiratePosition = new Vector2(26, 25);
            sim.Encounter.AttackRemaining = sim.Encounter.EnemyShotRemaining = 100;
        }

        private static void Advance(VoyageSimulation s, float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) s.Tick(.05f); }

        private static Vector2 Aim => new Vector2(Mathf.Atan2(26, 25) * Mathf.Rad2Deg, 0);

        [TestCase(0, 2.5f, 3, 34f)] [TestCase(1, 2f, 4, 40f)] [TestCase(2, 1.5f, 5, 50f)] [TestCase(9, 1.5f, 5, 50f)] [TestCase(-1, 2.5f, 3, 34f)]
        public void GunneryLevelsSpeedUpReloadGrowTheRackAndHitHarder(int level, float reload, int rack, float damage)
        {
            Assert.AreEqual(reload, CannonProgression.ReloadSeconds(level), .001f);
            Assert.AreEqual(rack, CannonProgression.RackCapacity(level));
            Assert.AreEqual(damage, CannonProgression.HitDamage(level), .001f);
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(7, 2)] [TestCase(-3, 0)]
        public void BatteryLevelsAddBarrels(int level, int barrels) => Assert.AreEqual(barrels, CannonProgression.ExtraBarrels(level));

        [Test] public void BothUpgradesAreBoughtAtTheDeskWithRisingPricesAndStopAtTheirMax()
        {
            var sim = NewSimulation();
            Assert.AreEqual(120, VoyageSimulation.UpgradeCost(StationKind.GunneryUpgrade, sim.State));
            Assert.AreEqual(180, VoyageSimulation.UpgradeCost(StationKind.BatteryUpgrade, sim.State));
            Assert.That(sim.BuyUpgrade(Gunner, StationKind.GunneryUpgrade), Does.StartWith("Upgrade purchased"));
            Advance(sim, .4f);
            Assert.That(sim.BuyUpgrade(Gunner, StationKind.BatteryUpgrade), Does.StartWith("Upgrade purchased"));
            Advance(sim, .4f);
            Assert.AreEqual(5000 - 120 - 180, sim.State.Wallet);
            Assert.AreEqual(240, VoyageSimulation.UpgradeCost(StationKind.GunneryUpgrade, sim.State));
            Assert.AreEqual(360, VoyageSimulation.UpgradeCost(StationKind.BatteryUpgrade, sim.State));
            sim.BuyUpgrade(Gunner, StationKind.GunneryUpgrade); Advance(sim, .4f);
            sim.BuyUpgrade(Gunner, StationKind.BatteryUpgrade); Advance(sim, .4f);
            Assert.AreEqual(2, sim.State.GunneryLevel); Assert.AreEqual(2, sim.State.BatteryLevel);
            int wallet = sim.State.Wallet;
            Assert.AreEqual("Upgrade maxed out.", sim.BuyUpgrade(Gunner, StationKind.GunneryUpgrade));
            Advance(sim, .4f);
            Assert.AreEqual("Upgrade maxed out.", sim.BuyUpgrade(Gunner, StationKind.BatteryUpgrade));
            Assert.AreEqual(wallet, sim.State.Wallet);
        }

        [Test] public void CannonUpgradesAreNotSoldWhileSailingOrWithoutCoins()
        {
            var sim = NewSimulation();
            sim.State.Wallet = 100;
            Assert.AreEqual("Need 120 coins.", sim.BuyUpgrade(Gunner, StationKind.GunneryUpgrade));
            Advance(sim, .4f);
            sim.State.Wallet = 5000; sim.State.Phase = VoyagePhase.Sailing;
            Assert.AreEqual("Upgrades are available at port.", sim.BuyUpgrade(Gunner, StationKind.BatteryUpgrade));
            Assert.AreEqual(0, sim.State.BatteryLevel);
        }

        [Test] public void LevelAndMaxLookupsCoverEveryUpgrade()
        {
            var state = new VoyageState { EngineLevel = 1, CapacityLevel = 2, ComfortLevel = 1, GunneryLevel = 2, BatteryLevel = 1 };
            Assert.AreEqual(1, VoyageSimulation.UpgradeLevel(StationKind.EngineUpgrade, state));
            Assert.AreEqual(2, VoyageSimulation.UpgradeLevel(StationKind.CapacityUpgrade, state));
            Assert.AreEqual(1, VoyageSimulation.UpgradeLevel(StationKind.ComfortUpgrade, state));
            Assert.AreEqual(2, VoyageSimulation.UpgradeLevel(StationKind.GunneryUpgrade, state));
            Assert.AreEqual(1, VoyageSimulation.UpgradeLevel(StationKind.BatteryUpgrade, state));
            Assert.AreEqual(1, VoyageSimulation.UpgradeMax(StationKind.ComfortUpgrade));
            Assert.AreEqual(2, VoyageSimulation.UpgradeMax(StationKind.GunneryUpgrade));
            Assert.AreEqual(2, VoyageSimulation.UpgradeMax(StationKind.BatteryUpgrade));
            Assert.IsTrue(VoyageSimulation.IsUpgrade(StationKind.GunneryUpgrade) && VoyageSimulation.IsUpgrade(StationKind.BatteryUpgrade));
            Assert.IsFalse(VoyageSimulation.IsUpgrade(StationKind.Cannon));
        }

        [TestCase(0, 2.5f)] [TestCase(1, 2f)] [TestCase(2, 1.5f)]
        public void GunneryShortensTheReload(int level, float seconds)
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.GunneryLevel = level;
            sim.FireCannon(Gunner, Aim);
            sim.Encounter.ReadyAmmo = 1;
            sim.ReloadCannon(Gunner);
            Assert.AreEqual(seconds, sim.Encounter.ReloadRemaining, .001f);
        }

        [Test] public void GunneryGrowsTheRackTheSupplyChainCanFill()
        {
            var sim = NewSimulation();
            sim.Encounter.ReadyAmmo = 3;
            Assert.AreEqual(3, VoyageSimulation.RackCapacity(sim.State));
            sim.State.GunneryLevel = 2;
            Assert.AreEqual(5, VoyageSimulation.RackCapacity(sim.State));
        }

        [TestCase(0, 66f)] [TestCase(1, 60f)] [TestCase(2, 50f)]
        public void GunneryDecidesHowHardTheMainBarrelHits(int level, float pirateHealth)
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.GunneryLevel = level;
            sim.FireCannon(Gunner, Aim);
            Advance(sim, 1.2f);
            Assert.AreEqual(1, sim.Encounter.Hits);
            Assert.AreEqual(pirateHealth, sim.Encounter.PirateHealth, .001f);
        }

        [Test] public void WithoutTheBatteryOnlyTheMainRoundFlies()
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.FireCannon(Gunner, Aim);
            Assert.IsTrue(sim.Encounter.CrewBall.Active);
            Assert.IsFalse(sim.Encounter.CrewBallB.Active);
            Assert.IsFalse(sim.Encounter.CrewBallC.Active);
        }

        [Test] public void ExtraBarrelsFireOneVolleyForOneRoundOfAmmo()
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.BatteryLevel = 2;
            int ammo = sim.Encounter.Ammo;
            string message = sim.FireCannon(Gunner, Aim);
            Assert.That(message, Does.StartWith("Volley"));
            Assert.AreEqual(ammo - 1, sim.Encounter.Ammo, "the whole volley costs one round");
            Assert.AreEqual(1, sim.Encounter.ShotsFired);
            Assert.IsTrue(sim.Encounter.CrewBall.Active && sim.Encounter.CrewBallB.Active && sim.Encounter.CrewBallC.Active);
            Vector3 main = sim.Encounter.CrewBall.Velocity, right = sim.Encounter.CrewBallB.Velocity, left = sim.Encounter.CrewBallC.Velocity;
            Assert.AreEqual(CannonProgression.VolleySpreadDegrees, Vector3.Angle(main, right), .2f);
            Assert.AreEqual(CannonProgression.VolleySpreadDegrees, Vector3.Angle(main, left), .2f);
            Assert.AreEqual(2 * CannonProgression.VolleySpreadDegrees, Vector3.Angle(right, left), .3f, "one fans out each way");
        }

        [Test] public void OneExtraBarrelFiresOneExtraRound()
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.BatteryLevel = 1;
            sim.FireCannon(Gunner, Aim);
            Assert.IsTrue(sim.Encounter.CrewBallB.Active);
            Assert.IsFalse(sim.Encounter.CrewBallC.Active);
        }

        [TestCase(1, 0, 49f)] [TestCase(2, 0, 32f)] [TestCase(2, 1, 20f)] [TestCase(2, 2, 0f)]
        public void VolleyRoundsHitForHalfDamage(int battery, int gunnery, float pirateHealth)
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.BatteryLevel = battery; sim.State.GunneryLevel = gunnery;
            sim.FireCannon(Gunner, Aim);
            Advance(sim, 1.2f);
            Assert.AreEqual(1 + battery, sim.Encounter.Hits);
            Assert.AreEqual(pirateHealth, sim.Encounter.PirateHealth, .001f);
            Assert.IsFalse(sim.Encounter.CrewBall.Active || sim.Encounter.CrewBallB.Active || sim.Encounter.CrewBallC.Active);
        }

        [Test] public void TheNextShotWaitsForTheWholeVolley()
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.BatteryLevel = 2;
            sim.FireCannon(Gunner, Aim);
            sim.Encounter.CrewBall.Active = false;
            sim.Encounter.CannonLoaded = true;
            Assert.AreEqual("Previous cannonball is still in flight.", sim.FireCannon(Gunner, Aim));
            Assert.AreEqual(1, sim.Encounter.ShotsFired);
        }

        [Test] public void SettlingTheVoyageRemovesRoundsStillInTheAir()
        {
            var sim = NewSimulation(); ManTheCannon(sim);
            sim.State.BatteryLevel = 2;
            sim.FireCannon(Gunner, Aim);
            sim.State.Position = VoyageSimulation.Destination;
            sim.State.Speed = 0;
            sim.Crew[0].AtCannon = false;
            sim.Crew[0].AtHelm = true;
            sim.HelmAction(Gunner);
            Assert.IsFalse(sim.Encounter.CrewBallB.Active || sim.Encounter.CrewBallC.Active);
        }

        [Test] public void CannonUpgradesSurviveANewVoyageAndAHostMigration()
        {
            var sim = NewSimulation();
            sim.State.GunneryLevel = 2; sim.State.BatteryLevel = 1;
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true);
            restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.AreEqual(2, restored.State.GunneryLevel);
            Assert.AreEqual(1, restored.State.BatteryLevel);
        }

        [Test] public void CareerSaveRemembersAndClampsTheCannonUpgrades()
        {
            var state = new VoyageState { GunneryLevel = 2, BatteryLevel = 1, Wallet = 10 };
            var restored = new VoyageState();
            CareerProgress.Capture(state).Apply(ref restored);
            Assert.AreEqual(2, restored.GunneryLevel); Assert.AreEqual(1, restored.BatteryLevel);
            var tampered = CareerProgress.Capture(state);
            tampered.GunneryLevel = 99; tampered.BatteryLevel = -4;
            tampered.Apply(ref restored);
            Assert.AreEqual(2, restored.GunneryLevel); Assert.AreEqual(0, restored.BatteryLevel);
        }

        [Test] public void OldCareerSavesWithoutCannonUpgradesStillLoad()
        {
            Assert.IsTrue(CareerProgressStore.TryDecode("{\"Version\":2,\"Wallet\":55,\"EngineLevel\":1}", out var progress));
            var state = new VoyageState();
            progress.Apply(ref state);
            Assert.AreEqual(1, state.EngineLevel);
            Assert.AreEqual(0, state.GunneryLevel); Assert.AreEqual(0, state.BatteryLevel);
        }
    }
}
