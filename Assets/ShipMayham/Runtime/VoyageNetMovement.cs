using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Command-driven crew movement. Clients send fixed-step move commands (see MoveCommand); the server executes each exactly
    /// once with the same locomotion model the client predicts with, and reports the last command it executed (InputAck) together
    /// with the resulting state so clients can reconcile. Crew without a recent command stream fall back to the held-input model.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>Seconds without a command after which a crew member is driven by the held-input fallback again.</summary>
        public const float CommandTimeout = .2f;
        /// <summary>Game time a client may "bank" and spend in a burst (jitter), also the cap that stops speed hacks.</summary>
        public const float MoveBudgetCap = .3f;
        private const float MoveBudgetRate = 1.25f;

        private readonly uint[] lastCommandSeq = new uint[MaxCrew];
        private readonly bool[] hasCommands = new bool[MaxCrew];
        private readonly float[] lastCommandClock = new float[MaxCrew], moveBudget = new float[MaxCrew];
        private readonly Vector2[] lastWalkPosition = new Vector2[MaxCrew];
        private ILocomotionWorld deckWorld;

        private ILocomotionWorld DeckWorldInstance => deckWorld ?? (deckWorld = new DeckLocomotionWorld(this));
        private static readonly ILocomotionWorld ShoreWorldInstance = new ShoreLocomotionWorld();

        /// <summary>The quay, shared with client prediction.</summary>
        public static ILocomotionWorld ShoreWorld => ShoreWorldInstance;

        /// <summary>Game time in whole milliseconds; stamped on replicated motion so clients can interpolate on the server clock.</summary>
        public uint ClockMs => (uint)(clock * 1000f);

        /// <summary>Updates the replicated timestamps; called by the session right before publishing.</summary>
        public void StampTime()
        {
            State.TimeMs = ClockMs;
            for (int i = 0; i < MaxCrew; i++) Crew[i].TimeMs = State.TimeMs;
        }

        public uint LastCommandSeq(int slot) => slot >= 0 && slot < MaxCrew ? lastCommandSeq[slot] : 0;

        private void ResetCommandStream(int i)
        {
            lastCommandSeq[i] = 0; hasCommands[i] = false; lastCommandClock[i] = float.NegativeInfinity; moveBudget[i] = MoveBudgetCap;
            Crew[i].InputAck = 0;
        }

        private bool Streaming(int i) => hasCommands[i] && clock - lastCommandClock[i] <= CommandTimeout;

        /// <summary>The speed factor used for walking, shared with the client predictor.</summary>
        public float WalkSpeedFactor(int i) =>
            Crew[i].OnShore ? 1 : CarriedPassenger(i) >= 0 ? .7f : Crew[i].Carrying == ServiceItem.Ammo ? .8f : 1;

        /// <summary>Executes every not-yet-seen command of a packet in order. Returns how many were executed.</summary>
        public int ApplyMoveCommands(ulong clientId, MoveCommandPacket packet)
        {
            int i = FindCrew(clientId);
            if (i < 0 || packet.Data == null || packet.Count == 0) return 0;
            int executed = 0;
            for (int k = 0; k < packet.Count; k++)
            {
                MoveCommand command = packet.At(k);
                // A client that restarted its counter (reconnect) must not be ignored forever.
                if (hasCommands[i] && lastCommandSeq[i] > 100000 && command.Seq < lastCommandSeq[i] - 100000) ResetCommandStream(i);
                if (hasCommands[i] ? command.Seq <= lastCommandSeq[i] : command.Seq == 0) continue;
                lastCommandSeq[i] = command.Seq;
                hasCommands[i] = true;
                lastCommandClock[i] = clock;
                Crew[i].InputAck = command.Seq;
                if (moveBudget[i] < CrewLocomotion.StepSeconds) continue;     // over budget: acknowledged, not simulated
                moveBudget[i] -= CrewLocomotion.StepSeconds;
                ExecuteMove(i, command);
                executed++;
            }
            return executed;
        }

        private void ExecuteMove(int i, MoveCommand command)
        {
            Vector2 input = command.Input;
            if (Crew[i].AtHelm || Crew[i].AtCrane || Crew[i].AtCannon)
            {
                // Station modes keep the held-input model: the command just refreshes the held input.
                inputs[i] = input;
                inputExpires[i] = clock + .35f;
                return;
            }
            StepWalker(i, input, command.Jump, CrewLocomotion.StepSeconds);
        }

        private void StepWalker(int i, Vector2 input, bool jump, float dt)
        {
            var state = new CrewMotionState
            {
                Position = Crew[i].Position, Velocity = Crew[i].Velocity, JumpHeight = Crew[i].JumpHeight, VerticalSpeed = Crew[i].VerticalSpeed
            };
            // Teleports (boarding, going ashore, respawn) must not carry the old momentum into the new place.
            if ((state.Position - lastWalkPosition[i]).sqrMagnitude > .0004f) state.Velocity = Vector2.zero;
            ILocomotionWorld world = Crew[i].OnShore ? ShoreWorldInstance : DeckWorldInstance;
            CrewLocomotion.Step(ref state, input, jump, CrewKinematics.WalkSpeed * WalkSpeedFactor(i), dt, world);
            Crew[i].Position = state.Position; Crew[i].Velocity = state.Velocity;
            Crew[i].JumpHeight = state.JumpHeight; Crew[i].VerticalSpeed = state.VerticalSpeed;
            lastWalkPosition[i] = state.Position;
        }

        // One crew member's share of Tick: stations use the held input, walkers are advanced here only when no command stream
        // is already doing it.
        private void AdvanceCrew(int i, Vector2 heldInput, float dt, ref Vector2 helmInput, ref bool helmManned)
        {
            if (Crew[i].AtHelm || Crew[i].AtCrane || Crew[i].AtCannon)
            {
                TickJump(i, dt);
                Crew[i].Velocity = Vector2.zero;
                if (Crew[i].AtHelm) { if (State.Phase == VoyagePhase.Sailing) { helmInput = heldInput; helmManned = true; } }
                else if (Crew[i].AtCrane) MoveHarborCrane(i, heldInput, dt);
                return;
            }
            if (Streaming(i)) return;
            StepWalker(i, heldInput, false, dt);
        }

        /// <summary>Radii of the obstacles on the deck; clients predicting movement use the same numbers.</summary>
        public const float StationClearance = .75f, SeatClearance = .7f, PassengerClearance = .6f;

        private sealed class DeckLocomotionWorld : ILocomotionWorld
        {
            private readonly VoyageSimulation sim;
            public DeckLocomotionWorld(VoyageSimulation sim) { this.sim = sim; }
            public Vector2 Min => -DeckBounds;
            public Vector2 Max => DeckBounds;
            public bool Walkable(Vector2 position) => sim.Walkable(position);
        }

        private sealed class ShoreLocomotionWorld : ILocomotionWorld
        {
            public Vector2 Min => new Vector2(HarborTownLayout.Bounds.xMin, HarborTownLayout.Bounds.yMin);
            public Vector2 Max => new Vector2(HarborTownLayout.Bounds.xMax, HarborTownLayout.Bounds.yMax);
            public bool Walkable(Vector2 position) => ShoreWalkable(position);
        }
    }
}
