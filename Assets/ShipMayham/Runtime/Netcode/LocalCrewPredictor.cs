using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Client-side prediction with server reconciliation for the local walker. Every fixed step the predictor turns the current
    /// input into a numbered MoveCommand and runs the shared locomotion model on it immediately, so the player never waits for
    /// the round trip. When an authoritative state arrives carrying the number of the last command the server executed, the
    /// predictor rewinds to that state, replays the commands the server has not seen yet, and feeds the (usually tiny)
    /// difference into a decaying visual offset instead of snapping. Pure and unit-testable.
    /// </summary>
    public sealed class LocalCrewPredictor
    {
        public const int MaxPending = 150;
        /// <summary>Larger corrections than this (metres) are teleports: snap instead of sliding.</summary>
        public const float SnapDistance = 2.5f;
        /// <summary>Corrections smaller than this are ignored to avoid micro-jitter.</summary>
        public const float DeadZone = .004f;
        /// <summary>Rate (1/s) at which the visual correction offset decays.</summary>
        public const float OffsetDecay = 9f;

        private struct Pending
        {
            public MoveCommand Command;
            public float SpeedFactor;
            public ILocomotionWorld World;
        }

        private readonly List<Pending> pending = new List<Pending>(MaxPending);
        private uint nextSeq = 1, lastAck;
        private double lastServerTime = double.NegativeInfinity;

        /// <summary>Predicted state (what the simulation will be once the server catches up).</summary>
        public CrewMotionState State;
        /// <summary>Visual correction still to be eased out: add to the predicted position for rendering.</summary>
        public Vector2 PositionOffset;
        public float JumpOffset;
        public bool Initialized { get; private set; }
        public int PendingCount => pending.Count;
        public uint LastAck => lastAck;
        /// <summary>Size of the most recent correction in metres (for diagnostics and tests).</summary>
        public float LastError { get; private set; }

        /// <summary>
        /// How far (0..1) rendering is between the previous and the current fixed step. The prediction advances in 30 Hz steps;
        /// drawing it directly would stair-step at 30 Hz, so the renderer interpolates between the last two steps.
        /// </summary>
        public float StepAlpha = 1f;
        private Vector2 previousPosition;
        private float previousJump;

        public Vector2 RenderPosition => Vector2.Lerp(previousPosition, State.Position, StepAlpha) + PositionOffset;
        public float RenderJump => Mathf.Max(0, Mathf.Lerp(previousJump, State.JumpHeight, StepAlpha) + JumpOffset);

        /// <summary>Adopts an authoritative state outright and forgets unacknowledged commands (mode changes, teleports, respawns).</summary>
        public void Resync(CrewMotionState server, uint ack)
        {
            State = server;
            previousPosition = server.Position; previousJump = server.JumpHeight;
            pending.Clear();
            PositionOffset = Vector2.zero; JumpOffset = 0;
            lastAck = ack;
            // Keep numbering ahead of everything the server has acknowledged so old and new commands can never be confused.
            if (nextSeq <= ack) nextSeq = ack + 1;
            Initialized = true;
            LastError = 0;
        }

        /// <summary>A numbered command without prediction (the player is at a station, or this is the host).</summary>
        public MoveCommand Issue(Vector2 input, bool jump) => MoveCommand.Create(nextSeq++, input, jump);

        /// <summary>One fixed step: creates the command to send and predicts it locally.</summary>
        public MoveCommand Step(Vector2 input, bool jump, float speedFactor, ILocomotionWorld world)
        {
            var command = MoveCommand.Create(nextSeq++, input, jump);
            previousPosition = State.Position; previousJump = State.JumpHeight;
            // Predict from the quantised input the server will actually see.
            CrewLocomotion.Step(ref State, command.Input, command.Jump, CrewKinematics.WalkSpeed * speedFactor, CrewLocomotion.StepSeconds, world);
            if (pending.Count >= MaxPending) pending.RemoveAt(0);
            pending.Add(new Pending { Command = command, SpeedFactor = speedFactor, World = world });
            return command;
        }

        /// <summary>Applies an authoritative state: rewind, replay unacknowledged commands, smooth the difference.</summary>
        public void Reconcile(CrewMotionState server, uint ack, double serverTime)
        {
            if (!Initialized) { Resync(server, ack); return; }
            // Datagrams can arrive late or twice: never go backwards in time.
            if (ack < lastAck || serverTime < lastServerTime) return;
            lastServerTime = serverTime;
            lastAck = ack;

            Vector2 beforePosition = RenderPosition;
            float beforeJump = RenderJump;
            Vector2 oldState = State.Position; float oldJump = State.JumpHeight;
            State = server;
            int firstUnseen = 0;
            while (firstUnseen < pending.Count && pending[firstUnseen].Command.Seq <= ack) firstUnseen++;
            if (firstUnseen > 0) pending.RemoveRange(0, firstUnseen);
            previousPosition = State.Position; previousJump = State.JumpHeight;
            for (int i = 0; i < pending.Count; i++)
            {
                var p = pending[i];
                previousPosition = State.Position; previousJump = State.JumpHeight;
                CrewLocomotion.Step(ref State, p.Command.Input, p.Command.Jump, CrewKinematics.WalkSpeed * p.SpeedFactor, CrewLocomotion.StepSeconds, p.World);
            }

            // The correction is measured between what was on screen and what the rewound-and-replayed prediction now puts on screen.
            Vector2 error = beforePosition - RenderPosition;
            LastError = (oldState - State.Position).magnitude;
            if (error.magnitude > SnapDistance) { PositionOffset = Vector2.zero; JumpOffset = 0; return; }
            PositionOffset += error.magnitude < DeadZone ? Vector2.zero : error;
            float jumpError = beforeJump - RenderJump;
            JumpOffset += Mathf.Abs(jumpError) < DeadZone ? 0 : jumpError;
        }

        /// <summary>Eases the visual correction offset toward zero.</summary>
        public void Decay(float dt)
        {
            float k = Mathf.Exp(-OffsetDecay * dt);
            PositionOffset *= k;
            JumpOffset *= k;
            if (PositionOffset.sqrMagnitude < 1e-8f) PositionOffset = Vector2.zero;
        }
    }
}
