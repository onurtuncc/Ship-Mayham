using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Communication without voice or text: a ping marks a spot (and names what is there); an emote is a short gesture with a
    /// speech bubble. Signals are presentation only: the host relays them, they never change the voyage.
    /// </summary>
    public enum SignalKind : byte { Ping, Help, Thanks, OverHere, WatchOut }

    /// <summary>A signal as every client receives it. <see cref="Point"/> is ship-local when <see cref="Attached"/>, world space otherwise.</summary>
    public readonly struct SignalMessage
    {
        public readonly int Slot;
        public readonly SignalKind Kind;
        public readonly Vector3 Point;
        public readonly bool Attached;
        public readonly string Label;

        public SignalMessage(int slot, SignalKind kind, Vector3 point, bool attached, string label)
        {
            Slot = slot; Kind = kind; Point = point; Attached = attached; Label = label ?? string.Empty;
        }
    }

    public static class Signals
    {
        /// <summary>Minimum time between two signals from one crew member.</summary>
        public const float CooldownSeconds = 1f;
        /// <summary>How long a ping marker or an emote bubble stays up.</summary>
        public const float PingSeconds = 5f, BubbleSeconds = 3f;
        /// <summary>Holding the signal key longer than this opens the emote wheel instead of pinging.</summary>
        public const float HoldSeconds = .25f;
        /// <summary>Distance (in input units) the pointer must move from the wheel centre to choose an emote.</summary>
        public const float WheelDeadZone = 24f;
        public const float GestureSeconds = 2.2f;
        public const float MaxPointDistance = 400f;

        private static readonly Color[] SlotColors =
        {
            new Color(1f, .78f, .28f), new Color(.46f, .76f, 1f), new Color(.4f, .84f, .55f), new Color(1f, .5f, .7f)
        };

        public static Color ColorFor(int slot) => SlotColors[Mathf.Abs(slot) % SlotColors.Length];

        public static string Text(SignalKind kind)
        {
            switch (kind)
            {
                case SignalKind.Help: return "HELP!";
                case SignalKind.Thanks: return "THANKS!";
                case SignalKind.OverHere: return "OVER HERE!";
                case SignalKind.WatchOut: return "WATCH OUT!";
                default: return "PING";
            }
        }

        public static CrewPose PoseFor(SignalKind kind)
        {
            switch (kind)
            {
                case SignalKind.Help: return CrewPose.Wave;
                case SignalKind.Thanks: return CrewPose.Bow;
                case SignalKind.WatchOut: return CrewPose.Angry;
                default: return CrewPose.Point;
            }
        }

        public static bool IsEmote(SignalKind kind) => kind != SignalKind.Ping;

        /// <summary>
        /// The emote chosen by pushing the pointer or stick away from the wheel centre: up Help, right Thanks, down Watch out,
        /// left Over here. Null inside the dead zone.
        /// </summary>
        public static SignalKind? WheelChoice(Vector2 vector, float deadZone = WheelDeadZone)
        {
            if (vector.sqrMagnitude < deadZone * deadZone) return null;
            if (Mathf.Abs(vector.x) > Mathf.Abs(vector.y)) return vector.x > 0 ? SignalKind.Thanks : SignalKind.OverHere;
            return vector.y > 0 ? SignalKind.Help : SignalKind.WatchOut;
        }

        public static bool IsValidPoint(Vector3 point) =>
            !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) && point.sqrMagnitude <= MaxPointDistance * MaxPointDistance * 4f;
    }

    /// <summary>Per-crew cooldown so nobody can flood the others with signals.</summary>
    public sealed class SignalLimiter
    {
        private readonly Dictionary<int, float> nextAllowed = new Dictionary<int, float>();

        /// <summary>True (and starts the cooldown) when the slot may signal at the given time.</summary>
        public bool TryAcquire(int slot, float now)
        {
            if (nextAllowed.TryGetValue(slot, out float next) && now < next) return false;
            nextAllowed[slot] = now + Signals.CooldownSeconds;
            return true;
        }

        public void Forget(int slot) => nextAllowed.Remove(slot);

        public void Clear() => nextAllowed.Clear();
    }
}
