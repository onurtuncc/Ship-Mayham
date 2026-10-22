using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    public class HumanPassengerTests
    {
        private static PassengerState Active(PassengerMotion motion = PassengerMotion.OnDeck) =>
            new PassengerState { Active = true, Motion = motion, Satisfaction = 100 };

        [Test] public void RelaxedPassengersSit()
        {
            Assert.AreEqual(PassengerPose.Seated, PassengerPresentation.PoseFor(Active(), PassengerCue.Relaxed));
            Assert.AreEqual(PassengerPose.Seated, PassengerPresentation.PoseFor(Active(), PassengerCue.WaitingForService));
        }

        [Test] public void ImpatientPassengersWaveForService() =>
            Assert.AreEqual(PassengerPose.Pleading, PassengerPresentation.PoseFor(Active(), PassengerCue.Impatient));

        [TestCase(PassengerCue.CheckingPockets)] [TestCase(PassengerCue.GuardingLuggage)]
        public void SuspiciousCuesFidget(PassengerCue cue) =>
            Assert.AreEqual(PassengerPose.Fidgeting, PassengerPresentation.PoseFor(Active(), cue));

        [TestCase(PassengerMotion.Carried)] [TestCase(PassengerMotion.Airborne)] [TestCase(PassengerMotion.Rescuing)]
        public void PhysicalComedyPanicsRegardlessOfCue(PassengerMotion motion) =>
            Assert.AreEqual(PassengerPose.Panicked, PassengerPresentation.PoseFor(Active(motion), PassengerCue.Impatient));

        [Test] public void InactivePassengersStaySeated() =>
            Assert.AreEqual(PassengerPose.Seated, PassengerPresentation.PoseFor(default, PassengerCue.Impatient));

        [UnityTest] public IEnumerator SceneUsesHumanoidPassengersDrivenByTheAnimator()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/ShipMayham/Scenes/ShipPrototype.unity", new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            var animator = hud.View.GetComponent<PrototypePassengerAnimator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(VoyageSimulation.MaxPassengers, animator.Animators.Length);
            for (int i = 0; i < animator.Animators.Length; i++)
            {
                Assert.IsNotNull(animator.Animators[i], "Passenger animator missing: " + i);
                Assert.IsTrue(animator.Animators[i].isHuman, "Passenger avatar is not Humanoid: " + i);
                Assert.IsNotNull(animator.Animators[i].runtimeAnimatorController);
                var capsule = hud.View.PassengerVisuals[i].Find("Passenger_Primitive");
                Assert.IsFalse(capsule.GetComponent<Renderer>().enabled, "Capsule body should be hidden: " + i);
            }
            yield return new ExitPlayMode();
        }
    }
}
