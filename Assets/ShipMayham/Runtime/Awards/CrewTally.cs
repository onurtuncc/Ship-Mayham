using System;
using Unity.Netcode;

namespace ShipMayham
{
    public enum TallyKind : byte { Throws, Orders, Repairs, Hits, Fares, Deliveries }

    /// <summary>
    /// What one crew member did during the current voyage; the host counts, everyone receives it and the voyage report turns it
    /// into awards. Counting never affects the rules.
    /// </summary>
    [Serializable]
    public struct CrewTally : INetworkSerializable, IEquatable<CrewTally>
    {
        public int Throws, Orders, Repairs, Hits, Fares, Deliveries, HelmSeconds;

        public int Get(TallyKind kind)
        {
            switch (kind)
            {
                case TallyKind.Throws: return Throws;
                case TallyKind.Orders: return Orders;
                case TallyKind.Repairs: return Repairs;
                case TallyKind.Hits: return Hits;
                case TallyKind.Fares: return Fares;
                default: return Deliveries;
            }
        }

        public void Add(TallyKind kind, int amount = 1)
        {
            switch (kind)
            {
                case TallyKind.Throws: Throws += amount; break;
                case TallyKind.Orders: Orders += amount; break;
                case TallyKind.Repairs: Repairs += amount; break;
                case TallyKind.Hits: Hits += amount; break;
                case TallyKind.Fares: Fares += amount; break;
                default: Deliveries += amount; break;
            }
        }

        public bool IsEmpty => Throws == 0 && Orders == 0 && Repairs == 0 && Hits == 0 && Fares == 0 && Deliveries == 0 && HelmSeconds == 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Throws); s.SerializeValue(ref Orders); s.SerializeValue(ref Repairs); s.SerializeValue(ref Hits);
            s.SerializeValue(ref Fares); s.SerializeValue(ref Deliveries); s.SerializeValue(ref HelmSeconds);
        }

        public bool Equals(CrewTally other) =>
            Throws == other.Throws && Orders == other.Orders && Repairs == other.Repairs && Hits == other.Hits &&
            Fares == other.Fares && Deliveries == other.Deliveries && HelmSeconds == other.HelmSeconds;
    }
}
