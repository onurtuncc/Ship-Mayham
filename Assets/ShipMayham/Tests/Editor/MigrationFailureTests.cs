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
                hud.OnlinePlayerLeft("departed-before-spawn");
                Assert.IsTrue(hud.Roster.HasPendingDeparture("departed-before-spawn"), "An unapplied departure must survive until the voyage is ready.");
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
            var roster = hud.Roster;
            Assert.IsTrue(roster.TryAdmit(77, System.Text.Encoding.UTF8.GetBytes("current-peer"), out _));
            roster.QueueDeparture("current-peer");
            // A stale/duplicated indexed departure must not clear a current member,
            // even while its NGO connection has not rebound after migration.
            sim.SuspendConnection(77);
            roster.ReconcileDepartures(new HashSet<string> { "current-peer" }, hud.Session.RemoveOnlinePlayer);
            Assert.IsTrue(sim.Crew[slot].Active);
            Assert.AreEqual(slot, sim.FindPlayer("current-peer"));
            Assert.IsTrue(roster.IsAdmitted("current-peer"));
            Assert.AreEqual(0, roster.PendingDepartureCount);
            Assert.AreEqual(slot, sim.Join(88, "current-peer"));

            roster.QueueDeparture("current-peer");
            roster.ReconcileDepartures(new HashSet<string>(), hud.Session.RemoveOnlinePlayer);
            Assert.IsFalse(sim.Crew[slot].Active);
            Assert.AreEqual(-1, sim.FindPlayer("current-peer"));
            Assert.IsFalse(roster.IsAdmitted("current-peer"));
            Assert.AreEqual(0, roster.PendingDepartureCount);
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
