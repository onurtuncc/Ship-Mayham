using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Arcade ship handling with weight: the throttle lever eases toward the input, speed builds from it, and the rudder builds
    /// and releases turn rate instead of snapping. Turning is weaker at a standstill (a ship cannot pirouette) but never zero so
    /// the helm never feels dead. Pure state machine; VoyageSimulation owns one instance.
    /// </summary>
    public struct ShipSteering
    {
        public const float ThrottleResponse = 3.2f;     // lever units per second toward the held input
        public const float SpeedPerThrottle = 3.4f;     // speed change per second at full lever
        public const float MaxTurnRate = 46f;           // degrees per second at cruising speed
        public const float TurnAcceleration = 90f;      // degrees per second squared while steering
        public const float TurnRelease = 140f;          // degrees per second squared when the wheel is released
        public const float MinTurnFactor = .4f;         // share of the turn rate available at a standstill
        public const float FullTurnSpeed = 3.5f;        // speed at which the full turn rate is available

        public float Throttle, TurnVelocity;

        /// <summary>Advances one step; returns how much to add to speed and to heading (degrees, clockwise).</summary>
        public void Step(Vector2 input, float speed, float dt, out float speedDelta, out float headingDelta)
        {
            input = Vector2.ClampMagnitude(input, 1);
            Throttle = Mathf.MoveTowards(Throttle, input.y, ThrottleResponse * dt);
            speedDelta = Throttle * SpeedPerThrottle * dt;

            float authority = Mathf.Lerp(MinTurnFactor, 1, Mathf.Clamp01(speed / FullTurnSpeed));
            float target = input.x * MaxTurnRate * authority;
            float rate = Mathf.Approximately(input.x, 0) || Mathf.Sign(input.x) != Mathf.Sign(TurnVelocity) && Mathf.Abs(TurnVelocity) > 1 ? TurnRelease : TurnAcceleration;
            TurnVelocity = Mathf.MoveTowards(TurnVelocity, target, rate * dt);
            headingDelta = TurnVelocity * dt;
        }

        /// <summary>Nobody is at the helm: the lever and the rudder settle back to neutral.</summary>
        public void Release(float dt)
        {
            Throttle = Mathf.MoveTowards(Throttle, 0, ThrottleResponse * dt);
            TurnVelocity = Mathf.MoveTowards(TurnVelocity, 0, TurnRelease * dt);
        }

        public void Reset() { Throttle = 0; TurnVelocity = 0; }
    }
}
