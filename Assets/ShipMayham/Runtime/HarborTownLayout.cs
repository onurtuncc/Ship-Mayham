using UnityEngine;

namespace ShipMayham
{
    /// <summary>Shared authored town footprint for server movement and scene construction.</summary>
    public static class HarborTownLayout
    {
        public const int StationsPerPort = 6, SalvageCount = 3;
        public static readonly Rect Bounds = Rect.MinMaxRect(-34, -23, 5.8f, 23);
        public static readonly Rect[] Buildings = {
            new Rect(-12, -14, 4, 3), new Rect(-28, -5, 6, 4),
            new Rect(-15, 13, 6, 4), new Rect(-29, -18, 6, 4), new Rect(-22, 16, 4, 3),
            new Rect(-30, 6, 4, 4), new Rect(-20, 6, 4, 4), new Rect(-32.5f, 2.5f, 3, 3),
            new Rect(-32, -18, 3, 6), new Rect(-8, 2, 2, 2),
            new Rect(-18.6f, -16.6f, 1.2f, 1.2f), new Rect(-16.6f, -3.6f, 1.2f, 1.2f),
            new Rect(-8.6f, 6.4f, 1.2f, 1.2f), new Rect(-26.6f, 11.4f, 1.2f, 1.2f)
        };
        public static Vector2 SalvagePoint(int index) => index == 0 ? new Vector2(-30, -20)
            : index == 1 ? new Vector2(-30, 19) : new Vector2(-5, 19);
        public static bool Walkable(Vector2 p)
        {
            foreach (Rect building in Buildings)
                if (Rect.MinMaxRect(building.xMin - .25f, building.yMin - .25f, building.xMax + .25f, building.yMax + .25f).Contains(p)) return false;
            return true;
        }
    }
}
