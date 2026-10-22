using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    public enum VoyagePhase : byte { Boarding, Sailing, Arrived, Failed }
    public enum ServiceItem : byte { None, Food, Drink, Cargo, Ammo, LuggageA, LuggageB, LuggageC }
    public enum StationKind : byte { Manifest, Helm, Food, Drink, Repair, EngineUpgrade, CapacityUpgrade, ComfortUpgrade, Cannon, Power, Ammo }
    public enum PiratePhase : byte { Waiting, Warning, Attacking, Defeated, Escaped }
    public enum PassengerMotion : byte { OnDeck, Carried, Airborne, Rescuing }
    public enum PassengerAction : byte { CheckTicket, CarryOrDrop, Throw }
    public enum PassengerReaction : byte { None, Thanks, TicketDue, FareCollected, Complaint, Rescued }
    public enum HarborAction : byte { GoAshore, BoardShip, BuyFood, BuyDrink, MaintainHull }
    public enum RouteProfile : byte { CoastalFerry, MarketRun, StormPassage }
    public enum ShipLivery : byte { Classic, Seafoam, Sunset }
    public enum DockJobKind : byte { CargoRelay, LuggageSort }

    [Serializable]
    public struct VoyageState : INetworkSerializable, IEquatable<VoyageState>
    {
        public VoyagePhase Phase;
        public Vector2 Position;
        public float Heading, Speed, Health, Elapsed;
        public float HullShield;
        public RouteProfile Route;
        public ShipLivery Livery;
        public bool DockJobActive;
        public DockJobKind DockJobType;
        public int DockJobDeliveredMask;
        public int Wallet, Voyage, Boarded, Served, FoodStock, DrinkStock, Leak, RepairSteps;
        public int DockJobProgress, DockJobPortKey, DockJobsCompleted;
        public int EngineLevel, CapacityLevel, ComfortLevel;
        public int Reputation, LastReputation;
        public int LastTickets, LastServices, LastTips, LastCosts, LastNet, LastBounty, LastEventBonus, LastRouteBonus;
        public int CheckedTickets, CollectedFares, RescueCount, LastUnpaid, LastMissing, LastRescueCosts;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Phase); s.SerializeValue(ref Position);
            s.SerializeValue(ref Heading); s.SerializeValue(ref Speed); s.SerializeValue(ref Health); s.SerializeValue(ref Elapsed);
            s.SerializeValue(ref HullShield);
            s.SerializeValue(ref Route);
            s.SerializeValue(ref Livery);
            s.SerializeValue(ref DockJobActive);
            s.SerializeValue(ref DockJobType); s.SerializeValue(ref DockJobDeliveredMask);
            s.SerializeValue(ref Wallet); s.SerializeValue(ref Voyage); s.SerializeValue(ref Boarded); s.SerializeValue(ref Served);
            s.SerializeValue(ref DockJobProgress); s.SerializeValue(ref DockJobPortKey); s.SerializeValue(ref DockJobsCompleted);
            s.SerializeValue(ref FoodStock); s.SerializeValue(ref DrinkStock); s.SerializeValue(ref Leak); s.SerializeValue(ref RepairSteps);
            s.SerializeValue(ref EngineLevel); s.SerializeValue(ref CapacityLevel); s.SerializeValue(ref ComfortLevel);
            s.SerializeValue(ref Reputation); s.SerializeValue(ref LastReputation);
            s.SerializeValue(ref LastTickets); s.SerializeValue(ref LastServices); s.SerializeValue(ref LastTips);
            s.SerializeValue(ref LastCosts); s.SerializeValue(ref LastNet);
            s.SerializeValue(ref LastBounty);
            s.SerializeValue(ref LastEventBonus);
            s.SerializeValue(ref LastRouteBonus);
            s.SerializeValue(ref CheckedTickets); s.SerializeValue(ref CollectedFares); s.SerializeValue(ref RescueCount);
            s.SerializeValue(ref LastUnpaid); s.SerializeValue(ref LastMissing); s.SerializeValue(ref LastRescueCosts);
        }
        public bool Equals(VoyageState other) => ValueType.Equals(this, other);
    }

    [Serializable]
    public struct CrewState : INetworkSerializable, IEquatable<CrewState>
    {
        public bool Active, AtHelm, AtCannon, OnShore;
        public ulong ClientId;
        public Vector2 Position;
        public ServiceItem Carrying;
        public float JumpHeight, VerticalSpeed;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Active); s.SerializeValue(ref AtHelm); s.SerializeValue(ref ClientId);
            s.SerializeValue(ref Position); s.SerializeValue(ref Carrying);
            s.SerializeValue(ref AtCannon);
            s.SerializeValue(ref OnShore);
            s.SerializeValue(ref JumpHeight); s.SerializeValue(ref VerticalSpeed);
        }
        public bool Equals(CrewState other) => Active == other.Active && AtHelm == other.AtHelm &&
            ClientId == other.ClientId && Position.Equals(other.Position) && Carrying == other.Carrying && AtCannon == other.AtCannon &&
            JumpHeight.Equals(other.JumpHeight) && VerticalSpeed.Equals(other.VerticalSpeed) && OnShore == other.OnShore;
    }

    [Serializable]
    public struct PassengerState : INetworkSerializable, IEquatable<PassengerState>
    {
        public bool Active;
        public ServiceItem Request;
        public float Satisfaction, RequestTimer;
        public bool TicketChecked, FarePaid;
        public PassengerMotion Motion;
        public int CarrierSlot;
        public Vector3 Position, Velocity;
        public float RecoveryRemaining, ReactionRemaining;
        public PassengerReaction Reaction;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Active); s.SerializeValue(ref Request);
            s.SerializeValue(ref Satisfaction); s.SerializeValue(ref RequestTimer);
            s.SerializeValue(ref TicketChecked); s.SerializeValue(ref FarePaid); s.SerializeValue(ref Motion); s.SerializeValue(ref CarrierSlot);
            s.SerializeValue(ref Position); s.SerializeValue(ref Velocity); s.SerializeValue(ref RecoveryRemaining);
            s.SerializeValue(ref Reaction); s.SerializeValue(ref ReactionRemaining);
        }
        public bool Equals(PassengerState other) => ValueType.Equals(this, other);
    }

    [Serializable]
    public struct StationDefinition
    {
        public StationKind Kind;
        public Vector2 Position;
        public StationDefinition(StationKind kind, Vector2 position) { Kind = kind; Position = position; }
    }
}
