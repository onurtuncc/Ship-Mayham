using UnityEngine;

namespace ShipMayham
{
    /// <summary>Player-facing audio preferences, persisted locally. Storage failures (blocked cookies, private windows) are non-fatal.</summary>
    public static class FeedbackSettings
    {
        internal const string MutedKey = "ship-mayham.audio.muted.v1";
        private static bool loaded, muted;

        public static bool Muted
        {
            get { Load(); return muted; }
            set
            {
                Load();
                if (muted == value) return;
                muted = value;
                try { PlayerPrefs.SetInt(MutedKey, value ? 1 : 0); PlayerPrefs.Save(); }
                catch (System.Exception exception) { Debug.LogWarning("Could not persist the sound setting: " + exception.Message); }
            }
        }

        /// <summary>Multiplier applied to every sound: 0 while muted, otherwise the master volume.</summary>
        public static float Gain => Muted ? 0f : GameSettings.MasterVolume;

        /// <summary>Gain for sound effects (master x effects volume).</summary>
        public static float SfxGain => Gain * GameSettings.SfxVolume;

        /// <summary>Gain for music (master x music volume).</summary>
        public static float MusicGain => Gain * GameSettings.MusicVolume;

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            try { muted = PlayerPrefs.GetInt(MutedKey, 0) == 1; }
            catch (System.Exception exception) { Debug.LogWarning("Could not read the sound setting: " + exception.Message); }
        }

        /// <summary>Forget the cached value so the next read goes to storage again (tests).</summary>
        public static void Reset() { loaded = false; muted = false; }
    }
}
