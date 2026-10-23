using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class HarborSceneTests
    {
        private static IEnumerable Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }
        [UnityTest]
        public IEnumerator HarborRpcMovesCameraTargetsShopsAndReplicatesPreparation()
        {
            yield return new EnterPlayMode();
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
            var harbor = session.Harbor;
            Assert.IsNotNull(harbor);
            Assert.AreEqual(18, harbor.ShoreMarkers.Length);
            Assert.AreEqual(12, session.Stations.Length, "Ammo and repair supply append stations; existing IDs stay stable.");
            sim.Crew[0].Position = VoyageSimulation.ShipGangway;
            session.HarborCommandRpc(HarborAction.GoAshore);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.IsTrue(session.Crew[0].OnShore);
            Assert.IsTrue(harbor.Walkways[0].gameObject.activeSelf);
            Assert.IsFalse(harbor.Walkways[1].gameObject.activeSelf);
            Vector3 shorePosition = VoyageSimulation.HarborOrigin(sim.State) + new Vector3(5, VoyageSimulation.ShoreHeight, -6);
            Assert.That(Vector3.Distance(view.CrewRoots[0].position, shorePosition), Is.LessThan(.03f));
            Assert.That(Vector3.Distance(view.Camera.transform.position, shorePosition + Vector3.up * view.EyeHeight), Is.LessThan(.03f));
            Assert.IsFalse(view.CrewRoots[0].Find("Visuals").gameObject.activeSelf);
            sim.State.Reputation = 0;
            session.SelectRouteRpc(RouteProfile.StormPassage);
            foreach (var frame in Wait(.1f)) yield return frame;
            Assert.AreEqual(RouteProfile.CoastalFerry, session.Voyage.Value.Route);
            Assert.That(session.Notice, Does.Contain("5 reputation"));
            sim.State.Reputation = 2;
            session.SelectRouteRpc(RouteProfile.MarketRun);
            foreach (var frame in Wait(.1f)) yield return frame;
            Assert.AreEqual(RouteProfile.MarketRun, session.Voyage.Value.Route);
            Assert.AreEqual(2, session.Voyage.Value.Reputation);
            sim.State.Reputation = 5;
            session.SelectRouteRpc(RouteProfile.StormPassage);
            foreach (var frame in Wait(.1f)) yield return frame;
            Assert.AreEqual(RouteProfile.StormPassage, session.Voyage.Value.Route);
            Assert.AreEqual(5, session.Voyage.Value.Reputation);
            sim.Crew[0].Position = new Vector2(5, -8);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(InteractionTarget.Harbor(HarborAction.BoardShip), view.Target);
            Assert.That(view.Prompt.Action, Does.Contain("Board ship"));
            Assert.That(harbor.Labels[1].characterSize, Is.EqualTo(.02f));
            Assert.That(harbor.Labels[1].transform.localScale.x, Is.InRange(.25f, 2));
            Assert.IsFalse(session.Stations[0].Label.gameObject.activeSelf, "Ship labels should not clutter the shore view.");
            sim.State.Wallet = 100;
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.BuyFood) + Vector2.right * 1.2f;
            session.HarborCommandRpc(HarborAction.BuyFood);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(24, session.Voyage.Value.FoodStock);
            Assert.AreEqual(88, session.Voyage.Value.Wallet);
            Assert.That(session.Notice, Does.Contain("+8 food"));
            Assert.Greater(session.NoticeUntil, Time.unscaledTime);
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.MaintainHull) + Vector2.right * 1.2f;
            session.HarborCommandRpc(HarborAction.MaintainHull);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(20, session.Voyage.Value.HullShield);
            Assert.AreEqual(58, session.Voyage.Value.Wallet);
            Assert.IsNotEmpty(view.LiveryRenderers, "Authored ship must expose paintable hull meshes.");
            var properties = new MaterialPropertyBlock();
            view.LiveryRenderers[0].GetPropertyBlock(properties);
            Color originalPaint = properties.GetColor("_BaseColor");
            sim.State.Reputation = 5;
            session.SelectLiveryRpc(ShipLivery.Sunset);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.AreEqual(ShipLivery.Sunset, session.Voyage.Value.Livery);
            view.LiveryRenderers[0].GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor").g, Is.EqualTo(.36f).Within(.001f));
            session.SelectLiveryRpc(ShipLivery.Classic);
            foreach (var frame in Wait(.6f)) yield return frame;
            view.LiveryRenderers[0].GetPropertyBlock(properties);
            Assert.AreEqual(originalPaint, properties.GetColor("_BaseColor"));
            sim.Crew[0].Position = VoyageSimulation.HarborPoint(HarborAction.BoardShip);
            session.HarborCommandRpc(HarborAction.BoardShip);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.IsFalse(session.Crew[0].OnShore);
            Assert.That(Vector3.Distance(view.CrewRoots[0].localPosition, new Vector3(-5, .5f, -6)), Is.LessThan(.03f));
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
