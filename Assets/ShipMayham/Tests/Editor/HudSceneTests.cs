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

        [UnityTest] public IEnumerator TheMenuPausesASoloVoyageAndUnsticksTheCrew()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.6f);
            Assert.IsTrue(hud.Session.IsSoloVoyage);

            hud.View.OpenMenu();
            yield return new WaitForSecondsRealtime(.4f);
            var menu = System.Array.Find(controller.GetComponentsInChildren<Transform>(true), t => t.name == "PauseMenu");
            Assert.IsNotNull(menu);
            Assert.IsTrue(menu.gameObject.activeSelf);
            Assert.AreEqual(0f, Time.timeScale, "A solo voyage pauses.");
            Assert.AreEqual("PAUSED", menu.Find("Card/Title").GetComponent<UnityEngine.UI.Text>().text);

            var settingsButton = System.Array.Find(menu.GetComponentsInChildren<UnityEngine.UI.Button>(true), b => b.name == "SettingsButton");
            settingsButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(menu.Find("Card/SettingsView").gameObject.activeSelf);
            Assert.IsFalse(menu.Find("Card/Main").gameObject.activeSelf);

            hud.View.CloseMenu(true);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(menu.gameObject.activeSelf);

            var sim = hud.Session.Simulation;
            sim.Crew[0].Position = new Vector2(4.5f, 7.5f);
            hud.Session.UnstuckRpc();
            yield return new WaitForSeconds(.6f);
            Assert.AreEqual(new Vector2(-1.5f, -6), sim.Crew[0].Position);
            yield return new ExitPlayMode();
        }

        private sealed class SignalRecorder
        {
            public readonly System.Collections.Generic.List<SignalMessage> Messages = new System.Collections.Generic.List<SignalMessage>();
            public readonly System.Collections.Generic.List<float> Times = new System.Collections.Generic.List<float>();
            public void Record(SignalMessage message) { Messages.Add(message); Times.Add(Time.unscaledTime); }
        }

        [UnityTest] public IEnumerator SignalsReachTheCrewAndAreRateLimited()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.6f);

            var recorder = new SignalRecorder();
            var received = recorder.Messages;
            hud.Session.SignalReceived += recorder.Record;
            float start = Time.unscaledTime;
            hud.Session.SignalRpc(SignalKind.Ping, new Vector3(0, .5f, 3), true, new Unity.Collections.FixedString32Bytes("Cannon <b>"));
            hud.Session.SignalRpc(SignalKind.Help, Vector3.zero, false, default);   // inside the cooldown: dropped
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(1, received.Count, "The second signal came too soon.");
            Assert.AreEqual(SignalKind.Ping, received[0].Kind);
            Assert.AreEqual(0, received[0].Slot);
            Assert.IsTrue(received[0].Attached);
            Assert.AreEqual("Cannon b", received[0].Label, "Markup characters are removed from labels.");
            var marker = System.Array.Find(controller.GetComponentsInChildren<Transform>(true), t => t.name.StartsWith("Ping") && t.gameObject.activeSelf);
            Assert.IsNotNull(marker, "A ping marker is shown.");

            // The limiter runs on unscaled time, so wait on the same clock instead of assuming frames take a fixed time.
            float cooldownEnds = start + Signals.CooldownSeconds + .3f;
            float giveUp = Time.realtimeSinceStartup + 30f;
            while (Time.unscaledTime < cooldownEnds && Time.realtimeSinceStartup < giveUp) yield return null;
            hud.Session.SignalRpc(SignalKind.Thanks, Vector3.zero, false, default);
            yield return new WaitForSeconds(.4f);
            hud.Session.SignalRpc((SignalKind)99, Vector3.zero, false, default);   // not a signal: ignored
            yield return new WaitForSeconds(.4f);
            hud.Session.SignalReceived -= recorder.Record;
            Assert.AreEqual(2, received.Count, "The signal after the cooldown was lost.");
            Assert.AreEqual(SignalKind.Thanks, received[1].Kind);
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator ShoalsAreDrawnAndAnnouncedWhileSailing()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            yield return new WaitForSeconds(.3f);
            hud.View.CloseMenu(false);        // without a real pointer lock the menu opens and a solo voyage pauses
            var sim = hud.Session.Simulation;
            Assert.IsNull(GameObject.Find("Shoals"), "no shoals in the harbor");

            sim.State.Route = RouteProfile.StormPassage;
            sim.State.Phase = VoyagePhase.Sailing;
            yield return new WaitForSeconds(.5f);
            var view = HazardView.Instance;
            Assert.IsNotNull(view, "the hazard view installs itself");
            Assert.Greater(sim.ShoalTotal, 0, $"phase {sim.State.Phase}, health {sim.State.Health}, elapsed {sim.State.Elapsed}, hazards {sim.Hazards.Wave}/{sim.Hazards.Seed}");
            deadline = Time.realtimeSinceStartup + 3;
            while (view.DrawnShoals == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(sim.ShoalTotal, view.DrawnShoals);
            Assert.AreEqual(sim.Hazards.Seed, hud.Session.Hazards.Value.Seed, "the seed reaches the clients");

            var shoal = sim.ShoalAt(0);
            sim.State.Heading = 0;
            sim.State.Speed = 0;
            sim.State.Position = shoal.Center - new Vector2(0, 25);
            yield return new WaitForSeconds(.6f);
            bool warned = false;
            for (int frame = 0; frame < 120 && !warned; frame++)
            {
                warned = System.Array.Exists(controller.GetComponentsInChildren<UnityEngine.UI.Text>(false), t => t.text.StartsWith("SHOAL AHEAD"));
                if (!warned) yield return null;
            }
            Assert.IsTrue(warned, "the alert stack warns about the shoal");

            sim.State.Phase = VoyagePhase.Arrived;
            deadline = Time.realtimeSinceStartup + 3;
            while (view.DrawnShoals != 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(0, view.DrawnShoals, $"the rocks leave with the voyage (sim {sim.State.Phase}, published {hud.Session.Voyage.Value.Phase}, scale {Time.timeScale}, solo {hud.Session.IsSoloVoyage})");
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

            // The voyage report with highlights.
            var reportSim = hud.Session.Simulation;
            reportSim.Tallies[0] = new CrewTally { Throws = 3, Orders = 5, Repairs = 4, Hits = 2 };
            reportSim.State.LastTickets = 160; reportSim.State.LastServices = 40; reportSim.State.LastTips = 12; reportSim.State.LastCosts = 18; reportSim.State.LastNet = 194;
            reportSim.State.Phase = VoyagePhase.Arrived;
            yield return new WaitForSeconds(.8f);
            yield return Capture(hud, controller, "Logs/HudReport.png");
            reportSim.State.Phase = VoyagePhase.Boarding;
            yield return new WaitForSeconds(.3f);

            // The in-game menu and its settings.
            hud.View.OpenMenu();
            yield return new WaitForSecondsRealtime(.4f);
            yield return Capture(hud, controller, "Logs/HudPause.png");
            var menuRoot = System.Array.Find(controller.GetComponentsInChildren<Transform>(true), t => t.name == "PauseMenu");
            System.Array.Find(menuRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true), b => b.name == "SettingsButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            yield return Capture(hud, controller, "Logs/HudPauseSettings.png");
            hud.View.CloseMenu(true);
            yield return new WaitForSeconds(.2f);

            // Signals: a ping on the cannon, an emote, and the wheel.
            Transform shipRoot = hud.Session.ShipRoot;
            hud.Session.SignalRpc(SignalKind.Ping, new Vector3(3.2f, 1.6f, 4f), true, new Unity.Collections.FixedString32Bytes("Cannon"));
            yield return new WaitForSeconds(1.1f);
            hud.Session.SignalRpc(SignalKind.Thanks, Vector3.zero, false, default);
            yield return new WaitForSeconds(.5f);
            yield return Capture(hud, controller, "Logs/HudSignals.png");
            var viewType = typeof(PrototypeView);
            viewType.GetProperty("WheelOpen").GetSetMethod(true).Invoke(hud.View, new object[] { true });
            viewType.GetProperty("WheelSelection").GetSetMethod(true).Invoke(hud.View, new object[] { (SignalKind?)SignalKind.Help });
            yield return null; yield return null;
            yield return Capture(hud, controller, "Logs/HudWheel.png");
            viewType.GetProperty("WheelOpen").GetSetMethod(true).Invoke(hud.View, new object[] { false });
            yield return new WaitForSeconds(.2f);

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

            // Passenger handling poses: lying after a throw, dizzy, walking back.
            sim.State.Phase = VoyagePhase.Boarding;
            sim.State.Boarded = 4;
            for (int p = 0; p < 4; p++) sim.Passengers[p] = new PassengerState { Active = true, Satisfaction = 100, CarrierSlot = -1, FarePaid = true, Position = new Vector3(sim.Seats[p].x, .5f, sim.Seats[p].y) };
            sim.Crew[0].Position = new Vector2(3.5f, -5f);
            // The captures look from another place than the game camera, so the body must animate even when the game camera cannot see it.
            foreach (var passengerAnimator in hud.View.GetComponent<PrototypePassengerAnimator>().Animators) passengerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return new WaitForSeconds(.5f);
            foreach (var (motion, file) in new[] { (PassengerMotion.Landing, "Lying"), (PassengerMotion.Dizzy, "Dizzy"), (PassengerMotion.Returning, "Walking") })
            {
                sim.Passengers[0].Motion = motion;
                sim.Passengers[0].RecoveryRemaining = 30;
                sim.Passengers[0].Position = new Vector3(motion == PassengerMotion.Returning ? -.5f : 0, .5f, -3f);
                sim.Passengers[0].Velocity = motion == PassengerMotion.Returning ? new Vector3(-.5f, 0, 1) : Vector3.zero;
                for (int frame = 0; frame < 40; frame++) yield return null;      // the animators need real frames, not just elapsed time
                sim.Passengers[0].Position = new Vector3(motion == PassengerMotion.Returning ? -.5f : 0, .5f, -3f);
                sim.Passengers[0].Position = new Vector3(motion == PassengerMotion.Returning ? -.5f : 0, .5f, -3f);
                yield return CaptureWorld(hud, controller, "Logs/WorldPassenger" + file + ".png", ship.TransformPoint(new Vector3(2.2f, 1.9f, -6.2f)), ship.TransformPoint(new Vector3(0, .9f, -3f)), 55);
            }
            sim.Passengers[0].Motion = PassengerMotion.OnDeck;

            // Swimming: a crew member in the water beside the port ladder, seen from outside and through their own eyes.
            sim.State.Phase = VoyagePhase.Boarding;
            sim.Crew[0].Position = VoyageSimulation.Ladders[0].Top;
            for (int frame = 0; frame < 20; frame++) yield return null;
            Debug.Log("SWIMCAPTURE " + sim.LadderCommand(sim.Crew[0].ClientId, 0) + " / " + sim.Crew[0].Swimming);
            sim.Crew[0].Position = VoyageSimulation.Ladders[0].Base + new Vector2(-1.4f, 0);
            foreach (var crewAnimator in hud.View.GetComponent<PrototypeCrewAnimator>().Animators) if (crewAnimator != null) crewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int frame = 0; frame < 60; frame++) yield return null;
            yield return CaptureWorld(hud, controller, "Logs/WorldSwimmerTread.png", ship.TransformPoint(new Vector3(-10f, 1.2f, -2f)), ship.TransformPoint(new Vector3(-7.9f, -1.1f, 3f)), 50);
            yield return CaptureWorld(hud, controller, "Logs/WorldLadder.png", ship.TransformPoint(new Vector3(-9.5f, .5f, 8f)), ship.TransformPoint(new Vector3(-5.4f, -.4f, 3f)), 55);
            yield return Capture(hud, controller, "Logs/HudSwim.png");
            sim.Crew[0].Swimming = false;

            // Passengers who swim and who do not: one treading water next to a flailing one.
            sim.State.Phase = VoyagePhase.Boarding;
            sim.State.Boarded = 4;
            for (int p = 0; p < 4; p++) sim.Passengers[p] = new PassengerState { Active = true, Satisfaction = 100, CarrierSlot = -1, FarePaid = true, Position = new Vector3(sim.Seats[p].x, .5f, sim.Seats[p].y), CanSwim = p % 2 == 0 };
            sim.Crew[0].Position = new Vector2(3.5f, -5f);
            foreach (var passengerAnimator in hud.View.GetComponent<PrototypePassengerAnimator>().Animators) passengerAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            sim.Passengers[0].Motion = PassengerMotion.Rescuing; sim.Passengers[0].RecoveryRemaining = 60; sim.Passengers[0].Position = new Vector3(-7.2f, VoyageSimulation.SeaLevel + VoyageSimulation.PassengerSwimDepth, -3f);
            sim.Passengers[1].Motion = PassengerMotion.Rescuing; sim.Passengers[1].RecoveryRemaining = 60; sim.Passengers[1].Position = new Vector3(-7.2f, VoyageSimulation.SeaLevel + VoyageSimulation.PassengerSwimDepth, 0f);
            for (int frame = 0; frame < 60; frame++) yield return null;
            yield return CaptureWorld(hud, controller, "Logs/WorldPassengersSwim.png", ship.TransformPoint(new Vector3(-5.2f, 2.4f, -7f)), ship.TransformPoint(new Vector3(-7.2f, -1.0f, -1.5f)), 55);
            sim.Passengers[0].Motion = PassengerMotion.OnDeck; sim.Passengers[1].Motion = PassengerMotion.OnDeck;
            for (int p = 0; p < 2; p++) sim.Passengers[p].Position = new Vector3(sim.Seats[p].x, .5f, sim.Seats[p].y);
            for (int frame = 0; frame < 30; frame++) yield return null;
            yield return CaptureWorld(hud, controller, "Logs/WorldFloaties.png", ship.TransformPoint(new Vector3(2.2f, 1.9f, -6.2f)), ship.TransformPoint(new Vector3(0, 1.0f, -1f)), 55);

            // The gangway gate, closed and open.
            sim.State.Phase = VoyagePhase.Boarding;
            sim.Crew[0].Position = new Vector2(-3.4f, -6f);
            for (int frame = 0; frame < 30; frame++) yield return null;
            yield return CaptureWorld(hud, controller, "Logs/WorldGateClosed.png", ship.TransformPoint(new Vector3(-0.5f, 2.2f, -9.5f)), ship.TransformPoint(new Vector3(-5f, 1.2f, -6f)), 60);
            sim.Crew[0].Position = new Vector2(-4.4f, -6f);
            for (int frame = 0; frame < 40; frame++) yield return null;
            yield return CaptureWorld(hud, controller, "Logs/WorldGateOpen.png", ship.TransformPoint(new Vector3(-0.5f, 2.2f, -9.5f)), ship.TransformPoint(new Vector3(-5f, 1.2f, -6f)), 60);

            // Captain hazards: a shoal ahead with the rogue wave warning up.
            sim.State.Route = RouteProfile.StormPassage;
            sim.State.Phase = VoyagePhase.Sailing;
            yield return new WaitForSeconds(.4f);
            var rock = sim.ShoalAt(0);
            sim.State.Heading = 0;
            sim.State.Speed = 0;
            sim.State.Position = rock.Center - new Vector2(0, 22);
            sim.Hazards.Wave = WavePhase.Warning;
            sim.Hazards.WaveRemaining = 4f;
            sim.Hazards.WaveBearing = 90f;
            yield return new WaitForSeconds(.5f);
            sim.Hazards.WaveRemaining = 4f;
            yield return Capture(hud, controller, "Logs/HudHazard.png");
            Vector3 center = new Vector3(rock.Center.x, 0, rock.Center.y);
            yield return CaptureWorld(hud, controller, "Logs/WorldShoal.png", center + new Vector3(9f, 5f, -16f), center + new Vector3(0, .3f, 0), 60);
            GameHud.CursorLockedOverride = null;
            yield return new ExitPlayMode();
        }

        // Optional visual check: the main menu with a saved world (set SHIPMAYHAM_CAPTURE=1).
        [UnityTest] public IEnumerator CaptureContinueMenu()
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SHIPMAYHAM_CAPTURE"))) Assert.Ignore("Set SHIPMAYHAM_CAPTURE=1 to render HUD previews.");
            yield return new EnterPlayMode();
            WorldSave.ForceEnabled = true;
            var sim = new VoyageSimulation(System.Array.Empty<StationDefinition>(), new Vector2[8]);
            sim.Join(5, "host");
            sim.State.Voyage = 4; sim.State.Wallet = 312; sim.State.Reputation = 2;
            WorldSave.Save(sim.ExportMigrationData(), WorldSaveInfo.From(sim.State, 2, "Captain Pip", WorldSave.NowUnix() - 1800));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var controller = hud.GetComponentInChildren<HudController>(true);
            Directory.CreateDirectory("Logs");
            yield return null;
            yield return Capture(hud, controller, "Logs/HudContinue.png");
            WorldSave.Clear();
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
