using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipMayham.Tests
{
    /// <summary>The visible islands: scenery rules, generated meshes and palettes, and the town roots wearing the island of the voyage chain.</summary>
    public sealed class IslandVisualTests
    {
        private const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";

        [Test] public void SceneryIsTheSameEveryTime()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var a = IslandDecor.Generate(IslandTerrain.For(i));
                var b = IslandDecor.Generate(IslandTerrain.For(i));
                Assert.AreEqual(a.Count, b.Count);
                for (int k = 0; k < a.Count; k++) { Assert.AreEqual(a[k].Position, b[k].Position); Assert.AreEqual(a[k].Kind, b[k].Kind); }
            }
        }

        [Test] public void EveryIslandHasPlentyOfSceneryAwayFromTheTown()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var items = IslandDecor.Generate(IslandTerrain.For(i));
                Assert.Greater(items.Count, 40, $"island {i}");
                foreach (var item in items) Assert.Less(IslandTerrain.PlatformInside(item.Position), -2.9f, $"island {i} {item.Kind} stands on or beside the town");
            }
        }

        [Test] public void SceneryKeepsClearOfThingsToFind()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                var all = new List<Vector2>();
                all.AddRange(terrain.DigSpots); all.AddRange(terrain.ChestSpots); all.AddRange(terrain.FishingSpots);
                foreach (var item in IslandDecor.Generate(terrain))
                    foreach (var spot in all) Assert.Greater(Vector2.Distance(item.Position, spot), 1.5f, $"island {i}: {item.Kind} sits on a spot");
            }
        }

        [Test] public void ThemesGetTheirOwnScenery()
        {
            bool Has(int island, DecorKind kind) => IslandDecor.Generate(IslandTerrain.For(island)).Exists(d => d.Kind == kind);
            Assert.IsTrue(Has(Campaign.CoralCay, DecorKind.Coral), "Has(Campaign.CoralCay, DecorKind.Coral)");
            Assert.IsTrue(Has(Campaign.CoralCay, DecorKind.Palm), "Has(Campaign.CoralCay, DecorKind.Palm)");
            Assert.IsFalse(Has(Campaign.SkullAtoll, DecorKind.Coral));
            Assert.IsTrue(Has(Campaign.SkullAtoll, DecorKind.SkullRock), "Has(Campaign.SkullAtoll, DecorKind.SkullRock)");
            Assert.IsTrue(Has(Campaign.SkullAtoll, DecorKind.Bone), "Has(Campaign.SkullAtoll, DecorKind.Bone)");
            Assert.IsTrue(Has(Campaign.SmugglersCove, DecorKind.Hut), "Has(Campaign.SmugglersCove, DecorKind.Hut)");
            Assert.IsTrue(Has(Campaign.SmugglersCove, DecorKind.Crate), "Has(Campaign.SmugglersCove, DecorKind.Crate)");
            Assert.IsTrue(Has(Campaign.StormReef, DecorKind.Wreck) || Has(Campaign.StormReef, DecorKind.Driftwood), "Has(Campaign.StormReef, DecorKind.Wreck) || Has(Campaign.StormReef, DecorKind.Driftwood)");
            Assert.IsTrue(Has(Campaign.NorthHaven, DecorKind.Reed), "Has(Campaign.NorthHaven, DecorKind.Reed)");
        }

        [Test] public void PalmsAreHumanSized()
        {
            foreach (var item in IslandDecor.Generate(IslandTerrain.For(Campaign.CoralCay)))
                if (item.Kind == DecorKind.Palm) Assert.That(item.Scale, Is.InRange(.8f, 1.7f));
        }

        [Test] public void PaletteReadsFromSeaFloorThroughSandAndGrassToRock()
        {
            float deep = IslandMeshes.PaletteU(IslandTerrain.FloorHeight, 0, 3), shallow = IslandMeshes.PaletteU(IslandTerrain.SeaLevel - .3f, 0, 3);
            float wet = IslandMeshes.PaletteU(IslandTerrain.SeaLevel + .2f, 0, 3), dry = IslandMeshes.PaletteU(IslandTerrain.SeaLevel + 1f, 0, 3);
            float town = IslandMeshes.PaletteU(IslandTerrain.PlatformHeight, 0, 3), hill = IslandMeshes.PaletteU(IslandTerrain.PlatformHeight + .5f, .3f, 3);
            float cliff = IslandMeshes.PaletteU(IslandTerrain.PlatformHeight + .5f, 1f, 3);
            Assert.Less(deep, shallow); Assert.Less(shallow, wet); Assert.Less(wet, dry); Assert.LessOrEqual(dry, town); Assert.Less(town, hill);
            Assert.Greater(cliff, hill, "steep ground shows rock");
            foreach (var u in new[] { deep, shallow, wet, dry, town, hill, cliff }) Assert.That(u, Is.InRange(0f, 1f));
        }

        [Test] public void GroundMeshFollowsTheHeightFunction()
        {
            for (int i = 0; i < Campaign.Count; i += 2)
            {
                var terrain = IslandTerrain.For(i);
                var mesh = IslandMeshes.BuildTerrain(terrain);
                try
                {
                    Assert.Greater(mesh.vertexCount, 5000);
                    Assert.Less(mesh.vertexCount, 40000, "small enough for the web build");
                    Assert.AreEqual(0, mesh.triangles.Length % 3);
                    var vertices = mesh.vertices;
                    var uvs = mesh.uv;
                    float lowest = float.MaxValue, highest = float.MinValue;
                    for (int v = 0; v < vertices.Length; v += 7)
                    {
                        var p = new Vector2(vertices[v].x, vertices[v].z);
                        Assert.IsFalse(float.IsNaN(vertices[v].y) || float.IsInfinity(vertices[v].y));
                        float expected = IslandTerrain.OnPlatform(p) ? IslandTerrain.PlatformHeight - .04f : terrain.Height(p);
                        Assert.AreEqual(expected, vertices[v].y, .001f);
                        Assert.That(uvs[v].x, Is.InRange(0f, 1f));
                        lowest = Mathf.Min(lowest, vertices[v].y); highest = Mathf.Max(highest, vertices[v].y);
                    }
                    Assert.Less(lowest, IslandTerrain.SeaLevel - 5f, "reaches the sea floor");
                    Assert.Greater(highest, IslandTerrain.SeaLevel + 1f, "and rises above the waves");
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test] public void NothingOfTheGroundRisesThroughTheTownPlatform()
        {
            var mesh = IslandMeshes.BuildTerrain(IslandTerrain.For(Campaign.NorthHaven));
            try
            {
                foreach (var v in mesh.vertices)
                    if (IslandTerrain.OnPlatform(new Vector2(v.x, v.z))) Assert.Less(v.y, IslandTerrain.PlatformHeight, "the platform's own surface must stay on top");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test] public void SceneryMeshesAreNotEmptyAndWellFormed()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var mesh = IslandMeshes.BuildDecor(IslandTerrain.For(i));
                try
                {
                    Assert.Greater(mesh.vertexCount, 1000, $"island {i}");
                    Assert.Less(mesh.vertexCount, 300000, "one island's scenery stays light");
                    Assert.AreEqual(0, mesh.triangles.Length % 3);
                    Assert.AreEqual(mesh.vertexCount, mesh.uv.Length);
                    Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test] public void PalettesHaveEveryColour()
        {
            foreach (IslandTheme theme in System.Enum.GetValues(typeof(IslandTheme)))
            {
                var palette = IslandMeshes.BuildGroundPalette(theme);
                Assert.AreEqual(256, palette.width);
                Assert.AreNotEqual(palette.GetPixel(10, 0), palette.GetPixel(200, 0));
                Object.DestroyImmediate(palette);
            }
            var decor = IslandMeshes.BuildDecorPalette();
            Assert.AreEqual((int)IslandMeshes.Paint.Count * 4, decor.width);
            Object.DestroyImmediate(decor);
        }

        // ---------------------------------------------------------------------- in the scene

        [UnityTest] public IEnumerator TheTownRootsWearTheIslandsOfTheChain()
        {
            yield return new EnterPlayMode();
            VoyageSimulation.WalkingPassengers = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            var hud = Object.FindAnyObjectByType<PrototypeHud>();
            hud.ConnectSolo();
            float deadline = Time.realtimeSinceStartup + 5;
            while (!hud.Session.IsSpawned && Time.realtimeSinceStartup < deadline) yield return null;
            for (int frame = 0; frame < 20; frame++) yield return null;
            hud.View.CloseMenu(false); Time.timeScale = 1;
            var harbor = hud.Session.Harbor;
            var sim = hud.Session.Simulation;
            var islands = harbor.Islands;
            Assert.AreEqual(Campaign.BrassQuay, islands.IslandOf(0));
            Assert.AreEqual(Campaign.NorthHaven, islands.IslandOf(1));
            Assert.IsTrue(islands.GroundVisible(0) && islands.GroundVisible(1));
            var town = harbor.PortRoots[0].Find("Town_Visuals");
            Assert.IsFalse(town.Find("SeaWall_0").gameObject.activeSelf, "the island continues past the platform");
            Assert.IsFalse(town.Find("WestSeaWall").gameObject.activeSelf);

            sim.State.Island = Campaign.SmugglersCove; sim.State.Target = Campaign.CoralCay;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(Campaign.SmugglersCove, islands.IslandOf(0));
            Assert.AreEqual(Campaign.CoralCay, islands.IslandOf(1));
            int coveExtra = IslandTerrain.For(Campaign.SmugglersCove).DigSpots.Length + IslandTerrain.For(Campaign.SmugglersCove).ChestSpots.Length + IslandTerrain.For(Campaign.SmugglersCove).FishingSpots.Length;
            Assert.AreEqual(coveExtra, islands.SpotProps(0), "a prop for every spot out on the island");
            var ground = harbor.PortRoots[0].Find("IslandGround").GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(ground);
            Assert.AreNotSame(ground, harbor.PortRoots[1].Find("IslandGround").GetComponent<MeshFilter>().sharedMesh, "each island has its own ground");

            // A dug mound and an opened chest on the docked island show as a hole and nothing.
            int firstDig = VoyageSimulation.DigSpots.Length;
            var terrain = IslandTerrain.For(Campaign.SmugglersCove);
            sim.State.ActivityPortKey = sim.State.PortVisit + 2;
            sim.State.DigMask = 1 << firstDig;
            yield return new WaitForSecondsRealtime(.4f);
            var visuals = harbor.PortRoots[0].Find("Visuals");
            Assert.IsFalse(visuals.Find("DigMound_" + firstDig).gameObject.activeSelf, "dug");
            Assert.IsTrue(visuals.Find("DigHole_" + firstDig).gameObject.activeSelf);
            Vector2 spot = VoyageSimulation.SpotsOf(MiniGameKind.Dig, Campaign.SmugglersCove)[firstDig];
            Assert.AreEqual(terrain.Height(spot), visuals.Find("DigMound_" + firstDig).localPosition.y, .001f, "standing on the island's ground");

            sim.State.Island = Campaign.BrassQuay; sim.State.Target = Campaign.NorthHaven; sim.State.DigMask = 0;
            yield return new WaitForSecondsRealtime(.4f);
            yield return new ExitPlayMode();
        }
    }
}
