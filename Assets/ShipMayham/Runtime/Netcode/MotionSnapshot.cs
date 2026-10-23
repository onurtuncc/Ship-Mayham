using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>One crew member inside a motion snapshot.</summary>
    public struct CrewMotionPacket : INetworkSerializable
    {
        public const byte OnShoreFlag = 1;

        public byte Slot, Flags;
        public Vector2 Position, Velocity;
        public float JumpHeight, VerticalSpeed;
        public uint InputAck;

        public bool OnShore => (Flags & OnShoreFlag) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Slot); serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Position); serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref JumpHeight); serializer.SerializeValue(ref VerticalSpeed);
            serializer.SerializeValue(ref InputAck);
        }
    }

    /// <summary>
    /// The motion of everything that moves continuously, sent unreliably at the publish rate: a lost datagram is simply
    /// superseded by the next one, so unlike NetworkVariable/NetworkList updates it can never stall behind a retransmission.
    /// Carries the server clock for interpolation and the per-crew input acknowledgements for prediction.
    /// </summary>
    public struct MotionSnapshot : INetworkSerializable
    {
        public ushort Seq;
        public uint TimeMs;
        public Vector2 ShipPosition;
        public float ShipHeading, ShipSpeed;
        public CrewMotionPacket[] Crew;

        public static MotionSnapshot From(VoyageSimulation sim, ushort seq)
        {
            int active = 0;
            for (int i = 0; i < VoyageSimulation.MaxCrew; i++) if (sim.Crew[i].Active) active++;
            var snapshot = new MotionSnapshot
            {
                Seq = seq, TimeMs = sim.ClockMs, ShipPosition = sim.State.Position, ShipHeading = sim.State.Heading, ShipSpeed = sim.State.Speed,
                Crew = new CrewMotionPacket[active]
            };
            int n = 0;
            for (int i = 0; i < VoyageSimulation.MaxCrew; i++)
            {
                var c = sim.Crew[i];
                if (!c.Active) continue;
                snapshot.Crew[n++] = new CrewMotionPacket
                {
                    Slot = (byte)i, Flags = (byte)(c.OnShore ? CrewMotionPacket.OnShoreFlag : 0), Position = c.Position, Velocity = c.Velocity,
                    JumpHeight = c.JumpHeight, VerticalSpeed = c.VerticalSpeed, InputAck = c.InputAck
                };
            }
            return snapshot;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seq); serializer.SerializeValue(ref TimeMs);
            serializer.SerializeValue(ref ShipPosition); serializer.SerializeValue(ref ShipHeading); serializer.SerializeValue(ref ShipSpeed);
            serializer.SerializeValue(ref Crew);
        }
    }
}
