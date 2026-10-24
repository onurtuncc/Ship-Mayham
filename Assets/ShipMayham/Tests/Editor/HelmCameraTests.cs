using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class HelmCameraTests
    {
        private static IEnumerable Wait(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest] public IEnumerator HelmsmanCanSwitchBetweenFirstPersonAndTheChaseCamera()
        {
            const string key = "ship-mayham.camera.helm-chase.v1";
            bool hadKey = PlayerPrefs.HasKey(key); int original = PlayerPrefs.GetInt(key, 0);
            PlayerPrefs.SetInt(key, 0);
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;   // these tests are not about the gangway walk
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session; var sim = session.Simulation; var view = hud.View;
            int helm = Array.FindIndex(session.Stations, s => s.Kind == StationKind.Helm);
            Assert.GreaterOrEqual(helm, 0);

            // Walking around: first person at eye height.
            foreach (var frame in Wait(.3f)) yield return frame;
            Assert.IsFalse(view.ChaseCameraActive);

            sim.Crew[0].Position = sim.Stations[helm].Position + Vector2.right;
            session.InteractRpc(helm, false);
            foreach (var frame in Wait(.6f)) yield return frame;
            Assert.IsTrue(session.Crew[0].AtHelm);
            Assert.IsFalse(view.ChaseCameraActive, "First person stays the default at the helm.");
            float eyeHeight = view.Camera.transform.position.y - session.ShipRoot.position.y;
            Assert.Less(eyeHeight, 4f);

            view.ToggleChaseCamera();
            sim.State.Phase = VoyagePhase.Sailing; sim.State.Speed = 6;
            foreach (var frame in Wait(.8f)) yield return frame;
            Assert.IsTrue(view.ChaseCameraActive);
            Vector3 toShip = session.ShipRoot.position - view.Camera.transform.position;
            Assert.Greater(-toShip.y, 8f, "The chase camera sits well above the ship.");
            Assert.That(toShip.magnitude, Is.InRange(14f, 40f));
            Assert.Greater(Vector3.Dot(view.Camera.transform.forward, toShip.normalized), .85f, "It looks at the ship.");
            Assert.IsTrue(view.CrewRoots[0].Find("Visuals").gameObject.activeSelf, "The helmsman body is visible in the chase view.");

            CaptureIfRequested(view.Camera);

            // Releasing the helm returns to first person even though the chase camera is still preferred.
            session.InteractRpc(-1, false);
            foreach (var frame in Wait(.8f)) yield return frame;
            Assert.IsFalse(session.Crew[0].AtHelm);
            Assert.IsFalse(view.ChaseCameraActive);

            // Back at the helm: remembered choice applies, and the key toggles it off again.
            sim.Crew[0].Position = sim.Stations[helm].Position + Vector2.right;
            session.InteractRpc(helm, false);
            foreach (var frame in Wait(.8f)) yield return frame;
            Assert.IsTrue(view.ChaseCameraActive, "The camera choice should be remembered.");
            view.ToggleChaseCamera();
            foreach (var frame in Wait(.4f)) yield return frame;
            Assert.IsFalse(view.ChaseCameraActive);

            if (hadKey) PlayerPrefs.SetInt(key, original); else PlayerPrefs.DeleteKey(key);
        }

        // Optional visual check: SHIPMAYHAM_CAPTURE=1 writes Logs/HelmChase.png from the live camera.
        private static void CaptureIfRequested(Camera camera)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHIPMAYHAM_CAPTURE"))) return;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
            camera.targetTexture = null; RenderTexture.active = null;
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/HelmChase.png", texture.EncodeToPNG());
        }
    }
}
