using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Authoritative crew locomotion: velocity ramps up and down instead of switching instantly, which is what makes walking read
    /// as weight and responsiveness rather than a teleporting cursor. Pure functions so the tuning is unit-testable.
    /// </summary>
    public static class CrewKinematics
    {
        public const float WalkSpeed = 4.2f;
        /// <summary>Metres per second squared when input is held (0 to full speed in about 0.15 s).</summary>
        public const float Acceleration = 28f;
        /// <summary>Deceleration when input is released or reversed; slightly stronger so stops feel crisp.</summary>
        public const float Deceleration = 40f;

        /// <summary>Advances the velocity toward input * speed and returns it.</summary>
        public static Vector2 Step(ref Vector2 velocity, Vector2 input, float maxSpeed, float dt)
        {
            Vector2 desired = Vector2.ClampMagnitude(input, 1) * maxSpeed;
            bool speedingUp = desired.sqrMagnitude > .0001f && Vector2.Dot(desired, velocity) >= 0 && desired.sqrMagnitude >= velocity.sqrMagnitude;
            velocity = Vector2.MoveTowards(velocity, desired, (speedingUp ? Acceleration : Deceleration) * dt);
            return velocity;
        }

        /// <summary>
        /// Removes the velocity component of every axis that did not actually move, so walking into a counter stops dead rather
        /// than building up speed against it.
        /// </summary>
        public static void ResolveBlocked(ref Vector2 velocity, Vector2 attempted, Vector2 moved)
        {
            if (Mathf.Abs(attempted.x) > 1e-5f && Mathf.Abs(moved.x) < Mathf.Abs(attempted.x) * .5f) velocity.x = 0;
            if (Mathf.Abs(attempted.y) > 1e-5f && Mathf.Abs(moved.y) < Mathf.Abs(attempted.y) * .5f) velocity.y = 0;
        }
    }
}
