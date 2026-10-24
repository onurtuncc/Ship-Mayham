using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    /// <summary>The real scene saves the world at port and a later session continues it.</summary>
    public sealed class WorldSaveSceneTests
    {
        private const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";

        // EnterPlayMode may only be yielded by the test itself, so each test repeats these three lines.
        private static void Prepare()
        {
            VoyageSimulation.WalkingPassengers = false;
            WorldSave.ForceEnabled = true;
        }

        private static IEnumerator Spawn(PrototypeHud hud)
        {
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            for (int frame = 0; frame < 10; frame++) yield return null;
        }

        [UnityTest] public IEnumerator ASavedWorldContinuesWithItsProgressAndAFreshCrewSlot()
        {
            yield return new EnterPlayMode();
            Prepare();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            WorldSave.Clear();
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            yield return Spawn(hud);
            var session = hud.Session;
            session.Simulation.State.Wallet = 444;
            session.Simulation.State.Voyage = 3;
            session.Simulation.State.Reputation = 4;
            Assert.IsTrue(session.SaveWorldNow(), "the host can save at port");
            Assert.IsTrue(WorldSave.TryPeekStored(out var info));
            Assert.AreEqual(3, info.Voyage);
            Assert.AreEqual(444, info.Wallet);
            yield return new ExitPlayMode();

            yield return new EnterPlayMode();
            Prepare();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            Assert.IsTrue(WorldSave.Exists, "the save outlives the session");
            Assert.IsTrue(WorldSave.Stage(RestoreMode.Solo));
            hud = Object.FindAnyObjectByType<PrototypeHud>();
            yield return Spawn(hud);
            session = hud.Session;
            Assert.AreEqual(444, session.Voyage.Value.Wallet);
            Assert.AreEqual(3, session.Voyage.Value.Voyage);
            Assert.AreEqual(4, session.Voyage.Value.Reputation);
            Assert.GreaterOrEqual(session.LocalCrewIndex, 0, "the player joined the restored world");
            Assert.AreEqual(1, session.Simulation.ActiveCrewCount);
            Assert.AreEqual(RestoreMode.None, WorldSave.PendingRestore, "the restore was consumed");
            yield return new ExitPlayMode();
            WorldSave.Clear();
        }

        [UnityTest] public IEnumerator ANewWorldDoesNotPickUpAnOldStagedSave()
        {
            yield return new EnterPlayMode();
            Prepare();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            WorldSave.Clear();
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            yield return Spawn(hud);
            hud.Session.Simulation.State.Wallet = 999;
            hud.Session.Simulation.State.Voyage = 7;
            Assert.IsTrue(hud.Session.SaveWorldNow());
            yield return new ExitPlayMode();

            yield return new EnterPlayMode();
            Prepare();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            Assert.IsTrue(WorldSave.Stage(RestoreMode.Solo));
            WorldSave.Discard();                                      // the player chose a new world instead
            hud = Object.FindAnyObjectByType<PrototypeHud>();
            yield return Spawn(hud);
            Assert.AreNotEqual(999, hud.Session.Voyage.Value.Wallet);
            Assert.AreEqual(1, hud.Session.Voyage.Value.Voyage);
            yield return new ExitPlayMode();
            WorldSave.Clear();
        }

        [UnityTest] public IEnumerator TheWorldIsNotSavedAtSeaOrInTheLobby()
        {
            yield return new EnterPlayMode();
            Prepare();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            WorldSave.Clear();
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            yield return Spawn(hud);
            var sim = hud.Session.Simulation;
            sim.State.Phase = VoyagePhase.Sailing;
            Assert.IsFalse(hud.Session.SaveWorldNow(), "never at sea");
            Assert.IsFalse(WorldSave.Exists);
            sim.State.Phase = VoyagePhase.Boarding;
            sim.OpenLobby();
            Assert.IsFalse(hud.Session.SaveWorldNow(), "never in the lobby");
            yield return new ExitPlayMode();
            WorldSave.Clear();
        }
    }
}
