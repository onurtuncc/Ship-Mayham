using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>
    /// Simple solid shapes that tell things apart without relying on colour: coins are round, reputation a diamond,
    /// food square, drinks a drop (triangle), and alert severity is repeated as a shape next to the coloured bar.
    /// </summary>
    public enum IconShape : byte { Circle, Square, Triangle, Diamond }

    public static class IconShapes
    {
        private static readonly Sprite[] sprites = new Sprite[4];

        /// <summary>True when the point (x, y), each in -1..1 from the centre of the icon, lies inside the shape.</summary>
        public static bool Contains(IconShape shape, float x, float y)
        {
            switch (shape)
            {
                case IconShape.Circle: return x * x + y * y <= 1f;
                case IconShape.Square: return Mathf.Abs(x) <= .82f && Mathf.Abs(y) <= .82f;
                case IconShape.Diamond: return Mathf.Abs(x) + Mathf.Abs(y) <= 1.1f;
                // Point up: wide at the bottom, a single point at the top.
                default: return y >= -.8f && y <= .95f && Mathf.Abs(x) <= (.95f - y) * .55f;
            }
        }

        /// <summary>The shape that repeats the severity of an alert colour.</summary>
        public static IconShape ForSeverity(Color color)
        {
            if (Approximately(color, HudKit.Danger)) return IconShape.Triangle;
            if (Approximately(color, HudKit.Warning)) return IconShape.Diamond;
            if (Approximately(color, HudKit.Good)) return IconShape.Square;
            return IconShape.Circle;
        }

        public static Sprite Sprite(IconShape shape)
        {
            int index = (int)shape;
            if (sprites[index] != null) return sprites[index];
            const int size = 48, samples = 3;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                int inside = 0;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float x = (px + (sx + .5f) / samples) / size * 2f - 1f, y = (py + (sy + .5f) / samples) / size * 2f - 1f;
                    if (Contains(shape, x, y)) inside++;
                }
                pixels[py * size + px] = new Color32(255, 255, 255, (byte)(inside * 255 / (samples * samples)));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprites[index] = sprite;
        }

        public static Image Create(Transform parent, string name, Color color, float size, IconShape shape)
        {
            var image = HudKit.Box(parent, name, color, false);
            image.sprite = Sprite(shape);
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        private static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < .02f;
    }
}
