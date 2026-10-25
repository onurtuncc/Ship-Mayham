using UnityEngine;

namespace ShipMayham
{
    /// <summary>Position, velocity and jump of one crew member: everything the walking model advances.</summary>
    public struct CrewMotionState
    {
        public Vector2 Position, Velocity;
        public float JumpHeight, VerticalSpeed;
        /// <summary>Which area's coordinates <see cref="Position"/> is in: the quay (true) or the ship (false).</summary>
        public bool OnShore;

        public bool Airborne => JumpHeight > 0 || VerticalSpeed != 0;
    }

    /// <summary>The ground a crew member walks on (deck or quay): bounds plus the obstacle test.</summary>
    public interface ILocomotionWorld
    {
        Vector2 Min { get; }
        Vector2 Max { get; }
        bool Walkable(Vector2 position);
    }

    /// <summary>
    /// The one authoritative walking-and-jumping model. The server integrates it for every crew member and a client runs the very
    /// same function to predict its own movement, so prediction and reconciliation agree bit for bit as long as both feed it the
    /// same input, step and world. No allocations, no engine objects besides Mathf/Vector2.
    /// </summary>
    public static class CrewLocomotion
    {
        /// <summary>Fixed duration of one movement command: inputs are sampled and simulated at this rate on both ends.</summary>
        public const float StepSeconds = 1f / 30f;
        public const float JumpSpeed = 6f, JumpGravity = 18f;

        public static void StepVertical(ref CrewMotionState state, float dt)
        {
            if (!state.Airborne) return;
            state.JumpHeight += state.VerticalSpeed * dt - .5f * JumpGravity * dt * dt;
            state.VerticalSpeed -= JumpGravity * dt;
            if (state.JumpHeight <= 0)
            {
                state.JumpHeight = 0;
                state.VerticalSpeed = 0;
            }
        }

        /// <summary>
        /// Advances one step. <paramref name="jump"/> starts a jump only from the ground; the caller decides whether jumping is
        /// allowed at all (not at a station, not while carrying a passenger over the side, ...).
        /// </summary>
        public static void Step(ref CrewMotionState state, Vector2 input, bool jump, float maxSpeed, float dt, ILocomotionWorld world)
        {
            if (jump && !state.Airborne) state.VerticalSpeed = JumpSpeed;
            StepVertical(ref state, dt);
            CrewKinematics.Step(ref state.Velocity, input, maxSpeed, dt);
            Vector2 attempted = state.Velocity * dt, position = state.Position;
            // Someone already standing inside an obstacle (put there by a ladder, a respawn or a passenger sitting down beside
            // them) could otherwise never take a single step: every step out still starts inside. Let them walk free.
            bool trapped = !world.Walkable(position);
            // Separate axes allow sliding along counters.
            Vector2 candidate = new Vector2(Mathf.Clamp(position.x + attempted.x, world.Min.x, world.Max.x), position.y);
            if (trapped || world.Walkable(candidate)) position = candidate;
            candidate = new Vector2(position.x, Mathf.Clamp(position.y + attempted.y, world.Min.y, world.Max.y));
            if (trapped || world.Walkable(candidate)) position = candidate;
            CrewKinematics.ResolveBlocked(ref state.Velocity, attempted, position - state.Position);
            state.Position = position;
        }
    }
}
