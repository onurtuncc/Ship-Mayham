using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Estimates the server clock from the timestamps on incoming snapshots and decides how far behind it to render. The
    /// smallest (local arrival time - server time) seen recently is the best-case path, so it defines the offset; how far the
    /// other packets exceed it measures jitter, which sizes the interpolation delay: enough to always have a snapshot on both
    /// sides of the render time, as small as possible so remote players do not feel laggy. Pure and unit-testable.
    /// </summary>
    public sealed class SnapshotClock
    {
        public const float MinDelay = .08f, MaxDelay = .4f;
        private const float WindowSeconds = 4f;

        private bool initialized;
        private double offset;                     // local - server, best case (target)
        private double usedOffset, usedAt;         // what rendering actually uses: slewed toward the target
        private float usedDelay, delayAt; private bool delayInitialized;
        private const double MaxSlew = .02;        // the render clock may run at most 2% fast or slow while it re-aligns
        private double currentMin = double.PositiveInfinity, previousMin = double.PositiveInfinity, windowStart;
        private float lastArrival, interval = .04f, peakGap = .04f, peakExcess, avgExcess;
        private double lastServerTime = double.NegativeInfinity;

        public bool HasData => initialized;
        /// <summary>Average spacing of snapshots in seconds.</summary>
        public float Interval => interval;
        /// <summary>Recent worst lateness of a snapshot compared to the best case, in seconds.</summary>
        public float Jitter => peakExcess;
        /// <summary>How far behind the estimated server time to render.</summary>
        public float Delay => Mathf.Clamp(Mathf.Max(interval * 2f, peakGap * 1.2f) + peakExcess * 1.5f + avgExcess, MinDelay, MaxDelay);

        public void Reset() { initialized = false; usedOffset = 0; delayInitialized = false; currentMin = previousMin = double.PositiveInfinity; peakExcess = avgExcess = 0; peakGap = interval = .04f; lastServerTime = double.NegativeInfinity; }

        public void OnSnapshot(float localTime, uint serverMs)
        {
            double server = serverMs / 1000.0;
            double sample = localTime - server;
            // A server clock that jumped (host migration, session restart) invalidates everything learned so far.
            if (initialized && (System.Math.Abs(sample - offset) > 3.0 || server < lastServerTime - 1.0)) Reset();
            if (!initialized)
            {
                initialized = true; offset = usedOffset = sample; usedAt = localTime; currentMin = sample; windowStart = localTime; lastArrival = localTime;
            }
            else
            {
                if (localTime - windowStart > WindowSeconds) { previousMin = currentMin; currentMin = double.PositiveInfinity; windowStart = localTime; }
                currentMin = System.Math.Min(currentMin, sample);
                offset = System.Math.Min(currentMin, previousMin);
                float gap = localTime - lastArrival;
                if (gap > 0 && gap < .25f) interval = Mathf.Lerp(interval, Mathf.Clamp(gap, .015f, .12f), .08f);
                if (gap > 0 && gap < .25f) peakGap = gap > peakGap ? Mathf.Lerp(peakGap, gap, .6f) : Mathf.Lerp(peakGap, gap, .01f);
                lastArrival = localTime;
            }
            float excess = Mathf.Max(0, (float)(sample - offset));
            avgExcess = Mathf.Lerp(avgExcess, excess, .05f);
            peakExcess = excess > peakExcess ? Mathf.Lerp(peakExcess, excess, .6f) : Mathf.Lerp(peakExcess, excess, .02f);
            lastServerTime = System.Math.Max(lastServerTime, server);
        }

        /// <summary>
        /// Estimated server time right now, in seconds. The offset estimate can step by a few milliseconds whenever a faster packet
        /// arrives; applying that directly would make the rendered motion jump, so the used offset slews toward it instead.
        /// </summary>
        public double ServerNow(float localTime)
        {
            double elapsed = System.Math.Max(0, localTime - usedAt);
            usedAt = localTime;
            double difference = offset - usedOffset;
            if (System.Math.Abs(difference) > .25) usedOffset = offset;
            else usedOffset += System.Math.Max(-MaxSlew * elapsed, System.Math.Min(MaxSlew * elapsed, difference));
            return localTime - usedOffset;
        }

        /// <summary>The server time that should be on screen right now.</summary>
        public double RenderTime(float localTime) => ServerNow(localTime) - SmoothedDelay(localTime);

        // The wanted delay moves a few milliseconds with every snapshot; used as is, each change would shift the sampled position
        // by speed * change and read as a stutter. It grows quickly when the link gets worse and shrinks slowly when it recovers.
        private float SmoothedDelay(float localTime)
        {
            float target = Delay;
            if (!delayInitialized) { delayInitialized = true; usedDelay = target; delayAt = localTime; return usedDelay; }
            float elapsed = Mathf.Max(0, localTime - delayAt);
            delayAt = localTime;
            float difference = target - usedDelay;
            float limit = (difference > 0 ? .12f : .03f) * elapsed;
            usedDelay += Mathf.Clamp(difference, -limit, limit);
            return usedDelay;
        }
    }
}
