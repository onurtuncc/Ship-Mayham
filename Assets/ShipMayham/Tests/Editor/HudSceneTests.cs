using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public sealed class HudSceneTests
    {
        private const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";

        [UnityTest] public IEnumerator LobbyThenVoyageHudSwitchOnSpawn()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            Assert.IsNotNull(controller, "The HUD builds its own canvas in play mode.");
            yield return null;
            Assert.IsTrue(controller.transform.Find("MenuScreen").gameObject.activeSelf);
            Assert.IsFalse(controller.transform.Find("GameHud").gameObject.activeSelf);

            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            yield return null; yield return null;
            Assert.IsFalse(controller.transform.Find("MenuScreen").gameObject.activeSelf);
            Assert.IsTrue(controller.transform.Find("GameHud").gameObject.activeSelf);
            yield return new ExitPlayMode();
        }

        // Optional visual check (set SHIPMAYHAM_CAPTURE=1): renders lobby and HUD states to Logs/Hud*.png.
        [UnityTest] public IEnumerator CaptureHudScreens()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SHIPMAYHAM_CAPTURE"))) Assert.Ignore("Set SHIPMAYHAM_CAPTURE=1 to render HUD previews.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            Directory.CreateDirectory("Logs");
            yield return null;
            yield return Capture(hud, controller, "Logs/HudLobby.png");

            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            GameHud.CursorLockedOverride = true;
            yield return new WaitForSeconds(.8f);
            yield return Capture(hud, controller, "Logs/HudGame.png");
            GameHud.PromptOverride = InteractionPrompt.Target("Engine upgrade", "Buy upgrade", "Level 0/2", "40 coins", hint1: new PromptHint(HudControl.Carry, "Lift"));
            yield return null; yield return null; yield return new WaitForSeconds(.3f);
            yield return Capture(hud, controller, "Logs/HudPrompt.png");
            GameHud.PromptOverride = PromptCatalog.ForCannon();
            yield return null; yield return null;
            yield return Capture(hud, controller, "Logs/HudContext.png");
            GameHud.PromptOverride = null;
            controller.Game.ToggleHelp();
            yield return null; yield return null;
            yield return Capture(hud, controller, "Logs/HudHelp.png");
            controller.Game.ToggleHelp();
            GameHud.CursorLockedOverride = null;
            yield return new ExitPlayMode();
        }

        private static IEnumerator Capture(PrototypeHud hud, HudController controller, string path)
        {
            yield return null;
            var camera = hud.View.Camera;
            var canvas = controller.Canvas;
            var target = new RenderTexture(1920, 1080, 24);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            camera.targetTexture = null; RenderTexture.active = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(target); Object.Destroy(texture);
        }
    }
}
