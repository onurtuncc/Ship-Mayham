using System;
using UnityEngine;

namespace ShipMayham
{
    [Serializable]
    public struct CareerProgress
    {
        public int Version;
        public int Wallet;
        public int Reputation;
        public int EngineLevel;
        public int CapacityLevel;
        public int ComfortLevel;
        public int DockJobsCompleted;
        public ShipLivery Livery;
        public ShipEquipment Equipment;
        public int OwnedEquipment;
        public bool OwnedPennant, PennantVisible;

        public static CareerProgress Capture(VoyageState state) => new CareerProgress
        {
            Version = 2,
            Wallet = Mathf.Max(0, state.Wallet),
            Reputation = Mathf.Max(0, state.Reputation),
            EngineLevel = Mathf.Clamp(state.EngineLevel, 0, 2),
            CapacityLevel = Mathf.Clamp(state.CapacityLevel, 0, 2),
            ComfortLevel = Mathf.Clamp(state.ComfortLevel, 0, 1),
            DockJobsCompleted = Mathf.Max(0, state.DockJobsCompleted),
            Livery = state.Livery <= ShipLivery.Sunset ? state.Livery : ShipLivery.Classic,
            Equipment = state.Equipment <= ShipEquipment.Workshop ? state.Equipment : ShipEquipment.Standard,
            OwnedEquipment = state.OwnedEquipment & 6,
            OwnedPennant = state.OwnedPennant, PennantVisible = state.OwnedPennant && state.PennantVisible
        };

        public void Apply(ref VoyageState state)
        {
            if (Version != 1 && Version != 2) return;
            state.Wallet = Mathf.Max(0, Wallet);
            state.Reputation = Mathf.Max(0, Reputation);
            state.EngineLevel = Mathf.Clamp(EngineLevel, 0, 2);
            state.CapacityLevel = Mathf.Clamp(CapacityLevel, 0, 2);
            state.ComfortLevel = Mathf.Clamp(ComfortLevel, 0, 1);
            state.DockJobsCompleted = Mathf.Max(0, DockJobsCompleted);
            state.Livery = Livery <= ShipLivery.Sunset && state.Reputation >= VoyageSimulation.LiveryReputation(Livery)
                ? Livery : ShipLivery.Classic;
            state.OwnedEquipment = Version == 2 ? OwnedEquipment & 6 : 0;
            state.Equipment = Equipment <= ShipEquipment.Workshop && (state.OwnedEquipment & (1 << (int)Equipment)) != 0 ? Equipment : ShipEquipment.Standard;
            state.OwnedPennant = Version == 2 && OwnedPennant; state.PennantVisible = state.OwnedPennant && PennantVisible;
        }
    }

    /// <summary>Small host-side career save. Runtime voyage state remains in the migration snapshot.</summary>
    public static class CareerProgressStore
    {
        internal const string PlayerPrefsKey = "ship-mayham.career.v1";
        private static string lastSavedJson;

        public static void Load(ref VoyageState state)
        {
            if (Application.isBatchMode) return;
            if (!PlayerPrefs.HasKey(PlayerPrefsKey)) return;
            if (!TryDecode(PlayerPrefs.GetString(PlayerPrefsKey), out CareerProgress progress)) return;
            progress.Apply(ref state);
            lastSavedJson = JsonUtility.ToJson(progress);
        }

        public static void SaveIfChanged(VoyageState state)
        {
            if (Application.isBatchMode) return;
            string json = JsonUtility.ToJson(CareerProgress.Capture(state));
            if (json == lastSavedJson) return;
            PlayerPrefs.SetString(PlayerPrefsKey, json);
            PlayerPrefs.Save();
            lastSavedJson = json;
        }

        public static bool TryDecode(string json, out CareerProgress progress)
        {
            progress = default;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                progress = JsonUtility.FromJson<CareerProgress>(json);
                return progress.Version == 1 || progress.Version == 2;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
