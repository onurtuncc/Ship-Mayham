using System;
using System.Text;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    internal sealed class VoyageMigrationSnapshot
    {
        public int Version = 24;
        public VoyageJourneyState Journey;
        public VoyageOpportunityState Opportunity;
        public ShipEmergencyState Emergency;
        public DeckConditionState Deck;
        public HazardState Hazards;

        public VoyageState State;
        public EncounterState Encounter;
        public VoyageEventState Event;
        public CrewState[] Crew;
        public PassengerState[] Passengers;
        public string[] CrewPlayerIds;
        public string[] CrewNames;
        public bool[] CrewReady;
        public CrewTally[] CrewTallies;
        public float[] HelmClock;
        public float Clock;
        public float NextLeak;
        public float PirateAt, PowerFaultAt;
        public bool WorkloadReady;
        public bool ScheduledPowerFault;
        public uint EventRandomState, RemainingEvents;
        public float[] PassengerCarryYaw;
    }

    public sealed class VoyageMigrationDataHandler : IMigrationDataHandler
    {
        private readonly Func<VoyageSimulation> simulation;
        private static byte[] pendingSnapshot;

        public VoyageMigrationDataHandler(Func<VoyageSimulation> simulation)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
        }

        public byte[] Generate()
        {
            VoyageSimulation current = simulation();
            return current == null ? Array.Empty<byte>() : current.ExportMigrationData();
        }

        public void Apply(byte[] migrationData)
        {
            pendingSnapshot = migrationData == null ? null : (byte[])migrationData.Clone();
        }

        /// <summary>Queues a snapshot for the next simulation, exactly like a host migration does (also used to continue a saved world).</summary>
        public static void Stage(byte[] snapshot) => pendingSnapshot = snapshot == null ? null : (byte[])snapshot.Clone();

        public static bool RestorePending(VoyageSimulation target)
        {
            if (target == null || pendingSnapshot == null || pendingSnapshot.Length == 0) return false;
            byte[] snapshot = pendingSnapshot;
            pendingSnapshot = null;
            target.ImportMigrationData(snapshot);
            return true;
        }
    }

    public sealed partial class VoyageSimulation
    {
        public byte[] ExportMigrationData()
        {
            var snapshot = new VoyageMigrationSnapshot
            {
                State = State,
                Encounter = Encounter,
                Event = Event,
                Hazards = Hazards,
                Deck = Deck,
                Emergency = Emergency,
                Opportunity = Opportunity,
                Journey = Journey,
                Crew = (CrewState[])Crew.Clone(),
                Passengers = (PassengerState[])Passengers.Clone(),
                CrewPlayerIds = (string[])crewPlayerIds.Clone(),
                CrewNames = (string[])crewNames.Clone(),
                CrewReady = (bool[])crewReady.Clone(),
                CrewTallies = (CrewTally[])tallies.Clone(),
                HelmClock = (float[])helmClock.Clone(),
                Clock = clock,
                NextLeak = nextLeak,
                PirateAt = pirateAt,
                PowerFaultAt = powerFaultAt,
                WorkloadReady = workloadReady,
                ScheduledPowerFault = scheduledPowerFault,
                EventRandomState = eventRandomState,
                RemainingEvents = remainingEvents,
                PassengerCarryYaw = (float[])passengerCarryYaw.Clone()
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
        }

        public void ImportMigrationData(byte[] data)
        {
            if (data == null || data.Length == 0) throw new ArgumentException("Migration data is empty.", nameof(data));
            VoyageMigrationSnapshot snapshot = JsonUtility.FromJson<VoyageMigrationSnapshot>(Encoding.UTF8.GetString(data));
            if (snapshot == null || snapshot.Version != 24 || snapshot.Crew?.Length != MaxCrew ||
                snapshot.Passengers?.Length != MaxPassengers || snapshot.CrewPlayerIds?.Length != MaxCrew)
                throw new InvalidOperationException("Unsupported or incomplete voyage migration snapshot.");

            State = snapshot.State;
            Encounter = snapshot.Encounter;
            Event = snapshot.Event;
            Hazards = snapshot.Hazards;
            Deck = snapshot.Deck;
            Emergency = snapshot.Emergency;
            Opportunity = snapshot.Opportunity;
            Journey = snapshot.Journey;
            Array.Copy(snapshot.Crew, Crew, MaxCrew);
            Array.Copy(snapshot.Passengers, Passengers, MaxPassengers);
            Array.Copy(snapshot.CrewPlayerIds, crewPlayerIds, MaxCrew);
            Array.Clear(crewNames, 0, MaxCrew);
            Array.Clear(crewReady, 0, MaxCrew);
            if (snapshot.CrewNames?.Length == MaxCrew) Array.Copy(snapshot.CrewNames, crewNames, MaxCrew);
            if (snapshot.CrewReady?.Length == MaxCrew) Array.Copy(snapshot.CrewReady, crewReady, MaxCrew);
            ResetTallies();
            if (snapshot.CrewTallies?.Length == MaxCrew) Array.Copy(snapshot.CrewTallies, tallies, MaxCrew);
            if (snapshot.HelmClock?.Length == MaxCrew) Array.Copy(snapshot.HelmClock, helmClock, MaxCrew);
            // NGO assigns new connection IDs after migration. Only the stable player IDs
            // may identify the restored slots until each participant reconnects.
            for (int i = 0; i < MaxCrew; i++) if (Crew[i].Active) Crew[i].ClientId = ulong.MaxValue;
            clock = snapshot.Clock;
            nextLeak = snapshot.NextLeak;
            pirateAt = snapshot.PirateAt > 0 ? snapshot.PirateAt : BasePirateArrival;
            powerFaultAt = snapshot.PowerFaultAt > 0 ? snapshot.PowerFaultAt : BasePowerFault;
            workloadReady = snapshot.WorkloadReady;
            scheduledPowerFault = snapshot.ScheduledPowerFault;
            eventRandomState = snapshot.EventRandomState == 0 ? 1u : snapshot.EventRandomState;
            remainingEvents = snapshot.RemainingEvents;
            RebuildHazardsAfterImport();
            if (snapshot.PassengerCarryYaw?.Length == MaxCrew)
                Array.Copy(snapshot.PassengerCarryYaw, passengerCarryYaw, MaxCrew);

            Array.Clear(inputs, 0, inputs.Length);
            Array.Clear(inputExpires, 0, inputExpires.Length);
            Array.Clear(interactionReady, 0, interactionReady.Length);
        }
    }
}
