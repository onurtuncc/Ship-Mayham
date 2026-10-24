using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>Walking into the gangway gate carries the crew across; there is no button.</summary>
    public sealed class GangwayGateTests
    {
        private const ulong Host = 10;
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

        private void Walk(Vector2 input, float seconds)
        {
            for (float t = 0; t < seconds; t += .02f) { sim.SetInput(Host, input); sim.Tick(.02f); }
        }

        [Test]
        public void WalkingWestIntoTheGateGoesAshoreWithoutAButton()
        {
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            Walk(Vector2.left, 1f);
            Assert.IsTrue(sim.Crew[0].OnShore);
            Assert.LessOrEqual(sim.Crew[0].Position.x, VoyageSimulation.GatePoint(true).x, "arrives at the pier end of the plank and walks on west");
        }

        [Test]
        public void WalkingEastBackIntoTheGateBoardsTheShip()
        {
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            Walk(Vector2.left, 1f);
            Assert.IsTrue(sim.Crew[0].OnShore);
            sim.Crew[0].Position = VoyageSimulation.GatePoint(true) + new Vector2(-.7f, 0);
            Walk(Vector2.zero, 1f);                                  // stand a moment past the cooldown
            Walk(Vector2.right, 1.2f);
            Assert.IsFalse(sim.Crew[0].OnShore);
        }

        [Test]
        public void ArrivingNeverBouncesTheCrewStraightBack()
        {
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            Walk(Vector2.left, 3f);                                  // keeps pushing west the whole time
            Assert.IsTrue(sim.Crew[0].OnShore, "walking on into town stays in town");
            Assert.Less(sim.Crew[0].Position.x, VoyageSimulation.GatePoint(true).x - .5f);
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
        public void AFarAwayCrewMemberCannotReachTheGate()
        {
            sim.Crew[0].Position = new Vector2(3, -2);
            Walk(Vector2.left, .3f);
            Assert.IsFalse(sim.Crew[0].OnShore);
        }

        [Test]
        public void FullHandsKeepTheGateShut()
        {
            sim.Crew[0].Position = new Vector2(-3, 0);
            sim.Crew[0].Carrying = ServiceItem.Food;
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            Assert.IsFalse(sim.GateIsOpenFor(0));
            Walk(Vector2.left, 1f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            sim.Crew[0].Carrying = ServiceItem.None;
            Assert.IsTrue(sim.GateIsOpenFor(0));
        }

        [Test]
        public void TheGateIsClosedWhileSailing()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            Walk(Vector2.left, 1f);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.IsFalse(sim.GateIsOpenFor(0));
        }

        [Test]
        public void ShipsWithoutAHarborHaveNoGate()
        {
            var plain = new VoyageSimulation(sim.Stations, sim.Seats, false);
            plain.Join(Host);
            plain.Crew[0].Position = VoyageSimulation.ShipGangway + new Vector2(.7f, 0);
            for (float t = 0; t < 1f; t += .02f) { plain.SetInput(Host, Vector2.left); plain.Tick(.02f); }
            Assert.IsFalse(plain.Crew[0].OnShore);
        }

        [Test]
        public void TheOldCommandStillWorksForScriptedPlayers()
        {
            sim.Crew[0].Position = VoyageSimulation.ShipGangway;
            for (int i = 0; i < 20; i++) sim.Tick(.02f);
            Assert.That(sim.HarborCommand(Host, HarborAction.GoAshore), Does.Contain("ashore"));
            Assert.IsTrue(sim.Crew[0].OnShore);
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
    }
}
