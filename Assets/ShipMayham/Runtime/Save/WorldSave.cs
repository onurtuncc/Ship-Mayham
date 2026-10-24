using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>How a saved world is brought back: for one player on this device, or as an online host that friends can rejoin.</summary>
    public enum RestoreMode : byte { None, Solo, Host }

    /// <summary>What the menu shows about the saved world.</summary>
    public struct WorldSaveInfo
    {
        public long SavedUnix;
        public int Voyage, Wallet, Reputation, Crew;
        public VoyagePhase Phase;
        public string Host;

        public static WorldSaveInfo From(in VoyageState state, int crew, string host, long nowUnix) => new WorldSaveInfo
        {
            SavedUnix = nowUnix, Voyage = state.Voyage, Wallet = state.Wallet, Reputation = state.Reputation, Crew = crew, Phase = state.Phase, Host = host
        };
    }

    /// <summary>
    /// The last world a host played, kept on the device so it can be continued and rejoined. The snapshot is the host-migration
    /// snapshot, so everything that survives a host change survives a restart too; a world is only written at port, never at sea.
    /// Storage failures are logged and never stop the game.
    /// </summary>
    public static class WorldSave
    {
        public const int FormatVersion = 1, MaxPayloadBytes = 512 * 1024;
        public const string Key = "ship-mayham.world.v1";

        [Serializable]
        private sealed class Envelope
        {
            public int Version;
            public long SavedUnix;
            public int Voyage, Wallet, Reputation, Crew, Phase;
            public string Host, Data;
        }

        /// <summary>Tests and batch runs must not write the player's real save, unless a test is about saving.</summary>
        public static bool ForceEnabled;

        public static bool Enabled => ForceEnabled || !Application.isBatchMode;

        public static RestoreMode PendingRestore { get; private set; }

        /// <summary>Whether a world in this phase may be saved or restored: only the quiet phases at port.</summary>
        public static bool IsSavablePhase(VoyagePhase phase) => phase == VoyagePhase.Boarding || phase == VoyagePhase.Arrived || phase == VoyagePhase.Failed;

        public static string Encode(byte[] snapshot, in WorldSaveInfo info)
        {
            if (snapshot == null || snapshot.Length == 0) throw new ArgumentException("There is nothing to save.", nameof(snapshot));
            var envelope = new Envelope
            {
                Version = FormatVersion, SavedUnix = info.SavedUnix, Voyage = info.Voyage, Wallet = info.Wallet,
                Reputation = info.Reputation, Crew = info.Crew, Phase = (int)info.Phase, Host = info.Host, Data = Convert.ToBase64String(Compress(snapshot))
            };
            return JsonUtility.ToJson(envelope);
        }

        /// <summary>Reads only the summary, which is cheap enough for the menu to do every time it opens.</summary>
        public static bool TryPeek(string text, out WorldSaveInfo info) => TryParse(text, false, out info, out _);

        public static bool TryDecode(string text, out byte[] snapshot, out WorldSaveInfo info) => TryParse(text, true, out info, out snapshot);

        private static bool TryParse(string text, bool withData, out WorldSaveInfo info, out byte[] snapshot)
        {
            info = default;
            snapshot = null;
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(text);
                if (envelope == null || envelope.Version != FormatVersion || string.IsNullOrEmpty(envelope.Data) || envelope.Voyage < 1) return false;
                if (envelope.Phase < 0 || envelope.Phase > byte.MaxValue || !IsSavablePhase((VoyagePhase)envelope.Phase)) return false;
                info = new WorldSaveInfo
                {
                    SavedUnix = envelope.SavedUnix, Voyage = envelope.Voyage, Wallet = envelope.Wallet, Reputation = envelope.Reputation,
                    Crew = envelope.Crew, Phase = (VoyagePhase)envelope.Phase, Host = envelope.Host
                };
                if (!withData) return true;
                snapshot = Decompress(Convert.FromBase64String(envelope.Data));
                return snapshot.Length > 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("The saved world could not be read: " + exception.Message);
                info = default;
                snapshot = null;
                return false;
            }
        }

        public static bool Exists => TryPeekStored(out _);

        public static bool TryPeekStored(out WorldSaveInfo info)
        {
            info = default;
            if (!Enabled) return false;
            try { return TryPeek(PlayerPrefs.GetString(Key, string.Empty), out info); }
            catch (Exception exception) { Debug.LogWarning("Could not look for a saved world: " + exception.Message); return false; }
        }

        public static bool Save(byte[] snapshot, in WorldSaveInfo info)
        {
            if (!Enabled || !IsSavablePhase(info.Phase)) return false;
            try
            {
                PlayerPrefs.SetString(Key, Encode(snapshot, info));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception) { Debug.LogWarning("Could not save the world: " + exception.Message); return false; }
        }

        public static void Clear()
        {
            try { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
            catch (Exception exception) { Debug.LogWarning("Could not erase the saved world: " + exception.Message); }
        }

        /// <summary>Queues the saved world so the next session that spawns starts from it. Returns false when there is none.</summary>
        public static bool Stage(RestoreMode mode)
        {
            if (mode == RestoreMode.None || !Enabled) return false;
            try
            {
                if (!TryDecode(PlayerPrefs.GetString(Key, string.Empty), out byte[] snapshot, out _)) return false;
                VoyageMigrationDataHandler.Stage(snapshot);
                PendingRestore = mode;
                return true;
            }
            catch (Exception exception) { Debug.LogWarning("Could not load the saved world: " + exception.Message); return false; }
        }

        /// <summary>Returns the staged restore mode once and forgets it, so a later new world starts clean.</summary>
        public static RestoreMode TakeRestore()
        {
            var mode = PendingRestore;
            PendingRestore = RestoreMode.None;
            return mode;
        }

        public static void CancelRestore() => PendingRestore = RestoreMode.None;

        /// <summary>Forgets any staged restore, so a brand new world starts empty.</summary>
        public static void Discard()
        {
            PendingRestore = RestoreMode.None;
            VoyageMigrationDataHandler.Stage(null);
        }

        /// <summary>"Voyage 4 • 312 coins • 2 reputation • saved 5 min ago".</summary>
        public static string Describe(in WorldSaveInfo info, long nowUnix)
        {
            string host = string.IsNullOrEmpty(info.Host) ? string.Empty : info.Host + " • ";
            return $"{host}Voyage {info.Voyage} • {info.Wallet} coins • {info.Reputation} reputation • saved {Age(nowUnix - info.SavedUnix)}";
        }

        public static string Age(long seconds)
        {
            if (seconds < 90) return "just now";
            if (seconds < 3600) return (seconds / 60) + " min ago";
            if (seconds < 86400) return (seconds / 3600) + " h ago";
            return (seconds / 86400) + " d ago";
        }

        public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true)) gzip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        private static byte[] Decompress(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096];
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    if (output.Length > MaxPayloadBytes) throw new InvalidDataException("The saved world is larger than any real save.");
                }
                return output.ToArray();
            }
        }
    }
}
