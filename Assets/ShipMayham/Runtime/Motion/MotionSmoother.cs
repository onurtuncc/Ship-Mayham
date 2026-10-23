using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Turns stepwise network snapshots into continuous motion. Snapshots arrive every ~25-50 ms; chasing each new value with a
    /// plain exponential lerp produces a visible sawtooth. This smoother estimates the velocity and the snapshot interval, aims at
    /// "latest snapshot plus the distance travelled since it arrived" (which is continuous across arrivals), and low-passes the
    /// result. When snapshots stop changing the extrapolation fades out so a stopped entity never overshoots. Pure and testable.
    /// </summary>
    public sealed class MotionSmoother
    {
        public const float DefaultInterval = .04f;

        public float CorrectionRate = 14f;
        public float SnapDistance = 4f;
        public float MaxSpeed = 40f;

        private Vector3 target, velocityEstimate;
        private float lastChange, interval = DefaultInterval;
        private bool initialized;

        public Vector3 Position { get; private set; }
        /// <summary>Velocity currently used for extrapolation (fades to zero when snapshots stop changing).</summary>
        public Vector3 Velocity { get; private set; }

        public void Snap(Vector3 value, float now)
        {
            Position = target = value;
            velocityEstimate = Velocity = Vector3.zero;
            interval = DefaultInterval;
            lastChange = now;
            initialized = true;
        }

        public Vector3 Step(Vector3 snapshot, float now, float dt)
        {
            if (!initialized || (snapshot - target).sqrMagnitude > SnapDistance * SnapDistance && (Position - snapshot).sqrMagnitude > SnapDistance * SnapDistance)
            {
                Snap(snapshot, now);
                return Position;
            }
            if (snapshot != target)
            {
                float elapsed = Mathf.Max(now - lastChange, .008f);
                Vector3 raw = Vector3.ClampMagnitude((snapshot - target) / elapsed, MaxSpeed);
                velocityEstimate = Vector3.Lerp(velocityEstimate, raw, .6f);
                // A long gap means the entity was standing still, not that snapshots are slow: only learn plausible intervals.
                if (elapsed < .12f) interval = Mathf.Lerp(interval, Mathf.Clamp(elapsed, .02f, .1f), .3f);
                target = snapshot;
                lastChange = now;
            }
            float age = Mathf.Max(0, now - lastChange);
            // Fade the estimate out shortly after the next snapshot was due and did not come.
            float weight = 1 - Mathf.Clamp01((age - interval * 1.1f) / (interval * .9f));
            Velocity = velocityEstimate * weight;
            Vector3 aim = target + velocityEstimate * (Mathf.Min(age, interval) * weight);
            Position = Vector3.Lerp(Position, aim, 1 - Mathf.Exp(-CorrectionRate * dt));
            return Position;
        }
    }

    /// <summary>Same idea for a heading in degrees: shortest-arc deltas, wrap-safe, with an estimated turn rate for banking.</summary>
    public sealed class AngleSmoother
    {
        public float CorrectionRate = 12f;
        public float MaxRate = 360f;

        private float target, rateEstimate, lastChange, interval = MotionSmoother.DefaultInterval;
        private bool initialized;

        public float Angle { get; private set; }
        /// <summary>Estimated turn rate in degrees per second (positive = clockwise seen from above).</summary>
        public float Rate { get; private set; }

        public void Snap(float degrees, float now)
        {
            Angle = target = degrees;
            rateEstimate = Rate = 0;
            interval = MotionSmoother.DefaultInterval;
            lastChange = now;
            initialized = true;
        }

        public float Step(float snapshot, float now, float dt)
        {
            if (!initialized) { Snap(snapshot, now); return Angle; }
            if (!Mathf.Approximately(Mathf.DeltaAngle(target, snapshot), 0))
            {
                float elapsed = Mathf.Max(now - lastChange, .008f);
                float raw = Mathf.Clamp(Mathf.DeltaAngle(target, snapshot) / elapsed, -MaxRate, MaxRate);
                rateEstimate = Mathf.Lerp(rateEstimate, raw, .6f);
                if (elapsed < .12f) interval = Mathf.Lerp(interval, Mathf.Clamp(elapsed, .02f, .1f), .3f);
                target = snapshot;
                lastChange = now;
            }
            float age = Mathf.Max(0, now - lastChange);
            float weight = 1 - Mathf.Clamp01((age - interval * 1.1f) / (interval * .9f));
            Rate = rateEstimate * weight;
            float aim = target + rateEstimate * (Mathf.Min(age, interval) * weight);
            Angle += Mathf.DeltaAngle(Angle, aim) * (1 - Mathf.Exp(-CorrectionRate * dt));
            Angle = Mathf.Repeat(Angle, 360);
            return Angle;
        }
    }
}
