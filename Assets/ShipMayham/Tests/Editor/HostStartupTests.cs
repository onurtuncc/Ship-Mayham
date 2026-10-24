using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class HostStartupTests
    {
        [UnityTest]
        public IEnumerator SerializedSceneCanStartHostWithoutPlayerPrefab() => StartHost(false);

        [UnityTest]
        public IEnumerator SoloUsesNoSocketsAndCanBoardPassengersThroughRpc() => StartHost(false, true);

        [UnityTest]
        public IEnumerator DestroyedPlayerReferenceCannotBreakHostStartup() => StartHost(true);

        private static IEnumerator StartHost(bool destroyedReference, bool solo = false)
        {
            // The bug is Editor-specific: a standalone player does not reproduce Unity fake-null.
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;   // these tests are not about the gangway walk
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            Assert.IsNotNull(hud);
            var manager = hud.Manager;
            if (destroyedReference)
            {
                var stalePrefab = new GameObject("Test_DestroyedPlayerPrefab");
                manager.NetworkConfig.PlayerPrefab = stalePrefab;
                Object.DestroyImmediate(stalePrefab);
                Assert.IsTrue(manager.NetworkConfig.PlayerPrefab == null);
                Assert.IsFalse(ReferenceEquals(manager.NetworkConfig.PlayerPrefab, null));
            }

            if (solo) hud.ConnectSolo(); else hud.Connect(true, "127.0.0.1", 0);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(manager.IsHost, "Host must start through the same Connect path as the HUD button.");
            Assert.AreEqual(PrototypeHud.NetworkProtocolVersion, manager.NetworkConfig.ProtocolVersion);
            Assert.IsTrue(hud.Session.IsSpawned);
            Assert.AreEqual(0, hud.Session.LocalCrewIndex);
            Assert.IsNull(manager.LocalClient.PlayerObject, "Crew slots are scene objects, not spawned player prefabs.");
            if (solo)
            {
                Assert.IsInstanceOf<SoloTransport>(manager.NetworkConfig.NetworkTransport);
                var sim = hud.Session.Simulation;
                sim.Crew[0].Position = sim.Stations[0].Position + Vector2.right;
                hud.Session.InteractRpc(0, false);
                yield return null;
                Assert.AreEqual(4, sim.State.Boarded);
                Assert.AreEqual(4, hud.Session.Voyage.Value.Boarded);
            }
            LogAssert.NoUnexpectedReceived();
            manager.Shutdown();
            deadline = Time.realtimeSinceStartup + 5;
            while ((manager.IsListening || manager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(manager.IsListening || manager.ShutdownInProgress);
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
