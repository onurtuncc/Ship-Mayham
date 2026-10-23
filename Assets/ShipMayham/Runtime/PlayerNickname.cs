using UnityEngine;

namespace ShipMayham
{
    /// <summary>The name this player shows in the lobby and on the ship. Stored on the device, never used as an identity.</summary>
    public static class PlayerNickname
    {
        internal const string Key = "ship-mayham.nickname.v1";
        private static string cached;

        public static string Current
        {
            get
            {
                if (cached != null) return cached;
                string stored = null;
                try { stored = LobbyRules.SanitizeName(PlayerPrefs.GetString(Key, string.Empty)); }
                catch (System.Exception exception) { Debug.LogWarning("Could not read the nickname: " + exception.Message); }
                cached = stored ?? "Player " + Random.Range(100, 1000);
                if (stored == null) Persist(cached);
                return cached;
            }
        }

        /// <summary>Saves a new nickname. Unusable input keeps the current one. Returns the name now in effect.</summary>
        public static string Set(string name)
        {
            string clean = LobbyRules.SanitizeName(name);
            if (clean == null) return Current;
            cached = clean;
            Persist(clean);
            return clean;
        }

        private static void Persist(string value)
        {
            try { PlayerPrefs.SetString(Key, value); PlayerPrefs.Save(); }
            catch (System.Exception exception) { Debug.LogWarning("Could not save the nickname: " + exception.Message); }
        }
    }
}
