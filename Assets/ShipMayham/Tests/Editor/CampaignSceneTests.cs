using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    /// <summary>The real scene: the two harbor towns stand wherever the voyage chain says, the destination is chosen over the network path.</summary>
    public sealed class CampaignSceneTests
    {
        private const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";

        [UnityTest] public IEnumerator TheTownsFollowTheVoyageChain()
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
            hud.View.CloseMenu(false);
            Time.timeScale = 1;
            var harbor = hud.Session.Harbor;
            var sim = hud.Session.Simulation;
            Assert.AreEqual(new Vector3(-14, 0, 0), harbor.PortRoots[0].position);
            Assert.AreEqual(new Vector3(-14, 0, 220), harbor.PortRoots[1].position);

            sim.State.Island = Campaign.NorthHaven;
            sim.State.Target = Campaign.CoralCay;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(VoyageSimulation.IslandHarborOrigin(Campaign.NorthHaven), harbor.PortRoots[0].position);
            Assert.AreEqual(VoyageSimulation.IslandHarborOrigin(Campaign.CoralCay), harbor.PortRoots[1].position);
            Assert.AreEqual("NORTH HAVEN", harbor.PortRoots[0].Find("PortLabel").GetComponent<TextMesh>().text);
            Assert.AreEqual("CORAL CAY", harbor.PortRoots[1].Find("PortLabel").GetComponent<TextMesh>().text);
            var marker = hud.View.DestinationMarker;
            Assert.AreEqual(Campaign.World(Campaign.CoralCay).x, marker.position.x, .01f);
            Assert.AreEqual(Campaign.World(Campaign.CoralCay).y, marker.position.z, .01f);

            sim.State.Island = Campaign.BrassQuay;
            sim.State.Target = Campaign.NorthHaven;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(new Vector3(-14, 0, 0), harbor.PortRoots[0].position);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator TheHarborMasterPlotsACourseThroughTheRpc()
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
            hud.View.CloseMenu(false);
            Time.timeScale = 1;
            var session = hud.Session;
            var sim = session.Simulation;
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.BrassQuay;
            sim.State.TaskMask = 1 << Campaign.NorthHaven; sim.State.Reputation = 3;
            sim.Crew[0].OnShore = true;
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.HarborMaster);
            yield return new WaitForSecondsRealtime(.6f);
            hud.View.CloseMenu(false); Time.timeScale = 1;
            session.SelectDestinationRpc(Campaign.SmugglersCove);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(Campaign.SmugglersCove, session.Voyage.Value.Target, "the choice reached the replicated state");
            hud.View.OpenMap();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(hud.View.MapOpen && hud.View.DeskOpen);
            hud.View.CloseMap();
            Assert.IsFalse(hud.View.MapOpen);
            Assert.That(session.Notice, Does.Contain("Smugglers"));
            yield return new ExitPlayMode();
        }
    }
}
