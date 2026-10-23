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

        [UnityTest] public IEnumerator LobbyHoldsTheVoyageUntilTheHostStarts()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            hud.Session.LobbyBeforeVoyage = true;
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            deadline = Time.realtimeSinceStartup + 2;
            while (!hud.Session.InLobby && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.5f);

            Assert.IsTrue(hud.Session.InLobby);
            Assert.IsTrue(controller.transform.Find("LobbyScreen").gameObject.activeSelf);
            Assert.IsFalse(controller.transform.Find("GameHud").gameObject.activeSelf);
            Assert.IsFalse(controller.transform.Find("MenuScreen").gameObject.activeSelf);
            Assert.IsFalse(hud.View.IsFirstPerson, "The lobby leaves the cursor free.");
            Assert.AreEqual(PlayerNickname.Current, hud.Session.Profiles[0].Name.ToString(), "The nickname reaches the host once the slot exists.");
            Vector2 standing = hud.Session.Simulation.Crew[0].Position;
            hud.Session.MoveRpc(Vector2.right);
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(standing, hud.Session.Simulation.Crew[0].Position, "Movement is ignored in the lobby.");

            hud.Session.StartVoyageRpc();
            yield return new WaitForSeconds(.6f);
            Assert.IsFalse(hud.Session.InLobby);
            Assert.IsFalse(controller.transform.Find("LobbyScreen").gameObject.activeSelf);
            Assert.IsTrue(controller.transform.Find("GameHud").gameObject.activeSelf);
            Assert.IsTrue(hud.View.IsFirstPerson);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator ThePurserSellsUpgradesAndReleasesTheCursor()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.6f);
            var sim = hud.Session.Simulation;
            Vector2 desk = sim.PurserPoint.Value;
            sim.Crew[0].Position = desk + new Vector2(.9f, 0);
            sim.State.Wallet = 500;
            yield return new WaitForSeconds(.4f);

            hud.View.OpenPurser();
            yield return new WaitForSeconds(.4f);
            var panel = System.Array.Find(controller.GetComponentsInChildren<Transform>(true), t => t.name == "PurserPanel");
            Assert.IsNotNull(panel);
            Assert.IsTrue(panel.gameObject.activeSelf, "The desk opens as a panel.");

            hud.Session.BuyUpgradeRpc(StationKind.EngineUpgrade);
            yield return new WaitForSeconds(.6f);
            Assert.AreEqual(1, sim.State.EngineLevel);
            Assert.AreEqual(400, sim.State.Wallet);

            hud.View.ClosePurser();
            yield return new WaitForSeconds(.3f);
            Assert.IsFalse(panel.gameObject.activeSelf);
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
            yield return Capture(hud, controller, "Logs/HudMenu.png");
            controller.Menu.Show(MenuScreen.Page.Join);
            yield return null;
            yield return Capture(hud, controller, "Logs/HudJoin.png");
            controller.Menu.Show(MenuScreen.Page.Settings);
            yield return null;
            yield return Capture(hud, controller, "Logs/HudSettings.png");

            hud.Session.LobbyBeforeVoyage = true;
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.8f);
            yield return Capture(hud, controller, "Logs/HudLobby.png");
            hud.Session.StartVoyageRpc();
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

            // The purser's desk, then the world around it.
            var sim = hud.Session.Simulation;
            sim.State.Wallet = 320;
            sim.Crew[0].Position = sim.PurserPoint.Value + new Vector2(.9f, 0);
            yield return new WaitForSeconds(.4f);
            hud.View.OpenPurser();
            yield return new WaitForSeconds(.4f);
            yield return Capture(hud, controller, "Logs/HudPurser.png");
            hud.View.ClosePurser();
            yield return null;
            Transform ship = hud.Session.ShipRoot;
            yield return CaptureWorld(hud, controller, "Logs/WorldPurser.png", ship.TransformPoint(new Vector3(-1.8f, 1.9f, -3.2f)), ship.TransformPoint(new Vector3(-4.9f, 1.3f, -4f)), 50);
            yield return CaptureWorld(hud, controller, "Logs/WorldGangway.png", new Vector3(-12f, 2.2f, -10f), new Vector3(-9f, .3f, -6f), 60);
            yield return CaptureWorld(hud, controller, "Logs/WorldGangwayLow.png", new Vector3(-10.5f, .9f, -4.5f), new Vector3(-8.7f, .3f, -6f), 60);
            yield return CaptureWorld(hud, controller, "Logs/WorldResident.png", new Vector3(-21f, 1.7f, -9f), new Vector3(-24f, 1.3f, -12f), 50);
            GameHud.CursorLockedOverride = null;
            yield return new ExitPlayMode();
        }

        private static IEnumerator CaptureWorld(PrototypeHud hud, HudController controller, string path, Vector3 from, Vector3 to, float fieldOfView)
        {
            yield return null;
            var camera = hud.View.Camera;
            var target = new RenderTexture(1920, 1080, 24);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            controller.Canvas.enabled = false;
            camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
            camera.fieldOfView = fieldOfView;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            camera.targetTexture = null; RenderTexture.active = null;
            controller.Canvas.enabled = true;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.Destroy(target); Object.Destroy(texture);
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
