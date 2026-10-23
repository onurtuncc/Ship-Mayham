using System;
using UnityEngine;

namespace ShipMayham
{
    public enum UiSize : byte { Small, Normal, Large }
    public enum ShakeLevel : byte { Off, Low, Full }

    /// <summary>
    /// Player preferences kept on the device: volumes, interface size, camera shake, look speed, rumble and tips.
    /// Every setter clamps, saves and raises <see cref="Changed"/>. Storage failures (blocked cookies, private windows)
    /// only log a warning; the value stays in effect for the session.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultMaster = 1f, DefaultMusic = .7f, DefaultSfx = 1f, DefaultLookSpeed = 1f;
        public const float MinLookSpeed = .5f, MaxLookSpeed = 2f;
        /// <summary>Storage key prefix; public so tests can plant corrupt values.</summary>
        public const string KeyPrefix = "ship-mayham.settings.";
        internal static readonly string[] Keys = { "master", "music", "sfx", "uisize", "shake", "look", "rumble", "tips" };

        private static bool loaded;
        private static float master = DefaultMaster, music = DefaultMusic, sfx = DefaultSfx, look = DefaultLookSpeed;
        private static UiSize uiSize = UiSize.Normal;
        private static ShakeLevel shake = ShakeLevel.Full;
        private static bool rumble = true, tips = true;

        /// <summary>Raised after any value changed (including a reset), so screens can refresh.</summary>
        public static event Action Changed;

        public static float MasterVolume { get { Load(); return master; } set { Set(ref master, Mathf.Clamp01(value), "master"); } }
        public static float MusicVolume { get { Load(); return music; } set { Set(ref music, Mathf.Clamp01(value), "music"); } }
        public static float SfxVolume { get { Load(); return sfx; } set { Set(ref sfx, Mathf.Clamp01(value), "sfx"); } }
        public static float LookSpeed { get { Load(); return look; } set { Set(ref look, Mathf.Clamp(value, MinLookSpeed, MaxLookSpeed), "look"); } }
        public static UiSize UiSize { get { Load(); return uiSize; } set { Set(ref uiSize, ClampEnum(value, UiSize.Large), "uisize"); } }
        public static ShakeLevel CameraShake { get { Load(); return shake; } set { Set(ref shake, ClampEnum(value, ShakeLevel.Full), "shake"); } }
        public static bool Rumble { get { Load(); return rumble; } set { Set(ref rumble, value, "rumble"); } }
        public static bool Tips { get { Load(); return tips; } set { Set(ref tips, value, "tips"); } }

        /// <summary>Interface scale factor for the chosen size.</summary>
        public static float UiScale => UiScaleFor(UiSize);
        public static float UiScaleFor(UiSize size) => size == UiSize.Small ? .85f : size == UiSize.Large ? 1.2f : 1f;

        /// <summary>Multiplier for camera shake strength.</summary>
        public static float ShakeScale => ShakeScaleFor(CameraShake);
        public static float ShakeScaleFor(ShakeLevel level) => level == ShakeLevel.Off ? 0f : level == ShakeLevel.Low ? .4f : 1f;

        /// <summary>Restores every preference to its default and saves them.</summary>
        public static void ResetToDefaults()
        {
            Load();
            master = DefaultMaster; music = DefaultMusic; sfx = DefaultSfx; look = DefaultLookSpeed;
            uiSize = UiSize.Normal; shake = ShakeLevel.Full; rumble = true; tips = true;
            foreach (string key in Keys) Persist(key);
            Changed?.Invoke();
        }

        /// <summary>Forgets the cached values so the next read goes to storage again (tests).</summary>
        public static void ReloadFromStorage()
        {
            loaded = false;
            master = DefaultMaster; music = DefaultMusic; sfx = DefaultSfx; look = DefaultLookSpeed;
            uiSize = UiSize.Normal; shake = ShakeLevel.Full; rumble = true; tips = true;
        }

        /// <summary>Removes the stored values (tests).</summary>
        public static void ClearStorage()
        {
            try { foreach (string key in Keys) PlayerPrefs.DeleteKey(KeyPrefix + key); }
            catch (Exception exception) { Debug.LogWarning("Could not clear the settings: " + exception.Message); }
            ReloadFromStorage();
        }

        private static T ClampEnum<T>(T value, T max) where T : struct, Enum =>
            Convert.ToInt32(value) < 0 || Convert.ToInt32(value) > Convert.ToInt32(max) ? default : value;

        private static void Set<T>(ref T field, T value, string key) where T : struct
        {
            Load();
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            Persist(key);
            Changed?.Invoke();
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyPrefix + "master", DefaultMaster));
                music = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyPrefix + "music", DefaultMusic));
                sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyPrefix + "sfx", DefaultSfx));
                look = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPrefix + "look", DefaultLookSpeed), MinLookSpeed, MaxLookSpeed);
                uiSize = ClampEnum((UiSize)PlayerPrefs.GetInt(KeyPrefix + "uisize", (int)UiSize.Normal), UiSize.Large);
                shake = ClampEnum((ShakeLevel)PlayerPrefs.GetInt(KeyPrefix + "shake", (int)ShakeLevel.Full), ShakeLevel.Full);
                rumble = PlayerPrefs.GetInt(KeyPrefix + "rumble", 1) == 1;
                tips = PlayerPrefs.GetInt(KeyPrefix + "tips", 1) == 1;
            }
            catch (Exception exception) { Debug.LogWarning("Could not read the settings: " + exception.Message); }
        }

        private static void Persist(string key)
        {
            try
            {
                switch (key)
                {
                    case "master": PlayerPrefs.SetFloat(KeyPrefix + key, master); break;
                    case "music": PlayerPrefs.SetFloat(KeyPrefix + key, music); break;
                    case "sfx": PlayerPrefs.SetFloat(KeyPrefix + key, sfx); break;
                    case "look": PlayerPrefs.SetFloat(KeyPrefix + key, look); break;
                    case "uisize": PlayerPrefs.SetInt(KeyPrefix + key, (int)uiSize); break;
                    case "shake": PlayerPrefs.SetInt(KeyPrefix + key, (int)shake); break;
                    case "rumble": PlayerPrefs.SetInt(KeyPrefix + key, rumble ? 1 : 0); break;
                    case "tips": PlayerPrefs.SetInt(KeyPrefix + key, tips ? 1 : 0); break;
                }
                PlayerPrefs.Save();
            }
            catch (Exception exception) { Debug.LogWarning("Could not save the settings: " + exception.Message); }
        }
    }
}
