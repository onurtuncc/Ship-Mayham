using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>One sampled movement input, valid for exactly <see cref="CrewLocomotion.StepSeconds"/> of game time.</summary>
    public struct MoveCommand : IEquatable<MoveCommand>
    {
        public uint Seq;
        public sbyte X, Y;
        public bool Jump;
        /// <summary>Held while swimming: sink instead of floating up.</summary>
        public bool Dive;

        public Vector2 Input => new Vector2(X, Y) / 127f;

        public static MoveCommand Create(uint seq, Vector2 input, bool jump, bool dive = false)
        {
            input = Vector2.ClampMagnitude(input, 1);
            return new MoveCommand { Seq = seq, X = (sbyte)Mathf.RoundToInt(input.x * 127f), Y = (sbyte)Mathf.RoundToInt(input.y * 127f), Jump = jump, Dive = dive };
        }

        public bool Equals(MoveCommand other) => Seq == other.Seq && X == other.X && Y == other.Y && Jump == other.Jump && Dive == other.Dive;
    }

    /// <summary>
    /// The most recent commands in one unreliable datagram. Every packet repeats the last few commands, so a lost packet costs
    /// nothing as long as one of the next ones arrives: no retransmission, no head-of-line blocking.
    /// </summary>
    public struct MoveCommandPacket : INetworkSerializable
    {
        public const int MaxCommands = 6;

        public uint LastSeq;
        public byte[] Data;       // 3 bytes per command, oldest first: x, y, flags

        public int Count => Data == null ? 0 : Data.Length / 3;

        public static MoveCommandPacket Pack(System.Collections.Generic.IReadOnlyList<MoveCommand> recent)
        {
            int count = Mathf.Min(recent.Count, MaxCommands);
            var packet = new MoveCommandPacket { Data = new byte[count * 3] };
            for (int i = 0; i < count; i++)
            {
                var command = recent[recent.Count - count + i];
                packet.Data[i * 3] = (byte)command.X;
                packet.Data[i * 3 + 1] = (byte)command.Y;
                packet.Data[i * 3 + 2] = (byte)((command.Jump ? 1 : 0) | (command.Dive ? 2 : 0));
                packet.LastSeq = command.Seq;
            }
            return packet;
        }

        /// <summary>Command k of the packet (0 = oldest); sequence numbers are consecutive and end at LastSeq.</summary>
        public MoveCommand At(int k)
        {
            int count = Count;
            return new MoveCommand
            {
                Seq = LastSeq - (uint)(count - 1 - k),
                X = (sbyte)Data[k * 3], Y = (sbyte)Data[k * 3 + 1], Jump = (Data[k * 3 + 2] & 1) != 0, Dive = (Data[k * 3 + 2] & 2) != 0
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref LastSeq);
            serializer.SerializeValue(ref Data);
        }
    }
}
