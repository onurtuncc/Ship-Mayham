using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipMayham.Tests
{
    public sealed class AdventureSceneTests
    {
        private static IEnumerator Wait()
        {
            float deadline = Time.realtimeSinceStartup + .45f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredSceneSupportsPreparationCleaningFireAndRecoveryThroughRpc()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;   // these tests are not about the gangway walk
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>();
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session;
            var sim = session.Simulation;
            int desk = Array.FindIndex(sim.Stations, s => s.Kind == StationKind.Manifest);
            int kits = Array.FindIndex(sim.Stations, s => s.Kind == StationKind.RepairSupply);
            Assert.That(kits, Is.GreaterThanOrEqualTo(0));
            sim.State.Wallet = 500;
            sim.Crew[0].Position = sim.Stations[desk].Position + Vector2.right;
            yield return Wait();
            hud.View.OpenPurser();
            yield return Wait();
            var controller = hud.GetComponentInChildren<HudController>(true);
            var tab = Array.Find(controller.GetComponentsInChildren<Button>(true), b => b.name == "GearTab");
            Assert.IsNotNull(tab);
            tab.onClick.Invoke();
            yield return null;
            var tray = Array.Find(controller.GetComponentsInChildren<Button>(true), b => b.name == "Preparation0");
            Assert.IsTrue(tray.gameObject.activeInHierarchy);
            tray.onClick.Invoke();
            yield return Wait();
            Assert.AreEqual(ShipEquipment.ServiceTray, session.Voyage.Value.Equipment);
            Assert.AreEqual(410, session.Voyage.Value.Wallet);
            session.PrepareVoyageRpc(PreparationAction.Workshop);
            yield return Wait();
            var supplyView = hud.View.GetComponent<PrototypeRepairSupplyView>();
            Assert.AreEqual(5, supplyView.StoredKits.Length);
            Assert.AreEqual(3, supplyView.ReadyKits.Length);
            Assert.IsTrue(supplyView.StoredKits[4].gameObject.activeInHierarchy);
            session.PrepareVoyageRpc(PreparationAction.ServiceTray);
            yield return Wait();
            session.PrepareVoyageRpc(PreparationAction.SafeCourse);
            yield return Wait();
            Assert.AreEqual(PassageCourse.Safe, session.Journey.Value.Course);
            hud.View.ClosePurser();
            session.InteractRpc(desk, false);
            yield return Wait();
            Assert.That(session.Voyage.Value.Boarded, Is.GreaterThan(0));
            int drinks = Array.FindIndex(sim.Stations, s => s.Kind == StationKind.Drink);
            sim.Crew[0].Position = sim.Stations[drinks].Position + Vector2.left;
            session.InteractRpc(drinks, false); yield return Wait();
            Assert.AreEqual(2, session.Crew[0].ServiceUnits);
            var secondOrder = hud.View.CarriedItems[0].Find("Tray_SecondOrder_Visuals");
            Assert.IsTrue(secondOrder.gameObject.activeInHierarchy);
            var tint = new MaterialPropertyBlock();
            secondOrder.GetComponent<Renderer>().GetPropertyBlock(tint);
            Assert.That(Vector4.Distance(hud.View.DrinkColor, tint.GetColor("_BaseColor")), Is.LessThan(.00001f));
            session.InteractRpc(drinks, false); yield return Wait();
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Deck.Spill = true;
            sim.Crew[0].Position = sim.Deck.SpillPosition + Vector2.left;
            yield return Wait();
            Assert.IsNotNull(hud.View.GetComponent<PrototypeNavigationView>());
            Assert.IsTrue(GameObject.Find("Deck_Spill_Visuals").activeInHierarchy);
            Vector3 eye = session.ShipRoot.TransformPoint(new Vector3(sim.Crew[0].Position.x, 2, sim.Crew[0].Position.y));
            Vector3 floor = session.ShipRoot.TransformPoint(new Vector3(sim.Deck.SpillPosition.x, .55f, sim.Deck.SpillPosition.y));
            var target = InteractionTargeting.Find(session, session.Crew[0], eye, (floor - eye).normalized);
            Assert.AreEqual(InteractionKind.DeckTask, target.Kind);
            Assert.AreEqual((int)DeckAction.CleanSpill, target.Index);
            for (int i = 0; i < 3; i++) { session.DeckCommandRpc(DeckAction.CleanSpill); yield return Wait(); }
            Assert.IsFalse(session.Deck.Value.Spill);
            Assert.AreEqual(1, session.Deck.Value.Cleaned);
            sim.Emergency.Fire = FirePhase.Burning; sim.Emergency.Heat = 1;
            sim.Crew[0].Position = sim.Stations[kits].Position + Vector2.left;
            session.DeckCommandRpc(DeckAction.TakeExtinguisher);
            yield return Wait();
            Assert.AreEqual(ServiceItem.Extinguisher, session.Crew[0].Carrying);
            Assert.IsTrue(hud.View.CarriedItems[0].Find("Extinguisher_Visuals").gameObject.activeInHierarchy);
            Assert.IsTrue(GameObject.Find("ElectricalFire_Visuals").activeInHierarchy);
            sim.Crew[0].Position = VoyageSimulation.FirePoint + Vector2.left;
            for (int i = 0; i < 2; i++) { session.DeckCommandRpc(DeckAction.Extinguish); yield return Wait(); }
            Assert.AreEqual(FirePhase.Extinguished, session.Emergency.Value.Fire);
            sim.Crew[0].Position = sim.Stations[kits].Position + Vector2.left;
            session.DeckCommandRpc(DeckAction.TakeExtinguisher); yield return Wait();
            sim.Opportunity.ChestAvailable = true;
            sim.State.Position = sim.Opportunity.ChestPosition;
            sim.Crew[0].Position = VoyageSimulation.RescueRope + Vector2.right;
            session.DeckCommandRpc(DeckAction.HaulChest); yield return Wait();
            Assert.IsTrue(session.Opportunity.Value.ChestClaimed);
            Assert.AreEqual(30, session.Opportunity.Value.ChestReward);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (!Application.isPlaying) yield break;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new ExitPlayMode();
        }
    }
}
