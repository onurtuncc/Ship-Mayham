using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class CombatSceneTests
    {
        private static IEnumerable Wait(float seconds)
        {
            float deadline = Time.time + seconds;
            float timeout = Time.realtimeSinceStartup + seconds + 5;
            while (Time.time < deadline && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.GreaterOrEqual(Time.time, deadline, "The simulation clock stopped during the scene test.");
        }

        [UnityTest]
        public IEnumerator AuthoredCombatSceneSupportsRpcOwnershipSnapshotsAndPresentation()
        {
            yield return new EnterPlayMode();
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var visual = Object.FindAnyObjectByType<PrototypeCombatView>();
            Assert.IsNotNull(visual);
            Assert.IsNotNull(visual.CannonPivot);
            Assert.IsNotNull(visual.MuzzleAnchor);
            Assert.IsNotNull(visual.PowerIndicator);
            Assert.IsNotNull(visual.PirateRoot);
            Assert.AreEqual(11, hud.Session.Stations.Length);
            hud.Connect(true);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session;
            var sim = session.Simulation;
            sim.Crew[0].Position = new Vector2(-3, -4);
            session.InteractRpc(0, false);
            foreach (var frame in Wait(.4f)) yield return frame;
            sim.Crew[0].Position = new Vector2(0, 5.7f);
            session.InteractRpc(1, false);
            session.InteractRpc(-1, true);
            foreach (var frame in Wait(.4f)) yield return frame;
            Assert.AreEqual(VoyagePhase.Sailing, session.Voyage.Value.Phase, "Cannon shots require an active voyage.");
            session.InteractRpc(-1, false); // release helm
            foreach (var frame in Wait(.4f)) yield return frame;
            sim.Crew[0].Position = new Vector2(3.1f, 4.7f);
            session.InteractRpc(8, false);
            foreach (var frame in Wait(.2f)) yield return frame;
            Assert.IsTrue(session.Crew[0].AtCannon);
            Assert.That(hud.View.FocusText, Does.Contain("reload"));
            session.AimCannonRpc(new Vector2(60, 10));
            foreach (var frame in Wait(.2f)) yield return frame;
            Assert.That(Quaternion.Angle(visual.CannonPivot.localRotation, Quaternion.Euler(10, 60, 0)), Is.LessThan(.01f));
            session.CannonActionRpc(new Vector2(60, 10), false);
            foreach (var frame in Wait(.1f)) yield return frame;
            Assert.AreEqual(11, session.Encounter.Value.Ammo);
            Assert.IsFalse(session.Encounter.Value.CannonLoaded);
            Assert.IsTrue(visual.CannonTrace.enabled);
            Assert.IsTrue(visual.CrewProjectileVisual.gameObject.activeSelf);
            Assert.Greater(Vector3.Distance(session.Encounter.Value.CrewBall.Position, session.Encounter.Value.ShotFrom), 1);
            Assert.That(Vector3.Distance(visual.CannonTrace.GetPosition(1), visual.CrewProjectileVisual.position), Is.LessThan(.01f));
            session.InteractRpc(-1, false); // solo gunner must leave to fetch a physical round
            foreach (var frame in Wait(.4f)) yield return frame;
            sim.Crew[0].Position = new Vector2(3, 3);
            session.InteractRpc(10, false);
            foreach (var frame in Wait(.4f)) yield return frame;
            Assert.AreEqual(ServiceItem.Ammo, session.Crew[0].Carrying);
            Assert.AreEqual(10, session.Encounter.Value.MagazineAmmo);
            Assert.IsTrue(hud.View.CarriedItems[0].Find("Ammo_Visual").gameObject.activeSelf);
            Assert.IsFalse(hud.View.CarriedItems[0].Find("Visuals").gameObject.activeSelf);
            sim.Crew[0].Position = new Vector2(3.1f, 4.7f);
            session.InteractRpc(8, false);
            foreach (var frame in Wait(.4f)) yield return frame;
            Assert.AreEqual(1, session.Encounter.Value.ReadyAmmo);
            Assert.AreEqual(3, visual.ReadyAmmoVisuals.Length);
            Assert.IsTrue(visual.ReadyAmmoVisuals[0].gameObject.activeSelf);
            Assert.IsFalse(visual.ReadyAmmoVisuals[1].gameObject.activeSelf);
            Assert.IsFalse(session.Crew[0].AtCannon, "Delivery must not steal the gunner role.");
            session.InteractRpc(8, false);
            foreach (var frame in Wait(.4f)) yield return frame;
            session.CannonActionRpc(Vector2.zero, true);
            foreach (var frame in Wait(2.7f)) yield return frame;
            Assert.IsTrue(session.Encounter.Value.CannonLoaded);
            Assert.IsFalse(visual.CannonTrace.enabled);
            sim.Encounter.PowerOut = true;
            sim.Encounter.Pirate = PiratePhase.Warning;
            sim.Encounter.WarningRemaining = 6;
            sim.Encounter.PiratePosition = new Vector2(25, 25);
            foreach (var frame in Wait(.2f)) yield return frame;
            Assert.IsTrue(visual.PirateRoot.gameObject.activeSelf);
            Assert.That(visual.PirateLabel.text, Does.Contain("Attack"));
            var properties = new MaterialPropertyBlock();
            visual.PowerIndicator.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor").r, Is.EqualTo(1).Within(.001f));
            session.InteractRpc(-1, false);
            foreach (var frame in Wait(.4f)) yield return frame;
            sim.Crew[0].Position = new Vector2(3, 0);
            for (int step = 0; step < 3; step++)
            {
                session.InteractRpc(9, false);
                foreach (var frame in Wait(.4f)) yield return frame;
            }
            Assert.IsFalse(session.Encounter.Value.PowerOut);
            visual.PowerIndicator.GetPropertyBlock(properties);
            Assert.That(properties.GetColor("_BaseColor").g, Is.EqualTo(.8f).Within(.001f));
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
