using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class GameFeelTests
    {
        private static FeedbackSnapshot Baseline()
        {
            var s = FeedbackSnapshot.Create();
            s.Valid = true; s.Phase = VoyagePhase.Sailing; s.Wallet = 100; s.Health = 100; s.HullShield = 20; s.PirateHealth = 100;
            s.CrewActive[0] = true;
            for (int i = 0; i < FeedbackSnapshot.MaxPassengers; i++) s.Passengers[i].Active = true;
            return s;
        }

        private static List<FeedbackEvent> Detect(FeedbackSnapshot before, FeedbackSnapshot after)
        {
            var events = new List<FeedbackEvent>();
            FeedbackDetector.Detect(before, after, events);
            return events;
        }

        [Test] public void NoChangeProducesNoFeedback() => Assert.IsEmpty(Detect(Baseline(), Baseline()));

        [Test] public void FirstFrameAndUnspawnedSessionsNeverReplayHistory()
        {
            var after = Baseline(); after.Wallet = 500;
            var invalid = Baseline(); invalid.Valid = false;
            Assert.IsEmpty(Detect(invalid, after));
            Assert.IsEmpty(Detect(after, invalid));
        }

        [Test] public void WalletGainsReportTheEarnedAmount()
        {
            var after = Baseline(); after.Wallet = 140;
            var events = Detect(Baseline(), after);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(FeedbackKind.CoinsEarned, events[0].Kind);
            Assert.AreEqual(40, events[0].Value);
        }

        [Test] public void SpendingCoinsIsSilent()
        {
            var after = Baseline(); after.Wallet = 60;
            Assert.IsEmpty(Detect(Baseline(), after));
        }

        [Test] public void ArrivalReportsTheTripEarningsInsteadOfPlainCoins()
        {
            var after = Baseline(); after.Phase = VoyagePhase.Arrived; after.Wallet = 260;
            var events = Detect(Baseline(), after);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(FeedbackKind.VoyageArrived, events[0].Kind);
            Assert.AreEqual(160, events[0].Value);
        }

        [Test] public void LosingHullShieldOrHealthShakes()
        {
            var after = Baseline(); after.HullShield = 5;
            var events = Detect(Baseline(), after);
            Assert.AreEqual(FeedbackKind.HullDamaged, events[0].Kind);
            Assert.AreEqual(15, events[0].Value);
        }

        [Test] public void LeakStartsAndStopsAreDistinct()
        {
            var leaking = Baseline(); leaking.Leak = 1;
            Assert.AreEqual(FeedbackKind.LeakStarted, Detect(Baseline(), leaking)[0].Kind);
            Assert.AreEqual(FeedbackKind.LeakFixed, Detect(leaking, Baseline())[0].Kind);
        }

        [Test] public void PassengerReactionsTargetTheRightSlot()
        {
            var after = Baseline();
            after.Passengers[3].Reacting = true; after.Passengers[3].Reaction = PassengerReaction.Thanks;
            after.Passengers[5].Reacting = true; after.Passengers[5].Reaction = PassengerReaction.Complaint;
            var events = Detect(Baseline(), after);
            Assert.That(events, Has.Some.Matches<FeedbackEvent>(e => e.Kind == FeedbackKind.PassengerThanked && e.Slot == 3));
            Assert.That(events, Has.Some.Matches<FeedbackEvent>(e => e.Kind == FeedbackKind.PassengerComplained && e.Slot == 5));
            Assert.AreEqual(2, events.Count);
        }

        [Test] public void AReactionIsReportedOnceNotEveryFrame()
        {
            var reacting = Baseline(); reacting.Passengers[1].Reacting = true; reacting.Passengers[1].Reaction = PassengerReaction.Thanks;
            Assert.IsEmpty(Detect(reacting, reacting));
        }

        [Test] public void ClearingARequestOnDeckMeansItWasServed()
        {
            var waiting = Baseline(); waiting.Passengers[2].Request = ServiceItem.Food;
            var served = Baseline();
            Assert.AreEqual(FeedbackKind.ServiceDelivered, Detect(waiting, served)[0].Kind);
            served.Passengers[2].Motion = PassengerMotion.Airborne;
            Assert.IsEmpty(Detect(waiting, served), "A thrown passenger is not a served passenger.");
        }

        [Test] public void CarryingChangesReportPickupAndDelivery()
        {
            var carrying = Baseline(); carrying.Carrying[0] = ServiceItem.Food;
            Assert.AreEqual(FeedbackKind.ItemPickedUp, Detect(Baseline(), carrying)[0].Kind);
            Assert.AreEqual(FeedbackKind.ItemDelivered, Detect(carrying, Baseline())[0].Kind);
        }

        [Test] public void CannonShotsReportFireAndLanding()
        {
            var flying = Baseline(); flying.CrewBallActive = true;
            Assert.AreEqual(FeedbackKind.CannonFired, Detect(Baseline(), flying)[0].Kind);
            Assert.AreEqual(FeedbackKind.CannonballLanded, Detect(flying, Baseline())[0].Kind);
        }

        [Test] public void EventOutcomesAreReportedForTheSameEventOnly()
        {
            var active = Baseline(); active.EventSerial = 4; active.EventPhase = VoyageEventPhase.Active;
            var resolved = active; resolved.EventPhase = VoyageEventPhase.Resolved;
            Assert.AreEqual(FeedbackKind.EventResolved, Detect(active, resolved)[0].Kind);
            var next = resolved; next.EventSerial = 5; next.EventPhase = VoyageEventPhase.Active;
            Assert.IsEmpty(Detect(resolved, next));
        }

        [Test] public void FeedbackSoundsAreGeneratedWithAudibleSamples()
        {
            foreach (SfxKind kind in System.Enum.GetValues(typeof(SfxKind)))
            {
                var clip = ProceduralSfx.Get(kind);
                Assert.IsNotNull(clip, kind.ToString());
                Assert.Greater(clip.length, .04f, kind.ToString());
                var data = new float[clip.samples];
                Assert.IsTrue(clip.GetData(data, 0));
                float peak = 0;
                foreach (float sample in data) { Assert.IsFalse(float.IsNaN(sample), kind + " produced NaN"); peak = Mathf.Max(peak, Mathf.Abs(sample)); }
                if (kind != SfxKind.Gull || peak > 0) Assert.Greater(peak, .02f, kind + " is silent");
                Assert.LessOrEqual(peak, 1.0001f, kind + " clips");
            }
        }

        [Test] public void SeaLoopWrapsWithoutAJump()
        {
            var loop = ProceduralSfx.SeaLoop();
            var data = new float[loop.samples];
            loop.GetData(data, 0);
            float seam = Mathf.Abs(data[0] - data[data.Length - 1]);
            float typicalStep = 0;
            for (int i = 1; i < 2000; i++) typicalStep = Mathf.Max(typicalStep, Mathf.Abs(data[i] - data[i - 1]));
            Assert.LessOrEqual(seam, typicalStep * 3 + .01f, "The loop seam must be no louder than normal sample-to-sample movement.");
        }

        [UnityTest] public IEnumerator EventsDriveParticlesPopupsAndBannersInTheRealScene()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var feedback = hud.View.GetComponent<PrototypeFeedback>();
            Assert.IsNotNull(feedback); Assert.IsNotNull(feedback.Fx);
            feedback.Play(new FeedbackEvent(FeedbackKind.CoinsEarned, -1, 80));
            feedback.Play(new FeedbackEvent(FeedbackKind.PassengerThanked, 2));
            feedback.Play(new FeedbackEvent(FeedbackKind.VoyageArrived, -1, 120));
            yield return null;
            int particles = 0;
            foreach (var system in feedback.Fx.GetComponentsInChildren<ParticleSystem>()) particles += system.particleCount;
            Assert.Greater(particles, 20, "Bursts should be alive.");
            Assert.Greater(feedback.Fx.GetComponentsInChildren<TextMesh>(false).Length, 0, "A popup should be showing.");

            // Optional visual check (set SHIPMAYHAM_CAPTURE=1): renders the running scene, effects included, to Logs/GameFeel.png.
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SHIPMAYHAM_CAPTURE")))
            {
                yield return new WaitForSeconds(.35f);
                var camera = hud.View.Camera;
                Transform ship = hud.Session.ShipRoot;
                Vector3 from = ship.TransformPoint(new Vector3(-6f, 3.2f, -3.5f)), to = ship.TransformPoint(new Vector3(0f, 1.3f, 1.5f));
                camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from));
                camera.fieldOfView = 62;
                var target = new RenderTexture(1280, 720, 24);
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                camera.targetTexture = null; RenderTexture.active = null;
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/GameFeel.png", texture.EncodeToPNG());
            }
        }
    }
}
