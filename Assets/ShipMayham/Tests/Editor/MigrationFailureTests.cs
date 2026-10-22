using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class MigrationFailureTests
    {
        [UnityTest]
        public IEnumerator DepartureBeforeVoyageSpawnIsQueued()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.ConnectSolo();
            float timeout = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(hud.Manager.IsServer);
            var original = hud.Session;
            var pending = new GameObject("PendingVoyageTest", typeof(NetworkObject), typeof(PrototypeSession));
            try
            {
                hud.Session = pending.GetComponent<PrototypeSession>();
                Assert.IsFalse(hud.Session.IsSpawned);
                Assert.IsNull(hud.Session.Simulation);
                typeof(PrototypeHud).GetMethod("OnlinePlayerLeft", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hud, new object[] { "departed-before-spawn" });
                var departures = (HashSet<string>)typeof(PrototypeHud)
                    .GetField("pendingDepartures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                CollectionAssert.Contains(departures, "departed-before-spawn", "An unapplied departure must survive until the voyage is ready.");
            }
            finally { hud.Session = original; Object.DestroyImmediate(pending); }
        }

        [UnityTest]
        public IEnumerator DepartureReconciliationPreservesCurrentMembersAndRemovesAbsentPlayers()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.ConnectSolo();
            float timeout = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(hud.Session.IsServer);
            var sim = hud.Session.Simulation;
            int slot = sim.Join(77, "current-peer");
            Assert.GreaterOrEqual(slot, 0);
            var left = typeof(PrototypeHud).GetMethod("OnlinePlayerLeft", BindingFlags.Instance | BindingFlags.NonPublic);
            var reconcile = typeof(PrototypeHud).GetMethod("ReconcileOnlineDepartures", BindingFlags.Instance | BindingFlags.NonPublic);
            var admitted = (HashSet<string>)typeof(PrototypeHud).GetField("admittedPlayers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            var pending = (HashSet<string>)typeof(PrototypeHud).GetField("pendingDepartures", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            admitted.Add("current-peer");
            left.Invoke(hud, new object[] { "current-peer" });
            // A stale/duplicated indexed departure must not clear a current member,
            // even while its NGO connection has not rebound after migration.
            sim.SuspendConnection(77);
            reconcile.Invoke(hud, new object[] { new HashSet<string> { "current-peer" } });
            Assert.IsTrue(sim.Crew[slot].Active);
            Assert.AreEqual(slot, sim.FindPlayer("current-peer"));
            Assert.IsTrue(admitted.Contains("current-peer"));
            Assert.IsEmpty(pending);
            Assert.AreEqual(slot, sim.Join(88, "current-peer"));

            left.Invoke(hud, new object[] { "current-peer" });
            reconcile.Invoke(hud, new object[] { new HashSet<string>() });
            Assert.IsFalse(sim.Crew[slot].Active);
            Assert.AreEqual(-1, sim.FindPlayer("current-peer"));
            Assert.IsFalse(admitted.Contains("current-peer"));
            Assert.IsEmpty(pending);
        }

        [Test]
        public void FailureIsReportedAndALaterCompletionClearsIt()
        {
            var root = new GameObject("MigrationFailureTest");
            try
            {
                var hud = root.AddComponent<PrototypeHud>();
                LogAssert.Expect(LogType.Warning, "Host migration failed (NetworkSetupFailed). Leave this session and create a new voyage.");
                var failed = typeof(PrototypeHud).GetMethod("OnlineMigrationFailed", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(failed);
                failed.Invoke(hud, new object[] { SessionError.NetworkSetupFailed });
                Assert.AreEqual(SessionError.NetworkSetupFailed, hud.LastMigrationError);
                Assert.AreEqual(0, hud.CompletedMigrations);

                var completed = typeof(PrototypeHud).GetMethod("OnlineSessionMigrated", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(completed);
                completed.Invoke(hud, null);
                Assert.IsNull(hud.LastMigrationError);
                Assert.AreEqual(1, hud.CompletedMigrations);
            }
            finally { Object.DestroyImmediate(root); }
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
