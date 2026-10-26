using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>Walking off the town onto the island, wading into the sea, swimming back to the beach and the things to find out there.</summary>
    public sealed class IslandWalkTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;
        private bool walkingBefore;
        private uint seq;

        [SetUp]
        public void SetUp()
        {
            walkingBefore = VoyageSimulation.WalkingPassengers;
            VoyageSimulation.WalkingPassengers = false;
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
            }, new Vector2[8], true);
            sim.Join(Host);
            seq = 0;
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        private IslandTerrain Terrain => IslandTerrain.For(VoyageSimulation.DockedIsland(sim.State));

        private void Ashore(Vector2 at)
        {
            sim.Crew[0].OnShore = true; sim.Crew[0].Swimming = false;
            sim.Crew[0].Position = at;
            sim.Crew[0].Velocity = Vector2.zero;
            for (int k = 0; k < 15; k++) sim.Tick(.02f);
        }

        private void Walk(float seconds, Vector2 input)
        {
            int steps = Mathf.RoundToInt(seconds / CrewLocomotion.StepSeconds);
            for (int k = 0; k < steps; k++)
            {
                sim.ApplyMoveCommands(Host, MoveCommandPacket.Pack(new[] { MoveCommand.Create(++seq, input, false) }));
                sim.Tick(CrewLocomotion.StepSeconds);
            }
        }

        /// <summary>A walkable point of dry beach a little inland of the western waterline, and the bearing out to sea.</summary>
        private Vector2 WestBeach(out Vector2 outward)
        {
            outward = Vector2.left;
            var terrain = Terrain;
            for (float r = 0; r < terrain.Radius * 1.6f; r += .25f)
            {
                var p = terrain.Center + outward * r;
                if (terrain.Height(p) < IslandTerrain.SeaLevel + .6f) return terrain.Center + outward * (r - 2.5f);
            }
            Assert.Fail("no west coast");
            return Vector2.zero;
        }

        [Test] public void ACrewMemberWalksOffTheTownPlatformOntoTheIsland()
        {
            Ashore(new Vector2(-30f, 0f));
            Walk(6f, Vector2.left);
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.Less(sim.Crew[0].Position.x, HarborTownLayout.Bounds.xMin - 5f, "well past the platform's west edge");
            Assert.IsTrue(Terrain.Walkable(sim.Crew[0].Position));
        }

        [Test] public void TheQuayEdgeOnTheShipSideStaysAWall()
        {
            Ashore(new Vector2(0f, 10f));
            Walk(4f, Vector2.right);
            Assert.IsTrue(sim.Crew[0].OnShore, "no way into the harbor channel");
            Assert.LessOrEqual(sim.Crew[0].Position.x, HarborTownLayout.Bounds.xMax + .01f);
        }

        [Test] public void WalkingIntoDeepWaterTurnsIntoSwimming()
        {
            Vector2 beach = WestBeach(out var outward);
            Ashore(beach);
            Walk(8f, outward);
            Assert.IsTrue(sim.Crew[0].Swimming, "the sea is deep here");
            Assert.IsFalse(sim.Crew[0].OnShore);
            var harbor = sim.Crew[0].Position - new Vector2(VoyageSimulation.QuayOffsetX, 0);
            Assert.IsTrue(Terrain.DeepEnoughToSwim(harbor));
            Assert.IsTrue(VoyageSimulation.WaterWalkable(sim.Crew[0].Position, sim.State.Island));
            Assert.AreEqual(0, sim.Crew[0].Depth, .5f);
        }

        [Test] public void WadingInTheShallowsIsStillWalking()
        {
            Vector2 beach = WestBeach(out var outward);
            Ashore(beach);
            Walk(1.6f, outward);                                   // a few metres: to the waterline and a step beyond
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.IsFalse(sim.Crew[0].Swimming);
        }

        [Test] public void SwimmingBackToTheBeachStandsUpAgain()
        {
            Vector2 beach = WestBeach(out var outward);
            Ashore(beach);
            Walk(8f, outward);
            Assert.IsTrue(sim.Crew[0].Swimming);
            Walk(10f, -outward);
            Assert.IsFalse(sim.Crew[0].Swimming, "the ground is shallow enough to stand on");
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.IsTrue(Terrain.Walkable(sim.Crew[0].Position));
            Assert.AreEqual(0, sim.Crew[0].Depth);
        }

        [Test] public void SwimmersCannotSwimOntoTheIsland()
        {
            Vector2 beach = WestBeach(out var outward);
            Ashore(beach);
            Walk(8f, outward);
            Assert.IsTrue(sim.Crew[0].Swimming);
            sim.Crew[0].Position = sim.Crew[0].Position;
            Vector2 land = beach + new Vector2(30f, 0f) + new Vector2(VoyageSimulation.QuayOffsetX, 0);
            Assert.IsFalse(VoyageSimulation.WaterWalkable(land, sim.State.Island), "dry ground is not water");
        }

        [Test] public void AWalkerCarryingSomethingDoesNotWadeIntoDeepWater()
        {
            Vector2 beach = WestBeach(out var outward);
            Ashore(beach);
            sim.Crew[0].Carrying = ServiceItem.Food;
            Walk(10f, outward);
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.IsFalse(sim.Crew[0].Swimming);
            Assert.IsFalse(Terrain.DeepEnoughToSwim(sim.Crew[0].Position), "stopped at the edge of the deep");
        }

        [Test] public void TheSeaFloorFollowsTheBeach()
        {
            Vector2 beach = WestBeach(out var outward);
            var shallow = beach + outward * 5f + new Vector2(VoyageSimulation.QuayOffsetX, 0);
            var deep = beach + outward * 40f + new Vector2(VoyageSimulation.QuayOffsetX, 0);
            Assert.Less(sim.SeabedDepthAt(shallow), 3f);
            Assert.AreEqual(VoyageSimulation.BaySeabedDepth, sim.SeabedDepthAt(deep), .01f);
            Assert.AreEqual(VoyageSimulation.BaySeabedDepth, sim.SeabedDepthAt(Vector2.zero), .01f, "the channel beside the ship is deep");
        }

        [Test] public void EachIslandHasItsOwnThingsToFind()
        {
            Assert.AreEqual(VoyageSimulation.ChestSpots.Length, VoyageSimulation.SpotsOf(MiniGameKind.Lockpick, Campaign.CoralCay).Length, "none at the reef");
            Assert.AreEqual(VoyageSimulation.ChestSpots.Length + 4, VoyageSimulation.SpotsOf(MiniGameKind.Lockpick, Campaign.SmugglersCove).Length);
            Assert.Greater(VoyageSimulation.SpotsOf(MiniGameKind.Fishing, Campaign.CoralCay).Length, VoyageSimulation.SpotsOf(MiniGameKind.Fishing, Campaign.StormReef).Length);
            for (int island = 0; island < Campaign.Count; island++)
                foreach (var kind in new[] { MiniGameKind.Lockpick, MiniGameKind.Dig, MiniGameKind.Fishing })
                {
                    var spots = VoyageSimulation.SpotsOf(kind, island);
                    Assert.LessOrEqual(spots.Length, 12, "a mask has room for them");
                    var town = VoyageSimulation.SpotsOf(kind);
                    for (int i = 0; i < town.Length; i++) Assert.AreEqual(town[i], spots[i], "the town's own spots come first");
                }
        }

        [Test] public void AChestOutOnTheIslandOpensLikeAnyOther()
        {
            sim.State.Island = Campaign.SmugglersCove; sim.State.Target = Campaign.BrassQuay;
            var spots = VoyageSimulation.SpotsOf(MiniGameKind.Lockpick, Campaign.SmugglersCove);
            int far = spots.Length - 1;
            Assert.GreaterOrEqual(far, VoyageSimulation.ChestSpots.Length);
            Ashore(spots[far]);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Lockpick, far), Is.EqualTo(string.Empty));
            Assert.IsTrue(sim.InMiniGame(0));
            int wallet = sim.State.Wallet;
            for (int k = 0; k < 200; k++) sim.Tick(.02f);                // a lock takes a few seconds to pick
            Assert.That(sim.FinishMiniGame(Host, new MiniGameResult { Kind = MiniGameKind.Lockpick, Target = far, Outcome = MiniGameOutcome.Succeeded, Score = 100, ElapsedMs = 3000 }), Does.Contain("chest"));
            for (int k = 0; k < 15; k++) sim.Tick(.02f);
            Assert.Greater(sim.State.Wallet, wallet);
            Assert.AreNotEqual(0, sim.State.ChestMask & (1 << far));
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Lockpick, far), Does.Contain("already open"));
        }

        [Test] public void ACommandForASpotOfAnotherIslandIsRefused()
        {
            // The cove has far more chests than the home island, so its last one does not exist at home.
            var cove = VoyageSimulation.SpotsOf(MiniGameKind.Lockpick, Campaign.SmugglersCove);
            int far = cove.Length - 1;
            Ashore(cove[far]);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Lockpick, far), Does.Contain("nothing there"), "the home island has no such chest");
        }

        [Test] public void EveryTownSpotStillStandsOnWalkableGroundOnEveryIsland()
        {
            for (int island = 0; island < Campaign.Count; island++)
                foreach (var kind in new[] { MiniGameKind.Lockpick, MiniGameKind.Dig, MiniGameKind.Fishing })
                    foreach (var spot in VoyageSimulation.SpotsOf(kind, island))
                        Assert.IsTrue(VoyageSimulation.ShoreStandable(island, spot), $"island {island} {kind} {spot}");
        }

        [Test] public void TheShorelineHandoverLeavesTheGangwayAlone()
        {
            Ashore(new Vector2(5f, -6f));                          // the board-ship point on the quay
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.IsFalse(sim.Crew[0].Swimming);
        }
    }
}
