using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// All client-side movement netcode with no engine dependencies: command generation and prediction for the local walker,
    /// reconciliation against server acknowledgements, snapshot buffering and interpolation for everyone else, clock estimation,
    /// and link statistics. PrototypeClientMotion is a thin Unity wrapper; the network simulator tests drive this class directly.
    /// </summary>
    public sealed class ClientMotionCore
    {
        public readonly SnapshotClock Clock = new SnapshotClock();
        public readonly LocalCrewPredictor Predictor = new LocalCrewPredictor();
        private readonly SnapshotBuffer shipBuffer = new SnapshotBuffer { MaxExtrapolation = .3f, TeleportDistance = 30f };
        private readonly SnapshotBuffer[] crewBuffers = new SnapshotBuffer[VoyageSimulation.MaxCrew];
        private readonly bool[] crewOnShore = new bool[VoyageSimulation.MaxCrew];
        private readonly List<MoveCommand> recent = new List<MoveCommand>(MoveCommandPacket.MaxCommands + 1);

        private bool predicting, hasServerState, hasSeq;
        private CrewMotionState lastServerState;
        private uint lastServerAck;
        private ushort lastSeq;
        private int received, missed;
        private float lastSnapshotTime;
        private readonly Vector3[] remoteFollow = new Vector3[VoyageSimulation.MaxCrew];
        private readonly float[] remoteFollowTime = new float[VoyageSimulation.MaxCrew];
        private readonly bool[] remoteFollowing = new bool[VoyageSimulation.MaxCrew];
        private Vector3 shipFollow; private float shipFollowTime; private bool shipFollowing;

        public int LocalSlot = -1;
        public float RttMs { get; private set; }
        public float LossPercent => received + missed == 0 ? 0 : 100f * missed / (received + missed);
        public float SecondsSinceSnapshot(float now) => Clock.HasData ? now - lastSnapshotTime : 0;
        public bool Predicting => predicting && Predictor.Initialized;
        public int SnapshotsReceived => received;
        /// <summary>Server time of the newest snapshot held for a crew slot (diagnostics and tests).</summary>
        public double NewestSampleTime(int slot) => slot >= 0 && slot < crewBuffers.Length ? crewBuffers[slot].NewestTime : double.NegativeInfinity;

        public ClientMotionCore()
        {
            for (int i = 0; i < crewBuffers.Length; i++) crewBuffers[i] = new SnapshotBuffer { MaxExtrapolation = .25f, TeleportDistance = 3f };
        }

        public void Reset()
        {
            Clock.Reset(); shipBuffer.Clear();
            foreach (var buffer in crewBuffers) buffer.Clear();
            Predictor.Resync(default, 0);
            recent.Clear();
            hasServerState = predicting = hasSeq = shipFollowing = false;
            System.Array.Clear(remoteFollowing, 0, remoteFollowing.Length);
            received = missed = 0;
        }

        /// <summary>Switches prediction on or off (off at stations, on the host); turning it on adopts the latest server state.</summary>
        public void SetPredicting(bool enabled)
        {
            if (enabled && !predicting && hasServerState) Predictor.Resync(lastServerState, lastServerAck);
            predicting = enabled;
        }

        // ---------------------------------------------------------------- outgoing

        /// <summary>One fixed step: builds this step's command (predicting it when allowed) and returns the packet to send.</summary>
        public MoveCommandPacket StepFixed(Vector2 input, bool jump, float speedFactor, ILocomotionWorld world)
        {
            MoveCommand command = Predicting ? Predictor.Step(input, jump, speedFactor, world) : Predictor.Issue(input, jump);
            recent.Add(command);
            if (recent.Count > MoveCommandPacket.MaxCommands) recent.RemoveAt(0);
            return MoveCommandPacket.Pack(recent);
        }

        // ---------------------------------------------------------------- incoming

        public void OnSnapshot(MotionSnapshot snapshot, float now)
        {
            Clock.OnSnapshot(now, snapshot.TimeMs);
            lastSnapshotTime = now;
            CountLoss(snapshot.Seq);
            double time = snapshot.TimeMs / 1000.0;
            float heading = snapshot.ShipHeading * Mathf.Deg2Rad;
            shipBuffer.Add(new MotionSample
            {
                Time = time, Position = new Vector3(snapshot.ShipPosition.x, 0, snapshot.ShipPosition.y), Angle = snapshot.ShipHeading,
                Velocity = new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading)) * snapshot.ShipSpeed
            });
            if (snapshot.Crew == null) return;
            foreach (var packet in snapshot.Crew)
            {
                int slot = packet.Slot;
                if (slot < 0 || slot >= crewBuffers.Length) continue;
                if (crewOnShore[slot] != packet.OnShore) { crewBuffers[slot].Clear(); crewOnShore[slot] = packet.OnShore; }
                crewBuffers[slot].Add(new MotionSample
                {
                    Time = time, Position = new Vector3(packet.Position.x, packet.JumpHeight, packet.Position.y),
                    Velocity = new Vector3(packet.Velocity.x, packet.VerticalSpeed, packet.Velocity.y)
                });
                if (slot != LocalSlot) continue;
                lastServerState = new CrewMotionState { Position = packet.Position, Velocity = packet.Velocity, JumpHeight = packet.JumpHeight, VerticalSpeed = packet.VerticalSpeed };
                lastServerAck = packet.InputAck; hasServerState = true;
                if (predicting) Predictor.Reconcile(lastServerState, lastServerAck, time);
                else Predictor.Resync(lastServerState, lastServerAck);
            }
        }

        public void OnPong(uint echoedMs, float now)
        {
            float rtt = (uint)(now * 1000f) - echoedMs;
            if (rtt < 0 || rtt > 10000) return;
            RttMs = RttMs <= 0 ? rtt : Mathf.Lerp(RttMs, rtt, .25f);
        }

        private void CountLoss(ushort seq)
        {
            if (hasSeq)
            {
                int gap = (ushort)(seq - lastSeq);
                if (gap == 0 || gap > 32768) return;                  // duplicate or reordered older datagram
                if (gap > 1) missed += gap - 1;
            }
            hasSeq = true; lastSeq = seq; received++;
            if (received + missed > 200) { received /= 2; missed /= 2; }
        }

        public void Decay(float dt) => Predictor.Decay(dt);

        // ---------------------------------------------------------------- what to draw

        /// <summary>
        /// Crew position (x, jump height, z) in the frame they walk in. The local walker is predicted, everyone else interpolated;
        /// <paramref name="authoritativeLocal"/> (the host's own crew) opts out because the host already has the truth.
        /// </summary>
        public bool TryGetCrew(int slot, float now, bool authoritativeLocal, out Vector3 position, out Vector3 velocity)
        {
            position = velocity = Vector3.zero;
            if (slot < 0 || slot >= crewBuffers.Length || !Clock.HasData) return false;
            if (slot == LocalSlot && predicting)
            {
                if (!Predictor.Initialized) return false;
                position = new Vector3(Predictor.RenderPosition.x, Predictor.RenderJump, Predictor.RenderPosition.y);
                velocity = new Vector3(Predictor.State.Velocity.x, Predictor.State.VerticalSpeed, Predictor.State.Velocity.y);
                return true;
            }
            if (slot == LocalSlot && authoritativeLocal) return false;
            if (!crewBuffers[slot].Sample(Clock.RenderTime(now), out Vector3 target, out velocity, out _)) return false;
            position = Follow(target, velocity, now, ref remoteFollow[slot], ref remoteFollowTime[slot], ref remoteFollowing[slot], 3f);
            return true;
        }

        // Interpolated motion is exact while data flows. After a stall or a burst the sampled target can leap ahead; instead of
        // popping, the drawn position closes the gap at a limited speed (a little faster than the entity itself) and only
        // truly large jumps (teleports) are taken at once.
        private static Vector3 Follow(Vector3 target, Vector3 velocity, float now, ref Vector3 drawn, ref float lastTime, ref bool following, float teleportDistance)
        {
            float dt = following ? Mathf.Clamp(now - lastTime, 0, .1f) : 0;
            lastTime = now;
            Vector3 gap = target - drawn;
            float distance = gap.magnitude;
            if (!following || distance > teleportDistance) { drawn = target; following = true; return drawn; }
            float reach = (new Vector2(velocity.x, velocity.z).magnitude * 1.6f + 4f) * dt;
            drawn = distance <= reach ? target : drawn + gap / distance * reach;
            return drawn;
        }

        public bool TryGetShip(float now, out Vector3 position, out float heading, out Vector3 velocity, out float turnRate)
        {
            position = velocity = Vector3.zero; heading = turnRate = 0;
            if (!Clock.HasData) return false;
            double t = Clock.RenderTime(now);
            if (!shipBuffer.Sample(t, out Vector3 target, out velocity, out heading)) return false;
            shipBuffer.Sample(t - .1, out _, out _, out float earlier);
            turnRate = Mathf.DeltaAngle(earlier, heading) / .1f;
            position = Follow(target, velocity, now, ref shipFollow, ref shipFollowTime, ref shipFollowing, 30f);
            return true;
        }
    }
}
