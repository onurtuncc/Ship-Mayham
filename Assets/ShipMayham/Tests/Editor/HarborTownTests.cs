using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class HarborTownTests
    {
        private VoyageSimulation sim;
        [SetUp] public void Setup()
        {
            sim = new VoyageSimulation(new[] { new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)) }, new Vector2[8], true);
            for (ulong id = 10; id < 14; id++) { sim.Join(id, "town-" + id); sim.Crew[id - 10].OnShore = true; }
            sim.ToggleDockJob(10, DockJobKind.SalvageRun);
        }
        private static void Advance(VoyageSimulation s) { for (int i = 0; i < 8; i++) s.Tick(.05f); }
        private void Pickup(ulong id, int index)
        { sim.Crew[sim.FindCrew(id)].Position = HarborTownLayout.SalvagePoint(index) + Vector2.right; Advance(sim); sim.PickUpSalvage(id, index); }
        private void Deliver(ulong id)
        { sim.Crew[sim.FindCrew(id)].Position = VoyageSimulation.HarborPoint(HarborAction.HarborMaster) + Vector2.down; Advance(sim); sim.DockItemCommand(id, HarborAction.HarborMaster, DockJobKind.SalvageRun); }
        [Test] public void ThreeCrewCanCollectDistinctBundlesAndReceiveOneSharedReward()
        {
            for (int i = 0; i < 3; i++) Pickup((ulong)(10 + i), i);
            Pickup(13, 0); Assert.AreEqual(ServiceItem.None, sim.Crew[3].Carrying);
            for (ulong id = 10; id < 13; id++) Deliver(id);
            Assert.AreEqual(7, sim.State.DockJobDeliveredMask); Assert.AreEqual(36, sim.State.Wallet);
            Assert.AreEqual(1, sim.State.Reputation); Assert.IsFalse(sim.State.DockJobActive);
            sim.ToggleDockJob(10, DockJobKind.SalvageRun); Pickup(13, 0);
            Assert.AreEqual(ServiceItem.None, sim.Crew[3].Carrying); Assert.AreEqual(36, sim.State.Wallet);
        }
        [Test] public void RemoteOrWrongDeskCannotPickUpOrBuyUsingSalvage()
        {
            sim.Crew[0].Position = Vector2.zero; sim.PickUpSalvage(10, 0); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            Pickup(10, 0); sim.State.Wallet = 100;
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.BuyDrink) + Vector2.right; Advance(sim);
            sim.HarborCommand(10, HarborAction.BuyDrink);
            Assert.AreEqual(100, sim.State.Wallet); Assert.AreEqual(ServiceItem.SalvageA, sim.Crew[0].Carrying);
        }
        [Test] public void DisconnectAndSnapshotPreserveDeliveredBundlesAndReleaseUndeliveredOnes()
        {
            Pickup(10, 0); Deliver(10); Pickup(11, 1);
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true); restored.ImportMigrationData(sim.ExportMigrationData());
            restored.LeavePlayer("town-11"); restored.Join(40, "town-12");
            restored.Crew[2].Position = HarborTownLayout.SalvagePoint(1) + Vector2.right; Advance(restored); restored.PickUpSalvage(40, 1);
            Assert.AreEqual(ServiceItem.SalvageB, restored.Crew[2].Carrying); Assert.AreEqual(1, restored.State.DockJobDeliveredMask);
        }
        [Test] public void TownHasActualWalkableSpaceBeyondOldPierAndBuildingsBlockMovement()
        {
            sim.Crew[0].Position = new Vector2(-17, -8); sim.SetInput(10, Vector2.left); sim.Tick(.1f);
            Assert.Less(sim.Crew[0].Position.x, -17);
            sim.Crew[0].Position = new Vector2(-21.6f, -3); sim.SetInput(10, Vector2.left); sim.Tick(.1f);
            Assert.AreEqual(-21.6f, sim.Crew[0].Position.x, .001f);
            Assert.IsFalse(HarborTownLayout.Walkable(new Vector2(-25,-3)));
            foreach (HarborAction action in new[] { HarborAction.BuyFood, HarborAction.BuyDrink, HarborAction.MaintainHull, HarborAction.HarborMaster, HarborAction.PaintShop })
                Assert.IsTrue(HarborTownLayout.Walkable(VoyageSimulation.HarborPoint(action) + Vector2.right * 1.2f));
        }
        [Test] public void ReturningSalvageAllowsCancellationWithoutIncome()
        {
            Pickup(10, 1); sim.ToggleDockJob(10, DockJobKind.SalvageRun); Assert.IsTrue(sim.State.DockJobActive);
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.BuyFood) + Vector2.right; Advance(sim); sim.HarborCommand(10, HarborAction.BuyFood);
            sim.ToggleDockJob(10, DockJobKind.SalvageRun); Assert.IsFalse(sim.State.DockJobActive); Assert.AreEqual(0, sim.State.Wallet);
        }
        [Test] public void OfficeAndPaintActionsNeverFallThroughToPurchases()
        {
            sim.State.DockJobActive = false; sim.State.Wallet = 100;
            foreach (HarborAction action in new[] { HarborAction.HarborMaster, HarborAction.PaintShop })
            { sim.Crew[0].Position = VoyageSimulation.HarborPoint(action) + Vector2.right; Advance(sim); sim.HarborCommand(10, action); }
            Assert.AreEqual(100, sim.State.Wallet); Assert.AreEqual(16, sim.State.DrinkStock);
        }
        private static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        [UnityTest] public IEnumerator TownSceneRpcShowsPickupAndRemovesDeliveredSalvage()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;   // these tests are not about the gangway walk
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float end = Time.realtimeSinceStartup + 5; while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned); var session = hud.Session;
            Assert.AreEqual(18, session.Harbor.ShoreMarkers.Length);
            Assert.IsNotNull(session.Harbor.PortRoots[0].Find("Town_Visuals/Tavern/Model_HarborHut"));
            GangwayWalk.Place(session.Simulation, 0, true); yield return Wait(.6f);
            session.SelectDockJobRpc(DockJobKind.SalvageRun); yield return Wait(.6f);
            Assert.IsTrue(session.Harbor.SalvageMarkers[0].gameObject.activeSelf);
            session.Simulation.Crew[0].Position = HarborTownLayout.SalvagePoint(0) + Vector2.right;
            session.PickUpSalvageRpc(0); yield return Wait(.6f);
            Assert.AreEqual(ServiceItem.SalvageA, session.Crew[0].Carrying);
            Assert.IsFalse(session.Harbor.SalvageMarkers[0].gameObject.activeSelf);
            Assert.IsTrue(hud.View.CarriedItems[0].Find("Model_RopeCoil").gameObject.activeInHierarchy);
            session.Simulation.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.HarborMaster) + Vector2.down;
            session.DockItemCommandRpc(HarborAction.HarborMaster, DockJobKind.SalvageRun); yield return Wait(.6f);
            Assert.AreEqual(1, session.Voyage.Value.DockJobDeliveredMask); Assert.AreEqual(ServiceItem.None, session.Crew[0].Carrying);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) { if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown(); yield return new ExitPlayMode(); } }
    }
}
