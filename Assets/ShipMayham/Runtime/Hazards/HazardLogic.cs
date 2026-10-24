using System;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham
{
    public enum WavePhase : byte { Idle, Warning }

    /// <summary>A rock shoal on the sailing route. Fixed for a voyage, derived from <see cref="HazardState.Seed"/> and the route.</summary>
    public readonly struct Shoal
    {
        public readonly Vector2 Center;
        public readonly float Radius;

        public Shoal(Vector2 center, float radius) { Center = center; Radius = radius; }
    }

    /// <summary>Replicated hazard state of the running voyage. Shoal positions are not sent: both sides derive them from the seed.</summary>
    [Serializable]
    public struct HazardState : INetworkSerializable, IEquatable<HazardState>
    {
        public uint Seed;
        public WavePhase Wave;
        /// <summary>Seconds until the wave arrives while <see cref="Wave"/> is Warning.</summary>
        public float WaveRemaining;
        /// <summary>Compass bearing (0 = north) the wave comes from; steering the bow toward it avoids the damage.</summary>
        public float WaveBearing;
        /// <summary>Voyage time at which the next wave warning starts; negative when the route has none.</summary>
        public float NextWaveAt;
        public int WaveIndex, ShoalHits, WavesDodged, WavesHit;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Seed); s.SerializeValue(ref Wave); s.SerializeValue(ref WaveRemaining);
            s.SerializeValue(ref WaveBearing); s.SerializeValue(ref NextWaveAt);
            s.SerializeValue(ref WaveIndex); s.SerializeValue(ref ShoalHits); s.SerializeValue(ref WavesDodged); s.SerializeValue(ref WavesHit);
        }

        public bool Equals(HazardState other) => ValueType.Equals(this, other);
    }

    /// <summary>The pure rules of the captain's hazards: where shoals are, what a hit costs and when a rogue wave comes.</summary>
    public static class HazardLogic
    {
        public const int MaxShoals = 8;
        /// <summary>Half-width of the ship used for shoal contact.</summary>
        public const float ShipRadius = 2.5f;
        /// <summary>Shoals keep this far from the departure point and the port so the harbor entrance is always clear.</summary>
        public const float SafeFromStart = 45f, SafeFromPort = 55f;
        public const float MaxOffset = 50f, MinGap = 16f, MinRadius = 5f, MaxRadius = 8f;
        public const float WarnRange = 45f, WarnArc = 60f, MapRange = 60f;
        public const float HitCooldown = 1.5f, ShoalSpeedFactor = .35f;
        public const float WaveWarningSeconds = 5f, WaveSafeAngle = 50f, WaveDamage = 12f, WaveSpeedFactor = .6f;

        public static int ShoalCount(RouteProfile route)
        {
            switch (route)
            {
                case RouteProfile.StormPassage: return 6;
                case RouteProfile.MarketRun: return 4;
                default: return 3;
            }
        }

        /// <summary>Seconds into the voyage of the first wave warning, or a negative number when the route has no waves.</summary>
        public static float FirstWaveDelay(RouteProfile route)
        {
            switch (route)
            {
                case RouteProfile.StormPassage: return 30f;
                case RouteProfile.MarketRun: return 50f;
                default: return -1f;
            }
        }

        /// <summary>The seed of one voyage: the same session seed gives every voyage its own layout.</summary>
        public static uint VoyageSeed(uint sessionSeed, int voyage)
        {
            unchecked
            {
                uint seed = (sessionSeed == 0 ? 1u : sessionSeed) ^ ((uint)voyage * 2654435761u);
                seed = (seed ^ (seed >> 15)) * 2246822519u;
                seed ^= seed >> 13;
                return seed == 0 ? 1u : seed;
            }
        }

        /// <summary>Fills <paramref name="buffer"/> with the shoals of a voyage and returns how many were placed.</summary>
        public static int Generate(uint seed, RouteProfile route, Shoal[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            int want = Mathf.Min(ShoalCount(route), buffer.Length);
            float minY = SafeFromStart, maxY = VoyageSimulation.Destination.y - SafeFromPort;
            uint state = seed == 0 ? 1u : seed;
            int count = 0;
            for (int attempt = 0; attempt < want * 40 && count < want; attempt++)
            {
                var center = new Vector2(Mathf.Lerp(-MaxOffset, MaxOffset, Unit(ref state)), Mathf.Lerp(minY, maxY, Unit(ref state)));
                float radius = Mathf.Lerp(MinRadius, MaxRadius, Unit(ref state));
                // The safe zones are measured to the shoal's edge, so the harbor entrance stays open whatever the radius.
                bool free = center.magnitude >= SafeFromStart + radius &&
                    Vector2.Distance(center, VoyageSimulation.Destination) >= SafeFromPort + radius;
                for (int j = 0; j < count && free; j++)
                    free = Vector2.Distance(center, buffer[j].Center) >= radius + buffer[j].Radius + MinGap;
                if (free) buffer[count++] = new Shoal(center, radius);
            }
            return count;
        }

        /// <summary>Hull damage of running into a shoal at a given speed.</summary>
        public static float ShoalDamage(float speed) => Mathf.Max(2f, speed * 2.5f);

        /// <summary>Seconds between the end of one wave and the warning of the next.</summary>
        public static float WaveInterval(RouteProfile route, uint seed, int index)
        {
            float low = route == RouteProfile.StormPassage ? 45f : 75f, high = route == RouteProfile.StormPassage ? 70f : 105f;
            return Mathf.Lerp(low, high, Roll(seed, index * 2 + 1));
        }

        /// <summary>The compass bearing of a wave, always one of the eight main directions so it is easy to call out.</summary>
        public static float WaveBearing(uint seed, int index) => Mathf.Floor(Roll(seed, index * 2) * 8f) % 8f * 45f;

        public static bool WaveSafe(float heading, float waveBearing) => Mathf.Abs(Mathf.DeltaAngle(heading, waveBearing)) <= WaveSafeAngle;

        /// <summary>Signed degrees to turn the bow to face the wave: positive is to starboard (right).</summary>
        public static float TurnToFace(float heading, float waveBearing) => Mathf.DeltaAngle(heading, waveBearing);

        public static string Compass(float bearing)
        {
            string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            int index = Mathf.RoundToInt(Mathf.Repeat(bearing, 360f) / 45f) % 8;
            return names[index];
        }

        /// <summary>
        /// The nearest shoal within <see cref="WarnRange"/> of the ship's edge that lies roughly ahead (or very close), or -1.
        /// <paramref name="edgeDistance"/> is the gap between the ship's edge and the shoal's.
        /// </summary>
        public static int NearestAhead(Vector2 position, float heading, Shoal[] shoals, int count, out float edgeDistance)
        {
            int best = -1;
            edgeDistance = float.MaxValue;
            float radians = heading * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = shoals[i].Center - position;
                float gap = offset.magnitude - shoals[i].Radius - ShipRadius;
                if (gap > WarnRange || gap >= edgeDistance) continue;
                bool ahead = gap < 12f || Vector2.Angle(forward, offset) <= WarnArc;
                if (!ahead) continue;
                best = i;
                edgeDistance = Mathf.Max(0, gap);
            }
            return best;
        }

        private static float Unit(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }

        private static float Roll(uint seed, int salt)
        {
            unchecked
            {
                uint state = (seed == 0 ? 1u : seed) ^ ((uint)(salt + 1) * 0x9E3779B9u);
                if (state == 0) state = 1;
                Unit(ref state); Unit(ref state);
                return Unit(ref state);
            }
        }
    }

    /// <summary>
    /// The shoals of the voyage being shown, derived once per seed and route. Shared by the HUD, the world view and the sound
    /// so they all agree with the host without anything being replicated.
    /// </summary>
    public static class ShoalSet
    {
        private static readonly Shoal[] Items = new Shoal[HazardLogic.MaxShoals];
        private static uint cachedSeed;
        private static RouteProfile cachedRoute;
        private static bool cached;
        private static int count;

        public static Shoal[] Shoals => Items;

        /// <summary>Returns the number of shoals for this voyage and fills <see cref="Shoals"/>.</summary>
        public static int For(uint seed, RouteProfile route)
        {
            if (cached && cachedSeed == seed && cachedRoute == route) return count;
            count = HazardLogic.Generate(seed, route, Items);
            cachedSeed = seed;
            cachedRoute = route;
            cached = true;
            return count;
        }
    }
}
