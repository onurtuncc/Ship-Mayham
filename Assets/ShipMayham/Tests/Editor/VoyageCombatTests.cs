using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class VoyageCombatTests
    {
        private VoyageSimulation sim;
        private const ulong Captain = 10, Gunner = 11, Supplier = 12;
        [SetUp]
        public void Setup()
        {
            sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
                new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4,0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4,3)),
                new StationDefinition(StationKind.Repair, new Vector2(4,-3)),
                new StationDefinition(StationKind.EngineUpgrade, new Vector2(3.8f,-7.8f)),
                new StationDefinition(StationKind.CapacityUpgrade, new Vector2(0,-7.8f)),
                new StationDefinition(StationKind.ComfortUpgrade, new Vector2(-3.8f,-7.8f)),
                new StationDefinition(StationKind.Cannon, new Vector2(4,6)),
                new StationDefinition(StationKind.Power, new Vector2(4,0)),
                new StationDefinition(StationKind.Ammo, new Vector2(4,3)) },
                new[] { new Vector2(-1.9f,-1.5f), new Vector2(1.9f,-1.5f), new Vector2(-1.9f,.6f), new Vector2(1.9f,.6f),
                    new Vector2(-1.9f,2.7f), new Vector2(1.9f,2.7f), new Vector2(-1.9f,4.8f), new Vector2(1.9f,4.8f) });
            sim.Join(Captain); sim.Join(Gunner); sim.Join(Supplier);
            sim.Join(904);                                  // a full crew keeps the base pirate timetable these tests rely on
        }
        private void Advance(float seconds)
        {
            for (int n = 0; n < Mathf.CeilToInt(seconds / .05f); n++) sim.Tick(.05f);
        }
        private void Use(int target, ulong client = Captain)
        {
            int slot = sim.FindCrew(client);
            sim.Crew[slot].Position = (target < sim.Stations.Length ? sim.Stations[target].Position : sim.Seats[target - sim.Stations.Length]) + Vector2.right;
            Advance(.4f);
            sim.Interact(client, target);
        }
        private void Depart()
        {
            Use(0); Use(1); sim.HelmAction(Captain); Use(8, Gunner);
        }
        private void Supply(int rounds = 1)
        {
            for (int round = 0; round < rounds; round++) { Use(10, Supplier); Use(8, Supplier); }
        }
        private Vector2 TargetAim()
        {
            Vector3 origin = VoyageSimulation.DeckToWorld(sim.State, new Vector3(4, 1.8f, 6));
            Vector3 delta = new Vector3(sim.Encounter.PiratePosition.x, 1.5f, sim.Encounter.PiratePosition.y) - origin;
            return new Vector2(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg - sim.State.Heading,
                -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg);
        }

        [Test] public void PortHasNoThreatsAndCannotFire()
        {
            Use(8, Gunner); Advance(60);
            sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(PiratePhase.Waiting, sim.Encounter.Pirate);
            Assert.IsFalse(sim.Encounter.PowerOut);
            Assert.AreEqual(12, sim.Encounter.Ammo);
        }
        [Test] public void CannonHasExclusiveOwnerAndDisconnectCancelsReload()
        {
            Depart(); Use(8, Captain); // captain releases helm first
            Use(8, Captain);
            Assert.IsFalse(sim.Crew[0].AtCannon);
            Assert.IsTrue(sim.Crew[1].AtCannon);
            sim.FireCannon(Gunner, Vector2.zero);
            Supply();
            sim.ReloadCannon(Gunner);
            Assert.Greater(sim.Encounter.ReloadRemaining, 0);
            sim.Leave(Gunner);
            Assert.AreEqual(0, sim.Encounter.ReloadRemaining);
            Use(8, Captain);
            Assert.IsTrue(sim.Crew[0].AtCannon);
        }
        [Test] public void NonOwnerCannotAimFireOrReload()
        {
            Depart();
            sim.AimCannon(Captain, new Vector2(120, 10));
            sim.FireCannon(Captain, Vector2.zero);
            sim.ReloadCannon(999);
            Assert.AreEqual(45, sim.Encounter.CannonYaw);
            Assert.AreEqual(12, sim.Encounter.Ammo);
            Assert.AreEqual(0, sim.Encounter.ShotsFired);
        }
        [Test] public void OutOfRangePlayerCannotOperateCannon()
        {
            Depart(); sim.Crew[1].Position = new Vector2(-4, -6);
            sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(12, sim.Encounter.Ammo);
        }
        [Test] public void ReloadIsManualTimedAndCannotBeSkippedBySpam()
        {
            Depart();
            sim.FireCannon(Gunner, Vector2.zero);
            sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(11, sim.Encounter.Ammo);
            Advance(3);
            Assert.IsFalse(sim.Encounter.CannonLoaded);
            Supply();
            sim.ReloadCannon(Gunner); sim.ReloadCannon(Gunner);
            Advance(2);
            sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(11, sim.Encounter.Ammo);
            Advance(.6f);
            Assert.IsTrue(sim.Encounter.CannonLoaded);
            sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(10, sim.Encounter.Ammo);
        }
        [Test] public void NoAmmoAndNonFiniteAimCannotConsumeOrCorruptState()
        {
            Depart();
            sim.FireCannon(Gunner, new Vector2(float.NaN, 0));
            sim.AimCannon(Gunner, new Vector2(0, float.PositiveInfinity));
            Assert.AreEqual(12, sim.Encounter.Ammo);
            Assert.AreEqual(0, sim.Encounter.CannonPitch);
            sim.AimCannon(Gunner, new Vector2(800, 100));
            Assert.AreEqual(80, sim.Encounter.CannonYaw);
            Assert.AreEqual(45, sim.Encounter.CannonPitch);
            sim.Encounter.Ammo = 0;
            sim.FireCannon(Gunner, Vector2.zero);
            sim.ReloadCannon(Gunner);
            Assert.AreEqual(0, sim.Encounter.ShotsFired);
        }
        [Test] public void CannonLocksWalkingAndRequiresEmptyHands()
        {
            Use(2, Gunner); Use(8, Gunner);
            Assert.IsFalse(sim.Crew[1].AtCannon);
            Use(2, Gunner); Use(8, Gunner);
            Vector2 position = sim.Crew[1].Position;
            sim.Jump(Gunner);
            sim.SetInput(Gunner, Vector2.left); Advance(.2f);
            Assert.AreEqual(position, sim.Crew[1].Position);
            Assert.AreEqual(0, sim.Crew[1].JumpHeight);
            Assert.AreEqual(0, sim.Crew[1].VerticalSpeed);
            Advance(.4f); sim.Interact(Gunner, -1);
            Assert.IsFalse(sim.Crew[1].AtCannon);
        }
        [Test] public void WarningPrecedesAttackAndFirstHitCutsPower()
        {
            sim.Join(13); // Four-person baseline; smaller crews get slower enemy fire.
            Depart(); Advance(12);
            Assert.AreEqual(PiratePhase.Warning, sim.Encounter.Pirate);
            Assert.AreEqual(0, sim.Encounter.PirateShotsFired);
            Advance(6);
            Assert.AreEqual(PiratePhase.Attacking, sim.Encounter.Pirate);
            Advance(6);
            Assert.IsTrue(sim.Encounter.PowerOut);
            sim.Encounter.PowerOut = false;
            sim.Encounter.PowerRepairSteps = 3;
            Advance(3);
            Assert.AreEqual(1, sim.Encounter.PirateShotsFired);
            Assert.IsTrue(sim.Encounter.PowerOut);
            Assert.AreEqual(0, sim.Encounter.PowerRepairSteps);
            Assert.Less(sim.State.Health, 93);
        }
        [Test] public void IgnoredPiratesWithdrawInsteadOfAttackingForever()
        {
            Depart(); Advance(60);
            Assert.AreEqual(PiratePhase.Escaped, sim.Encounter.Pirate);
            Assert.AreEqual(0, sim.Encounter.Bounty);
            int shots = sim.Encounter.PirateShotsFired;
            Advance(10);
            Assert.AreEqual(shots, sim.Encounter.PirateShotsFired);
        }
        [Test] public void MissAndOutOfRangeShotDoNotDamagePirates()
        {
            Depart(); Advance(19);
            sim.FireCannon(Gunner, new Vector2(-100, 0));
            Assert.AreEqual(100, sim.Encounter.PirateHealth);
            Supply();
            sim.ReloadCannon(Gunner); Advance(3);
            sim.Encounter.PiratePosition = new Vector2(200, 200);
            sim.FireCannon(Gunner, TargetAim());
            Assert.AreEqual(100, sim.Encounter.PirateHealth);
        }
        [Test] public void ThreeHitsPayBountyOnlyOnceOnSuccessfulArrivalAndResetRestocks()
        {
            Depart(); Supply(2); Advance(19);
            sim.State.Heading = 35; // aim is ship-relative, not world-relative
            Vector3 escort = VoyageSimulation.DeckToWorld(sim.State, new Vector3(26, 0, 25));
            sim.Encounter.PiratePosition = new Vector2(escort.x, escort.z);
            for (int shot = 0; shot < 3; shot++)
            {
                sim.FireCannon(Gunner, TargetAim());
                Advance(1); // Damage is authoritative only when the round arrives.
                if (shot < 2) { sim.ReloadCannon(Gunner); Advance(3); }
            }
            Assert.AreEqual(PiratePhase.Defeated, sim.Encounter.Pirate);
            Assert.AreEqual(3, sim.Encounter.Hits);
            Assert.AreEqual(50, sim.Encounter.Bounty);
            Assert.AreEqual(0, sim.State.Wallet);
            sim.State.Position = VoyageSimulation.Destination;
            sim.HelmAction(Captain);
            Assert.AreEqual(50, sim.State.LastBounty);
            Assert.IsFalse(sim.Crew[1].AtCannon);
            int wallet = sim.State.Wallet;
            sim.HelmAction(Captain);
            Assert.AreEqual(wallet, sim.State.Wallet);
            int shots = sim.Encounter.PirateShotsFired;
            Advance(30);
            Assert.AreEqual(shots, sim.Encounter.PirateShotsFired);
            Use(0);
            Assert.AreEqual(PiratePhase.Waiting, sim.Encounter.Pirate);
            Assert.AreEqual(12, sim.Encounter.Ammo);
            Assert.AreEqual(0, sim.Encounter.Bounty);
            Assert.IsFalse(sim.Encounter.PowerOut);
            Assert.AreEqual(wallet, sim.State.Wallet);
        }
        [Test] public void FailureDoesNotPayPirateBounty()
        {
            Depart(); sim.Encounter.Bounty = 50;
            sim.State.Health = .01f; sim.State.Leak = 1; sim.Tick(.1f);
            Assert.AreEqual(VoyagePhase.Failed, sim.State.Phase);
            Assert.AreEqual(0, sim.State.LastBounty);
            Assert.AreEqual(0, sim.State.Wallet);
        }
        [Test] public void PowerOutBlocksNewOrdersButAllowsServingAndReturns()
        {
            Use(0); Use(2); Use(3, Gunner);
            sim.Encounter.PowerOut = true;
            Use(sim.Stations.Length); // first passenger, independent of appended stations
            Assert.AreEqual(1, sim.State.Served);
            Use(2);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            Use(3, Gunner); // held drink returns despite outage
            Assert.AreEqual(16, sim.State.DrinkStock);
            Use(3, Gunner);
            Assert.AreEqual(ServiceItem.None, sim.Crew[1].Carrying);
        }
        [Test] public void BreakersNeedThreeValidInteractionsAndRejectDistantSpam()
        {
            sim.Encounter.PowerOut = true;
            sim.Interact(Captain, 9);
            Assert.AreEqual(0, sim.Encounter.PowerRepairSteps);
            Use(9); sim.Interact(Captain, 9);
            Assert.AreEqual(1, sim.Encounter.PowerRepairSteps);
            Use(9, Gunner); Use(9);
            Assert.IsFalse(sim.Encounter.PowerOut);
            Use(2);
            Assert.AreEqual(ServiceItem.Food, sim.Crew[0].Carrying);
        }
        [Test] public void NewStationsAreNotUpgrades()
        {
            Assert.IsFalse(VoyageSimulation.IsUpgrade(StationKind.Cannon));
            Assert.IsFalse(VoyageSimulation.IsUpgrade(StationKind.Power));
            Assert.IsTrue(VoyageSimulation.IsUpgrade(StationKind.ComfortUpgrade));
        }
    }
}
