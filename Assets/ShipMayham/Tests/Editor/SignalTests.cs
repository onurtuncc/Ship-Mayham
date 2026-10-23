using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class SignalTests
    {
        [Test]
        public void TheLimiterAllowsOneSignalPerCooldownPerSlot()
        {
            var limiter = new SignalLimiter();
            Assert.IsTrue(limiter.TryAcquire(0, 10f));
            Assert.IsFalse(limiter.TryAcquire(0, 10.5f));
            Assert.IsTrue(limiter.TryAcquire(1, 10.5f), "Each crew member has their own cooldown.");
            Assert.IsTrue(limiter.TryAcquire(0, 10f + Signals.CooldownSeconds + .01f));
            limiter.Forget(0);
            Assert.IsTrue(limiter.TryAcquire(0, 10.1f));
            limiter.Clear();
            Assert.IsTrue(limiter.TryAcquire(1, 10.2f));
        }

        [Test]
        public void TheWheelMapsDirectionsToEmotes()
        {
            Assert.IsNull(Signals.WheelChoice(Vector2.zero));
            Assert.IsNull(Signals.WheelChoice(new Vector2(5, 5)), "Inside the dead zone nothing is chosen.");
            Assert.AreEqual(SignalKind.Help, Signals.WheelChoice(new Vector2(2, 40)));
            Assert.AreEqual(SignalKind.WatchOut, Signals.WheelChoice(new Vector2(-3, -40)));
            Assert.AreEqual(SignalKind.Thanks, Signals.WheelChoice(new Vector2(40, 10)));
            Assert.AreEqual(SignalKind.OverHere, Signals.WheelChoice(new Vector2(-40, -10)));
        }

        [Test]
        public void EveryKindHasATextAGestureAndAColour()
        {
            foreach (SignalKind kind in System.Enum.GetValues(typeof(SignalKind)))
            {
                Assert.IsNotEmpty(Signals.Text(kind), kind.ToString());
                Assert.IsFalse(Signals.IsEmote(kind) && Signals.Text(kind) == "PING");
                Signals.PoseFor(kind);
            }
            Assert.IsFalse(Signals.IsEmote(SignalKind.Ping));
            Assert.AreNotEqual(Signals.ColorFor(0), Signals.ColorFor(1));
            Assert.AreEqual(Signals.ColorFor(0), Signals.ColorFor(4), "Colours wrap around the four crew slots.");
        }

        [Test]
        public void ThePointFromTheNetworkIsChecked()
        {
            Assert.IsTrue(Signals.IsValidPoint(new Vector3(3, 1, -40)));
            Assert.IsFalse(Signals.IsValidPoint(new Vector3(float.NaN, 0, 0)));
            Assert.IsFalse(Signals.IsValidPoint(new Vector3(0, float.PositiveInfinity, 0)));
            Assert.IsFalse(Signals.IsValidPoint(new Vector3(1e9f, 0, 0)));
        }

        [Test]
        public void TheCrewControllerHasAStateForEveryPose()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/ShipMayham/Art/Generated/Animations/Crew.controller");
            Assert.IsNotNull(controller);
            var states = controller.layers[0].stateMachine.states.Select(s => s.state.name).ToList();
            foreach (CrewPose pose in System.Enum.GetValues(typeof(CrewPose)))
                CollectionAssert.Contains(states, pose.ToString(), "Run Ship Mayham > Upgrade Prototype (Crew Gestures).");
        }

        [Test]
        public void ATargetRemembersWhereItWasAimedAt()
        {
            var point = new Vector3(1, 2, 3);
            Assert.AreEqual(point, InteractionTarget.Station(2, default, point).WorldPoint);
            Assert.AreEqual(point, InteractionTarget.Passenger(1, default, point).WorldPoint);
            Assert.AreEqual(Vector3.zero, InteractionTarget.None.WorldPoint);
        }
    }
}
