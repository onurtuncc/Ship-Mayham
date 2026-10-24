using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    public enum VoyagePhase : byte { Boarding, Sailing, Arrived, Failed }
    /// <summary>Online voyages start in the lobby; Voyage is the default so solo play and old states are unchanged.</summary>
    public enum SessionPhase : byte { Voyage, Lobby }
    public enum ServiceItem : byte { None, Food, Drink, Cargo, Ammo, LuggageA, LuggageB, LuggageC, RepairKit, SalvageA, SalvageB, SalvageC, CraneCrate, Extinguisher, TemporaryPlug }
    public enum StationKind : byte { Manifest, Helm, Food, Drink, Repair, EngineUpgrade, CapacityUpgrade, ComfortUpgrade, Cannon, Power, Ammo, RepairSupply }
    public enum PiratePhase : byte { Waiting, Warning, Attacking, Defeated, Escaped }
    /// <summary>Landing (lying after a throw), Dizzy, Standing (just put down) and Returning (walking to a seat) are the transitions between being handled and sitting again.</summary>
    public enum PassengerMotion : byte { OnDeck, Carried, Airborne, Rescuing, Landing, Dizzy, Standing, Returning, Boarding, Leaving }
    public enum PassengerAction : byte { CheckTicket, CarryOrDrop, Throw }
    public enum PassengerReaction : byte { None, Thanks, TicketDue, FareCollected, Complaint, Rescued }
    public enum PassengerPersonality : byte { Patient, Impatient, Nervous }
    public enum PassengerRoutine : byte { Seated, Strolling, Watching, Returning, Working }
    public enum HarborAction : byte { GoAshore, BoardShip, BuyFood, BuyDrink, MaintainHull, HarborMaster, PaintShop, FishMarket, CraneLoadingPad, CraneControls }
    public enum RouteProfile : byte { CoastalFerry, MarketRun, StormPassage }
    public enum ShipLivery : byte { Classic, Seafoam, Sunset }
    public enum DockJobKind : byte { CargoRelay, LuggageSort, SalvageRun, CraneLoading }

    [Serializable]
    public struct VoyageState : INetworkSerializable, IEquatable<VoyageState>
    {
        public VoyagePhase Phase;
        public SessionPhase SessionPhase;
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
        public int RepairKitStock, ReadyRepairKits, RepairKitsUsed;
        public Vector2 CraneHook;
        public bool CraneHolding;
        public int CraneStock, CraneReady;
        public float CraneSwayTime;
        public int EngineLevel, CapacityLevel, ComfortLevel;
        public ShipEquipment Equipment;
        public int OwnedEquipment;
        public bool OwnedPennant, PennantVisible;
        /// <summary>Server clock in milliseconds when this state was produced; ignored by equality so it never forces a send.</summary>
        public uint TimeMs;
        public int Reputation, LastReputation;
        public int LastTickets, LastServices, LastTips, LastCosts, LastNet, LastBounty, LastEventBonus, LastRouteBonus;
        public int CheckedTickets, CollectedFares, RescueCount, LastUnpaid, LastMissing, LastRescueCosts;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Phase); s.SerializeValue(ref SessionPhase); s.SerializeValue(ref Position);
            s.SerializeValue(ref Heading); s.SerializeValue(ref Speed); s.SerializeValue(ref Health); s.SerializeValue(ref Elapsed);
            s.SerializeValue(ref HullShield);
            s.SerializeValue(ref Route);
            s.SerializeValue(ref Livery);
            s.SerializeValue(ref DockJobActive);
            s.SerializeValue(ref DockJobType); s.SerializeValue(ref DockJobDeliveredMask);
            s.SerializeValue(ref Wallet); s.SerializeValue(ref Voyage); s.SerializeValue(ref Boarded); s.SerializeValue(ref Served);
            s.SerializeValue(ref DockJobProgress); s.SerializeValue(ref DockJobPortKey); s.SerializeValue(ref DockJobsCompleted);
            s.SerializeValue(ref FoodStock); s.SerializeValue(ref DrinkStock); s.SerializeValue(ref Leak); s.SerializeValue(ref RepairSteps);
            s.SerializeValue(ref RepairKitStock); s.SerializeValue(ref ReadyRepairKits); s.SerializeValue(ref RepairKitsUsed);
            s.SerializeValue(ref CraneHook); s.SerializeValue(ref CraneHolding);
            s.SerializeValue(ref CraneStock); s.SerializeValue(ref CraneReady); s.SerializeValue(ref CraneSwayTime);
            s.SerializeValue(ref EngineLevel); s.SerializeValue(ref CapacityLevel); s.SerializeValue(ref ComfortLevel);
            s.SerializeValue(ref Equipment); s.SerializeValue(ref OwnedEquipment); s.SerializeValue(ref OwnedPennant); s.SerializeValue(ref PennantVisible);
            s.SerializeValue(ref TimeMs);
            s.SerializeValue(ref Reputation); s.SerializeValue(ref LastReputation);
            s.SerializeValue(ref LastTickets); s.SerializeValue(ref LastServices); s.SerializeValue(ref LastTips);
            s.SerializeValue(ref LastCosts); s.SerializeValue(ref LastNet);
            s.SerializeValue(ref LastBounty);
            s.SerializeValue(ref LastEventBonus);
            s.SerializeValue(ref LastRouteBonus);
            s.SerializeValue(ref CheckedTickets); s.SerializeValue(ref CollectedFares); s.SerializeValue(ref RescueCount);
            s.SerializeValue(ref LastUnpaid); s.SerializeValue(ref LastMissing); s.SerializeValue(ref LastRescueCosts);
        }
        public bool Equals(VoyageState other) { other.TimeMs = TimeMs; return ValueType.Equals(this, other); }
    }

    [Serializable]
    public struct CrewState : INetworkSerializable, IEquatable<CrewState>
    {
        public bool Active, AtHelm, AtCannon, OnShore, AtCrane;
        public ulong ClientId;
        public Vector2 Position;
        public ServiceItem Carrying;
        public byte ServiceUnits;
        public float JumpHeight, VerticalSpeed;
        /// <summary>Walking velocity, so a predicting client can reconcile to the exact server state.</summary>
        public Vector2 Velocity;
        /// <summary>Sequence number of the last move command the server executed for this crew member.</summary>
        public uint InputAck;
        /// <summary>Server clock in milliseconds; ignored by equality so it never forces a send.</summary>
        public uint TimeMs;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Active); s.SerializeValue(ref AtHelm); s.SerializeValue(ref ClientId);
            s.SerializeValue(ref Position); s.SerializeValue(ref Carrying);
            s.SerializeValue(ref ServiceUnits);
            s.SerializeValue(ref AtCannon);
            s.SerializeValue(ref OnShore);
            s.SerializeValue(ref AtCrane);
            s.SerializeValue(ref JumpHeight); s.SerializeValue(ref VerticalSpeed);
            s.SerializeValue(ref Velocity); s.SerializeValue(ref InputAck); s.SerializeValue(ref TimeMs);
        }
        public bool Equals(CrewState other) => Active == other.Active && AtHelm == other.AtHelm &&
            ClientId == other.ClientId && Position.Equals(other.Position) && Carrying == other.Carrying && AtCannon == other.AtCannon &&
            JumpHeight.Equals(other.JumpHeight) && VerticalSpeed.Equals(other.VerticalSpeed) && OnShore == other.OnShore && AtCrane == other.AtCrane &&
            Velocity.Equals(other.Velocity) && InputAck == other.InputAck && ServiceUnits == other.ServiceUnits;
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
        /// <summary>0 = the passenger's own seat (their index); n = seat n-1, claimed after being put down.</summary>
        public byte SeatClaim;
        public Vector3 Position, Velocity;
        public float RecoveryRemaining, ReactionRemaining;
        public PassengerReaction Reaction;
        public PassengerPersonality Personality;
        public PassengerRoutine Routine;
        public Vector2 RoutineTarget;
        public float RoutineTimer, RoamIn, CalmRemaining;
        public float WorkRemaining;
        public bool WorkedFare;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Active); s.SerializeValue(ref Request);
            s.SerializeValue(ref Satisfaction); s.SerializeValue(ref RequestTimer);
            s.SerializeValue(ref TicketChecked); s.SerializeValue(ref FarePaid); s.SerializeValue(ref Motion); s.SerializeValue(ref CarrierSlot);
            s.SerializeValue(ref SeatClaim);
            s.SerializeValue(ref Position); s.SerializeValue(ref Velocity); s.SerializeValue(ref RecoveryRemaining);
            s.SerializeValue(ref Reaction); s.SerializeValue(ref ReactionRemaining);
            s.SerializeValue(ref Personality); s.SerializeValue(ref Routine); s.SerializeValue(ref RoutineTarget);
            s.SerializeValue(ref RoutineTimer); s.SerializeValue(ref RoamIn); s.SerializeValue(ref CalmRemaining);
            s.SerializeValue(ref WorkRemaining); s.SerializeValue(ref WorkedFare);
        }
        public bool Equals(PassengerState other) => ValueType.Equals(this, other);
    }

    /// <summary>What the lobby shows about a crew slot. Names are truncated to what the network string holds.</summary>
    [Serializable]
    public struct CrewProfile : INetworkSerializable, IEquatable<CrewProfile>
    {
        public bool Ready;
        public FixedString64Bytes Name;

        public static CrewProfile From(string name, bool ready)
        {
            var profile = new CrewProfile { Ready = ready };
            if (!string.IsNullOrEmpty(name)) profile.Name.CopyFromTruncated(name);
            return profile;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Ready); s.SerializeValue(ref Name);
        }

        public bool Equals(CrewProfile other) => Ready == other.Ready && Name.Equals(other.Name);
    }

    [Serializable]
    public struct StationDefinition
    {
        public StationKind Kind;
        public Vector2 Position;
        public StationDefinition(StationKind kind, Vector2 position) { Kind = kind; Position = position; }
    }
}
