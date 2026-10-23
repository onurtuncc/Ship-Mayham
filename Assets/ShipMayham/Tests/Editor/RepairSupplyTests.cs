using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class RepairSupplyTests
    {
        private VoyageSimulation sim;
        private const int Store = 2, Bench = 3;
        [SetUp] public void Setup()
        {
            sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
                new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                new StationDefinition(StationKind.RepairSupply, new Vector2(4,-5.5f)),
                new StationDefinition(StationKind.Repair, new Vector2(4,-3)),
                new StationDefinition(StationKind.Food, new Vector2(-4,0)) }, new Vector2[8], true);
            for (ulong id = 10; id < 14; id++) sim.Join(id, "repair-" + id);
        }
        private static void Advance(VoyageSimulation s) { for (int i = 0; i < 8; i++) s.Tick(.05f); }
        private static string Use(VoyageSimulation s, ulong client, int station)
        {
            s.Crew[s.FindCrew(client)].Position = s.Stations[station].Position + Vector2.left;
            Advance(s); return s.Interact(client, station);
        }
        private string Use(ulong client, int station) => Use(sim, client, station);
        private void Conserved(VoyageSimulation s = null)
        {
            s ??= sim;
            int held = 0;
            foreach (var crew in s.Crew) if (crew.Active && crew.Carrying == ServiceItem.RepairKit) held++;
            Assert.AreEqual(VoyageSimulation.VoyageRepairKits, s.State.RepairKitStock + s.State.ReadyRepairKits + s.State.RepairKitsUsed + held);
            Assert.That(s.State.RepairKitStock, Is.InRange(0, VoyageSimulation.VoyageRepairKits));
            Assert.That(s.State.ReadyRepairKits, Is.InRange(0, VoyageSimulation.RepairKitBufferCapacity));
        }

        [Test] public void ThreeKitsAreFiniteAndFourthCrewCannotDuplicateThem()
        {
            for (ulong id = 10; id < 14; id++) Use(id, Store);
            for (int i = 0; i < 3; i++) Assert.AreEqual(ServiceItem.RepairKit, sim.Crew[i].Carrying);
            Assert.AreEqual(ServiceItem.None, sim.Crew[3].Carrying); Conserved();
        }
        [Test] public void PickupReturnAndDuplicatePickupRespectCooldown()
        {
            Use(10, Store); sim.Interact(10, Store);
            Assert.AreEqual(2, sim.State.RepairKitStock); Conserved();
            Use(10, Store); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying); Conserved();
        }
        [Test] public void TwoBenchSlotsDoNotConsumeThirdHeldKit()
        {
            for (ulong id = 10; id < 13; id++) { Use(id, Store); Use(id, Bench); }
            Assert.AreEqual(2, sim.State.ReadyRepairKits); Assert.AreEqual(ServiceItem.RepairKit, sim.Crew[2].Carrying);
            Use(12, Store); Conserved();
        }
        [Test] public void DeliveryPreparesButDoesNotApplyPatchAndAnotherCrewCanUseIt()
        {
            sim.State.Leak = 1;
            Use(10, Store); Use(10, Bench);
            Assert.AreEqual(1, sim.State.Leak); Assert.AreEqual(1, sim.State.ReadyRepairKits);
            Use(11, Bench);
            Assert.AreEqual(0, sim.State.Leak); Assert.AreEqual(1, sim.State.RepairKitsUsed);
            Assert.AreEqual(4, sim.State.RepairSteps); Conserved();
        }
        [Test] public void WatertightHullDoesNotConsumeReadyKit()
        {
            Use(10, Store); Use(10, Bench); Use(11, Bench);
            Assert.AreEqual(1, sim.State.ReadyRepairKits); Assert.AreEqual(0, sim.State.RepairKitsUsed); Conserved();
        }
        [Test] public void NoKitsStillAllowsFourStepManualRepair()
        {
            sim.State.Leak = 1;
            sim.State.Health = 70;
            for (int step = 1; step <= 4; step++)
            {
                Use(10, Bench);
                Assert.AreEqual(step < 4 ? 1 : 0, sim.State.Leak);
            }
            Assert.AreEqual(70, sim.State.Health);
            Assert.AreEqual(0, sim.State.RepairKitsUsed); Conserved();
        }
        [Test] public void FoodDoesNotDisappearWhenTakingOrApplyingKit()
        {
            Use(10, Store); Use(10, Bench); Use(11, 4);
            sim.State.Leak = 1;
            Use(11, Store); Use(11, Bench);
            Assert.AreEqual(ServiceItem.Food, sim.Crew[1].Carrying);
            Assert.AreEqual(1, sim.State.ReadyRepairKits); Assert.AreEqual(1, sim.State.Leak); Conserved();
        }
        [Test] public void HeldKitCannotOperateOtherStationsOrGoAshore()
        {
            Use(10, Store); Use(10, 0); Use(10, 1); Use(10, 4);
            Assert.AreEqual(0, sim.State.Boarded); Assert.IsFalse(sim.Crew[0].AtHelm);
            sim.Crew[0].Position = VoyageSimulation.ShipGangway; Advance(sim);
            sim.HarborCommand(10, HarborAction.GoAshore);
            Assert.IsFalse(sim.Crew[0].OnShore); Assert.AreEqual(ServiceItem.RepairKit, sim.Crew[0].Carrying); Conserved();
        }
        [Test] public void RangeAndAirborneAndShoreChecksRejectPickup()
        {
            sim.Interact(10, Store); Assert.AreEqual(3, sim.State.RepairKitStock);
            Advance(sim);
            sim.Crew[0].Position = sim.Stations[Store].Position; sim.Crew[0].JumpHeight = 1;
            sim.Interact(10, Store); Assert.AreEqual(3, sim.State.RepairKitStock);
            sim.Crew[0].JumpHeight = 0; sim.Crew[0].OnShore = true; Advance(sim);
            sim.Interact(10, Store); Assert.AreEqual(3, sim.State.RepairKitStock); Conserved();
        }
        [Test] public void LeaveAndTemporaryDisconnectReturnHeldKitOnlyOnce()
        {
            Use(10, Store); sim.SuspendConnection(10); sim.SuspendConnection(10);
            Assert.AreEqual(3, sim.State.RepairKitStock); Conserved();
            sim.Join(20, "repair-10"); Use(20, Store); sim.Leave(20); sim.Leave(20); Conserved();
        }
        [Test] public void MigrationPreservesStoreBenchHeldAndConsumedAndDepartedHostRecovery()
        {
            Use(10, Store); Use(10, Bench); sim.State.Leak = 1; Use(11, Bench);
            Use(10, Store); Use(10, Bench); Use(12, Store);
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true);
            restored.ImportMigrationData(sim.ExportMigrationData());
            restored.Join(50, "repair-12"); Assert.AreEqual(ServiceItem.RepairKit, restored.Crew[2].Carrying);
            Assert.AreEqual(1, restored.State.ReadyRepairKits); Assert.AreEqual(1, restored.State.RepairKitsUsed); Conserved(restored);
            restored.LeavePlayer("repair-12"); Assert.AreEqual(1, restored.State.RepairKitStock); Conserved(restored);
        }
        [Test] public void PreviousV8SnapshotIsRejected()
        {
            byte[] old = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(sim.ExportMigrationData()).Replace("\"Version\":10", "\"Version\":9"));
            Assert.Throws<InvalidOperationException>(() => sim.ImportMigrationData(old));
            Assert.AreEqual(14, PrototypeHud.NetworkProtocolVersion);
        }
        [TestCase(VoyagePhase.Arrived)] [TestCase(VoyagePhase.Failed)]
        public void ClosedVoyageDoesNotMoveOrConsumeSupplies(VoyagePhase phase)
        {
            Use(10, Store); Use(10, Bench); sim.State.Phase = phase; sim.State.Leak = 1;
            Use(11, Store); Use(11, Bench);
            Assert.AreEqual(1, sim.State.ReadyRepairKits); Assert.AreEqual(1, sim.State.Leak); Conserved();
        }
        [Test] public void NewVoyageRestocksThreeAndClearsConsumedAndReadyCounts()
        {
            Use(10, Store); Use(10, Bench); sim.State.Leak = 1; Use(11, Bench);
            Use(10, Store); Use(10, Bench); sim.State.Phase = VoyagePhase.Arrived;
            Use(11, 0);
            Assert.AreEqual(2, sim.State.Voyage); Assert.AreEqual(0, sim.State.ReadyRepairKits);
            Assert.AreEqual(0, sim.State.RepairKitsUsed); Conserved();
        }

        [Test] public void ExhaustedSupplyStillAllowsManualPatchWithoutCreatingKits()
        {
            for (int kit = 0; kit < VoyageSimulation.VoyageRepairKits; kit++)
            {
                Use(10, Store); Use(10, Bench); sim.State.Leak = 1; Use(11, Bench);
            }
            Use(10, Store); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            sim.State.Leak = 1; sim.State.RepairSteps = 0;
            for (int step = 0; step < 4; step++) Use(10, Bench);
            Assert.AreEqual(0, sim.State.Leak); Assert.AreEqual(3, sim.State.RepairKitsUsed); Conserved();
        }

        [TestCase(70f, 78f)] [TestCase(98f, 100f)]
        public void QuickPatchRestoresLimitedHullWithoutHealingWatertightShip(float health, float expected)
        {
            Use(10, Store); Use(10, Bench); sim.State.Health = health;
            Use(11, Bench); Assert.AreEqual(health, sim.State.Health);
            sim.State.Leak = 1; sim.State.RepairSteps = 2; Use(11, Bench);
            Assert.AreEqual(expected, sim.State.Health); Assert.AreEqual(4, sim.State.RepairSteps); Conserved();
        }

        [Test] public void QuickPatchPostponesNextScheduledLeakForFortyFiveSeconds()
        {
            Use(10, Store); Use(10, Bench); sim.State.Leak = 1;
            sim.State.Phase = VoyagePhase.Sailing; sim.Event.NextIn = 1000;
            Use(11, Bench);
            for (int i = 0; i < 880; i++) sim.Tick(.05f);
            Assert.AreEqual(0, sim.State.Leak);
            for (int i = 0; i < 24; i++) sim.Tick(.05f);
            Assert.AreEqual(1, sim.State.Leak); Assert.AreEqual(0, sim.State.RepairSteps); Conserved();
        }

        private static IEnumerator Wait(float seconds)
        { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }
        [UnityTest] public IEnumerator RealRpcDisplaysToolboxAndReplicatesReadyAndUsedKits()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session;
            int store = Array.FindIndex(session.Stations, s => s.Kind == StationKind.RepairSupply);
            int bench = Array.FindIndex(session.Stations, s => s.Kind == StationKind.Repair);
            Assert.That(store, Is.GreaterThanOrEqualTo(0));
            session.Simulation.Crew[0].Position = session.Simulation.Stations[store].Position + Vector2.left;
            session.InteractRpc(store, false); yield return Wait(.6f);
            Assert.AreEqual(ServiceItem.RepairKit, session.Crew[0].Carrying);
            var box = hud.View.CarriedItems[0].Find("Model_Toolbox");
            Assert.IsNotNull(box); Assert.IsTrue(box.gameObject.activeInHierarchy);
            Assert.AreEqual(Vector3.one * .7f, box.localScale);
            var renderers = box.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.magnitude, Is.InRange(.2f, .7f), "Held toolbox must be readable without filling the first-person view.");
            Assert.IsFalse(hud.View.CarriedItems[0].Find("Visuals").gameObject.activeSelf);
            var presentation = hud.View.GetComponent<PrototypeRepairSupplyView>();
            Assert.IsFalse(presentation.StoredKits[2].gameObject.activeSelf);
            session.Simulation.Crew[0].Position = session.Simulation.Stations[bench].Position + Vector2.left;
            session.InteractRpc(bench, false); yield return Wait(.6f);
            Assert.AreEqual(1, session.Voyage.Value.ReadyRepairKits); Assert.IsTrue(presentation.ReadyKits[0].gameObject.activeSelf);
            Assert.IsFalse(box.gameObject.activeSelf);
            session.Simulation.State.Leak = 1; session.InteractRpc(bench, false); yield return Wait(.6f);
            Assert.AreEqual(1, session.Voyage.Value.RepairKitsUsed); Assert.AreEqual(0, session.Voyage.Value.Leak);
            Assert.IsFalse(presentation.ReadyKits[0].gameObject.activeSelf); LogAssert.NoUnexpectedReceived();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (Application.isPlaying)
            {
                if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
                yield return new ExitPlayMode();
            }
        }
    }
}
