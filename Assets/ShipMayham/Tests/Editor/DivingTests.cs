using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>Diving, breath, the wide round swim zone and coins on the seabed.</summary>
    public sealed class DivingTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;
        private bool walkingBefore;
        private uint seq;
        private readonly List<string> notices = new List<string>();

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
            notices.Clear();
            sim.CrewNotice += (slot, text) => notices.Add(text);
            sim.Crew[0].Swimming = true;
            sim.Crew[0].Position = new Vector2(-6.6f, 3f);
            seq = 0;
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        /// <summary>Sends <paramref name="seconds"/> of move commands (30 a second) through the same path a client uses.</summary>
        private void Swim(float seconds, Vector2 input = default, bool dive = false)
        {
            int steps = Mathf.RoundToInt(seconds / CrewLocomotion.StepSeconds);
            for (int k = 0; k < steps; k++)
            {
                var packet = MoveCommandPacket.Pack(new[] { MoveCommand.Create(++seq, input, false, dive) });
                sim.ApplyMoveCommands(Host, packet);
                sim.Tick(CrewLocomotion.StepSeconds);
            }
        }

        [Test] public void HoldingDiveSinksAndLettingGoFloatsUp()
        {
            Swim(1f, dive: true);
            Assert.AreEqual(VoyageSimulation.DiveSpeed, sim.Crew[0].Depth, .15f);
            float down = sim.Crew[0].Depth;
            Swim(.5f);
            Assert.Less(sim.Crew[0].Depth, down, "released: floats up");
            Swim(2f);
            Assert.AreEqual(0, sim.Crew[0].Depth, .001f);
        }

        [Test] public void TheBayFloorStopsTheDescent()
        {
            Swim(8f, dive: true);
            Assert.AreEqual(VoyageSimulation.BaySeabedDepth, sim.Crew[0].Depth, .001f);
        }

        [Test] public void ChooseTheFloorByWhereTheShipIs()
        {
            Assert.AreEqual(VoyageSimulation.BaySeabedDepth, sim.SeabedDepthAt(Vector2.zero));
            sim.State.Phase = VoyagePhase.Sailing;
            Assert.AreEqual(VoyageSimulation.OpenSeaDepth, sim.SeabedDepthAt(Vector2.zero));
        }

        [Test] public void BreathIsUsedUnderWaterAndRecoveredAtTheSurface()
        {
            Swim(3f, dive: true);
            Assert.Greater(sim.Crew[0].BreathUsed, 1f);
            Assert.Less(VoyageSimulation.BreathLeft(sim.Crew[0]), VoyageSimulation.BreathSeconds);
            Assert.IsTrue(VoyageSimulation.Submerged(sim.Crew[0]));
            Swim(4f);
            Assert.AreEqual(0, sim.Crew[0].BreathUsed, .001f);
            Assert.IsFalse(VoyageSimulation.Submerged(sim.Crew[0]));
        }

        [Test] public void RunningOutOfBreathForcesAnAscentAndAWarning()
        {
            Swim(VoyageSimulation.BreathSeconds + 1f, dive: true);
            Assert.AreEqual(1, notices.FindAll(n => n.StartsWith("Out of breath")).Count);
            Assert.Less(sim.Crew[0].Depth, VoyageSimulation.BaySeabedDepth, "pushed up although the key is still held");
            Swim(3f, dive: true);
            Assert.Less(sim.Crew[0].Depth, VoyageSimulation.SurfaceBand, "still gasping at the surface, cannot dive again at once");
        }

        [Test] public void AfterGaspingTheSwimmerCanDiveAgain()
        {
            Swim(VoyageSimulation.BreathSeconds + 1f, dive: true);
            Swim(12f);
            Assert.AreEqual(0, sim.Crew[0].BreathUsed, .001f);
            Swim(1f, dive: true);
            Assert.Greater(sim.Crew[0].Depth, 1f);
        }

        [Test] public void LadderRefusesAClimbFromDeepWater()
        {
            sim.Crew[0].Position = VoyageSimulation.Ladders[0].Base;
            Swim(2f, dive: true);
            Assert.That(sim.LadderCommand(Host, 0), Does.Contain("surface"));
            Assert.IsTrue(sim.Crew[0].Swimming);
            Swim(3f);
            Assert.That(sim.LadderCommand(Host, 0), Does.StartWith("Up you go"));
            Assert.AreEqual(0, sim.Crew[0].Depth);
            Assert.AreEqual(0, sim.Crew[0].BreathUsed);
        }

        [Test] public void ClimbingOutAndBeingHauledAboardResetTheDive()
        {
            Swim(3f, dive: true);
            sim.Unstuck(Host);
            Assert.AreEqual(0, sim.Crew[0].Depth);
            Assert.AreEqual(0, sim.Crew[0].BreathUsed);
        }

        [Test] public void CoinsOnlyCountWhenTheSwimmerIsDownAtThem()
        {
            Vector2 spot = VoyageSimulation.CoinSpots[0];
            int wallet = sim.State.Wallet;
            sim.Crew[0].Position = spot;
            Swim(.3f);
            Assert.AreEqual(wallet, sim.State.Wallet, "paddling over it at the surface is not enough");
            sim.Crew[0].Depth = VoyageSimulation.CoinDepth(0);
            sim.Crew[0].Position = spot;
            Swim(.1f);
            Assert.AreEqual(wallet + VoyageSimulation.CoinValue, sim.State.Wallet);
        }

        [Test] public void CoinsLieBetweenTheSurfaceAndTheFloor()
        {
            for (int i = 0; i < VoyageSimulation.CoinSpots.Length; i++)
            {
                Assert.Greater(VoyageSimulation.CoinDepth(i), 2f);
                Assert.Less(VoyageSimulation.CoinDepth(i), VoyageSimulation.BaySeabedDepth);
            }
        }

        [Test] public void TheSwimZoneIsWideRoundAndLeavesOutHullAndQuay()
        {
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(0, 60)));
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(70, 0)));
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(-60, 60)));
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(0, 220)), "out at the next island's waters, with nothing to stop the swim");
            Assert.IsFalse(VoyageSimulation.WaterWalkable(new Vector2(VoyageSimulation.SwimZoneRadius + 1, 0)));
            Assert.IsFalse(VoyageSimulation.WaterWalkable(new Vector2(0, 0)), "inside the hull");
            Assert.IsFalse(VoyageSimulation.WaterWalkable(new Vector2(-30, 0)), "under the quay");
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(-30, 30)), "beside the quay");
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(-60, 0)), "beyond the quay");
        }

        [Test] public void ASwimmerCanSwimOffAsFarAsTheNextIsland()
        {
            sim.Crew[0].Position = new Vector2(8f, 12f);
            Swim(60f, Vector2.up);
            Assert.Greater(sim.Crew[0].Position.y, 100f, "free water far from the ship, no buoys and no walls");
        }

        [Test] public void TheNeighbouringIslandIsSolidForSwimmers()
        {
            // The destination island lies 220 m north: its platform and beach are land, the water beside them is not.
            Assert.IsFalse(VoyageSimulation.WaterWalkable(new Vector2(-20, 220), Campaign.BrassQuay, Campaign.NorthHaven), "on its town platform");
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(60, 220), Campaign.BrassQuay, Campaign.NorthHaven), "in the channel beside it");
            Assert.IsTrue(VoyageSimulation.WaterWalkable(new Vector2(-20, 220), Campaign.BrassQuay), "without the neighbour it is open water");
        }

        [Test] public void EveryLadderBaseIsOpenWater()
        {
            foreach (var ladder in VoyageSimulation.Ladders) Assert.IsTrue(VoyageSimulation.WaterWalkable(ladder.Base), ladder.Name);
        }

        [Test] public void TheDiveFlagTravelsInTheMoveCommandPacket()
        {
            var packet = MoveCommandPacket.Pack(new[] { MoveCommand.Create(1, Vector2.up, false, true), MoveCommand.Create(2, Vector2.up, true, false) });
            Assert.IsTrue(packet.At(0).Dive); Assert.IsFalse(packet.At(0).Jump);
            Assert.IsFalse(packet.At(1).Dive); Assert.IsTrue(packet.At(1).Jump);
            Assert.AreNotEqual(MoveCommand.Create(1, Vector2.up, false, true), MoveCommand.Create(1, Vector2.up, false, false));
        }

        [Test] public void DepthAndBreathAreReplicatedWithTheCrewState()
        {
            var a = new CrewState { Active = true, Swimming = true, Depth = 2f, BreathUsed = 5f };
            var b = a; b.Depth = 2.5f;
            Assert.IsFalse(a.Equals(b));
            b = a; b.BreathUsed = 6f;
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a.Equals(a));
        }

        [Test] public void ADiverIsNotWashedAwayByTheFarCurrentButStaysSwimmingAfterAMiniGameCheck()
        {
            sim.Crew[0].Depth = 3f;
            Swim(.5f);
            Assert.IsTrue(sim.Crew[0].Swimming);
            Assert.Less(sim.Crew[0].Depth, 3f, "a swimmer who is not holding the key floats up");
        }
    }
}
