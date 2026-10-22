using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class HumanPassengerTests : PrototypeSceneFixture
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

        [Test] public void SceneUsesHumanoidPassengersDrivenByTheAnimator()
        {
            var view = FindInScene<PrototypeView>();
            var animator = view.GetComponent<PrototypePassengerAnimator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(VoyageSimulation.MaxPassengers, animator.Animators.Length);
            for (int i = 0; i < animator.Animators.Length; i++)
            {
                Assert.IsNotNull(animator.Animators[i], "Passenger animator missing: " + i);
                Assert.IsTrue(animator.Animators[i].isHuman, "Passenger avatar is not Humanoid: " + i);
                Assert.IsNotNull(animator.Animators[i].runtimeAnimatorController);
                var capsule = view.PassengerVisuals[i].Find("Passenger_Primitive");
                Assert.IsFalse(capsule.GetComponent<Renderer>().enabled, "Capsule body should be hidden: " + i);
            }
        }
    }
}
