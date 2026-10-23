using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class MotionTests
    {
        // ---------------------------------------------------------------- snapshot smoothing

        // Renders 144 fps frames from snapshots published every 40 ms of an entity moving at constant speed along x. Returns the
        // frame-to-frame speeds measured after a warm-up, as a (max deviation from the true speed) fraction.
        private static float SpeedJitter(System.Func<float, float, float, float> render, float speed)
        {
            const float frame = 1f / 144f, snapshotEvery = .04f, warmUp = .4f, duration = 2f;
            float snapshot = 0, nextSnapshot = 0, previous = 0, worst = 0;
            bool started = false;
            for (float t = 0; t < duration; t += frame)
            {
                if (t >= nextSnapshot) { snapshot = speed * nextSnapshot; nextSnapshot += snapshotEvery; }
                float position = render(snapshot, t, frame);
                if (t >= warmUp)
                {
                    if (started) worst = Mathf.Max(worst, Mathf.Abs((position - previous) / frame - speed) / speed);
                    started = true;
                }
                previous = position;
            }
            return worst;
        }

        [Test] public void SmootherRemovesTheSnapshotSawtooth()
        {
            var smoother = new MotionSmoother();
            float smoothed = SpeedJitter((snapshot, t, dt) => smoother.Step(new Vector3(snapshot, 0, 0), t, dt).x, 4.2f);
            float naivePosition = 0; bool first = true;
            float naive = SpeedJitter((snapshot, t, dt) =>
            {
                if (first) { naivePosition = snapshot; first = false; }
                naivePosition = Mathf.Lerp(naivePosition, snapshot, 1 - Mathf.Exp(-18 * dt));
                return naivePosition;
            }, 4.2f);
            Assert.Less(smoothed, .25f, "Smoothed motion should stay within 25% of the true speed every frame.");
            Assert.Less(smoothed, naive * .6f, $"Smoother ({smoothed:0.00}) should be clearly steadier than a plain lerp ({naive:0.00}).");
        }

        [Test] public void SmootherSettlesOnAStopWithoutOvershoot()
        {
            var smoother = new MotionSmoother();
            const float frame = 1f / 144f;
            float t = 0, x = 0;
            for (; t < 1f; t += frame) { x = 4.2f * Mathf.Floor(t / .04f) * .04f; smoother.Step(new Vector3(x, 0, 0), t, frame); }
            float finalX = x, peak = 0;
            for (float end = t + .6f; t < end; t += frame)
            {
                float position = smoother.Step(new Vector3(finalX, 0, 0), t, frame).x;
                peak = Mathf.Max(peak, position - finalX);
            }
            Assert.LessOrEqual(peak, .12f, "Dead reckoning must not run far past the last snapshot.");
            Assert.AreEqual(finalX, smoother.Position.x, .02f, "The smoother should settle on the last authoritative value.");
        }

        [Test] public void TeleportsSnapInsteadOfSlidingAcrossTheMap()
        {
            var smoother = new MotionSmoother { SnapDistance = 3f };
            smoother.Snap(Vector3.zero, 0);
            Assert.AreEqual(new Vector3(20, 0, 0), smoother.Step(new Vector3(20, 0, 0), .02f, .02f));
        }

        [Test] public void AngleSmootherFollowsTheShortWayAcrossNorth()
        {
            var smoother = new AngleSmoother();
            const float frame = 1f / 144f;
            float previous = 350, worstStep = 0;
            smoother.Snap(350, 0);
            for (float t = 0; t < 1.2f; t += frame)
            {
                float heading = Mathf.Repeat(350 + 40f * Mathf.Floor(t / .04f) * .04f, 360);   // 40 deg/s, crossing 0
                float angle = smoother.Step(heading, t, frame);
                worstStep = Mathf.Max(worstStep, Mathf.Abs(Mathf.DeltaAngle(previous, angle)));
                previous = angle;
            }
            Assert.Less(worstStep, 2f, "No frame should swing the long way round.");
            Assert.AreEqual(40f, smoother.Rate, 8f, "The turn rate estimate should match the real rate for banking.");
        }

        // ---------------------------------------------------------------- crew locomotion

        [Test] public void CrewBuildsSpeedInAboutAFifthOfASecond()
        {
            Vector2 velocity = Vector2.zero;
            CrewKinematics.Step(ref velocity, Vector2.right, CrewKinematics.WalkSpeed, .02f);
            Assert.Less(velocity.magnitude, CrewKinematics.WalkSpeed * .5f, "One tick must not already be full speed.");
            for (int i = 0; i < 10; i++) CrewKinematics.Step(ref velocity, Vector2.right, CrewKinematics.WalkSpeed, .02f);
            Assert.AreEqual(CrewKinematics.WalkSpeed, velocity.magnitude, .001f);
        }

        [Test] public void CrewStopsCrispButNotInstantly()
        {
            Vector2 velocity = Vector2.right * CrewKinematics.WalkSpeed;
            CrewKinematics.Step(ref velocity, Vector2.zero, CrewKinematics.WalkSpeed, .02f);
            Assert.Greater(velocity.magnitude, 0, "There should be a little slide.");
            for (int i = 0; i < 6; i++) CrewKinematics.Step(ref velocity, Vector2.zero, CrewKinematics.WalkSpeed, .02f);
            Assert.AreEqual(0, velocity.magnitude, .001f);
        }

        [Test] public void ReversingUsesTheStrongerBraking()
        {
            Vector2 velocity = Vector2.right * CrewKinematics.WalkSpeed;
            CrewKinematics.Step(ref velocity, Vector2.left, CrewKinematics.WalkSpeed, .02f);
            Assert.AreEqual(CrewKinematics.WalkSpeed - CrewKinematics.Deceleration * .02f, velocity.x, .001f);
        }

        [Test] public void DiagonalInputDoesNotExceedWalkSpeed()
        {
            Vector2 velocity = Vector2.zero;
            for (int i = 0; i < 30; i++) CrewKinematics.Step(ref velocity, new Vector2(1, 1), CrewKinematics.WalkSpeed, .02f);
            Assert.AreEqual(CrewKinematics.WalkSpeed, velocity.magnitude, .001f);
        }

        [Test] public void WalkingIntoAWallCancelsOnlyTheBlockedAxis()
        {
            Vector2 velocity = new Vector2(4, 3);
            CrewKinematics.ResolveBlocked(ref velocity, new Vector2(.08f, .06f), new Vector2(0, .06f));
            Assert.AreEqual(0, velocity.x);
            Assert.AreEqual(3, velocity.y);
        }

        // ---------------------------------------------------------------- ship handling

        private static void Run(ref ShipSteering steering, Vector2 input, float seconds, ref float speed, ref float heading, float maxSpeed = 7)
        {
            for (float t = 0; t < seconds - 1e-4f; t += .02f)
            {
                steering.Step(input, speed, .02f, out float ds, out float dh);
                speed = Mathf.Clamp(speed + ds, 0, maxSpeed);
                heading += dh;
            }
        }

        [Test] public void ThrottleEasesInInsteadOfSnapping()
        {
            var steering = new ShipSteering();
            float speed = 0, heading = 0;
            Run(ref steering, Vector2.up, .1f, ref speed, ref heading);
            Assert.Less(steering.Throttle, .5f);
            float early = speed;
            Run(ref steering, Vector2.up, 1.4f, ref speed, ref heading);
            Assert.Greater(speed, early * 4, "Speed should build as the lever rises.");
            Assert.AreEqual(1f, steering.Throttle, .01f);
        }

        [Test] public void RudderBuildsAndReleasesTurnRate()
        {
            var steering = new ShipSteering();
            float speed = 5, heading = 0;
            Run(ref steering, Vector2.right, .1f, ref speed, ref heading);
            Assert.Less(steering.TurnVelocity, ShipSteering.MaxTurnRate * .3f, "Turn rate must ramp, not jump to the maximum.");
            Run(ref steering, Vector2.right, 1f, ref speed, ref heading);
            Assert.AreEqual(ShipSteering.MaxTurnRate, steering.TurnVelocity, .5f);
            float before = heading;
            Run(ref steering, Vector2.zero, .1f, ref speed, ref heading);
            Assert.Greater(heading, before, "The ship keeps swinging briefly after the wheel is released.");
            Run(ref steering, Vector2.zero, 1f, ref speed, ref heading);
            Assert.AreEqual(0, steering.TurnVelocity, .01f);
        }

        [Test] public void ShipsTurnSlowerAtAStandstillButStillTurn()
        {
            var parked = new ShipSteering(); var cruising = new ShipSteering();
            float speed = 0, heading = 0, fastSpeed = 6, fastHeading = 0;
            Run(ref parked, Vector2.right, 2f, ref speed, ref heading);
            Run(ref cruising, Vector2.right, 2f, ref fastSpeed, ref fastHeading);
            Assert.Greater(heading, 20f, "The helm must never feel dead.");
            Assert.Less(heading, fastHeading * .8f);
        }

        [Test] public void ReleasingTheHelmSettlesTheControls()
        {
            var steering = new ShipSteering { Throttle = 1, TurnVelocity = 40 };
            for (int i = 0; i < 60; i++) steering.Release(.02f);
            Assert.AreEqual(0, steering.Throttle, .001f);
            Assert.AreEqual(0, steering.TurnVelocity, .001f);
        }
    }
}
