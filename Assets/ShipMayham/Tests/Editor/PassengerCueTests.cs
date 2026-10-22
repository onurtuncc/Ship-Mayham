using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class PassengerCueTests
    {
        private static PassengerState Paid => new PassengerState { Active = true, FarePaid = true, TicketChecked = true, Satisfaction = 100, Motion = PassengerMotion.OnDeck };
        private static VoyageState Sailing => new VoyageState { Phase = VoyagePhase.Sailing, Elapsed = 5.5f, Health = 100 };
        private static VoyageEventState Inspection => new VoyageEventState { Kind = VoyageEventKind.ContrabandInspection, Phase = VoyageEventPhase.Active, TargetPassenger = 2, Remaining = 20, Goal = 1 };

        [Test] public void ActiveInspectionOnlyChangesTheTargetCue()
        {
            Assert.AreEqual(PassengerCue.GuardingLuggage, PassengerPresentation.CueFor(Paid, Inspection, Sailing, 2));
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(Paid, Inspection, Sailing, 1));
        }

        [TestCase(VoyageEventPhase.Waiting)] [TestCase(VoyageEventPhase.Resolved)] [TestCase(VoyageEventPhase.Failed)]
        public void InactiveInspectionDoesNotLeaveASuspectCue(VoyageEventPhase phase)
        {
            var e = Inspection; e.Phase = phase;
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(Paid, e, Sailing, 2));
        }

        [TestCase(PassengerMotion.Carried)] [TestCase(PassengerMotion.Airborne)] [TestCase(PassengerMotion.Rescuing)]
        public void PhysicalComedyStatesOverrideTheStandingPose(PassengerMotion motion)
        {
            var p = Paid; p.Motion = motion;
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(p, Inspection, Sailing, 2));
        }

        [TestCase(VoyagePhase.Boarding)] [TestCase(VoyagePhase.Arrived)] [TestCase(VoyagePhase.Failed)]
        public void AClosedVoyageCannotKeepAnOldInspectionPose(VoyagePhase phase)
        {
            var voyage = Sailing; voyage.Phase = phase;
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(Paid, Inspection, voyage, 2));
        }

        [Test] public void PocketGestureIsNotProofOfAnUnpaidFare()
        {
            var innocent = Paid; innocent.TicketChecked = false;
            var unpaid = innocent; unpaid.FarePaid = false;
            Assert.AreEqual(PassengerCue.CheckingPockets, PassengerPresentation.CueFor(innocent, default, Sailing, 1));
            Assert.AreEqual(PassengerCue.CheckingPockets, PassengerPresentation.CueFor(unpaid, default, Sailing, 1));
            Assert.AreEqual("Ticket: unchecked", PassengerPresentation.TicketText(innocent));
            Assert.AreEqual("Ticket: unchecked", PassengerPresentation.TicketText(unpaid));
        }

        [Test] public void ValidInspectionStopsTheInnocentPocketHint()
        {
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(Paid, default, Sailing, 1));
            var p = Paid; p.FarePaid = false;
            Assert.AreEqual(PassengerCue.CheckingPockets, PassengerPresentation.CueFor(p, default, Sailing, 1));
            p.FarePaid = true;
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(p, default, Sailing, 1));
        }

        [Test] public void ServiceImpatienceDoesNotOverrideTheInspectionTell()
        {
            var p = Paid; p.Request = ServiceItem.Drink; p.Satisfaction = 30;
            Assert.AreEqual(PassengerCue.Impatient, PassengerPresentation.CueFor(p, default, Sailing, 2));
            Assert.AreEqual(PassengerCue.GuardingLuggage, PassengerPresentation.CueFor(p, Inspection, Sailing, 2));
            p.Request = ServiceItem.None;
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(p, default, Sailing, 2));
        }

        [Test] public void SnapshotRestoresTheSameCueWithoutExtraNetworkFields()
        {
            var original = new VoyageSimulation(System.Array.Empty<StationDefinition>(), new Vector2[8]);
            original.State = Sailing; original.Passengers[2] = Paid; original.Event = Inspection;
            var restored = new VoyageSimulation(System.Array.Empty<StationDefinition>(), new Vector2[8]);
            restored.ImportMigrationData(original.ExportMigrationData());
            restored.Tick(.05f);
            Assert.AreEqual(PassengerPresentation.CueFor(original.Passengers[2], original.Event, original.State, 2),
                PassengerPresentation.CueFor(restored.Passengers[2], restored.Event, restored.State, 2));
        }

        [Test] public void InactiveAndOutOfRangePassengersCannotBecomeSuspects()
        {
            var p = Paid; p.Active = false;
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(p, Inspection, Sailing, 2));
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(Paid, Inspection, Sailing, -1));
            Assert.AreEqual(PassengerCue.Relaxed, PassengerPresentation.CueFor(Paid, Inspection, Sailing, 8));
        }

        private static IEnumerator Wait(float seconds)
        { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        [UnityTest] public IEnumerator AuthoredSuitcasesReactToInspectionAndHideDuringCarryWithoutMovingAnchors()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>(); hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(hud.Session.IsSpawned);
            var session = hud.Session; var sim = session.Simulation;
            var cues = hud.View.GetComponent<PrototypePassengerCueView>();
            Assert.IsNotNull(cues); Assert.AreEqual(8, cues.Bags.Length);
            Vector3 anchor = session.PassengerAnchors[2].localPosition;
            Quaternion body = cues.Bodies[2].localRotation;
            sim.Crew[0].Position = sim.Stations[0].Position + Vector2.right;
            session.InteractRpc(0, false); yield return Wait(.6f);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Event = new VoyageEventState { Kind = VoyageEventKind.ContrabandInspection, Phase = VoyageEventPhase.Active, TargetPassenger = 2, Remaining = 100, Goal = 1 };
            yield return Wait(.6f);
            Assert.IsTrue(cues.Bags[2].gameObject.activeInHierarchy);
            Assert.Greater(cues.Bags[2].localPosition.y, .65f);
            Assert.That(cues.Bags[1].localPosition.y, Is.EqualTo(0).Within(.01f));
            Assert.Greater(Quaternion.Angle(cues.Bodies[2].localRotation, body), 5);
            foreach (var bag in cues.Bags)
            {
                foreach (var collider in bag.GetComponentsInChildren<Collider>(true)) Assert.IsFalse(collider.enabled);
                foreach (var renderer in bag.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials) Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            }
            sim.Crew[0].Position = sim.Seats[2] + Vector2.right;
            session.PassengerCommandRpc(2, PassengerAction.CheckTicket, 0);
            yield return Wait(.6f);
            Assert.AreEqual(VoyageEventPhase.Resolved, session.ActiveEvent.Value.Phase);
            Assert.IsTrue(session.Passengers[2].TicketChecked);
            Assert.Less(cues.Bags[2].localPosition.y, .03f);
            session.PassengerCommandRpc(2, PassengerAction.CarryOrDrop, 0);
            yield return Wait(.2f);
            Assert.AreEqual(PassengerMotion.Carried, session.Passengers[2].Motion);
            Assert.IsFalse(cues.Bags[2].gameObject.activeSelf);
            Assert.That(Quaternion.Angle(cues.Bodies[2].localRotation, body), Is.LessThan(.01f));
            Assert.AreEqual(anchor, session.PassengerAnchors[2].localPosition);
            cues.enabled = false;
            foreach (var bag in cues.Bags) Assert.IsFalse(bag.gameObject.activeSelf);
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
