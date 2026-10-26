using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipMayham
{
    /// <summary>
    /// Builds the visible island: a ground mesh straight from the terrain's height function (so what one sees is what one walks on),
    /// its colour palette, and the scenery as one combined mesh. Colours come from small palette textures, so each island is two
    /// draw calls and needs no authored art. Presentation only.
    /// </summary>
    public static class IslandMeshes
    {
        public const float Cell = 2.5f, Margin = 30f;

        // ---------------------------------------------------------------- the ground

        /// <summary>Palette position (0..1) of a point of ground: sea floor, wet and dry sand, grass and rock.</summary>
        public static float PaletteU(float height, float slope, float hillHeight)
        {
            float depth = IslandTerrain.SeaLevel - height;
            if (depth > 0) return Mathf.Lerp(.2f, 0f, Mathf.Clamp01(depth / VoyageSimulation.BaySeabedDepth));
            float above = -depth;
            if (slope > .85f && above > .6f) return .9f;
            if (above < .5f) return Mathf.Lerp(.24f, .32f, above / .5f);
            if (above < 1.4f) return Mathf.Lerp(.32f, .42f, (above - .5f) / .9f);
            float extra = Mathf.Max(0, height - IslandTerrain.PlatformHeight);
            if (extra <= 0) return Mathf.Lerp(.42f, .52f, Mathf.Clamp01((above - 1.4f) / .4f));
            float rise = Mathf.Clamp01(extra / Mathf.Max(.5f, hillHeight * .7f));
            return rise < .75f ? Mathf.Lerp(.52f, .74f, rise / .75f) : Mathf.Lerp(.78f, 1f, (rise - .75f) / .25f);
        }

        public static Mesh BuildTerrain(IslandTerrain terrain)
        {
            float x0 = terrain.Bounds.xMin - Margin, x1 = terrain.Bounds.xMax + 4f, y0 = terrain.Bounds.yMin - Margin, y1 = terrain.Bounds.yMax + Margin;
            int nx = Mathf.CeilToInt((x1 - x0) / Cell) + 1, ny = Mathf.CeilToInt((y1 - y0) / Cell) + 1;
            var vertices = new Vector3[nx * ny];
            var uvs = new Vector2[vertices.Length];
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    var p = new Vector2(x0 + i * Cell, y0 + j * Cell);
                    // Under the town platform the ground dips just below it, so the platform's own surface is what shows.
                    float h = IslandTerrain.OnPlatform(p) ? IslandTerrain.PlatformHeight - .04f : terrain.Height(p);
                    int index = j * nx + i;
                    vertices[index] = new Vector3(p.x, h, p.y);
                    uvs[index] = new Vector2(PaletteU(h, IslandTerrain.OnPlatform(p) ? 0 : terrain.Slope(p), terrain.HillHeight), .5f);
                }
            var triangles = new int[(nx - 1) * (ny - 1) * 6];
            int t = 0;
            for (int j = 0; j < ny - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            var mesh = new Mesh { name = "IslandGround_" + terrain.Island, indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private struct Stop { public float At; public Color Color; public Stop(float at, Color color) { At = at; Color = color; } }

        /// <summary>The ground's colours for a theme, along <see cref="PaletteU"/>.</summary>
        public static Texture2D BuildGroundPalette(IslandTheme theme)
        {
            Color deep, shallow, wet, sand, grassLow, grassHigh, rock, rockLight;
            switch (theme)
            {
                case IslandTheme.Reef:
                    deep = new Color(.1f, .4f, .5f); shallow = new Color(.45f, .85f, .8f); wet = new Color(.85f, .8f, .6f); sand = new Color(.97f, .92f, .72f);
                    grassLow = new Color(.42f, .66f, .32f); grassHigh = new Color(.28f, .5f, .24f); rock = new Color(.78f, .62f, .55f); rockLight = new Color(.9f, .78f, .7f); break;
                case IslandTheme.Cove:
                    deep = new Color(.08f, .26f, .34f); shallow = new Color(.28f, .5f, .5f); wet = new Color(.42f, .36f, .28f); sand = new Color(.62f, .54f, .4f);
                    grassLow = new Color(.34f, .48f, .27f); grassHigh = new Color(.22f, .36f, .2f); rock = new Color(.42f, .34f, .28f); rockLight = new Color(.56f, .47f, .38f); break;
                case IslandTheme.Storm:
                    deep = new Color(.06f, .2f, .28f); shallow = new Color(.25f, .4f, .42f); wet = new Color(.35f, .35f, .34f); sand = new Color(.5f, .5f, .47f);
                    grassLow = new Color(.36f, .42f, .32f); grassHigh = new Color(.26f, .32f, .26f); rock = new Color(.28f, .29f, .33f); rockLight = new Color(.42f, .43f, .47f); break;
                case IslandTheme.Skull:
                    deep = new Color(.07f, .2f, .22f); shallow = new Color(.25f, .38f, .36f); wet = new Color(.5f, .46f, .38f); sand = new Color(.76f, .72f, .62f);
                    grassLow = new Color(.46f, .44f, .26f); grassHigh = new Color(.34f, .32f, .2f); rock = new Color(.24f, .2f, .26f); rockLight = new Color(.4f, .34f, .42f); break;
                default:
                    deep = new Color(.1f, .34f, .46f); shallow = new Color(.4f, .72f, .74f); wet = new Color(.78f, .7f, .5f); sand = new Color(.88f, .8f, .58f);
                    grassLow = new Color(.44f, .6f, .3f); grassHigh = new Color(.3f, .46f, .24f); rock = new Color(.55f, .52f, .48f); rockLight = new Color(.7f, .67f, .62f); break;
            }
            var stops = new[]
            {
                new Stop(0f, deep), new Stop(.2f, shallow), new Stop(.24f, wet), new Stop(.32f, wet), new Stop(.42f, sand), new Stop(.52f, grassLow),
                new Stop(.74f, grassHigh), new Stop(.78f, rock), new Stop(1f, rockLight)
            };
            return Palette(256, u => Evaluate(stops, u), "IslandGround_" + theme);
        }

        private static Color Evaluate(Stop[] stops, float u)
        {
            for (int i = 1; i < stops.Length; i++)
                if (u <= stops[i].At) return Color.Lerp(stops[i - 1].Color, stops[i].Color, Mathf.InverseLerp(stops[i - 1].At, stops[i].At, u));
            return stops[stops.Length - 1].Color;
        }

        private static Texture2D Palette(int width, System.Func<float, Color> color, string name)
        {
            var texture = new Texture2D(width, 1, TextureFormat.RGB24, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[width];
            for (int i = 0; i < width; i++) pixels[i] = color((i + .5f) / width);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // ---------------------------------------------------------------- the scenery

        public enum Paint : byte { Trunk, Leaf, Rock, RockDark, Shell, CoralPink, CoralOrange, CoralPurple, Wood, WoodDark, Bone, Cloth, Roof, Reed, Ember, Count }

        private static readonly Color[] PaintColors =
        {
            new Color(.5f, .34f, .2f), new Color(.28f, .6f, .26f), new Color(.44f, .42f, .4f), new Color(.27f, .26f, .29f), new Color(.96f, .92f, .86f),
            new Color(.95f, .5f, .6f), new Color(.98f, .62f, .25f), new Color(.65f, .4f, .85f), new Color(.6f, .42f, .26f), new Color(.34f, .24f, .17f),
            new Color(.92f, .9f, .82f), new Color(.8f, .2f, .18f), new Color(.72f, .55f, .3f), new Color(.45f, .65f, .3f), new Color(.9f, .5f, .2f)
        };

        public static Texture2D BuildDecorPalette()
        {
            int count = (int)Paint.Count;
            return Palette(count * 4, u => PaintColors[Mathf.Clamp(Mathf.FloorToInt(u * count), 0, count - 1)], "IslandDecor");
        }

        private static float PaintU(Paint paint) => ((int)paint + .5f) / (int)Paint.Count;

        private static Mesh cube, sphere, cylinder;

        private static Mesh Primitive(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            return mesh;
        }

        private sealed class Combiner
        {
            private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void Add(Mesh primitive, Vector3 position, Quaternion rotation, Vector3 scale, Paint paint)
            {
                var matrix = Matrix4x4.TRS(position, rotation, scale);
                var normalMatrix = Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
                int first = vertices.Count;
                var verts = primitive.vertices; var norms = primitive.normals; var tris = primitive.triangles;
                for (int i = 0; i < verts.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(verts[i]));
                    normals.Add(normalMatrix.MultiplyVector(norms[i]));
                    uvs.Add(new Vector2(PaintU(paint), .5f));
                }
                for (int i = 0; i < tris.Length; i++) triangles.Add(first + tris[i]);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public static Mesh BuildDecor(IslandTerrain terrain)
        {
            if (cube == null) { cube = Primitive(PrimitiveType.Cube); sphere = Primitive(PrimitiveType.Sphere); cylinder = Primitive(PrimitiveType.Cylinder); }
            var combiner = new Combiner();
            foreach (var item in IslandDecor.Generate(terrain))
            {
                float ground = terrain.Height(item.Position);
                var origin = new Vector3(item.Position.x, ground, item.Position.y);
                var yaw = Quaternion.Euler(0, item.Yaw, 0);
                float s = item.Scale;
                switch (item.Kind)
                {
                    case DecorKind.Palm: AddPalm(combiner, origin, yaw, s); break;
                    case DecorKind.Rock: combiner.Add(sphere, origin + Vector3.up * s * .25f, yaw, new Vector3(1.4f, .9f, 1.1f) * s, s > 2f ? Paint.RockDark : Paint.Rock); break;
                    case DecorKind.Boulder:
                        combiner.Add(sphere, origin + Vector3.up * s * .35f, yaw, new Vector3(1.5f, 1.1f, 1.3f) * s, Paint.Rock);
                        combiner.Add(sphere, origin + yaw * new Vector3(s * .9f, s * .1f, s * .3f), yaw, new Vector3(1f, .7f, .9f) * s * .7f, Paint.RockDark);
                        combiner.Add(sphere, origin + yaw * new Vector3(-s * .7f, s * .1f, -s * .5f), yaw, new Vector3(.9f, .6f, .8f) * s * .6f, Paint.Rock);
                        break;
                    case DecorKind.Coral: AddCoral(combiner, origin, yaw, s); break;
                    case DecorKind.Shell: combiner.Add(sphere, origin + Vector3.up * .05f, yaw, new Vector3(.5f, .18f, .4f) * s, Paint.Shell); break;
                    case DecorKind.Driftwood: combiner.Add(cylinder, origin + Vector3.up * .15f, yaw * Quaternion.Euler(0, 0, 90), new Vector3(.18f, 1.1f, .18f) * s, Paint.Wood); break;
                    case DecorKind.Bone: AddBone(combiner, origin, yaw, s); break;
                    case DecorKind.SkullRock: AddSkull(combiner, origin, yaw, s); break;
                    case DecorKind.Hut: AddHut(combiner, origin, yaw, s); break;
                    case DecorKind.Barrel:
                        combiner.Add(cylinder, origin + Vector3.up * .45f * s, yaw, new Vector3(.7f, .45f, .7f) * s, Paint.Wood);
                        combiner.Add(cylinder, origin + Vector3.up * .45f * s, yaw, new Vector3(.76f, .1f, .76f) * s, Paint.WoodDark);
                        break;
                    case DecorKind.Crate: combiner.Add(cube, origin + Vector3.up * .4f * s, yaw, Vector3.one * .8f * s, Paint.Wood); break;
                    case DecorKind.Wreck: AddWreck(combiner, origin, yaw, s); break;
                    case DecorKind.Reed:
                        for (int i = 0; i < 3; i++) combiner.Add(cube, origin + yaw * new Vector3(i * .2f - .2f, .5f * s, i % 2 * .15f), yaw * Quaternion.Euler(i * 6 - 6, 0, i * 5 - 5), new Vector3(.07f, 1f, .07f) * s, Paint.Reed);
                        break;
                }
            }
            return combiner.Build("IslandDecor_" + terrain.Island);
        }

        private static void AddPalm(Combiner c, Vector3 origin, Quaternion yaw, float s)
        {
            var lean = yaw * Quaternion.Euler(7, 0, 0);
            c.Add(cylinder, origin + lean * Vector3.up * 1.6f * s, lean, new Vector3(.34f, 1.6f, .34f) * s, Paint.Trunk);
            Vector3 top = origin + lean * Vector3.up * 3.2f * s;
            for (int i = 0; i < 6; i++)
            {
                var frond = yaw * Quaternion.Euler(0, i * 60f, 0) * Quaternion.Euler(24, 0, 0);
                c.Add(cube, top + frond * Vector3.forward * 1.1f * s, frond, new Vector3(.55f, .07f, 2.4f) * s, Paint.Leaf);
            }
            c.Add(sphere, top + Vector3.down * .15f * s, Quaternion.identity, Vector3.one * .45f * s, Paint.Trunk);
        }

        private static void AddCoral(Combiner c, Vector3 origin, Quaternion yaw, float s)
        {
            var paint = (Paint)((int)Paint.CoralPink + Mathf.Abs(Mathf.RoundToInt(origin.x * 1.7f + origin.z)) % 3);
            for (int i = 0; i < 4; i++)
            {
                var branch = yaw * Quaternion.Euler(i * 12 - 18, i * 90, i * 9 - 14);
                c.Add(cylinder, origin + branch * Vector3.up * .4f * s, branch, new Vector3(.2f, .4f + i % 2 * .15f, .2f) * s, paint);
            }
            c.Add(sphere, origin + Vector3.up * .15f * s, yaw, new Vector3(.7f, .35f, .7f) * s, paint);
        }

        private static void AddBone(Combiner c, Vector3 origin, Quaternion yaw, float s)
        {
            c.Add(cylinder, origin + Vector3.up * .12f, yaw * Quaternion.Euler(0, 0, 90), new Vector3(.12f, .8f, .12f) * s, Paint.Bone);
            c.Add(sphere, origin + yaw * new Vector3(.8f * s, .12f, .08f * s), yaw, Vector3.one * .2f * s, Paint.Bone);
            c.Add(sphere, origin + yaw * new Vector3(.8f * s, .12f, -.08f * s), yaw, Vector3.one * .2f * s, Paint.Bone);
            c.Add(sphere, origin + yaw * new Vector3(-.8f * s, .12f, .08f * s), yaw, Vector3.one * .2f * s, Paint.Bone);
            c.Add(sphere, origin + yaw * new Vector3(-.8f * s, .12f, -.08f * s), yaw, Vector3.one * .2f * s, Paint.Bone);
        }

        private static void AddSkull(Combiner c, Vector3 origin, Quaternion yaw, float s)
        {
            c.Add(sphere, origin + Vector3.up * s * .9f, yaw, new Vector3(1.9f, 1.6f, 1.8f) * s, Paint.Bone);
            c.Add(cube, origin + yaw * new Vector3(0, s * .15f, s * .4f), yaw, new Vector3(1.2f, .6f, 1f) * s, Paint.Bone);
            c.Add(sphere, origin + yaw * new Vector3(-s * .5f, s * 1.0f, s * .8f), yaw, new Vector3(.55f, .6f, .5f) * s, Paint.RockDark);
            c.Add(sphere, origin + yaw * new Vector3(s * .5f, s * 1.0f, s * .8f), yaw, new Vector3(.55f, .6f, .5f) * s, Paint.RockDark);
            c.Add(cube, origin + yaw * new Vector3(0, s * .55f, s * .95f), yaw, new Vector3(.2f, .35f, .2f) * s, Paint.RockDark);
        }

        private static void AddHut(Combiner c, Vector3 origin, Quaternion yaw, float s)
        {
            c.Add(cube, origin + Vector3.up * .9f * s, yaw, new Vector3(2.2f, 1.8f, 2.2f) * s, Paint.Wood);
            c.Add(cube, origin + yaw * new Vector3(0, 2f * s, -.55f * s), yaw * Quaternion.Euler(28, 0, 0), new Vector3(2.7f, .15f, 1.8f) * s, Paint.Roof);
            c.Add(cube, origin + yaw * new Vector3(0, 2f * s, .55f * s), yaw * Quaternion.Euler(-28, 0, 0), new Vector3(2.7f, .15f, 1.8f) * s, Paint.Roof);
            c.Add(cube, origin + yaw * new Vector3(0, .6f * s, 1.12f * s), yaw, new Vector3(.7f, 1.2f, .08f) * s, Paint.WoodDark);
        }

        private static void AddWreck(Combiner c, Vector3 origin, Quaternion yaw, float s)
        {
            var tilt = yaw * Quaternion.Euler(0, 0, 14);
            c.Add(cube, origin + Vector3.up * .5f * s, tilt, new Vector3(2.6f, .5f, 6f) * s, Paint.WoodDark);
            for (int i = -2; i <= 2; i++)
            {
                c.Add(cube, origin + tilt * new Vector3(-1.2f * s, 1.1f * s, i * 1.1f * s), tilt, new Vector3(.12f, 1.5f, .12f) * s, Paint.Wood);
                c.Add(cube, origin + tilt * new Vector3(1.2f * s, 1.1f * s, i * 1.1f * s), tilt, new Vector3(.12f, 1.5f, .12f) * s, Paint.Wood);
            }
            c.Add(cylinder, origin + tilt * new Vector3(0, 2.2f * s, .5f * s), tilt * Quaternion.Euler(0, 0, 18), new Vector3(.18f, 2.2f, .18f) * s, Paint.Wood);
            c.Add(cube, origin + tilt * new Vector3(.3f, 3.2f * s, .5f * s), tilt, new Vector3(.08f, 1.2f, 1.4f) * s, Paint.Cloth);
        }
    }
}
