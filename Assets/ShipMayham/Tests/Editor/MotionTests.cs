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

        private const float Dt = .02f, Cruise = 4f;

        /// <summary>Runs the helm for a while; returns the heading change in degrees.</summary>
        private static float Run(ref ShipMotion m, Vector2 input, float seconds, bool manned = true, float maxSpeed = Cruise)
        {
            float heading = 0;
            for (float t = 0; t < seconds - 1e-4f; t += Dt) heading += ShipSteering.Step(ref m, input, manned, maxSpeed, Dt);
            return heading;
        }

        [Test] public void TheEngineLeverStaysWhereItIsSet()
        {
            var m = new ShipMotion();
            Run(ref m, Vector2.up, .5f);
            float lever = m.Throttle;
            Assert.Greater(lever, .2f);
            Assert.Less(lever, .4f, "the lever travels, it does not snap");
            Run(ref m, Vector2.zero, 3f);
            Assert.AreEqual(lever, m.Throttle, 1e-4f, "letting go of W leaves the engine order as it was");
        }

        [Test] public void AShipGathersWaySlowlyAndReachesItsTopSpeed()
        {
            var m = new ShipMotion { Throttle = 1 };
            Run(ref m, Vector2.zero, 1f);
            Assert.Less(m.Speed, 1.2f, "a hull does not leap forward");
            Run(ref m, Vector2.zero, 40f);
            Assert.AreEqual(Cruise, m.Speed, .05f, "full ahead settles on the top speed against the water's resistance");
        }

        [Test] public void StoppingTheEngineCoastsAndAsternTakesTheWayOff()
        {
            var coasting = new ShipMotion { Speed = Cruise };
            Run(ref coasting, Vector2.zero, 3f);
            Assert.Greater(coasting.Speed, 1.8f, "with the engine stopped she carries her way");
            var braking = new ShipMotion { Speed = Cruise, Throttle = -ShipSteering.MaxAstern };
            Run(ref braking, Vector2.zero, 3f);
            Assert.Less(braking.Speed, coasting.Speed - .6f, "astern brakes harder than coasting");
            Run(ref braking, Vector2.zero, 10f);
            Assert.Less(braking.Speed, 0, "held astern, she starts to make sternway");
            Assert.Greater(braking.Speed, -Cruise * .5f, "and goes astern slower than ahead");
        }

        [Test] public void TheRudderNeedsWaterFlowingPastIt()
        {
            var drifting = new ShipMotion();
            var underWay = new ShipMotion { Speed = Cruise, Throttle = 1 };
            float still = Run(ref drifting, Vector2.right, 3f);
            float moving = Run(ref underWay, Vector2.right, 3f);
            Assert.Less(Mathf.Abs(still), 1f, "a ship lying still does not answer the helm");
            Assert.Greater(moving, 15f);
            var wash = new ShipMotion { Throttle = 1 };
            float kicked = 0;
            for (int n = 0; n < 50; n++) { wash.Speed = 0; kicked += ShipSteering.Step(ref wash, Vector2.right, true, Cruise, Dt); }
            Assert.Greater(kicked, .5f, "propeller wash gives a little steering even before she moves");
        }

        [Test] public void TheBowTakesAMomentToSwingAndKeepsSwingingAfterTheRudderIsCentred()
        {
            var m = new ShipMotion { Speed = Cruise, Throttle = 1, Rudder = 1 };
            ShipSteering.Step(ref m, Vector2.right, true, Cruise, Dt);
            Assert.Less(m.YawRate, ShipSteering.TurnRateGain * .1f, "rate of turn builds with the Nomoto lag");
            Run(ref m, Vector2.right, 8f);
            float steady = m.YawRate;
            Assert.Greater(steady, ShipSteering.TurnRateGain * .8f);
            m.Rudder = 0;
            ShipSteering.Step(ref m, Vector2.zero, true, Cruise, Dt);
            Assert.Greater(m.YawRate, steady * .9f, "the swing carries on after the wheel is centred");
            Run(ref m, Vector2.zero, 10f);
            Assert.AreEqual(0, m.YawRate, .3f, "and dies away");
        }

        [Test] public void GoingAsternReversesTheSteering()
        {
            var m = new ShipMotion { Speed = -1.5f, Throttle = -ShipSteering.MaxAstern };
            Assert.Less(Run(ref m, Vector2.right, 2f), 0, "starboard rudder swings the bow to port going astern");
        }

        [Test] public void ATurningHullSkidsOutwardAndLosesSpeed()
        {
            var straight = new ShipMotion { Speed = Cruise, Throttle = 1 };
            var turning = new ShipMotion { Speed = Cruise, Throttle = 1 };
            Run(ref straight, Vector2.zero, 6f);
            Run(ref turning, Vector2.right, 6f);
            Assert.Less(turning.Drift, -.1f, "turning to starboard, the hull slips to port");
            Assert.Less(turning.Speed, straight.Speed - .1f);
            Assert.Less(ShipSteering.Velocity(turning, 0).x, 0, "the skid shows in the ship's track");
        }

        [Test] public void AnEmptyHelmEasesTheControlsBackWhileTheShipCarriesItsWay()
        {
            var m = new ShipMotion { Speed = Cruise, Throttle = 1, Rudder = 1 };
            Run(ref m, Vector2.zero, 3f, false);
            Assert.AreEqual(0, m.Throttle, .001f);
            Assert.AreEqual(0, m.Rudder, .001f);
            Assert.Greater(m.Speed, 1.5f);
        }

        [Test] public void EngineOrdersReadLikeATelegraph()
        {
            Assert.AreEqual("STOP", ShipSteering.EngineOrder(0));
            Assert.AreEqual("FULL AHEAD", ShipSteering.EngineOrder(1));
            Assert.AreEqual("FULL ASTERN", ShipSteering.EngineOrder(-ShipSteering.MaxAstern));
            Assert.AreEqual("amidships", GameHud.RudderText(0));
            Assert.AreEqual("35° starboard", GameHud.RudderText(1));
            Assert.AreEqual("35° port", GameHud.RudderText(-1));
        }
    }
}
