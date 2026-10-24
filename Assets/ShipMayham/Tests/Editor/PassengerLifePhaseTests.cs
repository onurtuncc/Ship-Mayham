using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class PassengerLifePhaseTests
    {
        private static VoyageSimulation Board()
        {
            var sim = NavigationPhaseTests.Create(); sim.State.Phase = VoyagePhase.Boarding;
            sim.Crew[0].Position = new Vector2(-3, -4); sim.Interact(10, 0);
            sim.State.Phase = VoyagePhase.Sailing; sim.State.Elapsed = 15; return sim;
        }
        [Test] public void BoardCreatesThreeProfilesAndAtMostTwoRoamingPassengers()
        {
            var sim = Board();
            Assert.AreEqual(PassengerPersonality.Nervous, sim.Passengers[2].Personality);
            for (int i = 0; i < 4; i++) sim.Passengers[i].RoamIn = 0;
            sim.Tick(.1f); int moving = 0;
            foreach (var p in sim.Passengers) if (p.Active && p.Routine != PassengerRoutine.Seated) moving++;
            Assert.LessOrEqual(moving, 2); Assert.Greater(moving, 0);
        }
        [Test] public void PatienceChangesWaitingCost()
        {
            Assert.Greater(VoyageSimulation.PatienceFactor(PassengerPersonality.Impatient), VoyageSimulation.PatienceFactor(PassengerPersonality.Patient));
        }
        [Test] public void PickupInterruptsRoamingAndRecoveryRestoresSeat()
        {
            var sim = Board(); sim.Passengers[0].RoamIn = 0; sim.Tick(.1f);
            sim.Crew[0].Position = new Vector2(sim.Passengers[0].Position.x - 1, sim.Passengers[0].Position.z);
            for (int i = 0; i < 25; i++) sim.Tick(.02f);
            sim.PassengerCommand(10, 0, PassengerAction.CarryOrDrop, 0);
            Assert.AreEqual(PassengerMotion.Carried, sim.Passengers[0].Motion);
            sim.Leave(10); Assert.AreEqual(PassengerRoutine.Seated, sim.Passengers[0].Routine);
        }
        [Test] public void SpillNeedsThreeValidSpacedActionsAndCannotPayTwice()
        {
            var sim = Board(); sim.Deck.Spill = true; sim.Crew[0].Position = sim.Deck.SpillPosition + Vector2.right;
            for (int step = 0; step < 3; step++)
            { for (int n = 0; n < 25; n++) sim.Tick(.02f); sim.DeckCommand(10, DeckAction.CleanSpill); }
            Assert.IsFalse(sim.Deck.Spill); Assert.AreEqual(1, sim.Deck.Cleaned);
            sim.DeckCommand(10, DeckAction.CleanSpill); Assert.AreEqual(1, sim.Deck.Cleaned);
        }
        [Test] public void DistantUnknownAndOccupiedPlayersCannotClean()
        {
            var sim = Board(); sim.Deck.Spill = true;
            sim.DeckCommand(999, DeckAction.CleanSpill); sim.DeckCommand(10, DeckAction.CleanSpill);
            sim.Crew[0].Position = sim.Deck.SpillPosition; sim.Crew[0].Carrying = ServiceItem.Food;
            sim.DeckCommand(10, DeckAction.CleanSpill); Assert.AreEqual(0, sim.Deck.CleanSteps);
        }
        [Test] public void ServicePausesAWalkerStandingAndThenReturnsThemToTheirSeat()
        {
            var sim = Board();
            for (int i = 0; i < 25; i++) sim.Tick(.02f); // Boarding used the shared interaction cooldown.
            sim.Passengers[0].Position = new Vector3(-.35f, .5f, -1.5f);
            sim.Passengers[0].Routine = PassengerRoutine.Strolling;
            sim.Passengers[0].RoutineTarget = new Vector2(-.35f, -1.5f);
            sim.Passengers[0].Request = ServiceItem.Food;
            sim.Crew[0].Position = new Vector2(-1.35f, -1.5f);
            sim.Crew[0].Carrying = ServiceItem.Food;
            sim.Interact(10, sim.Stations.Length);
            Assert.AreEqual(PassengerRoutine.Watching, sim.Passengers[0].Routine);
            for (int i = 0; i < 600; i++) sim.Tick(.02f);
            Assert.AreEqual(PassengerRoutine.Seated, sim.Passengers[0].Routine);
            Assert.AreEqual(sim.Seats[0].x, sim.Passengers[0].Position.x);
        }
        [Test] public void RoamingReturnsToTheClaimedSeatRatherThanTheOriginalSeat()
        {
            var sim = Board();
            sim.Passengers[0].SeatClaim = 5; // Seat four is free in this four-passenger voyage.
            sim.Passengers[0].Position = new Vector3(-.35f, .5f, 2.7f);
            sim.Passengers[0].Routine = PassengerRoutine.Watching;
            sim.Passengers[0].RoutineTimer = .01f;
            for (int i = 0; i < 600; i++) sim.Tick(.02f);
            Assert.AreEqual(PassengerRoutine.Seated, sim.Passengers[0].Routine);
            Assert.AreEqual(sim.Seats[4].x, sim.Passengers[0].Position.x);
            Assert.AreEqual(sim.Seats[4].y, sim.Passengers[0].Position.z);
        }
        [Test] public void WorkingPassengerWalksToTheJobRatherThanSlidingSeated()
        {
            var passenger = new PassengerState { Active = true, Motion = PassengerMotion.OnDeck,
                Routine = PassengerRoutine.Working, Position = Vector3.zero, RoutineTarget = new Vector2(4, 0) };
            Assert.AreEqual(PassengerPose.Walking, PassengerPresentation.PoseFor(passenger, PassengerCue.Relaxed));
            passenger.Position = new Vector3(4, .5f, 0);
            Assert.AreEqual(PassengerPose.Standing, PassengerPresentation.PoseFor(passenger, PassengerCue.Relaxed));
        }
        [Test] public void MigrationKeepsRoutineTargetAndCleaningProgress()
        {
            var sim = Board(); sim.Passengers[0].Routine = PassengerRoutine.Watching; sim.Passengers[0].RoutineTimer = 3;
            sim.Deck.Spill = true; sim.Deck.CleanSteps = 2;
            var restored = Board(); restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsTrue(sim.Deck.Equals(restored.Deck)); Assert.IsTrue(sim.Passengers[0].Equals(restored.Passengers[0]));
        }
    }
}
