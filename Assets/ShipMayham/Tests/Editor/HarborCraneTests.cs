using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class HarborCraneTests
    {
        private VoyageSimulation sim;
        [SetUp] public void Setup()
        {
            sim = new VoyageSimulation(new[] { new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)) }, new Vector2[8], true);
            for (ulong id = 10; id < 14; id++) { sim.Join(id, "crane-" + id); sim.Crew[id - 10].OnShore = true; }
            sim.ToggleDockJob(10, DockJobKind.CraneLoading);
        }
        private static void Advance(VoyageSimulation s) { for (int i = 0; i < 8; i++) s.Tick(.05f); }
        private static void Use(VoyageSimulation s, ulong id, HarborAction action)
        { s.Crew[s.FindCrew(id)].Position = VoyageSimulation.HarborPoint(action) + Vector2.right * 1.2f; Advance(s); s.HarborCommand(id, action); }
        private void Supply(ulong id) { Use(sim, id, HarborAction.FishMarket); Use(sim, id, HarborAction.CraneLoadingPad); }
        private void Hoist(bool lift = true) { Advance(sim); sim.HarborCraneAction(10, lift); }
        private static void Conserved(VoyageSimulation s)
        {
            int held = 0; foreach (var crew in s.Crew) if (crew.Active && crew.Carrying == ServiceItem.CraneCrate) held++;
            Assert.AreEqual(3, s.State.CraneStock + s.State.CraneReady + (s.State.CraneHolding ? 1 : 0) + s.State.DockJobProgress + held);
            Assert.That(s.State.CraneReady, Is.InRange(0, VoyageSimulation.CranePadCapacity));
            Assert.That(s.State.CraneStock, Is.InRange(0, 3));
        }
        [Test] public void SuppliersCanFillTwoSlotPadAndKeepThirdCrateWithoutDuplication()
        {
            Supply(11); Supply(12); Supply(13); Use(sim, 10, HarborAction.FishMarket);
            Assert.AreEqual(2, sim.State.CraneReady); Assert.AreEqual(ServiceItem.CraneCrate, sim.Crew[3].Carrying);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying); Conserved(sim);
        }
        [Test] public void OperatorMovesHookNotBodyAndCannotJumpOrStealOccupiedCrane()
        {
            Use(sim, 10, HarborAction.CraneControls); Use(sim, 11, HarborAction.CraneControls);
            Assert.IsTrue(sim.Crew[0].AtCrane); Assert.IsFalse(sim.Crew[1].AtCrane);
            Vector2 body = sim.Crew[0].Position; sim.Jump(10);
            for (int i = 0; i < 100; i++) { sim.SetInput(10, Vector2.left); sim.Tick(.05f); }
            Assert.AreEqual(body, sim.Crew[0].Position); Assert.AreEqual(0, sim.Crew[0].JumpHeight);
            Assert.AreEqual(VoyageSimulation.CraneTravel.xMin, sim.State.CraneHook.x, .001f); Conserved(sim);
        }
        [Test] public void ThreeCratesOnBargePayOnceAndReleaseOperator()
        {
            Use(sim, 10, HarborAction.CraneControls);
            for (int i = 0; i < 3; i++)
            {
                Supply(11); sim.State.CraneHook = VoyageSimulation.HarborPoint(HarborAction.CraneLoadingPad); Hoist();
                Assert.IsTrue(sim.State.CraneHolding); sim.State.CraneHook = VoyageSimulation.CraneBarge; Hoist(); Conserved(sim);
            }
            Assert.AreEqual(45, sim.State.Wallet); Assert.AreEqual(1, sim.State.Reputation);
            Assert.IsFalse(sim.Crew[0].AtCrane); Assert.IsFalse(sim.State.DockJobActive);
            Hoist(); sim.ToggleDockJob(10, DockJobKind.CraneLoading); Assert.AreEqual(45, sim.State.Wallet); Conserved(sim);
        }
        [Test] public void MisdropIsRecoveredAndHeldHookPreventsCancellation()
        {
            Supply(11); Use(sim, 10, HarborAction.CraneControls); Hoist();
            sim.ToggleDockJob(11, DockJobKind.CraneLoading); Assert.IsTrue(sim.State.DockJobActive);
            Supply(12); Supply(13); Hoist();
            Assert.IsFalse(sim.State.CraneHolding); Assert.AreEqual(0, sim.State.DockJobProgress);
            Assert.AreEqual(1, sim.State.CraneStock); Assert.AreEqual(0, sim.State.Wallet); Conserved(sim);
        }
        [Test] public void SnapshotAndDepartedOperatorKeepHookLoadForReplacement()
        {
            Supply(11); Use(sim, 10, HarborAction.CraneControls); Hoist();
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true); restored.ImportMigrationData(sim.ExportMigrationData());
            restored.LeavePlayer("crane-10"); restored.Join(40, "crane-11"); Use(restored, 40, HarborAction.CraneControls);
            Assert.IsTrue(restored.Crew[1].AtCrane); Assert.IsTrue(restored.State.CraneHolding);
            restored.State.CraneHook = VoyageSimulation.CraneBarge; Advance(restored); restored.HarborCraneAction(40, true);
            Assert.AreEqual(1, restored.State.DockJobProgress); Conserved(restored);
        }
        [Test] public void RangeClosedJobAndWrongDeskDoNotMoveCratesOrMoney()
        {
            sim.Crew[0].Position = Vector2.zero; sim.HarborCommand(10, HarborAction.FishMarket); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            Use(sim, 11, HarborAction.FishMarket); sim.State.Wallet = 100; Use(sim, 11, HarborAction.BuyDrink);
            Assert.AreEqual(100, sim.State.Wallet); Assert.AreEqual(ServiceItem.CraneCrate, sim.Crew[1].Carrying);
            sim.SuspendConnection(11); Assert.AreEqual(3, sim.State.CraneStock); Conserved(sim);
            sim.State.Phase = VoyagePhase.Sailing; Use(sim, 12, HarborAction.FishMarket); Assert.AreEqual(ServiceItem.None, sim.Crew[2].Carrying);
        }
        private static IEnumerator Wait(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        [UnityTest] public IEnumerator SceneRpcCarriesCrateUsesCabViewAndLandsOnBarge()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float end = Time.realtimeSinceStartup + 5; while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned); var session = hud.Session;
            session.Simulation.Crew[0].Position = VoyageSimulation.ShipGangway; session.HarborCommandRpc(HarborAction.GoAshore); yield return Wait(.4f);
            session.SelectDockJobRpc(DockJobKind.CraneLoading); yield return Wait(.4f);
            session.Simulation.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.FishMarket) + Vector2.right;
            session.HarborCommandRpc(HarborAction.FishMarket); yield return Wait(.4f);
            Assert.IsTrue(hud.View.CarriedItems[0].Find("Model_CrateStack").gameObject.activeInHierarchy);
            session.Simulation.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.CraneLoadingPad) + Vector2.right;
            session.HarborCommandRpc(HarborAction.CraneLoadingPad); yield return Wait(.4f);
            session.Simulation.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.CraneControls) + Vector2.right;
            session.HarborCommandRpc(HarborAction.CraneControls); yield return Wait(.4f);
            Assert.IsTrue(session.Crew[0].AtCrane); Assert.Greater(hud.View.Camera.transform.position.y, 10);
            session.HarborCraneActionRpc(true); yield return Wait(.4f); Assert.IsTrue(session.Voyage.Value.CraneHolding);
            var presentation = hud.View.GetComponent<PrototypeHarborCraneView>(); Assert.IsTrue(presentation.HangingCrates[0].gameObject.activeInHierarchy);
            session.Simulation.State.CraneHook = VoyageSimulation.CraneBarge;
            session.HarborCraneActionRpc(true); yield return Wait(.4f);
            Assert.AreEqual(1, session.Voyage.Value.DockJobProgress); Assert.IsTrue(presentation.DeliveredCrates[0].gameObject.activeSelf);
            session.HarborCraneActionRpc(false); yield return Wait(.4f); Assert.IsFalse(session.Crew[0].AtCrane);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if (Application.isPlaying) { if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown(); yield return new ExitPlayMode(); } }
    }
}
