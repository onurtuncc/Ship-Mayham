using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>The pure ground of the islands: shape, heights, where one can walk, wade and swim, and what there is to find.</summary>
    public sealed class IslandTerrainTests
    {
        [Test] public void EveryIslandIsTheSameOnEveryMachine()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var a = IslandTerrain.For(i);
                Assert.AreSame(a, IslandTerrain.For(i), "built once");
                var b = new System.Collections.Generic.List<float>();
                for (int k = 0; k < 20; k++) b.Add(a.Height(new Vector2(-10 - k * 3.7f, k * 2.3f - 20)));
                Assert.AreEqual(b.Count, 20);
            }
            Assert.AreEqual(IslandTerrain.For(3).Height(new Vector2(-50, 10)), IslandTerrain.For(3).Height(new Vector2(-50, 10)));
            Assert.AreNotEqual(IslandTerrain.For(2).Center, IslandTerrain.For(3).Center, "no two islands share a shape");
        }

        [Test] public void OutOfRangeIslandsFallBackToTheNearestOne()
        {
            Assert.AreSame(IslandTerrain.For(0), IslandTerrain.For(-4));
            Assert.AreSame(IslandTerrain.For(Campaign.Count - 1), IslandTerrain.For(99));
        }

        [Test] public void TheTownPlatformIsFlatAndWalkableEverywhere()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                foreach (var p in new[] { new Vector2(-10, -10), new Vector2(-30, 20), new Vector2(0, 0), new Vector2(5.7f, -22.9f), new Vector2(-33.9f, 22.9f) })
                {
                    Assert.AreEqual(IslandTerrain.PlatformHeight, terrain.Height(p), .0001f, $"island {i} at {p}");
                    Assert.IsTrue(terrain.Walkable(p), $"island {i} at {p}");
                    Assert.IsFalse(terrain.DeepEnoughToSwim(p));
                }
            }
        }

        [Test] public void TheEastShoreStopsBeforeTheShip()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                for (float y = -60; y <= 60; y += 5)
                    for (float x = 6.2f; x < 12; x += 1.5f)
                        Assert.Less(terrain.CoastDistance(new Vector2(x, y)), 0, $"island {i} has land at ({x},{y}), where the ship lies");
            }
        }

        [Test] public void TheQuayEdgeIsACliffNotAWayIntoTheWater()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                Assert.IsTrue(terrain.Walkable(new Vector2(5.7f, -6f)));
                Assert.IsFalse(terrain.Walkable(new Vector2(6.3f, -6f)), "nothing to walk on past the quay");
            }
        }

        [Test] public void EveryIslandHasLandToExploreBeyondTheTown()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                int dry = 0, tried = 0;
                for (float x = terrain.Bounds.xMin; x < terrain.Bounds.xMax; x += 4)
                    for (float y = terrain.Bounds.yMin; y < terrain.Bounds.yMax; y += 4)
                    {
                        var p = new Vector2(x, y);
                        if (IslandTerrain.OnPlatform(p)) continue;
                        tried++;
                        if (terrain.Walkable(p) && terrain.Height(p) > IslandTerrain.SeaLevel + .3f) dry++;
                    }
                Assert.Greater(dry * 16, 4000, $"island {i}: only {dry} of {tried} samples are dry land, about {dry * 16} m2");
            }
        }

        [Test] public void BeachesSlopeIntoTheSeaWithoutAWall()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                // Walk out from the island's centre along several bearings: the ground falls through the waterline to the floor.
                for (int bearing = 0; bearing < 8; bearing++)
                {
                    float angle = bearing * Mathf.PI / 4 + 2.4f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    if (dir.x > .3f) continue;                          // east is the quay side
                    bool crossed = false, wasWalkable = true;
                    float last = float.MaxValue;
                    for (float r = 4; r < terrain.Radius * 1.6f; r += .5f)
                    {
                        var p = terrain.Center + dir * r;
                        float h = terrain.Height(p);
                        Assert.LessOrEqual(h, last + 1.5f, "no sudden rise out at sea");
                        if (h < IslandTerrain.SeaLevel) crossed = true;
                        if (crossed && h > IslandTerrain.SeaLevel + 1f) Assert.Fail("another island out at sea");
                        wasWalkable = terrain.Walkable(p);
                        last = h;
                    }
                    Assert.IsTrue(crossed, $"island {i} bearing {bearing} never reaches the sea");
                    Assert.IsFalse(wasWalkable, "the far end is deep water");
                }
            }
        }

        [Test] public void WadingIsWalkableAndDeepWaterMakesAWalkerSwim()
        {
            var terrain = IslandTerrain.For(CampaignIsland());
            // Find the waterline to the west of the island's centre.
            var dir = Vector2.left;
            float shore = 0;
            for (float r = 0; r < terrain.Radius * 1.5f; r += .1f)
                if (terrain.Height(terrain.Center + dir * r) < IslandTerrain.SeaLevel) { shore = r; break; }
            Assert.Greater(shore, 10f);
            var wading = terrain.Center + dir * (shore + .4f);
            Assert.IsTrue(terrain.Walkable(wading), "ankle deep");
            Assert.IsFalse(terrain.DeepEnoughToSwim(wading));
            Vector2 deep = wading;
            for (float r = shore; r < shore + 30; r += .1f)
            {
                var p = terrain.Center + dir * r;
                if (terrain.DeepEnoughToSwim(p)) { deep = p; break; }
            }
            Assert.AreNotEqual(wading, deep);
            Assert.IsTrue(terrain.DeepEnoughToSwim(deep));
            Assert.IsFalse(terrain.ShallowEnoughToStand(deep));
            Assert.IsTrue(terrain.ShallowEnoughToStand(wading));
        }

        private static int CampaignIsland() => Campaign.CoralCay;

        [Test] public void CliffsAreNotWalkable()
        {
            var terrain = IslandTerrain.For(Campaign.SkullAtoll);
            int steep = 0, walked = 0;
            for (float x = terrain.Bounds.xMin; x < terrain.Bounds.xMax; x += 2)
                for (float y = terrain.Bounds.yMin; y < terrain.Bounds.yMax; y += 2)
                {
                    var p = new Vector2(x, y);
                    if (IslandTerrain.OnPlatform(p) || terrain.Height(p) < IslandTerrain.SeaLevel) continue;
                    if (terrain.Slope(p) > IslandTerrain.MaxWalkSlope) { steep++; Assert.IsFalse(terrain.Walkable(p)); }
                    else walked++;
                }
            Assert.Greater(walked, 100);
        }

        [Test] public void ThemesShapeTheHills()
        {
            Assert.Less(IslandTerrain.For(Campaign.CoralCay).HillHeight, IslandTerrain.For(Campaign.NorthHaven).HillHeight);
            Assert.Greater(IslandTerrain.For(Campaign.SkullAtoll).HillHeight, IslandTerrain.For(Campaign.SmugglersCove).HillHeight);
        }

        [Test] public void ThingsToFindAreOnDryWalkableGroundAwayFromTheTown()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                foreach (var spots in new[] { terrain.DigSpots, terrain.ChestSpots, terrain.FishingSpots })
                    foreach (var p in spots)
                    {
                        Assert.IsTrue(terrain.Walkable(p), $"island {i} {p}");
                        Assert.IsFalse(IslandTerrain.OnPlatform(p));
                        Assert.Greater(terrain.Height(p), IslandTerrain.SeaLevel, $"island {i} {p} is under water");
                    }
                foreach (var p in terrain.ChestSpots) Assert.Greater(-IslandTerrain.PlatformInside(p), 23f, "chests are far from the town");
            }
        }

        [Test] public void EachThemeOffersItsOwnThings()
        {
            Assert.AreEqual(0, IslandTerrain.For(Campaign.CoralCay).ChestSpots.Length);
            Assert.Greater(IslandTerrain.For(Campaign.CoralCay).FishingSpots.Length, IslandTerrain.For(Campaign.SmugglersCove).FishingSpots.Length);
            Assert.Greater(IslandTerrain.For(Campaign.SmugglersCove).ChestSpots.Length, IslandTerrain.For(Campaign.NorthHaven).ChestSpots.Length);
            Assert.AreEqual(0, IslandTerrain.For(Campaign.StormReef).FishingSpots.Length, "too rough to fish");
            Assert.Greater(IslandTerrain.For(Campaign.SkullAtoll).DigSpots.Length, IslandTerrain.For(Campaign.StormReef).DigSpots.Length);
        }

        [Test] public void SpotsKeepTheirDistance()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                var terrain = IslandTerrain.For(i);
                var all = new System.Collections.Generic.List<Vector2>();
                all.AddRange(terrain.DigSpots); all.AddRange(terrain.ChestSpots); all.AddRange(terrain.FishingSpots);
                for (int a = 0; a < all.Count; a++)
                    for (int b = a + 1; b < all.Count; b++)
                        Assert.Greater(Vector2.Distance(all[a], all[b]), 8f, $"island {i}: spots {a} and {b} are too close");
            }
        }
    }
}
