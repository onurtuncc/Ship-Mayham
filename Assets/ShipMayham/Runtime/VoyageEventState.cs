using System;
using Unity.Netcode;

namespace ShipMayham
{
    public enum VoyageEventKind : byte { None, ContrabandInspection, GalleyRush, LooseCargo, VipService, TicketAudit, FuseOverload, EngineOverheat, SeatShuffle }
    public enum VoyageEventPhase : byte { Waiting, Active, Resolved, Failed }

    [Serializable]
    public struct VoyageEventState : INetworkSerializable, IEquatable<VoyageEventState>
    {
        public VoyageEventKind Kind;
        public VoyageEventPhase Phase;
        public int Serial, TargetPassenger, Progress, Goal, ResolvedCount, FailedCount, Reward;
        public float Remaining, NextIn;
        public int InspectedMask;
        public float CoolingTime;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind); serializer.SerializeValue(ref Phase);
            serializer.SerializeValue(ref Serial); serializer.SerializeValue(ref TargetPassenger);
            serializer.SerializeValue(ref Progress); serializer.SerializeValue(ref Goal);
            serializer.SerializeValue(ref ResolvedCount); serializer.SerializeValue(ref FailedCount);
            serializer.SerializeValue(ref Reward); serializer.SerializeValue(ref Remaining); serializer.SerializeValue(ref NextIn);
            serializer.SerializeValue(ref InspectedMask); serializer.SerializeValue(ref CoolingTime);
        }

        public bool Equals(VoyageEventState other) => ValueType.Equals(this, other);
    }
}
