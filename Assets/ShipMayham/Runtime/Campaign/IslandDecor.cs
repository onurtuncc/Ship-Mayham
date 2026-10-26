using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    public enum DecorKind : byte { Palm, Rock, Boulder, Coral, Shell, Driftwood, Bone, SkullRock, Hut, Barrel, Wreck, Reed, Crate }

    /// <summary>One piece of scenery: what it is, where (harbor coordinates), how big and which way it faces.</summary>
    public readonly struct DecorItem
    {
        public readonly DecorKind Kind;
        public readonly Vector2 Position;
        public readonly float Scale, Yaw;

        public DecorItem(DecorKind kind, Vector2 position, float scale, float yaw) { Kind = kind; Position = position; Scale = scale; Yaw = yaw; }
    }

    /// <summary>
    /// Scenery of an island: palms and boulders, coral in the reef's shallows, bones on the lair's beaches. Pure and seeded by the
    /// island, so every machine builds the same; nothing here blocks walking (the walkable ground is the terrain's alone).
    /// </summary>
    public static class IslandDecor
    {
        public static List<DecorItem> Generate(IslandTerrain terrain)
        {
            var items = new List<DecorItem>();
            uint seed = (uint)(terrain.Island + 3) * 2246822519u;
            switch (terrain.Theme)
            {
                case IslandTheme.Reef:
                    Scatter(items, terrain, seed, DecorKind.Palm, 60, .9f, 1.6f, onLand: true, minHeight: .35f, maxHeight: 1.2f);
                    Scatter(items, terrain, seed + 1, DecorKind.Coral, 140, .7f, 1.4f, onLand: false, minHeight: IslandTerrain.SeaLevel - 3.4f, maxHeight: IslandTerrain.SeaLevel - .5f);
                    Scatter(items, terrain, seed + 2, DecorKind.Shell, 40, .5f, 1f, onLand: true, minHeight: IslandTerrain.SeaLevel + .1f, maxHeight: .1f);
                    Scatter(items, terrain, seed + 3, DecorKind.Rock, 14, 1.2f, 2.6f, onLand: true, minHeight: .3f, maxHeight: 3f);
                    break;
                case IslandTheme.Cove:
                    Scatter(items, terrain, seed, DecorKind.Boulder, 34, 1.3f, 3.4f, onLand: true, minHeight: .3f, maxHeight: 9f);
                    Scatter(items, terrain, seed + 1, DecorKind.Palm, 14, .8f, 1.3f, onLand: true, minHeight: .3f, maxHeight: 1.6f);
                    Scatter(items, terrain, seed + 2, DecorKind.Hut, 5, 1.2f, 1.6f, onLand: true, minHeight: .3f, maxHeight: 2.5f);
                    Scatter(items, terrain, seed + 3, DecorKind.Barrel, 18, .9f, 1.2f, onLand: true, minHeight: .3f, maxHeight: 2.5f);
                    Scatter(items, terrain, seed + 4, DecorKind.Crate, 12, .9f, 1.3f, onLand: true, minHeight: .3f, maxHeight: 2.5f);
                    break;
                case IslandTheme.Storm:
                    Scatter(items, terrain, seed, DecorKind.Boulder, 48, 1.4f, 3.8f, onLand: true, minHeight: .3f, maxHeight: 12f);
                    Scatter(items, terrain, seed + 1, DecorKind.Driftwood, 26, 1f, 2f, onLand: true, minHeight: IslandTerrain.SeaLevel + .1f, maxHeight: 1f);
                    Scatter(items, terrain, seed + 2, DecorKind.Rock, 40, 1.5f, 3.4f, onLand: false, minHeight: IslandTerrain.SeaLevel - 2.5f, maxHeight: IslandTerrain.SeaLevel + .3f);
                    Scatter(items, terrain, seed + 3, DecorKind.Wreck, 2, 1.6f, 2.2f, onLand: true, minHeight: IslandTerrain.SeaLevel + .1f, maxHeight: .6f);
                    break;
                case IslandTheme.Skull:
                    Scatter(items, terrain, seed + 2, DecorKind.SkullRock, 3, 3.2f, 4.5f, onLand: true, minHeight: .3f, maxHeight: 4f);
                    Scatter(items, terrain, seed, DecorKind.Boulder, 40, 1.4f, 3.6f, onLand: true, minHeight: .3f, maxHeight: 12f);
                    Scatter(items, terrain, seed + 1, DecorKind.Bone, 36, .8f, 2f, onLand: true, minHeight: .3f, maxHeight: 6f);
                    Scatter(items, terrain, seed + 3, DecorKind.Wreck, 3, 1.6f, 2.4f, onLand: true, minHeight: IslandTerrain.SeaLevel + .1f, maxHeight: .8f);
                    Scatter(items, terrain, seed + 4, DecorKind.Driftwood, 16, 1f, 2f, onLand: true, minHeight: IslandTerrain.SeaLevel + .1f, maxHeight: 1f);
                    break;
                default:
                    Scatter(items, terrain, seed, DecorKind.Palm, 36, .9f, 1.5f, onLand: true, minHeight: .3f, maxHeight: 2.2f);
                    Scatter(items, terrain, seed + 1, DecorKind.Rock, 24, 1.2f, 3f, onLand: true, minHeight: .3f, maxHeight: 4f);
                    Scatter(items, terrain, seed + 2, DecorKind.Reed, 30, .8f, 1.3f, onLand: true, minHeight: IslandTerrain.SeaLevel + .1f, maxHeight: .1f);
                    Scatter(items, terrain, seed + 3, DecorKind.Barrel, 8, .9f, 1.2f, onLand: true, minHeight: .3f, maxHeight: 1.6f);
                    break;
            }
            return items;
        }

        private static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
                return x;
            }
        }

        private static float Unit(uint seed, uint n) => Hash(seed * 0x9e3779b1u + n * 0x85ebca6bu) / 4294967296f;

        /// <summary>Places up to <paramref name="count"/> items by rejection sampling over the island: clear of the town, of the spots to find and of each other.</summary>
        private static void Scatter(List<DecorItem> items, IslandTerrain terrain, uint seed, DecorKind kind, int count, float minScale, float maxScale,
            bool onLand, float minHeight, float maxHeight)
        {
            var bounds = terrain.Bounds;
            int placed = 0;
            for (uint attempt = 0; attempt < count * 30u && placed < count; attempt++)
            {
                var p = new Vector2(Mathf.Lerp(bounds.xMin - 12f, bounds.xMax, Unit(seed, attempt * 3)), Mathf.Lerp(bounds.yMin - 12f, bounds.yMax + 12f, Unit(seed, attempt * 3 + 1)));
                if (IslandTerrain.PlatformInside(p) > -3f) continue;                 // keep the town and a margin around it clear
                float h = terrain.Height(p);
                if (h < minHeight || h > maxHeight) continue;
                if (onLand && terrain.CoastDistance(p) < 0) continue;
                if (kind != DecorKind.Boulder && kind != DecorKind.SkullRock && terrain.Slope(p) > .6f) continue;
                if (NearThingsToFind(terrain, p, kind)) continue;
                float scale = Mathf.Lerp(minScale, maxScale, Unit(seed, attempt * 3 + 2));
                bool crowded = false;
                float gap = (kind == DecorKind.Palm ? 5f : kind == DecorKind.Coral ? 1.2f : kind == DecorKind.SkullRock ? 14f : 2f) * Mathf.Max(1f, scale * .6f);
                for (int i = items.Count - 1; i >= 0 && !crowded; i--)
                {
                    float need = items[i].Kind == DecorKind.SkullRock ? 9f : kind == DecorKind.SkullRock ? gap * .5f : gap * .7f;
                    crowded = (items[i].Position - p).sqrMagnitude < need * need;
                }
                if (crowded) continue;
                items.Add(new DecorItem(kind, p, scale, Unit(seed, attempt * 3 + 3) * 360f));
                placed++;
            }
        }

        private static bool NearThingsToFind(IslandTerrain terrain, Vector2 p, DecorKind kind)
        {
            float clear = kind == DecorKind.Reed || kind == DecorKind.Coral || kind == DecorKind.Shell ? 1.6f : 3.4f;
            foreach (var s in terrain.DigSpots) if ((s - p).sqrMagnitude < clear * clear) return true;
            foreach (var s in terrain.ChestSpots) if ((s - p).sqrMagnitude < clear * clear) return true;
            foreach (var s in terrain.FishingSpots) if ((s - p).sqrMagnitude < clear * clear) return true;
            return false;
        }
    }
}
