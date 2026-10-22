using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class FirstPersonViewTests
    {
        [TestCase(0, 0, 1)]
        [TestCase(90, 1, 0)]
        [TestCase(180, 0, -1)]
        [TestCase(270, -1, 0)]
        public void ForwardFollowsLookYaw(float yaw, float x, float z)
        {
            Assert.That(Vector2.Distance(PrototypeView.ToDeckMovement(Vector2.up, yaw), new Vector2(x, z)), Is.LessThan(.0001f));
        }

        [Test]
        public void DiagonalWalkingIsNormalizedAndAnalogMagnitudeIsPreserved()
        {
            Assert.That(PrototypeView.ToDeckMovement(Vector2.one, 37).magnitude, Is.EqualTo(1).Within(.0001f));
            Assert.That(PrototypeView.ToDeckMovement(new Vector2(.2f, .3f), 127).magnitude,
                Is.EqualTo(new Vector2(.2f, .3f).magnitude).Within(.0001f));
        }

        private static IEnumerable StartScene()
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.IsNotNull(hud);
            hud.Connect(true);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            foreach (var frame in Wait(.2f)) yield return frame;
        }

        // EditMode runners do not process nested runtime yield instructions like a coroutine.
        // Forward null frames explicitly, including during the Play Mode part of these tests.
        private static IEnumerable Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator CameraTracksLocalCrewAndCarriedItemsUseReplicatedType()
        {
            yield return new EnterPlayMode();
            foreach (var frame in StartScene()) yield return frame;
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var view = hud.View;
            var session = hud.Session;
            var simulation = session.Simulation;
            Assert.IsTrue(view.IsFirstPerson);
            Assert.IsFalse(view.CrewRoots[0].Find("Visuals").gameObject.activeSelf);
            Assert.IsFalse(view.CrewLabels[0].gameObject.activeSelf);
            Assert.That(view.Camera.fieldOfView, Is.EqualTo(75));
            Assert.That(view.Camera.nearClipPlane, Is.EqualTo(.05f));

            var renderer = view.CarriedItems[0].GetComponentInChildren<Renderer>(true);
            Color originalSharedColor = renderer.sharedMaterial.GetColor("_BaseColor");
            simulation.Crew[0].Position = new Vector2(-4, 1.5f);
            int drink = System.Array.FindIndex(session.Stations, s => s.Kind == StationKind.Drink);
            session.InteractRpc(drink, false);
            // Exercise another slot's replicated presentation without pretending it is a real client.
            int other = simulation.Join(999);
            simulation.Crew[other].Carrying = ServiceItem.Drink;
            foreach (var frame in Wait(.25f)) yield return frame;

            Assert.AreEqual(ServiceItem.Drink, session.Crew[0].Carrying);
            Assert.IsTrue(view.CarriedItems[0].gameObject.activeInHierarchy);
            AssertItemColor(view.CarriedItems[0], view.DrinkColor);
            AssertItemColor(view.CarriedItems[other], view.DrinkColor);
            Assert.IsTrue(view.CrewRoots[other].Find("Visuals").gameObject.activeInHierarchy);
            Assert.That(Vector3.Distance(view.CarriedItems[other].localPosition, new Vector3(.55f, 1, .2f)), Is.LessThan(.001f));
            Assert.AreEqual(originalSharedColor, renderer.sharedMaterial.GetColor("_BaseColor"), "Shared food material must not be changed.");

            simulation.State.Position = new Vector2(10, 20);
            simulation.State.Heading = 90;
            foreach (var frame in Wait(.25f)) yield return frame;
            Assert.That(Vector3.Distance(view.Camera.transform.position,
                view.CrewRoots[0].position + session.ShipRoot.up * view.EyeHeight), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(view.Camera.transform.rotation,
                session.ShipRoot.rotation * Quaternion.Euler(12, 0, 0)), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(view.CarriedItems[0].position,
                view.Camera.transform.TransformPoint(view.HeldItemOffset)), Is.LessThan(.001f));

            // Return the drink, then take food through the same RPC path used by the controller.
            session.InteractRpc(drink, false);
            foreach (var frame in Wait(.4f)) yield return frame;
            Assert.AreEqual(ServiceItem.None, session.Crew[0].Carrying);
            Assert.IsFalse(view.CarriedItems[0].gameObject.activeSelf);
            int food = System.Array.FindIndex(session.Stations, s => s.Kind == StationKind.Food);
            session.InteractRpc(food, false);
            foreach (var frame in Wait(.2f)) yield return frame;
            Assert.AreEqual(ServiceItem.Food, session.Crew[0].Carrying);
            AssertItemColor(view.CarriedItems[0], view.FoodColor);
            AssertItemColor(view.CarriedItems[other], view.DrinkColor);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InteractionSelectsNearbyStationInFrontNotBehind()
        {
            yield return new EnterPlayMode();
            foreach (var frame in StartScene()) yield return frame;
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.Session.Simulation.Crew[0].Position = new Vector2(-4, 1.5f);
            foreach (var frame in Wait(.6f)) yield return frame;
            int drink = System.Array.FindIndex(hud.Session.Stations, s => s.Kind == StationKind.Drink);
            Assert.AreEqual(drink, hud.View.FocusedTarget, "Equally nearby food is behind the camera.");
            hud.Session.Simulation.Crew[0].Position = new Vector2(-4, 4.5f);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(-1, hud.View.FocusedTarget, "The drink station is still in range, but now behind us.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator JumpRpcLiftsCameraAndCrewThenLandsOnMovingShip()
        {
            yield return new EnterPlayMode();
            foreach (var frame in StartScene()) yield return frame;
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var sim = hud.Session.Simulation;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Heading = 90;
            sim.State.Speed = 4;
            sim.Crew[0].Carrying = ServiceItem.Drink;
            int other = sim.Join(999);
            hud.Session.JumpRpc();
            sim.Jump(999);
            foreach (var frame in Wait(.25f)) yield return frame;
            Assert.Greater(hud.Session.Crew[0].JumpHeight, .5f);
            Assert.Greater(hud.Session.Crew[other].JumpHeight, .5f);
            Assert.Greater(hud.View.CrewRoots[other].localPosition.y, .9f);
            Assert.Greater(hud.View.Camera.transform.position.y, .5f + hud.View.EyeHeight + .3f);
            Assert.That(Vector3.Distance(hud.View.Camera.transform.position,
                hud.View.CrewRoots[0].position + hud.Session.ShipRoot.up * hud.View.EyeHeight), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(hud.View.CarriedItems[0].position,
                hud.View.Camera.transform.TransformPoint(hud.View.HeldItemOffset)), Is.LessThan(.001f));
            foreach (var frame in Wait(1)) yield return frame;
            Assert.AreEqual(0, hud.Session.Crew[0].JumpHeight);
            Assert.AreEqual(0, hud.Session.Crew[other].JumpHeight);
            Assert.That(hud.View.CrewRoots[0].localPosition.y, Is.EqualTo(.5f).Within(.01f));
            Assert.Greater(sim.State.Position.x, 2);
            Assert.AreEqual(ServiceItem.Drink, hud.Session.Crew[0].Carrying);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertItemColor(Transform item, Color expected)
        {
            var properties = new MaterialPropertyBlock();
            foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.gameObject.activeInHierarchy) continue;
                renderer.GetPropertyBlock(properties);
                Color actual = properties.GetColor("_BaseColor");
                Assert.That(Vector4.Distance(actual, expected), Is.LessThan(.001f));
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (Application.isPlaying)
            {
                if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
                yield return new ExitPlayMode();
            }
        }
    }
}
