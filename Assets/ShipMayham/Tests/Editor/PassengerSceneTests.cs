using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class PassengerSceneTests
    {
        private static IEnumerable Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator PassengerRpcFlowPreservesAnchorsAndShowsCarryAndRescue()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;   // these tests are not about the gangway walk
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.Connect(true);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session;
            var sim = session.Simulation;
            var view = hud.View;
            Vector3 anchor = session.PassengerAnchors[0].localPosition;
            Transform buoy = view.PassengerVisuals[0].Find("RescueFloat_Visual");
            Assert.IsNotNull(buoy);
            sim.Crew[0].Position = new Vector2(-3, -4);
            session.InteractRpc(0, false);
            foreach (var frame in Wait(.6f)) yield return frame;
            sim.Crew[0].Position = new Vector2(-3, -1.5f);
            session.PassengerCommandRpc(0, PassengerAction.CheckTicket, 0);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.That(view.PassengerLabels[0].text, Does.Contain("DUE"));
            session.PassengerCommandRpc(0, PassengerAction.CheckTicket, 0);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.IsTrue(session.Passengers[0].FarePaid);
            Assert.AreEqual(1, session.Voyage.Value.CheckedTickets);
            Assert.AreEqual(1, session.Voyage.Value.CollectedFares);
            Assert.AreEqual(0, session.Voyage.Value.Wallet);
            session.PassengerCommandRpc(0, PassengerAction.CarryOrDrop, 0);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(0, session.LocalCarriedPassengerIndex);
            Assert.IsFalse(view.PassengerLabels[0].gameObject.activeSelf);
            Assert.That(Vector3.Distance(view.PassengerVisuals[0].position,
                view.Camera.transform.TransformPoint(new Vector3(.9f, -1.25f, 1.4f))), Is.LessThan(.02f));
            Assert.AreEqual(anchor, session.PassengerAnchors[0].localPosition);
            session.PassengerCommandRpc(-1, PassengerAction.CarryOrDrop, 0);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(-1, session.LocalCarriedPassengerIndex);
            Assert.That(session.Passengers[0].Motion, Is.EqualTo(PassengerMotion.Standing).Or.EqualTo(PassengerMotion.Returning), "put down: stands, then walks to a seat");
            Assert.IsTrue(view.PassengerLabels[0].gameObject.activeSelf);
            session.PassengerCommandRpc(0, PassengerAction.CarryOrDrop, 0);
            foreach (var frame in Wait(.6f)) yield return frame;
            sim.Crew[0].Position = new Vector2(-5, -1.5f);
            session.Simulation.Passengers[0].CanSwim = false;
            session.PassengerCommandRpc(-1, PassengerAction.Throw, -90);
            foreach (var frame in Wait(.3f)) yield return frame;
            Assert.AreEqual(PassengerMotion.Rescuing, session.Passengers[0].Motion);
            Assert.IsTrue(buoy.gameObject.activeSelf);
            Assert.That(view.PassengerLabels[0].text, Does.Contain("HELP"));
            Assert.AreEqual(1, session.Voyage.Value.RescueCount);
            Assert.AreEqual(anchor, session.PassengerAnchors[0].localPosition);
            foreach (var frame in Wait(8.2f)) yield return frame;
            Assert.AreEqual(PassengerMotion.Dizzy, session.Passengers[0].Motion, "hauled aboard");
            for (float waited = 0; session.Passengers[0].Motion != PassengerMotion.OnDeck && waited < 10; waited += Time.deltaTime) yield return null;
            Assert.AreEqual(PassengerMotion.OnDeck, session.Passengers[0].Motion);
            Assert.IsFalse(buoy.gameObject.activeSelf);
            Assert.AreEqual(-1, session.LocalCarriedPassengerIndex);
            Assert.IsTrue(session.Passengers[0].FarePaid);
            LogAssert.NoUnexpectedReceived();
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
