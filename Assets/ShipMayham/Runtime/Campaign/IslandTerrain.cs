using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The ground of one island, a pure height function in harbor coordinates (the town platform sits at its east side, the ship
    /// lies beyond it). The host's rules use it to decide where a crew member can walk, wade or must swim; every client builds the
    /// same ground mesh from it. Beaches slope into the sea, so the shoreline is a result of the heights and not a wall.
    /// </summary>
    public sealed class IslandTerrain
    {
        public const float SeaLevel = VoyageSimulation.SeaLevel, PlatformHeight = VoyageSimulation.ShoreHeight;
        /// <summary>Rise of the land above the waterline per metre inland, until it reaches the height of the town platform.</summary>
        public const float BeachRise = .2f;
        /// <summary>Fall of the sea floor per metre out from the waterline, down to <see cref="FloorHeight"/>.</summary>
        public const float BeachDrop = .25f;
        public const float FloorHeight = SeaLevel - VoyageSimulation.BaySeabedDepth;
        /// <summary>Wading: ground up to this far under the surface can be walked on; deeper, a walker starts to swim.</summary>
        public const float WadeDepth = .75f, WalkLimitDepth = 1.1f;
        /// <summary>Slopes steeper than this (rise over run) are cliffs that nobody climbs.</summary>
        public const float MaxWalkSlope = 1.1f;
        public const float EastLimit = 4f;

        private static readonly Rect Platform = HarborTownLayout.Bounds;

        public readonly int Island;
        public readonly IslandTheme Theme;
        public readonly Vector2 Center;
        public readonly float Radius, HillHeight;
        public readonly Rect Bounds;
        private readonly uint seed;
        private readonly float[] outline = new float[OutlineSamples];
        private const int OutlineSamples = 16;

        public Vector2[] DigSpots { get; private set; }
        public Vector2[] ChestSpots { get; private set; }
        public Vector2[] FishingSpots { get; private set; }

        private static readonly Dictionary<int, IslandTerrain> Cache = new Dictionary<int, IslandTerrain>();

        /// <summary>The terrain of an island (built once; the same on every machine).</summary>
        public static IslandTerrain For(int island)
        {
            island = Mathf.Clamp(island, 0, Campaign.Count - 1);
            lock (Cache)
            {
                if (!Cache.TryGetValue(island, out var terrain)) Cache[island] = terrain = new IslandTerrain(island);
                return terrain;
            }
        }

        private IslandTerrain(int island)
        {
            Island = island;
            Theme = Campaign.Island(island).Theme;
            seed = (uint)(island + 1) * 2654435761u + 977u;
            Radius = 56f + Unit(1) * 14f;
            Center = new Vector2(EastLimit - Radius * (.95f + Unit(2) * .1f), (Unit(3) - .5f) * 16f);
            HillHeight = Theme == IslandTheme.Reef ? .7f : Theme == IslandTheme.Town ? 1.1f : Theme == IslandTheme.Cove ? 3.4f : Theme == IslandTheme.Storm ? 4f : 5f;
            for (int i = 0; i < OutlineSamples; i++) outline[i] = .82f + Unit(10u + (uint)i) * .36f;
            float extent = Radius * 1.4f;
            Bounds = Rect.MinMaxRect(Mathf.Min(Platform.xMin, Center.x - extent), Mathf.Min(Platform.yMin, Center.y - extent), Platform.xMax + 1f, Mathf.Max(Platform.yMax, Center.y + extent));
            PlaceActivitySpots();
        }

        // ------------------------------------------------------------------------------ the shape

        private float Unit(uint salt) => Hash(seed ^ salt * 0x9e3779b1u) / 4294967296f;

        private static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
                return x;
            }
        }

        private float Lattice(int x, int y) => Hash(seed + (uint)(x * 73856093) ^ (uint)(y * 19349663)) / 4294967296f;

        /// <summary>Smooth value noise in 0..1.</summary>
        private float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Lattice(ix, iy), b = Lattice(ix + 1, iy), c = Lattice(ix, iy + 1), d = Lattice(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>The island's outline radius in the direction of an angle (radians): lumpy, and never reaching the ship.</summary>
        private float OutlineRadius(float angle)
        {
            float t = Mathf.Repeat(angle / (Mathf.PI * 2), 1) * OutlineSamples;
            int i = Mathf.FloorToInt(t) % OutlineSamples;
            float f = t - Mathf.Floor(t);
            f = f * f * (3 - 2 * f);
            float radius = Radius * Mathf.Lerp(outline[i], outline[(i + 1) % OutlineSamples], f);
            float cos = Mathf.Cos(angle);
            if (cos > .01f) radius = Mathf.Min(radius, (EastLimit - Center.x) / cos);       // the east shore stops before the berth
            return radius;
        }

        /// <summary>Metres from the platform's edge, positive inside it and negative outside.</summary>
        public static float PlatformInside(Vector2 p)
        {
            float dx = Mathf.Max(Mathf.Max(Platform.xMin - p.x, 0), p.x - Platform.xMax);
            float dy = Mathf.Max(Mathf.Max(Platform.yMin - p.y, 0), p.y - Platform.yMax);
            if (dx > 0 || dy > 0) return -Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Min(Mathf.Min(p.x - Platform.xMin, Platform.xMax - p.x), Mathf.Min(p.y - Platform.yMin, Platform.yMax - p.y));
        }

        public static bool OnPlatform(Vector2 p) => PlatformInside(p) >= 0;

        /// <summary>Metres inland of the waterline (negative out at sea); the platform counts as land.</summary>
        public float CoastDistance(Vector2 p)
        {
            Vector2 offset = p - Center;
            float blob = OutlineRadius(Mathf.Atan2(offset.y, offset.x)) - offset.magnitude;
            return Mathf.Max(blob, PlatformInside(p));
        }

        // ------------------------------------------------------------------------------ heights

        /// <summary>Ground height in world units (the sea surface is <see cref="SeaLevel"/>, the town platform <see cref="PlatformHeight"/>).</summary>
        public float Height(Vector2 p)
        {
            if (OnPlatform(p)) return PlatformHeight;
            float d = CoastDistance(p);
            if (d <= 0)
            {
                float sea = Mathf.Max(FloorHeight, SeaLevel + d * BeachDrop);
                // Past the quay wall the harbor channel drops away quickly, where the ship lies.
                if (p.x > Platform.xMax) sea = Mathf.Min(sea, SeaLevel - Mathf.Clamp(1.5f + 3f * (p.x - Platform.xMax), 1.5f, VoyageSimulation.BaySeabedDepth));
                return sea;
            }
            float height = Mathf.Min(PlatformHeight, SeaLevel + d * BeachRise);
            if (d > 6.75f)
            {
                float inland = Mathf.Clamp01((d - 6.75f) / 18f);
                float nearTown = Mathf.Clamp01((-PlatformInside(p) - 4f) / 10f);
                float hills = Noise(p.x * .045f, p.y * .045f) * .75f + Noise(p.x * .11f + 40, p.y * .11f + 40) * .25f;
                height += HillHeight * inland * nearTown * hills * hills;
            }
            return height;
        }

        /// <summary>How steep the ground is around a point (rise over run).</summary>
        public float Slope(Vector2 p)
        {
            const float step = .5f;
            float h = Height(p);
            float dx = Mathf.Abs(Height(p + new Vector2(step, 0)) - h), dy = Mathf.Abs(Height(p + new Vector2(0, step)) - h);
            return Mathf.Max(dx, dy) / step;
        }

        /// <summary>Whether a point is ground a crew member can stand on (wading in shallows included), not a cliff.</summary>
        public bool Walkable(Vector2 p)
        {
            if (!Bounds.Contains(p) || p.x > Platform.xMax) return false;       // the quay edge on the ship side is a wall
            if (OnPlatform(p)) return true;
            if (Height(p) < SeaLevel - WalkLimitDepth) return false;
            return Slope(p) <= MaxWalkSlope;
        }

        /// <summary>Whether a walker standing here is deep enough in the sea to be swimming.</summary>
        public bool DeepEnoughToSwim(Vector2 p) => !OnPlatform(p) && Height(p) < SeaLevel - WadeDepth;

        /// <summary>Whether a swimmer here could stand up: the ground is shallow enough and a walker could be there.</summary>
        public bool ShallowEnoughToStand(Vector2 p) => Walkable(p) && Height(p) >= SeaLevel - WadeDepth + .1f;

        // ------------------------------------------------------------------------------ things to find


        private void PlaceActivitySpots()
        {
            int digs, chests, rods;
            switch (Theme)
            {
                case IslandTheme.Reef: digs = 3; chests = 0; rods = 5; break;
                case IslandTheme.Cove: digs = 2; chests = 4; rods = 2; break;
                case IslandTheme.Storm: digs = 1; chests = 2; rods = 0; break;
                case IslandTheme.Skull: digs = 4; chests = 3; rods = 0; break;
                default: digs = 3; chests = 1; rods = 3; break;
            }
            var taken = new List<Vector2>();
            DigSpots = Scatter(digs, 8f, 40f, 11, taken, false);
            ChestSpots = Scatter(chests, 22f, 40f, 12, taken, true);
            FishingSpots = Shore(rods, 13, taken);
        }

        private Vector2[] Scatter(int count, float minInland, float separation, uint salt, List<Vector2> taken, bool remote)
        {
            var result = new List<Vector2>();
            for (int attempt = 0; attempt < 400 && result.Count < count; attempt++)
            {
                float u = Unit(salt * 1000 + (uint)attempt * 2), v = Unit(salt * 1000 + (uint)attempt * 2 + 1);
                var p = new Vector2(Mathf.Lerp(Bounds.xMin, Bounds.xMax, u), Mathf.Lerp(Bounds.yMin, Bounds.yMax, v));
                if (OnPlatform(p) || -PlatformInside(p) < 7f || CoastDistance(p) < minInland || !Walkable(p) || Slope(p) > .45f) continue;
                if (remote && -PlatformInside(p) < 24f) continue;
                bool apart = true;
                foreach (var other in taken) if (Vector2.Distance(p, other) < Mathf.Min(separation, 18f)) { apart = false; break; }
                if (!apart) continue;
                result.Add(p); taken.Add(p);
            }
            return result.ToArray();
        }

        /// <summary>Fishing places: just inland of the waterline, spread around the island.</summary>
        private Vector2[] Shore(int count, uint salt, List<Vector2> taken)
        {
            var result = new List<Vector2>();
            for (int attempt = 0; attempt < 600 && result.Count < count; attempt++)
            {
                float angle = Unit(salt * 1000 + (uint)attempt) * Mathf.PI * 2;
                float radius = OutlineRadius(angle);
                var p = Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - 1.4f);
                if (OnPlatform(p) || -PlatformInside(p) < 5f || !Walkable(p) || Height(p) < SeaLevel + .1f) continue;
                bool apart = true;
                foreach (var other in taken) if (Vector2.Distance(p, other) < 14f) { apart = false; break; }
                if (!apart) continue;
                result.Add(p); taken.Add(p);
            }
            return result.ToArray();
        }
    }
}
