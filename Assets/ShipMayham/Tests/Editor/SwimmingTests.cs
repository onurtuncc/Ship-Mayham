using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class SwimmingTests
    {
        private const ulong Host = 10, Friend = 11;
        private VoyageSimulation sim;
        private bool walkingBefore;

        [SetUp]
        public void SetUp()
        {
            walkingBefore = VoyageSimulation.WalkingPassengers;
            VoyageSimulation.WalkingPassengers = false;
            sim = NewSim();
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        private static VoyageSimulation NewSim()
        {
            var created = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(4.5f, 3f)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            }, true);
            created.Join(Host);
            created.Join(Friend);
            return created;
        }

        private void Tick(float seconds, Vector2 input = default, ulong client = Host)
        {
            for (float t = 0; t < seconds; t += .02f) { sim.SetInput(client, input); sim.Tick(.02f); }
        }

        private void GoTo(int slot, Vector2 position) { sim.Crew[slot].Position = position; Tick(.4f); }

        private void Dive(int ladder = 0, ulong client = Host)
        {
            int slot = sim.FindCrew(client);
            GoTo(slot, VoyageSimulation.Ladders[ladder].Top);
            Assert.That(sim.LadderCommand(client, ladder), Does.StartWith("Splash"));
            Assert.IsTrue(sim.Crew[slot].Swimming);
        }

        // ------------------------------------------------------------ diving in and climbing out

        [Test]
        public void ADiveStartsAtTheFootOfTheLadder()
        {
            Dive(0);
            Assert.AreEqual(VoyageSimulation.Ladders[0].Base, sim.Crew[0].Position);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.AreEqual(1, sim.CrewInWater);
        }

        [Test]
        public void AClimbBringsThemBackOntoTheDeck()
        {
            Dive(1);
            Tick(.8f);
            Assert.That(sim.LadderCommand(Host, 1), Does.StartWith("Up you go"));
            Assert.IsFalse(sim.Crew[0].Swimming);
            Assert.AreEqual(VoyageSimulation.Ladders[1].Top, sim.Crew[0].Position);
            Assert.AreEqual(0, sim.CrewInWater);
        }

        [Test]
        public void ClimbingNeedsAMatchingLadderWithinReach()
        {
            Dive(0);
            Tick(.8f);
            sim.Crew[0].Position = VoyageSimulation.Ladders[0].Base + new Vector2(-VoyageSimulation.LadderReach - .5f, 0);
            Assert.That(sim.LadderCommand(Host, 0), Does.Contain("closer"));
            Assert.IsTrue(sim.Crew[0].Swimming);
            Assert.That(sim.LadderCommand(Host, 99), Does.Contain("Unknown"));
        }

        [Test]
        public void TheQuayLadderLeadsAshoreAndBack()
        {
            sim.Crew[0].OnShore = true;
            sim.Crew[0].Position = VoyageSimulation.Ladders[3].Top;
            Tick(.4f);
            Assert.That(sim.LadderCommand(Host, 3), Does.StartWith("Splash"));
            Assert.IsFalse(sim.Crew[0].OnShore, "swimmers live in the water around the ship");
            Tick(.8f);
            Assert.That(sim.LadderCommand(Host, 3), Does.StartWith("Up you go"));
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.AreEqual(VoyageSimulation.Ladders[3].Top, sim.Crew[0].Position);
        }

        [Test]
        public void ALadderOnTheOtherSideIsNotReachable()
        {
            GoTo(0, VoyageSimulation.Ladders[0].Top);
            Assert.That(sim.LadderCommand(Host, 3), Does.Contain("other side"));
            Assert.IsFalse(sim.Crew[0].Swimming);
        }

        [Test]
        public void NobodyDivesWhileTheShipIsUnderWayOrWithFullHands()
        {
            GoTo(0, VoyageSimulation.Ladders[0].Top);
            sim.Crew[0].Carrying = ServiceItem.Food;
            Assert.That(sim.LadderCommand(Host, 0), Does.Contain("Put your item"));
            sim.Crew[0].Carrying = ServiceItem.None;
            sim.State.Phase = VoyagePhase.Sailing;
            Assert.That(sim.LadderCommand(Host, 0), Does.Contain("under way"));
            Assert.IsFalse(sim.Crew[0].Swimming);
        }

        [Test]
        public void AStationMustBeReleasedBeforeDiving()
        {
            GoTo(0, VoyageSimulation.Ladders[0].Top);
            sim.Crew[0].AtHelm = true;
            Assert.That(sim.LadderCommand(Host, 0), Does.Contain("Release your station"));
        }

        [Test]
        public void TheLadderCommandCoolsDownSoAClickCannotDiveAndClimbInOneFrame()
        {
            GoTo(0, VoyageSimulation.Ladders[0].Top);
            Assert.That(sim.LadderCommand(Host, 0), Does.StartWith("Splash"));
            Assert.AreEqual(string.Empty, sim.LadderCommand(Host, 0));
            Assert.IsTrue(sim.Crew[0].Swimming);
            Tick(VoyageSimulation.LadderCooldownSeconds + .1f);
            Assert.That(sim.LadderCommand(Host, 0), Does.StartWith("Up you go"));
        }

        // ------------------------------------------------------------ moving in the water

        [Test]
        public void SwimmingIsSlowerThanWalking()
        {
            Dive(2);
            Vector2 start = sim.Crew[0].Position;
            Tick(1f, Vector2.left);
            float swum = Vector2.Distance(start, sim.Crew[0].Position);
            Assert.AreEqual(CrewKinematics.WalkSpeed * VoyageSimulation.SwimSpeedFactor, swum, .6f);
            Assert.Less(swum, CrewKinematics.WalkSpeed * .8f);
            Assert.Greater(swum, 1f);
        }

        [Test]
        public void SwimmersCannotEnterTheHullOrLeaveTheBay()
        {
            Dive(0);
            Tick(3f, Vector2.right);                                   // straight at the hull from the port side
            Assert.LessOrEqual(sim.Crew[0].Position.x, -VoyageSimulation.HullHalfExtents.x + .01f);
            Dive(2, Friend);
            Tick(6f, Vector2.down, Friend);
            Assert.GreaterOrEqual(sim.Crew[1].Position.y, VoyageSimulation.WaterMin.y - .01f);
            Assert.IsTrue(VoyageSimulation.WaterWalkable(sim.Crew[0].Position));
        }

        [Test]
        public void SwimmersCannotJump()
        {
            Dive(2);
            sim.Jump(Host);
            Tick(.5f);
            Assert.AreEqual(0f, sim.Crew[0].JumpHeight);
        }

        [Test]
        public void ASwimmerCannotUseStationsPassengersOrTheHarbor()
        {
            Dive(1);
            sim.Crew[0].Position = new Vector2(5.9f, 3f);               // as close to the food counter as the water allows
            Tick(.5f);
            Assert.That(sim.Interact(Host, 2), Does.Contain("in the water"));
            Assert.That(sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0), Does.Contain("in the water"));
            Assert.That(sim.HarborCommand(Host, HarborAction.GoAshore), Does.Contain("in the water"));
            Assert.That(sim.BuyUpgrade(Host, StationKind.EngineUpgrade), Does.Contain("Climb out"));
            Assert.That(sim.DeckCommand(Host, DeckAction.CleanSpill), Does.Contain("empty hands"));
        }

        [Test]
        public void TheGangwayGateIgnoresSwimmers()
        {
            Dive(0);
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(-1f, 0);
            Tick(1f, Vector2.left);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.IsFalse(sim.GateIsOpenFor(0));
        }

        // ------------------------------------------------------------ the ship cannot leave swimmers behind

        [Test]
        public void TheShipWaitsForSwimmersBeforeDeparting()
        {
            sim.Crew[1].Position = new Vector2(-3, -3.5f);
            Tick(.4f);
            sim.Interact(Friend, 0);
            Tick(.4f);
            Dive(0);
            sim.Crew[1].Position = sim.Stations[1].Position + Vector2.right;
            Tick(.4f);
            sim.Interact(Friend, 1);
            Assert.That(sim.HelmAction(Friend), Does.Contain("still swimming"));
            Tick(.8f);
            sim.LadderCommand(Host, 0);
            Assert.That(sim.HelmAction(Friend), Does.Not.Contain("swimming"));
        }

        // ------------------------------------------------------------ washed overboard

        private void SailWithWave(float bearing)
        {
            sim.State.Route = RouteProfile.StormPassage;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Tick(.05f);
            sim.State.Heading = Mathf.Repeat(bearing + 180f, 360f);    // stern to the wave: it hits and does damage
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            Assert.AreEqual(WavePhase.Warning, sim.Hazards.Wave);
            sim.Hazards.WaveBearing = bearing;
        }

        [Test]
        public void ABroadsideWaveWashesTheCrewAtTheLeeRailOverboard()
        {
            SailWithWave(90f);                                         // from the east, so it runs west
            sim.State.Heading = 0;                                     // bow north: the wave hits the starboard side and runs toward port
            sim.Crew[0].Position = new Vector2(-4.6f, 0);              // at the port (lee) rail
            sim.Crew[1].Position = new Vector2(0, 0);                  // safe in the middle
            for (float t = 0; t < HazardLogic.WaveWarningSeconds + .3f; t += .1f) sim.Tick(.1f);
            Assert.IsTrue(sim.Crew[0].Swimming, "swept off the lee rail");
            Assert.IsFalse(sim.Crew[1].Swimming, "in the middle of the deck");
            Assert.IsTrue(VoyageSimulation.WaterWalkable(sim.Crew[0].Position));
            Assert.Greater(sim.OverboardRemaining(0), 25f);
        }

        [Test]
        public void HoldingTheHelmOrFacingTheWaveKeepsEveryoneAboard()
        {
            SailWithWave(0f);
            sim.State.Heading = 0;                                     // facing the wave: dodged, nothing happens
            sim.Crew[0].Position = new Vector2(4.6f, 0);
            for (float t = 0; t < HazardLogic.WaveWarningSeconds + .3f; t += .1f) sim.Tick(.1f);
            Assert.IsFalse(sim.Crew[0].Swimming);

            sim = NewSim();
            SailWithWave(90f);
            sim.State.Heading = 0;
            sim.Crew[0].Position = new Vector2(-4.6f, 6.8f);
            sim.Crew[0].AtHelm = true;
            for (float t = 0; t < HazardLogic.WaveWarningSeconds + .3f; t += .1f) sim.Tick(.1f);
            Assert.IsFalse(sim.Crew[0].Swimming, "gripping the wheel");
        }

        [Test]
        public void AnOverboardCrewMemberIsHauledAboardWhenTheTimeRunsOut()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Tick(.05f);
            sim.State.Wallet = 50;
            sim.Crew[0].Position = new Vector2(-4.6f, 0);
            var washOverboard = typeof(VoyageSimulation).GetMethod("WashOverboard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            washOverboard.Invoke(sim, new object[] { 0, Vector2.left });
            Assert.IsTrue(sim.Crew[0].Swimming);
            for (float t = 0; t < VoyageSimulation.OverboardLimitSeconds + 1f; t += .1f) sim.Tick(.1f);
            Assert.IsFalse(sim.Crew[0].Swimming);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.AreEqual(50 - VoyageSimulation.HaulAboardFine, sim.State.Wallet);
            Assert.AreEqual(0f, sim.OverboardRemaining(0));
        }

        [Test]
        public void ClimbingOutInTimeCancelsTheHaulAndTheFine()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Tick(.05f);
            sim.State.Wallet = 50;
            var washOverboard = typeof(VoyageSimulation).GetMethod("WashOverboard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sim.Crew[0].Position = new Vector2(-4.6f, 0);
            washOverboard.Invoke(sim, new object[] { 0, Vector2.left });
            sim.State.Phase = VoyagePhase.Boarding;                    // at sea nobody may dive, but anybody may climb; use port for the command
            sim.Crew[0].Position = VoyageSimulation.Ladders[0].Base;
            Tick(.1f);
            Assert.That(sim.LadderCommand(Host, 0), Does.StartWith("Up you go"));
            Tick(VoyageSimulation.OverboardLimitSeconds + 1f);
            Assert.AreEqual(50, sim.State.Wallet);
        }

        [Test]
        public void UnstuckPutsASwimmerBackOnTheDeck()
        {
            Dive(0);
            Tick(.5f);
            sim.Unstuck(Host);
            Assert.IsFalse(sim.Crew[0].Swimming);
            Assert.IsFalse(sim.Crew[0].OnShore);
        }

        [Test]
        public void ANewVoyageStartsEveryoneDry()
        {
            Dive(0);
            sim.State.Phase = VoyagePhase.Failed;
            Tick(.2f);
            sim.Crew[1].Position = new Vector2(-3, -3.5f);
            Tick(.4f);
            sim.Interact(Friend, 0);
            Assert.AreEqual(0, sim.CrewInWater);
        }

        // ------------------------------------------------------------ network and migration

        [Test]
        public void TheSwimmingFlagTravelsWithTheCrewState()
        {
            var state = new CrewState { Active = true, Swimming = true, Position = new Vector2(1, 2) };
            using (var writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(state);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out CrewState copy);
                    Assert.IsTrue(copy.Swimming);
                    Assert.AreEqual(state, copy);
                    Assert.AreNotEqual(new CrewState { Active = true, Position = new Vector2(1, 2) }, copy);
                }
            }
        }

        [Test]
        public void MigrationKeepsASwimmerInTheWater()
        {
            Dive(2);
            byte[] data = sim.ExportMigrationData();
            var restored = NewSim();
            restored.ImportMigrationData(data);
            Assert.IsTrue(restored.Crew[0].Swimming);
            Assert.AreEqual(VoyageSimulation.Ladders[2].Base, restored.Crew[0].Position);
        }

        // ------------------------------------------------------------ prompts and poses

        [Test]
        public void LadderPromptsExplainWhatHappensOrWhyNot()
        {
            var ladder = VoyageSimulation.Ladders[0];
            Assert.AreEqual("Dive in and swim", PromptCatalog.ForLadder(ladder, default, true).Action);
            Assert.AreEqual(PromptTone.Blocked, PromptCatalog.ForLadder(ladder, default, false).Tone);
            Assert.AreEqual(PromptTone.Blocked, PromptCatalog.ForLadder(ladder, new CrewState { Carrying = ServiceItem.Food }, true).Tone);
            Assert.AreEqual("Climb up", PromptCatalog.ForLadder(ladder, new CrewState { Swimming = true }, false).Action);
            Assert.AreEqual("Overboard!", PromptCatalog.ForSwimming(true).Title);
            Assert.AreEqual("Swimming", PromptCatalog.ForSwimming(false).Title);
        }

        [Test]
        public void SwimmersUseSwimmingPosesInsteadOfWalking()
        {
            var swimmer = new CrewState { Active = true, Swimming = true };
            Assert.AreEqual(CrewPose.Tread, CrewPresentation.PoseFor(swimmer, 0f));
            Assert.AreEqual(CrewPose.Swim, CrewPresentation.PoseFor(swimmer, 2f));
            Assert.AreEqual(CrewPose.Swim, CrewPresentation.PoseFor(swimmer, 2f, VoyagePhase.Arrived), "no victory dance in the water");
        }

        [Test]
        public void EveryLadderIsReachableFromItsOwnSide()
        {
            foreach (var ladder in VoyageSimulation.Ladders)
            {
                Assert.IsTrue(VoyageSimulation.WaterWalkable(ladder.Base), ladder.Name + " ends in open water");
                Assert.GreaterOrEqual(ladder.Base.x, VoyageSimulation.WaterMin.x);
                Assert.LessOrEqual(ladder.Base.x, VoyageSimulation.WaterMax.x);
                Assert.That(ladder.Base.y, Is.InRange(VoyageSimulation.WaterMin.y, VoyageSimulation.WaterMax.y));
            }
            Assert.IsTrue(VoyageSimulation.Ladders[3].OnQuay);
            Assert.IsFalse(VoyageSimulation.Ladders[0].OnQuay);
        }
    }
}
