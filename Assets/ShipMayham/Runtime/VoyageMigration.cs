using System;
using System.Text;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    internal sealed class VoyageMigrationSnapshot
    {
        public int Version = 3;
        public VoyageState State;
        public EncounterState Encounter;
        public VoyageEventState Event;
        public CrewState[] Crew;
        public PassengerState[] Passengers;
        public string[] CrewPlayerIds;
        public float Clock;
        public float NextLeak;
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
                Crew = (CrewState[])Crew.Clone(),
                Passengers = (PassengerState[])Passengers.Clone(),
                CrewPlayerIds = (string[])crewPlayerIds.Clone(),
                Clock = clock,
                NextLeak = nextLeak,
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
            if (snapshot == null || snapshot.Version != 3 || snapshot.Crew?.Length != MaxCrew ||
                snapshot.Passengers?.Length != MaxPassengers || snapshot.CrewPlayerIds?.Length != MaxCrew)
                throw new InvalidOperationException("Unsupported or incomplete voyage migration snapshot.");

            State = snapshot.State;
            Encounter = snapshot.Encounter;
            Event = snapshot.Event;
            Array.Copy(snapshot.Crew, Crew, MaxCrew);
            Array.Copy(snapshot.Passengers, Passengers, MaxPassengers);
            Array.Copy(snapshot.CrewPlayerIds, crewPlayerIds, MaxCrew);
            clock = snapshot.Clock;
            nextLeak = snapshot.NextLeak;
            scheduledPowerFault = snapshot.ScheduledPowerFault;
            eventRandomState = snapshot.EventRandomState == 0 ? 1u : snapshot.EventRandomState;
            remainingEvents = snapshot.RemainingEvents;
            if (snapshot.PassengerCarryYaw?.Length == MaxCrew)
                Array.Copy(snapshot.PassengerCarryYaw, passengerCarryYaw, MaxCrew);

            Array.Clear(inputs, 0, inputs.Length);
            Array.Clear(inputExpires, 0, inputExpires.Length);
            Array.Clear(interactionReady, 0, interactionReady.Length);
        }
    }
}
