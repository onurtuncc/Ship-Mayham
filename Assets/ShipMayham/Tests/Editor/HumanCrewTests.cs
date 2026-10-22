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
