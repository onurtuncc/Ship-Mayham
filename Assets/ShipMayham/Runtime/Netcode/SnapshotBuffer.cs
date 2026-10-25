using UnityEngine;

namespace ShipMayham
{
    public struct MotionSample
    {
        public double Time;           // server seconds
        public Vector3 Position, Velocity;
        public float Angle;           // degrees (ship heading); unused for crew
    }

    /// <summary>
    /// Time-ordered motion samples of one entity, sampled at an arbitrary render time: cubic Hermite between the two samples that
    /// bracket it (positions and velocities), short capped extrapolation past the newest, and out-of-order or duplicate datagrams
    /// handled by sorted insertion. The core of "other players move smoothly even though packets arrive in bursts".
    /// </summary>
    public sealed class SnapshotBuffer
    {
        public const int Capacity = 32;
        /// <summary>How far beyond the newest snapshot we are willing to guess, in seconds.</summary>
        public float MaxExtrapolation = .25f;
        /// <summary>A gap longer than this between neighbours is a teleport, not motion: do not interpolate across it.</summary>
        public float TeleportDistance = 4f;

        private readonly MotionSample[] samples = new MotionSample[Capacity];
        private int count;

        public int Count => count;
        public double NewestTime => count == 0 ? double.NegativeInfinity : samples[count - 1].Time;
        public MotionSample Newest => samples[count - 1];

        public void Clear() => count = 0;

        /// <summary>Moves every held sample by the same offset (the entity's coordinates changed, not its place).</summary>
        public void Shift(Vector3 offset)
        {
            for (int i = 0; i < count; i++) samples[i].Position += offset;
        }

        public void Add(MotionSample sample)
        {
            // Insert keeping time order; drop exact duplicates (the same datagram arriving twice).
            int index = count;
            while (index > 0 && samples[index - 1].Time > sample.Time) index--;
            if (index > 0 && samples[index - 1].Time == sample.Time) return;
            if (count == Capacity)
            {
                if (index == 0) return;                       // older than everything we keep
                System.Array.Copy(samples, 1, samples, 0, Capacity - 1);
                index--;
                count--;
            }
            System.Array.Copy(samples, index, samples, index + 1, count - index);
            samples[index] = sample;
            count++;
        }

        /// <summary>Returns false when the buffer is empty.</summary>
        public bool Sample(double time, out Vector3 position, out Vector3 velocity, out float angle)
        {
            position = velocity = Vector3.zero; angle = 0;
            if (count == 0) return false;
            if (time <= samples[0].Time || count == 1)
            {
                var first = samples[0];
                position = first.Position; velocity = first.Velocity; angle = first.Angle;
                return true;
            }
            var newest = samples[count - 1];
            if (time >= newest.Time)
            {
                float ahead = Mathf.Min((float)(time - newest.Time), MaxExtrapolation);
                // Keep the full velocity for a short hold (a late packet should not look like braking), then ease to rest so a
                // stopped or vanished entity does not run on forever.
                float hold = MaxExtrapolation * .4f, rest = Mathf.Max(0, ahead - hold), window = MaxExtrapolation - hold;
                float scale = window > 0 ? 1 - rest / window : 0;
                float travelled = Mathf.Min(ahead, hold) + rest * (1 + scale) * .5f;
                position = newest.Position + newest.Velocity * travelled;
                velocity = newest.Velocity * Mathf.Max(0, scale);
                angle = newest.Angle;
                return true;
            }
            int i = count - 2;
            while (i > 0 && samples[i].Time > time) i--;
            var a = samples[i]; var b = samples[i + 1];
            double span = b.Time - a.Time;
            float t = span <= 1e-6 ? 1f : (float)((time - a.Time) / span);
            if ((b.Position - a.Position).sqrMagnitude > TeleportDistance * TeleportDistance)
            {
                var pick = t < .5f ? a : b;
                position = pick.Position; velocity = pick.Velocity; angle = pick.Angle;
                return true;
            }
            float h = (float)span, t2 = t * t, t3 = t2 * t;
            position = (2 * t3 - 3 * t2 + 1) * a.Position + (t3 - 2 * t2 + t) * h * a.Velocity +
                       (-2 * t3 + 3 * t2) * b.Position + (t3 - t2) * h * b.Velocity;
            velocity = Vector3.Lerp(a.Velocity, b.Velocity, t);
            angle = Mathf.LerpAngle(a.Angle, b.Angle, t);
            return true;
        }
    }
}
