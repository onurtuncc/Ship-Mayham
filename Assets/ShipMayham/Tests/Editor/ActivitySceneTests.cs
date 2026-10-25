using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    /// <summary>The real scene: a mini-game opens through the RPC, reaches the player, and its result pays out.</summary>
    public sealed class ActivitySceneTests
    {
        private const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";

        // Locals captured by a lambda inside a UnityTest iterator are null in this runner, so the listener is a plain object.
        private sealed class Recorder
        {
            public bool Opened;
            public MiniGameStart Received;
            public void OnOpened(MiniGameStart start) { Received = start; Opened = true; }
        }

        [UnityTest] public IEnumerator AChestOpensThroughTheRpcAndPaysOutOnSuccess()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            for (int frame = 0; frame < 15; frame++) yield return null;
            GameHud.CursorLockedOverride = true;                       // there is no real pointer lock in a test
            hud.View.CloseMenu(false);
            var session = hud.Session;
            var sim = session.Simulation;
            var recorder = new Recorder();
            session.MiniGameOpened += recorder.OnOpened;

            sim.Crew[0].OnShore = true;
            sim.Crew[0].Position = VoyageSimulation.ChestSpots[0];
            yield return new WaitForSecondsRealtime(.6f);
            hud.View.CloseMenu(false); Time.timeScale = 1;
            session.ActivityRpc(MiniGameKind.Lockpick, 0);
            for (float waited = 0; !recorder.Opened && waited < 3f; waited += Time.deltaTime) yield return null;
            Assert.IsTrue(recorder.Opened, "the game reached the player");
            Assert.AreEqual(MiniGameKind.Lockpick, recorder.Received.Kind);
            Assert.AreEqual(0, recorder.Received.Target);

            int wallet = sim.State.Wallet;
            yield return new WaitForSecondsRealtime(2f);
            hud.View.CloseMenu(false); Time.timeScale = 1;
            session.MiniGameResultRpc(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 100, 2000);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase, "timeScale=" + Time.timeScale + " clock");
            Assert.Greater(sim.State.Wallet, wallet, "the chest paid. Notice: " + session.Notice + " mini=" + sim.InMiniGame(0) + " timeScale=" + Time.timeScale);
            Assert.AreEqual(1, sim.State.ChestMask);
            Assert.AreEqual(sim.State.ChestMask, session.Voyage.Value.ChestMask, "and the mask reached the clients");
            Assert.That(session.Notice, Does.Contain("chest"));
            GameHud.CursorLockedOverride = null;
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator DivingInAndClimbingOutWorksThroughTheRpcs()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            for (int frame = 0; frame < 15; frame++) yield return null;
            hud.View.CloseMenu(false);
            var session = hud.Session;
            var sim = session.Simulation;
            sim.Crew[0].Position = VoyageSimulation.Ladders[1].Top;
            yield return new WaitForSecondsRealtime(.6f);
            hud.View.CloseMenu(false); Time.timeScale = 1;
            session.LadderRpc(1);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsTrue(session.Crew[0].Swimming, "the swimming flag reaches the client view of the crew");
            Assert.AreEqual(VoyageSimulation.Ladders[1].Base, session.Crew[0].Position);
            yield return new WaitForSecondsRealtime(.8f);
            hud.View.CloseMenu(false); Time.timeScale = 1;
            session.LadderRpc(1);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.IsFalse(session.Crew[0].Swimming);
            yield return new ExitPlayMode();
        }
    }
}
