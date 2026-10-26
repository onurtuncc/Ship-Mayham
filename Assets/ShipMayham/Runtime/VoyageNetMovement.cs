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
        private readonly GangwayWorld[] gangwayWorlds = { new GangwayWorld(), new GangwayWorld(), new GangwayWorld(), new GangwayWorld() };
        private ILocomotionWorld deckWorld;

        private ILocomotionWorld DeckWorldInstance => deckWorld ?? (deckWorld = new DeckLocomotionWorld(this));
        private static readonly ILocomotionWorld[] ShoreWorlds = BuildShoreWorlds();

        private static ILocomotionWorld[] BuildShoreWorlds()
        {
            var worlds = new ILocomotionWorld[Campaign.Count];
            for (int i = 0; i < worlds.Length; i++) worlds[i] = new ShoreLocomotionWorld(i);
            return worlds;
        }

        /// <summary>The ground of one island (town platform and the land around it), shared with client prediction.</summary>
        public static ILocomotionWorld ShoreWorldFor(int island) => ShoreWorlds[Mathf.Clamp(island, 0, ShoreWorlds.Length - 1)];

        /// <summary>The ground of the home island; the platform is the same on every island.</summary>
        public static ILocomotionWorld ShoreWorld => ShoreWorlds[Campaign.BrassQuay];

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
            Crew[i].Swimming ? SwimSpeedFactor : Crew[i].OnShore ? 1 : CarriedPassenger(i) >= 0 ? .7f : Crew[i].Carrying == ServiceItem.Ammo ? .8f : 1;

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
            if (Crew[i].Doing != MiniGameKind.None) input = Vector2.zero;       // busy with a skill game: standing still
            if (Crew[i].AtHelm || Crew[i].AtCrane || Crew[i].AtCannon)
            {
                // Station modes keep the held-input model: the command just refreshes the held input.
                inputs[i] = input;
                inputExpires[i] = clock + .35f;
                return;
            }
            StepWalker(i, input, command.Jump, CrewLocomotion.StepSeconds, command.Dive);
        }

        private void StepWalker(int i, Vector2 input, bool jump, float dt, bool dive = false)
        {
            if (Crew[i].Doing != MiniGameKind.None) { input = Vector2.zero; jump = false; }       // busy with a skill game: standing still
            var state = new CrewMotionState
            {
                Position = Crew[i].Position, Velocity = Crew[i].Velocity, JumpHeight = Crew[i].JumpHeight, VerticalSpeed = Crew[i].VerticalSpeed,
                OnShore = Crew[i].OnShore
            };
            Vector2 previousPosition = state.Position;
            // Teleports (ladders, respawn) must not carry the old momentum into the new place.
            if ((state.Position - lastWalkPosition[i]).sqrMagnitude > .0004f) state.Velocity = Vector2.zero;
            bool gate = GateIsOpenFor(i);
            ILocomotionWorld world = Crew[i].Swimming ? WaterWorldFor(DockedIsland(State))
                : gangwayWorlds[i].Use(Crew[i].OnShore ? ShoreWorldFor(DockedIsland(State)) : DeckWorldInstance, gate, Crew[i].OnShore);
            CrewLocomotion.Step(ref state, input, jump && !Crew[i].Swimming, CrewKinematics.WalkSpeed * WalkSpeedFactor(i), dt, world);
            // Walking the gangway: halfway along the plank the crew member changes area without a button or a jump.
            if (gate) GangwayPassage.Handover(ref state);
            Crew[i].Position = state.Position; Crew[i].Velocity = state.Velocity;
            Crew[i].JumpHeight = state.JumpHeight; Crew[i].VerticalSpeed = state.VerticalSpeed;
            Crew[i].OnShore = state.OnShore;
            lastWalkPosition[i] = state.Position;
            if (Crew[i].Swimming) StepDive(i, dive, dt);
            if (!(gate && GangwayPassage.InLane(Crew[i].Position, Crew[i].OnShore))) TickShoreline(i, previousPosition);
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
            private readonly int island;
            public ShoreLocomotionWorld(int island) { this.island = island; }
            public Vector2 Min => IslandTerrain.For(island).Bounds.min;
            public Vector2 Max => IslandTerrain.For(island).Bounds.max;
            public bool Walkable(Vector2 position) => ShoreStandable(island, position);
        }
    }
}
