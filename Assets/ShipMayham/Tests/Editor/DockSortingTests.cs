using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class DockSortingTests
    {
        private VoyageSimulation sim;
        private static VoyageSimulation Create() => new VoyageSimulation(new[] {
            new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
            new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)) }, new Vector2[8], true);
        [SetUp] public void Setup()
        {
            sim = Create();
            for (ulong id = 10; id < 14; id++) sim.Join(id, "player-" + id);
        }
        private static void Advance(VoyageSimulation s)
        { for (int i = 0; i < 8; i++) s.Tick(.05f); }
        private static void Use(VoyageSimulation s, ulong id, HarborAction action)
        {
            if (action == HarborAction.GoAshore || action == HarborAction.BoardShip)
            {
                GangwayWalk.Cross(s, id, action == HarborAction.GoAshore);
                return;
            }
            s.Crew[s.FindCrew(id)].Position = VoyageSimulation.HarborPoint(action) + (action >= HarborAction.BuyFood ? Vector2.right * 1.2f : Vector2.zero);
            Advance(s); s.HarborCommand(id, action);
        }
        private void Start()
        {
            for (ulong id = 10; id < 14; id++) Use(sim, id, HarborAction.GoAshore);
            sim.ToggleDockJob(10, DockJobKind.LuggageSort);
        }
        private static void Deliver(VoyageSimulation s, ulong id) => Use(s, id, VoyageSimulation.LuggageDestination(s.State, s.Crew[s.FindCrew(id)].Carrying));

        [Test] public void ThreeUniqueBagsCanBeCarriedInParallelAndRewardOnlyOnce()
        {
            Start();
            for (ulong id = 10; id < 14; id++) Use(sim, id, HarborAction.BuyFood);
            Assert.AreEqual(ServiceItem.LuggageA, sim.Crew[0].Carrying);
            Assert.AreEqual(ServiceItem.LuggageB, sim.Crew[1].Carrying);
            Assert.AreEqual(ServiceItem.LuggageC, sim.Crew[2].Carrying);
            Assert.AreEqual(ServiceItem.None, sim.Crew[3].Carrying);
            for (ulong id = 10; id < 13; id++) Deliver(sim, id);
            Assert.AreEqual(7, sim.State.DockJobDeliveredMask); Assert.AreEqual(3, sim.State.DockJobProgress);
            Assert.AreEqual(30, sim.State.Wallet); Assert.AreEqual(1, sim.State.Reputation);
            Assert.IsFalse(sim.State.DockJobActive); Assert.AreEqual(1, sim.State.DockJobsCompleted);
            sim.ToggleDockJob(10, DockJobKind.LuggageSort);
            sim.ToggleDockJob(10, DockJobKind.CargoRelay);
            Assert.IsFalse(sim.State.DockJobActive); Assert.AreEqual(30, sim.State.Wallet);
        }

        [Test] public void WrongDeskDoesNotConsumeBagBuySuppliesOrPayRewards()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood);
            var item = sim.Crew[0].Carrying;
            var wrong = VoyageSimulation.LuggageDestination(sim.State, item) == HarborAction.BuyDrink ? HarborAction.MaintainHull : HarborAction.BuyDrink;
            sim.State.Wallet = 100; Use(sim, 10, wrong);
            Assert.AreEqual(item, sim.Crew[0].Carrying);
            Assert.AreEqual(0, sim.State.DockJobProgress); Assert.AreEqual(0, sim.State.DockJobDeliveredMask);
            Assert.AreEqual(100, sim.State.Wallet); Assert.AreEqual(16, sim.State.DrinkStock); Assert.AreEqual(0, sim.State.HullShield);
            Use(sim, 10, HarborAction.BoardShip); Assert.IsTrue(sim.Crew[0].OnShore);
        }

        [Test] public void ReturnAndCancellationAreAtomicAndContractCannotSwitchWithHeldBags()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood);
            sim.ToggleDockJob(10, DockJobKind.CargoRelay); Assert.AreEqual(DockJobKind.LuggageSort, sim.State.DockJobType);
            sim.ToggleDockJob(10, DockJobKind.LuggageSort); Assert.IsTrue(sim.State.DockJobActive);
            Use(sim, 10, HarborAction.BuyFood); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            sim.ToggleDockJob(10, DockJobKind.LuggageSort);
            Assert.IsFalse(sim.State.DockJobActive); Assert.AreEqual(0, sim.State.DockJobDeliveredMask);
            Assert.AreEqual(0, sim.State.Wallet);
            sim.ToggleDockJob(10, DockJobKind.CargoRelay); Assert.AreEqual(DockJobKind.CargoRelay, sim.State.DockJobType);
        }

        [Test] public void PartialJobCanBeCancelledWithoutPartialIncomeOrLostBags()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood); Deliver(sim, 10);
            Assert.AreEqual(1, sim.State.DockJobDeliveredMask);
            sim.ToggleDockJob(10, DockJobKind.LuggageSort); sim.ToggleDockJob(10, DockJobKind.LuggageSort);
            Assert.AreEqual(0, sim.State.DockJobDeliveredMask); Assert.AreEqual(0, sim.State.DockJobProgress);
            Use(sim, 10, HarborAction.BuyFood); Assert.AreEqual(ServiceItem.LuggageA, sim.Crew[0].Carrying);
            Assert.AreEqual(0, sim.State.Wallet);
        }

        [Test] public void DisconnectAndSuspendReturnOnlyUndeliveredBagsToAvailability()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood); Deliver(sim, 10);
            Use(sim, 10, HarborAction.BuyFood); Use(sim, 11, HarborAction.BuyFood);
            Assert.AreEqual(ServiceItem.LuggageB, sim.Crew[0].Carrying);
            sim.Leave(10); sim.SuspendConnection(11);
            Use(sim, 12, HarborAction.BuyFood); Use(sim, 13, HarborAction.BuyFood);
            Assert.AreEqual(ServiceItem.LuggageB, sim.Crew[2].Carrying);
            Assert.AreEqual(ServiceItem.LuggageC, sim.Crew[3].Carrying);
            Assert.AreEqual(1, sim.State.DockJobDeliveredMask);
            Deliver(sim, 12); Deliver(sim, 13); Assert.AreEqual(30, sim.State.Wallet);
        }

        [Test] public void SnapshotRestoresCompletedMaskHeldBagAndStableDestinations()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood); Deliver(sim, 10); Use(sim, 10, HarborAction.BuyFood);
            var restored = Create(); restored.ImportMigrationData(sim.ExportMigrationData()); restored.Join(77, "player-10");
            Assert.AreEqual(DockJobKind.LuggageSort, restored.State.DockJobType);
            Assert.AreEqual(1, restored.State.DockJobDeliveredMask);
            Assert.AreEqual(ServiceItem.LuggageB, restored.Crew[0].Carrying);
            Assert.AreEqual(VoyageSimulation.LuggageDestination(sim.State, ServiceItem.LuggageB), VoyageSimulation.LuggageDestination(restored.State, ServiceItem.LuggageB));
            Deliver(restored, 77); Use(restored, 77, HarborAction.BuyFood); Deliver(restored, 77);
            Assert.AreEqual(30, restored.State.Wallet); Assert.AreEqual(7, restored.State.DockJobDeliveredMask);
        }

        [Test] public void DeliveryRangeAndCooldownCannotBeBypassed()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood);
            var destination = VoyageSimulation.LuggageDestination(sim.State, sim.Crew[0].Carrying);
            Advance(sim); sim.HarborCommand(10, destination); Assert.AreEqual(0, sim.State.DockJobProgress);
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(destination) + Vector2.right;
            Use(sim, 10, destination); sim.HarborCommand(10, destination);
            Assert.AreEqual(1, sim.State.DockJobProgress); Assert.AreEqual(1, sim.State.DockJobDeliveredMask);
        }

        [Test] public void InvalidJobAndOffshoreCallerCannotStartAContract()
        {
            sim.ToggleDockJob(10, DockJobKind.LuggageSort); Assert.IsFalse(sim.State.DockJobActive);
            Use(sim, 10, HarborAction.GoAshore); sim.ToggleDockJob(10, (DockJobKind)255);
            Assert.IsFalse(sim.State.DockJobActive);
        }

        [Test] public void PortChangesDestinationsButNotAnActiveContractsRouteSelection()
        {
            Start(); var old = VoyageSimulation.LuggageDestination(sim.State, ServiceItem.LuggageA);
            sim.State.Route = RouteProfile.StormPassage;
            Assert.AreEqual(old, VoyageSimulation.LuggageDestination(sim.State, ServiceItem.LuggageA));
            var arrived = sim.State; arrived.Phase = VoyagePhase.Arrived;
            Assert.AreNotEqual(old, VoyageSimulation.LuggageDestination(arrived, ServiceItem.LuggageA));
        }

        [TestCase(false)] [TestCase(true)] public void DelayedJobInteractionCannotBecomeASupplyPurchase(bool replacementJob)
        {
            Start(); sim.State.Wallet = 100;
            sim.ToggleDockJob(10, DockJobKind.LuggageSort);
            if (replacementJob) sim.ToggleDockJob(10, DockJobKind.CargoRelay);
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.BuyFood) + Vector2.right;
            Advance(sim); sim.DockItemCommand(10, HarborAction.BuyFood, DockJobKind.LuggageSort);
            Assert.AreEqual(100, sim.State.Wallet); Assert.AreEqual(16, sim.State.FoodStock);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
        }

        [Test] public void DepartedHostReturnsHeldBagWithoutClearingOtherDeliveries()
        {
            Start(); Use(sim, 10, HarborAction.BuyFood); Deliver(sim, 10); Use(sim, 10, HarborAction.BuyFood);
            var restored = Create(); restored.ImportMigrationData(sim.ExportMigrationData());
            restored.Join(77, "player-11"); restored.LeavePlayer("player-10");
            Use(restored, 77, HarborAction.BuyFood);
            Assert.AreEqual(ServiceItem.LuggageB, restored.Crew[1].Carrying);
            Assert.AreEqual(1, restored.State.DockJobDeliveredMask);
            Deliver(restored, 77); Use(restored, 77, HarborAction.BuyFood); Deliver(restored, 77);
            Assert.AreEqual(30, restored.State.Wallet); Assert.AreEqual(7, restored.State.DockJobDeliveredMask);
        }

        [TestCase(HarborAction.BuyDrink)] [TestCase(HarborAction.MaintainHull)]
        public void ARepeatedJobDeliveryWithoutAnItemNeverBuysStockOrMaintenance(HarborAction action)
        {
            Start(); Use(sim, 10, HarborAction.BuyFood); Deliver(sim, 10);
            sim.State.Wallet = 100;
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(action) + Vector2.right;
            Advance(sim); sim.DockItemCommand(10, action, DockJobKind.LuggageSort);
            Assert.AreEqual(100, sim.State.Wallet); Assert.AreEqual(16, sim.State.DrinkStock);
            Assert.AreEqual(0, sim.State.HullShield); Assert.AreEqual(1, sim.State.DockJobProgress);
        }

        private static IEnumerator Wait(float seconds)
        { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        [UnityTest] public IEnumerator RealShoreRpcShowsTaggedSuitcaseAndReplicatesDeliveryMask()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;   // these tests are not about the gangway walk
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float end = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session; var s = session.Simulation;
            GangwayWalk.Place(s, 0, true);
            yield return Wait(.6f); session.SelectDockJobRpc(DockJobKind.LuggageSort);
            s.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.BuyFood) + Vector2.right;
            session.DockItemCommandRpc(HarborAction.BuyFood, DockJobKind.LuggageSort); yield return Wait(.3f);
            Assert.AreEqual(ServiceItem.LuggageA, session.Crew[0].Carrying);
            Transform bag = hud.View.CarriedItems[0].Find("Model_Suitcase");
            Assert.IsNotNull(bag); Assert.IsTrue(bag.gameObject.activeInHierarchy);
            Assert.IsFalse(hud.View.CarriedItems[0].Find("Visuals").gameObject.activeSelf);
            var block = new MaterialPropertyBlock(); bag.Find("Tag_Visual").GetComponent<Renderer>().GetPropertyBlock(block);
            Assert.That(Vector4.Distance(block.GetColor("_BaseColor"), VoyageSimulation.LuggageColor(ServiceItem.LuggageA)), Is.LessThan(.001f));
            yield return Wait(.2f);
            var destination = VoyageSimulation.LuggageDestination(s.State, ServiceItem.LuggageA);
            s.Crew[0].Position = VoyageSimulation.HarborPoint(destination) + Vector2.right;
            session.DockItemCommandRpc(destination, DockJobKind.LuggageSort); yield return Wait(.2f);
            Assert.AreEqual(1, session.Voyage.Value.DockJobDeliveredMask);
            Assert.AreEqual(1, session.Voyage.Value.DockJobProgress);
            Assert.IsFalse(bag.gameObject.activeSelf);
            LogAssert.NoUnexpectedReceived();
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
