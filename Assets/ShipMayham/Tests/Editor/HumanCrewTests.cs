using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class HumanCrewTests : PrototypeSceneFixture
    {
        [Test] public void StandingCrewIdle() =>
            Assert.AreEqual(CrewPose.Idle, CrewPresentation.PoseFor(new CrewState { Active = true }, 0));

        [Test] public void MovingCrewWalk() =>
            Assert.AreEqual(CrewPose.Walking, CrewPresentation.PoseFor(new CrewState { Active = true }, CrewPresentation.WalkSpeedThreshold + .5f));

        [Test] public void TinyDriftDoesNotStartWalking() =>
            Assert.AreEqual(CrewPose.Idle, CrewPresentation.PoseFor(new CrewState { Active = true }, CrewPresentation.WalkSpeedThreshold * .5f));

        [TestCase(0f)] [TestCase(3f)]
        public void CarryingOverridesMovement(float speed) =>
            Assert.AreEqual(CrewPose.Carrying, CrewPresentation.PoseFor(new CrewState { Active = true, Carrying = ServiceItem.Ammo }, speed));

        [Test] public void ArrivalMakesTheCrewDanceInTwoStyles()
        {
            var crew = new CrewState { Active = true };
            Assert.AreEqual(CrewPose.Dance, CrewPresentation.PoseFor(crew, 0, VoyagePhase.Arrived, 0));
            Assert.AreEqual(CrewPose.Dance2, CrewPresentation.PoseFor(crew, 0, VoyagePhase.Arrived, 1));
        }

        [Test] public void ALostShipLeavesTheCrewDefeated() =>
            Assert.AreEqual(CrewPose.Defeated, CrewPresentation.PoseFor(new CrewState { Active = true, Carrying = ServiceItem.Food }, 2, VoyagePhase.Failed, 0));

        [Test] public void JumpingWinsOverWalkingAndCarrying() =>
            Assert.AreEqual(CrewPose.Jump, CrewPresentation.PoseFor(new CrewState { Active = true, JumpHeight = .4f, Carrying = ServiceItem.Ammo }, 3));

        [TestCase(true, false)] [TestCase(false, true)]
        public void OperatingTheCannonOrCraneUsesTheInteractionPose(bool cannon, bool crane) =>
            Assert.AreEqual(CrewPose.Interact, CrewPresentation.PoseFor(new CrewState { Active = true, AtCannon = cannon, AtCrane = crane }, 0));

        [Test] public void SceneUsesHumanoidCrewDrivenByTheAnimator()
        {
            var view = FindInScene<PrototypeView>();
            var animator = view.GetComponent<PrototypeCrewAnimator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(view.CrewRoots.Length, animator.Animators.Length);
            for (int i = 0; i < animator.Animators.Length; i++)
            {
                Assert.IsNotNull(animator.Animators[i], "Crew animator missing: " + i);
                Assert.IsTrue(animator.Animators[i].isHuman, "Crew avatar is not Humanoid: " + i);
                var capsule = view.CrewRoots[i].Find("Visuals/Body_Primitive");
                Assert.IsFalse(capsule.GetComponent<Renderer>().enabled, "Capsule body should be hidden: " + i);
            }
        }
    }
}
