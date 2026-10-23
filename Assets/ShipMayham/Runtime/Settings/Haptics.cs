using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipMayham
{
    /// <summary>Short controller rumble for impacts. Does nothing without a gamepad or when rumble is turned off in the settings.</summary>
    public static class Haptics
    {
        private static float endsAt;
        private static Gamepad active;

        /// <summary>Rumbles the current gamepad; low is the heavy motor, high the light one (both 0..1).</summary>
        public static void Pulse(float low, float high, float seconds)
        {
            if (!GameSettings.Rumble || seconds <= 0) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            try
            {
                pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
                active = pad;
                endsAt = Mathf.Max(endsAt, Time.unscaledTime + seconds);
                HapticsRunner.Ensure();
            }
            catch (System.Exception exception) { Debug.LogWarning("Could not rumble the controller: " + exception.Message); }
        }

        /// <summary>Stops any rumble that is due to end (called every frame) or all of it when forced.</summary>
        internal static void Tick(bool force = false)
        {
            if (active == null || (!force && Time.unscaledTime < endsAt)) return;
            try { active.ResetHaptics(); }
            catch (System.Exception exception) { Debug.LogWarning("Could not stop the rumble: " + exception.Message); }
            active = null;
            endsAt = 0;
        }
    }

    /// <summary>Keeps rumble from sticking: stops the motors once a pulse is over, even while the game is paused.</summary>
    [DefaultExecutionOrder(500)]
    public sealed class HapticsRunner : MonoBehaviour
    {
        private static HapticsRunner instance;

        public static void Ensure()
        {
            if (instance != null || !Application.isPlaying) return;
            var go = new GameObject("HapticsRunner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HapticsRunner>();
        }

        private void Update() => Haptics.Tick();

        private void OnDisable() => Haptics.Tick(true);

        private void OnApplicationFocus(bool focused) { if (!focused) Haptics.Tick(true); }
    }
}
