using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>Some passengers swim to a ladder when they land in the sea; the others float, flail and wait to be hauled out.</summary>
    public sealed class PassengerSwimTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;
        private bool walkingBefore;

        [SetUp]
        public void SetUp()
        {
            walkingBefore = VoyageSimulation.WalkingPassengers;
            VoyageSimulation.WalkingPassengers = false;
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            });
            sim.Join(Host);
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Advance(.4f);
            sim.Interact(Host, 0);
            Advance(.4f);
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        private void Advance(float seconds)
        {
            for (int n = 0; n < Mathf.CeilToInt(seconds / .02f); n++) sim.Tick(.02f);
        }

        private void ThrowOverboard(int passenger, bool canSwim)
        {
            sim.Passengers[passenger].CanSwim = canSwim;
            var p = sim.Passengers[passenger];
            sim.Crew[0].Position = new Vector2(p.Position.x - 1, p.Position.z);
            Advance(.4f);
            sim.PassengerCommand(Host, passenger, PassengerAction.CarryOrDrop, 0);
            sim.Crew[0].Position = new Vector2(-5, -1.5f);
            Advance(.4f);
            sim.PassengerCommand(Host, -1, PassengerAction.Throw, -90);
            while (sim.Passengers[passenger].Motion != PassengerMotion.Rescuing) Advance(.02f);
        }

        // ------------------------------------------------------------ who swims

        [Test]
        public void AboutSevenInTenPassengersSwimAndItChangesWithTheVoyage()
        {
            int swimmers = 0, total = 0;
            bool changes = false;
            for (int voyage = 1; voyage <= 25; voyage++)
                for (int passenger = 0; passenger < VoyageSimulation.MaxPassengers; passenger++)
                {
                    bool swims = VoyageSimulation.PassengerSwims(passenger, voyage);
                    Assert.AreEqual(swims, VoyageSimulation.PassengerSwims(passenger, voyage), "deterministic");
                    if (swims) swimmers++;
                    total++;
                    if (voyage > 1 && swims != VoyageSimulation.PassengerSwims(passenger, 1)) changes = true;
                }
            Assert.That(swimmers / (float)total, Is.InRange(.6f, .8f));
            Assert.IsTrue(changes);
        }

        [Test]
        public void EveryBoardingHasBothKindsAboardSoTheDifferenceMatters()
        {
            int swimmers = 0;
            for (int p = 0; p < sim.State.Boarded; p++) if (sim.Passengers[p].CanSwim) swimmers++;
            Assert.AreEqual(swimmers, CountSwimmers(sim.State.Voyage, sim.State.Boarded));
            Assert.Greater(sim.State.Boarded, 0);
        }

        private static int CountSwimmers(int voyage, int boarded)
        {
            int count = 0;
            for (int p = 0; p < boarded; p++) if (VoyageSimulation.PassengerSwims(p, voyage)) count++;
            return count;
        }

        // ------------------------------------------------------------ a swimmer

        [Test]
        public void ASwimmerPaddlesToTheNearestLadderAndClimbsOut()
        {
            ThrowOverboard(0, true);
            var p = sim.Passengers[0];
            Assert.AreEqual(VoyageSimulation.SeaLevel + VoyageSimulation.PassengerSwimDepth, p.Position.y, .001f);
            Vector2 start = new Vector2(p.Position.x, p.Position.z);
            int ladder = VoyageSimulation.NearestShipLadder(start);
            Assert.AreEqual(0, ladder, "thrown to port: the port ladder is nearest");
            float swimTime = VoyageSimulation.SwimBackSeconds(start);
            Assert.Less(swimTime, VoyageSimulation.PassengerNonSwimmerWaitSeconds + .01f);
            Advance(.6f);
            Vector2 later = new Vector2(sim.Passengers[0].Position.x, sim.Passengers[0].Position.z);
            Assert.Less(Vector2.Distance(later, VoyageSimulation.Ladders[ladder].Base), Vector2.Distance(start, VoyageSimulation.Ladders[ladder].Base), "closer to the ladder");
            Assert.Greater(sim.Passengers[0].Velocity.magnitude, .5f, "swimming has a velocity the body faces");
            Assert.AreEqual(PassengerMotion.Rescuing, sim.Passengers[0].Motion);
            Advance(swimTime + .2f);
            Assert.AreEqual(PassengerMotion.Dizzy, sim.Passengers[0].Motion, "pulled themselves out");
            Assert.LessOrEqual(Mathf.Abs(sim.Passengers[0].Position.x), VoyageSimulation.DeckBounds.x + .01f);
            Assert.Greater(sim.Passengers[0].Position.y, 0f, "back on the deck");
            Advance(VoyageSimulation.DizzySeconds + VoyageSimulation.ReturnTimeoutSeconds);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.AreEqual(1, sim.State.RescueCount);
        }

        [Test]
        public void ANonSwimmerFloatsInPlaceForTheFullWaitAndIsHauledOut()
        {
            ThrowOverboard(1, false);
            Vector3 where = sim.Passengers[1].Position;
            Advance(4f);
            Assert.AreEqual(where.x, sim.Passengers[1].Position.x, .001f);
            Assert.AreEqual(where.z, sim.Passengers[1].Position.z, .001f);
            Assert.AreEqual(0f, sim.Passengers[1].Velocity.magnitude, .001f);
            Assert.AreEqual(PassengerMotion.Rescuing, sim.Passengers[1].Motion);
            Advance(VoyageSimulation.PassengerNonSwimmerWaitSeconds - 3.8f);
            Assert.AreEqual(PassengerMotion.Dizzy, sim.Passengers[1].Motion);
        }

        [Test]
        public void SwimmersAreBackFasterThanNonSwimmersFarFromTheLadder()
        {
            var far = new Vector2(-7.5f, 2f);
            Assert.Less(VoyageSimulation.SwimBackSeconds(far), VoyageSimulation.PassengerNonSwimmerWaitSeconds);
            Assert.GreaterOrEqual(VoyageSimulation.SwimBackSeconds(far), 3f);
            Assert.AreEqual(3f, VoyageSimulation.SwimBackSeconds(VoyageSimulation.Ladders[0].Base), .001f, "never instant");
            Assert.LessOrEqual(VoyageSimulation.SwimBackSeconds(new Vector2(-30, 30)), 8f, "never longer than waiting");
        }

        [Test]
        public void ArrivalDuringTheSwimStillCountsThePassengerAsMissing()
        {
            ThrowOverboard(0, true);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = VoyageSimulation.Destination;
            sim.Crew[0].AtHelm = true;
            Advance(.4f);
            sim.HelmAction(Host);
            Assert.AreEqual(1, sim.State.LastMissing);
        }

        [Test]
        public void ASwimmerWhoCannotReachTheLadderStillComesBackWhenTimeRunsOut()
        {
            ThrowOverboard(0, true);
            sim.Passengers[0].Position = new Vector3(-60, sim.Passengers[0].Position.y, 60);     // somewhere impossible to swim from in time
            Advance(8.5f);
            Assert.AreNotEqual(PassengerMotion.Rescuing, sim.Passengers[0].Motion);
        }

        // ------------------------------------------------------------ presentation, network, migration

        [Test]
        public void SwimmersSwimOrTreadAndNonSwimmersPanic()
        {
            var swimmer = new PassengerState { Active = true, Motion = PassengerMotion.Rescuing, CanSwim = true, Velocity = new Vector3(1, 0, 0) };
            Assert.AreEqual(PassengerPose.Swim, PassengerPresentation.PoseFor(swimmer, PassengerCue.Relaxed));
            swimmer.Velocity = Vector3.zero;
            Assert.AreEqual(PassengerPose.Tread, PassengerPresentation.PoseFor(swimmer, PassengerCue.Relaxed));
            var sinker = new PassengerState { Active = true, Motion = PassengerMotion.Rescuing, CanSwim = false };
            Assert.AreEqual(PassengerPose.SwimPanic, PassengerPresentation.PoseFor(sinker, PassengerCue.Relaxed));
        }

        [Test]
        public void TheLabelTellsWhoCannotSwim()
        {
            var sinker = new PassengerState { Active = true, Motion = PassengerMotion.Rescuing, RecoveryRemaining = 5 };
            StringAssert.Contains("can't swim", PassengerPresentation.ReactionText(sinker));
            sinker.CanSwim = true;
            StringAssert.Contains("ladder", PassengerPresentation.ReactionText(sinker));
        }

        [Test]
        public void TheSwimmingFlagTravelsOverTheNetworkAndThroughMigration()
        {
            var state = new PassengerState { Active = true, CanSwim = true, Satisfaction = 50 };
            using (var writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(state);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out PassengerState copy);
                    Assert.IsTrue(copy.CanSwim);
                    Assert.AreEqual(state, copy);
                }
            }
            sim.Passengers[2].CanSwim = false;
            sim.Passengers[3].CanSwim = true;
            var restored = new VoyageSimulation(sim.Stations, sim.Seats);
            restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsFalse(restored.Passengers[2].CanSwim);
            Assert.IsTrue(restored.Passengers[3].CanSwim);
        }
    }
}
