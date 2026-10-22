using System;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class AmmoFlowTests
    {
        private VoyageSimulation sim;
        private const ulong Captain = 10, Gunner = 11, Supplier = 12, Other = 13;
        private const int Magazine = 2, Cannon = 3;

        [SetUp] public void Setup()
        {
            var seats = new Vector2[8];
            for (int i = 0; i < seats.Length; i++) seats[i] = new Vector2(i % 2 == 0 ? -1.9f : 1.9f, -1.5f + i / 2 * 2.1f);
            sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Ammo, new Vector2(4, 3)),
                new StationDefinition(StationKind.Cannon, new Vector2(4, 6)),
                new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4, 3)),
                new StationDefinition(StationKind.Repair, new Vector2(4, -3)),
                new StationDefinition(StationKind.Power, new Vector2(4, 0)) }, seats, true);
            sim.Join(Captain, "captain"); sim.Join(Gunner, "gunner");
            sim.Join(Supplier, "supplier"); sim.Join(Other, "other");
        }

        private void Advance(float seconds = .4f)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++) sim.Tick(.02f);
        }
        private string Use(ulong client, int station)
        {
            sim.Crew[sim.FindCrew(client)].Position = sim.Stations[station].Position + Vector2.left;
            Advance();
            return sim.Interact(client, station);
        }
        private void Depart()
        {
            Use(Captain, 0); Use(Captain, 1); sim.HelmAction(Captain);
            Use(Gunner, Cannon);
            sim.Encounter.Pirate = PiratePhase.Defeated;
        }
        private void Supply() { Use(Supplier, Magazine); Use(Supplier, Cannon); }
        private void Conserved(VoyageSimulation voyage = null)
        {
            voyage ??= sim;
            int held = 0;
            foreach (var crew in voyage.Crew) if (crew.Active && crew.Carrying == ServiceItem.Ammo) held++;
            var e = voyage.Encounter;
            int reserved = e.ReloadRemaining > 0 ? 1 : 0;
            Assert.AreEqual(e.Ammo, e.MagazineAmmo + e.ReadyAmmo + held + reserved + (e.CannonLoaded ? 1 : 0));
            Assert.That(e.MagazineAmmo, Is.GreaterThanOrEqualTo(0));
            Assert.That(e.ReadyAmmo + reserved, Is.InRange(0, VoyageSimulation.CannonBufferCapacity));
            Assert.That(e.Ammo, Is.InRange(0, VoyageSimulation.VoyageAmmo));
        }

        [Test] public void StartHasOneLoadedRoundAndElevenInMagazine()
        {
            Assert.AreEqual(12, sim.Encounter.Ammo);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Assert.AreEqual(0, sim.Encounter.ReadyAmmo);
            Assert.IsTrue(sim.Encounter.CannonLoaded);
            Conserved();
        }

        [Test] public void PickupAndReturnAreCooldownProtectedAndDoNotCreateRounds()
        {
            Use(Supplier, Magazine);
            Assert.AreEqual(ServiceItem.Ammo, sim.Crew[2].Carrying);
            Assert.AreEqual(10, sim.Encounter.MagazineAmmo);
            sim.Interact(Supplier, Magazine);
            Assert.AreEqual(ServiceItem.Ammo, sim.Crew[2].Carrying);
            Conserved();
            Use(Supplier, Magazine);
            Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Conserved();
        }

        [Test] public void CrewCanSupplyAnOccupiedCannonWithoutStealingIt()
        {
            Depart(); Supply();
            Assert.IsTrue(sim.Crew[1].AtCannon);
            Assert.IsFalse(sim.Crew[2].AtCannon);
            Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
            Assert.AreEqual(1, sim.Encounter.ReadyAmmo);
            Assert.AreEqual(12, sim.Encounter.Ammo);
            Conserved();
        }

        [Test] public void EmptyRackCannotReloadOrTeleportMagazineStock()
        {
            Depart(); sim.FireCannon(Gunner, Vector2.zero);
            StringAssert.Contains("rack empty", sim.ReloadCannon(Gunner));
            Advance(3);
            Assert.IsFalse(sim.Encounter.CannonLoaded);
            Assert.AreEqual(0, sim.Encounter.ReloadRemaining);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Conserved();
        }

        [Test] public void ReloadReservesOneDeliveredRoundAndCannotBeSpammed()
        {
            Depart(); Supply(); sim.FireCannon(Gunner, Vector2.zero);
            sim.ReloadCannon(Gunner); sim.ReloadCannon(Gunner);
            Assert.AreEqual(0, sim.Encounter.ReadyAmmo);
            Assert.Greater(sim.Encounter.ReloadRemaining, 0);
            Conserved();
            Advance(2);
            Assert.IsFalse(sim.Encounter.CannonLoaded);
            Advance(.6f);
            Assert.IsTrue(sim.Encounter.CannonLoaded);
            sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(10, sim.Encounter.Ammo);
            Conserved();
        }

        [Test] public void FullRackRejectsDeliveryAndKeepsRoundInCrewHands()
        {
            Depart(); Supply(); Supply(); Supply();
            Use(Other, Magazine);
            StringAssert.Contains("rack full", Use(Other, Cannon));
            Assert.AreEqual(ServiceItem.Ammo, sim.Crew[3].Carrying);
            Assert.AreEqual(3, sim.Encounter.ReadyAmmo);
            Conserved();
        }

        [Test] public void PendingReloadOccupiesRackCapacityAndDisconnectReturnsReservation()
        {
            Depart(); Supply(); Supply(); Supply();
            sim.FireCannon(Gunner, Vector2.zero); sim.ReloadCannon(Gunner);
            Use(Other, Magazine);
            StringAssert.Contains("rack full", Use(Other, Cannon));
            sim.Leave(Gunner);
            Assert.AreEqual(0, sim.Encounter.ReloadRemaining);
            Assert.AreEqual(3, sim.Encounter.ReadyAmmo);
            Assert.IsFalse(sim.Encounter.CannonLoaded);
            Conserved();
        }

        [Test] public void SupplierDisconnectReturnsHeldRoundToMagazine()
        {
            Use(Supplier, Magazine); sim.Leave(Supplier);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Conserved();
        }

        [Test] public void HandsMustBeEmptyAndAmmoCannotBeLaunderedAtOtherStations()
        {
            Use(Supplier, 4);
            Use(Supplier, Magazine);
            Assert.AreEqual(ServiceItem.Food, sim.Crew[2].Carrying);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Use(Supplier, 4); Use(Supplier, Magazine);
            int food = sim.State.FoodStock;
            foreach (int station in new[] { 0, 1, 4, 5, 6, 7 }) Use(Supplier, station);
            Assert.AreEqual(ServiceItem.Ammo, sim.Crew[2].Carrying);
            Assert.IsFalse(sim.Crew[2].AtHelm);
            Assert.AreEqual(food, sim.State.FoodStock);
            Conserved();
        }

        [Test] public void DistantUnknownAndAirborneRequestsCannotTakeAmmo()
        {
            sim.Interact(999, Magazine); sim.Interact(Supplier, Magazine);
            sim.Crew[2].Position = sim.Stations[Magazine].Position + Vector2.left;
            Advance(); sim.Jump(Supplier); Advance(.05f); sim.Interact(Supplier, Magazine);
            Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Conserved();
        }

        [Test] public void AmmoCannotBeTakenAshoreAndSlowsWalking()
        {
            Use(Supplier, Magazine);
            sim.Crew[2].Position = VoyageSimulation.ShipGangway;
            Advance(); sim.HarborCommand(Supplier, HarborAction.GoAshore);
            Assert.IsFalse(sim.Crew[2].OnShore);
            sim.Crew[2].Position = new Vector2(0, -6);
            sim.SetInput(Supplier, Vector2.up);
            sim.Tick(.1f);
            Assert.AreEqual(-6 + 4.2f * .8f * .1f, sim.Crew[2].Position.y, .001f);
            Conserved();
        }

        [Test] public void AllTwelveRoundsMustBeTransportedBeforeFiringAndCannotGoNegative()
        {
            Depart();
            for (int shot = 0; shot < 12; shot++)
            {
                if (!sim.Encounter.CannonLoaded) { Supply(); sim.ReloadCannon(Gunner); Advance(2.6f); }
                sim.FireCannon(Gunner, Vector2.zero);
                Conserved();
            }
            Assert.AreEqual(12, sim.Encounter.ShotsFired);
            Assert.AreEqual(0, sim.Encounter.Ammo);
            Use(Supplier, Magazine); sim.ReloadCannon(Gunner); sim.FireCannon(Gunner, Vector2.zero);
            Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
            Conserved();
        }

        [Test] public void SettlementReturnsHeldAndReservedRoundsAndResetRestocksExactlyTwelve()
        {
            Depart(); Supply(); Use(Supplier, Magazine);
            sim.FireCannon(Gunner, Vector2.zero); sim.ReloadCannon(Gunner);
            sim.State.Position = VoyageSimulation.Destination;
            sim.HelmAction(Captain);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase);
            Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
            Assert.AreEqual(0, sim.Encounter.ReloadRemaining);
            Conserved();
            Use(Captain, 0);
            Assert.AreEqual(12, sim.Encounter.Ammo);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Conserved();
        }

        [Test] public void MigrationPreservesHandsRackAndReservedRoundWithoutDuplicatingOnDeparture()
        {
            Depart(); Supply(); Supply(); Use(Supplier, Magazine);
            sim.FireCannon(Gunner, Vector2.zero); sim.ReloadCannon(Gunner);
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true);
            restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.AreEqual(ServiceItem.Ammo, restored.Crew[2].Carrying);
            Assert.AreEqual(1, restored.Encounter.ReadyAmmo);
            Assert.Greater(restored.Encounter.ReloadRemaining, 0);
            Conserved(restored);
            restored.LeavePlayer("supplier"); restored.LeavePlayer("gunner");
            Assert.AreEqual(2, restored.Encounter.ReadyAmmo);
            Conserved(restored);
        }

        [Test] public void PreAmmoSnapshotVersionIsRejected()
        {
            string old = Encoding.UTF8.GetString(sim.ExportMigrationData()).Replace("\"Version\":4", "\"Version\":3");
            Assert.Throws<InvalidOperationException>(() => sim.ImportMigrationData(Encoding.UTF8.GetBytes(old)));
        }

        [Test] public void SuspendedSupplierReturnsRoundOnceButKeepsStableReconnectionSlot()
        {
            Use(Supplier, Magazine);
            Vector2 position = sim.Crew[2].Position;
            sim.SuspendConnection(Supplier); sim.SuspendConnection(Supplier);
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Assert.IsTrue(sim.Crew[2].Active);
            Assert.AreEqual(-1, sim.FindCrew(Supplier));
            Assert.AreEqual(-1, sim.FindCrew(ulong.MaxValue));
            Assert.AreEqual(-1, sim.Join(ulong.MaxValue, "supplier"));
            Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
            Assert.AreEqual(2, sim.Join(77, "supplier"));
            Assert.AreEqual(position, sim.Crew[2].Position);
            sim.LeavePlayer("supplier");
            Assert.AreEqual(11, sim.Encounter.MagazineAmmo);
            Conserved();
        }

        [Test] public void SuspendedGunnerReleasesStationAndReservedRoundImmediately()
        {
            Depart(); Supply(); sim.FireCannon(Gunner, Vector2.zero); sim.ReloadCannon(Gunner);
            sim.SuspendConnection(Gunner);
            Assert.AreEqual(1, sim.Encounter.ReadyAmmo);
            Assert.AreEqual(0, sim.Encounter.ReloadRemaining);
            Assert.IsFalse(sim.Crew[1].AtCannon);
            Use(Other, Cannon);
            Assert.IsTrue(sim.Crew[3].AtCannon);
            Assert.AreEqual(1, sim.Join(77, "gunner"));
            Assert.IsFalse(sim.Crew[1].AtCannon, "A reconnect must not steal the replacement gunner's role.");
            Conserved();
        }

        [Test] public void SuspendedAshorePlayerCannotBlockDeparture()
        {
            sim.Crew[2].Position = VoyageSimulation.ShipGangway;
            sim.HarborCommand(Supplier, HarborAction.GoAshore);
            Assert.AreEqual(1, sim.CrewAshore);
            sim.SuspendConnection(Supplier);
            Assert.AreEqual(0, sim.CrewAshore);
            Assert.AreEqual(VoyageSimulation.ShipGangway, sim.Crew[2].Position);
            Use(Captain, 0); Use(Captain, 1); sim.HelmAction(Captain);
            Assert.AreEqual(VoyagePhase.Sailing, sim.State.Phase);
            Conserved();
        }
    }
}
