using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>The gangway is walked: through the gate, along the plank and onto the quay, with no button and no jump.</summary>
    public sealed class GangwayGateTests
    {
        private const ulong Host = 10;
        private const float Step = .02f;
        private VoyageSimulation sim;

        [SetUp]
        public void SetUp()
        {
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4, 0)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            }, true);
            sim.Join(Host);
        }

        /// <summary>Where the crew member stands in world x/z at the departure berth (ship at the origin, heading 0).</summary>
        private Vector2 World => sim.Crew[0].OnShore ? GangwayPassage.ShoreToShip(sim.Crew[0].Position) : sim.Crew[0].Position;

        /// <summary>Walks and returns the largest distance the body moved in one step.</summary>
        private float Walk(Vector2 input, float seconds)
        {
            float largest = 0;
            for (float t = 0; t < seconds; t += Step)
            {
                Vector2 before = World;
                sim.SetInput(Host, input);
                sim.Tick(Step);
                largest = Mathf.Max(largest, Vector2.Distance(before, World));
            }
            return largest;
        }

        private void StartAtGate() => sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);

        [Test]
        public void WalkingWestThroughTheGateGoesAshoreWithoutAButton()
        {
            StartAtGate();
            Walk(Vector2.left, 1.6f);
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.Less(sim.Crew[0].Position.x, HarborTownLayout.Bounds.xMax, "walked off the plank onto the quay");
        }

        [Test]
        public void TheWalkAcrossIsContinuousWithNoJump()
        {
            StartAtGate();
            float largest = Walk(Vector2.left, 2f);
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.LessOrEqual(largest, CrewKinematics.WalkSpeed * Step + 1e-3f, "no step may move further than walking speed allows");
            largest = Walk(Vector2.right, 2.5f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.LessOrEqual(largest, CrewKinematics.WalkSpeed * Step + 1e-3f);
        }

        [Test]
        public void TheVelocityCarriesOnThroughTheHandover()
        {
            StartAtGate();
            for (int n = 0; n < 200 && !sim.Crew[0].OnShore; n++) { sim.SetInput(Host, Vector2.left); sim.Tick(Step); }
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.Greater(-sim.Crew[0].Velocity.x, CrewKinematics.WalkSpeed * .9f, "walking on, not stopping at the line");
        }

        [Test]
        public void WalkingEastBackBoardsTheShip()
        {
            StartAtGate();
            Walk(Vector2.left, 2f);
            Assert.IsTrue(sim.Crew[0].OnShore);
            Walk(Vector2.right, 2.5f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.GreaterOrEqual(sim.Crew[0].Position.x, -VoyageSimulation.DeckBounds.x, "back on the deck");
        }

        [Test]
        public void StandingOnThePlankStaysInOneArea()
        {
            sim.Crew[0].Position = new Vector2(-6.7f, GangwayPassage.LaneY);     // mid-plank, between the handover lines
            bool area = sim.Crew[0].OnShore;
            // Small steps back and forth across the middle of the plank must not flip the area every time.
            for (int k = 0; k < 6; k++) { Walk(Vector2.right, .04f); Walk(Vector2.left, .04f); }
            Assert.AreEqual(area, sim.Crew[0].OnShore);
        }

        [Test]
        public void TheRailIsSolidAwayFromTheGate()
        {
            sim.Crew[0].Position = new Vector2(-4.5f, 0);
            Walk(Vector2.left, 1f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.GreaterOrEqual(sim.Crew[0].Position.x, -VoyageSimulation.DeckBounds.x - .001f);
        }

        [Test]
        public void WalkingAwayFromTheGateOrStandingStillDoesNothing()
        {
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.5f, 0);
            Walk(Vector2.right, 1f);
            Walk(Vector2.zero, 1f);
            Walk(Vector2.up, 1f);
            Assert.IsFalse(sim.Crew[0].OnShore);
        }

        [Test]
        public void FullHandsKeepTheGateShut()
        {
            StartAtGate();
            sim.Crew[0].Carrying = ServiceItem.Food;
            Assert.IsFalse(sim.GateIsOpenFor(0));
            Walk(Vector2.left, 1.5f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.GreaterOrEqual(sim.Crew[0].Position.x, -VoyageSimulation.DeckBounds.x - .001f, "stopped at the closed gate");
            sim.Crew[0].Carrying = ServiceItem.None;
            Assert.IsTrue(sim.GateIsOpenFor(0));
        }

        [Test]
        public void TheGateIsClosedWhileSailing()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            StartAtGate();
            Walk(Vector2.left, 1.5f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.IsFalse(sim.GateIsOpenFor(0));
        }

        [Test]
        public void ShipsWithoutAHarborHaveNoGate()
        {
            var plain = new VoyageSimulation(sim.Stations, sim.Seats, false);
            plain.Join(Host);
            plain.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            for (float t = 0; t < 1.5f; t += Step) { plain.SetInput(Host, Vector2.left); plain.Tick(Step); }
            Assert.IsFalse(plain.Crew[0].OnShore);
        }

        [Test]
        public void TheHarborCommandNoLongerCarriesAnyoneAcross()
        {
            sim.Crew[0].Position = VoyageSimulation.ShipGangway;
            for (int i = 0; i < 20; i++) sim.Tick(Step);
            Assert.AreEqual(VoyageSimulation.WalkThroughGate, sim.HarborCommand(Host, HarborAction.GoAshore));
            Assert.IsFalse(sim.Crew[0].OnShore);
        }

        [Test]
        public void NobodyDepartsWhileSomeoneIsOnThePlank()
        {
            sim.Crew[0].Position = new Vector2(-6.2f, GangwayPassage.LaneY);
            Walk(Vector2.zero, .3f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.IsTrue(sim.CrewOnGangway);
            sim.State.Boarded = 1;
            sim.Crew[0].AtHelm = true;
            for (int i = 0; i < 20; i++) sim.Tick(Step);
            Assert.That(sim.HelmAction(Host), Does.Contain("gangway"));
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
        }

        [Test]
        public void PredictionWalksTheGangwayLikeTheServer()
        {
            StartAtGate();
            for (int i = 0; i < 20; i++) sim.Tick(Step);
            var predictor = new LocalCrewPredictor();
            predictor.Resync(new CrewMotionState { Position = sim.Crew[0].Position, OnShore = false }, 0);
            var deck = new OpenDeck();
            var packet = new System.Collections.Generic.List<MoveCommand>();
            for (int n = 0; n < 60; n++)
            {
                var command = predictor.Step(Vector2.left, false, 1, deck, VoyageSimulation.ShoreWorld, true);
                packet.Clear(); packet.Add(command);
                sim.ApplyMoveCommands(Host, MoveCommandPacket.Pack(packet));
                sim.Tick(CrewLocomotion.StepSeconds);            // refills the move budget; a streaming walker is not stepped twice
            }
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.AreEqual(sim.Crew[0].OnShore, predictor.State.OnShore);
            Assert.Less(Vector2.Distance(predictor.State.Position, sim.Crew[0].Position), 1e-4f, "prediction and server agree on the quay");
        }

        [Test]
        public void TheGatePromptHasNoButtonAndWarnsAboutFullHands()
        {
            var free = PromptCatalog.ForGate(true, default);
            Assert.AreEqual(HudControl.None, free.Key);
            Assert.AreNotEqual(PromptTone.Blocked, free.Tone);
            var holding = PromptCatalog.ForGate(false, new CrewState { Carrying = ServiceItem.Drink });
            Assert.AreEqual(PromptTone.Blocked, holding.Tone);
            Assert.That(holding.Detail, Does.Contain("Hands must be empty"));
        }

        /// <summary>The test ship's deck as client prediction sees it: same bounds, same stations.</summary>
        private sealed class OpenDeck : ILocomotionWorld
        {
            public Vector2 Min => -VoyageSimulation.DeckBounds;
            public Vector2 Max => VoyageSimulation.DeckBounds;
            public bool Walkable(Vector2 p)
            {
                foreach (var station in new[] { new Vector2(-4, -4), new Vector2(0, 6.8f), new Vector2(-4, 0) })
                    if ((p - station).sqrMagnitude < VoyageSimulation.StationClearance * VoyageSimulation.StationClearance) return false;
                return true;
            }
        }
    }
}
