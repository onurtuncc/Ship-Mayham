using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class OpportunityPhaseTests
    {
        private static void Wait(VoyageSimulation sim, float seconds = .5f) { for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++) sim.Tick(.02f); }
        [Test] public void TemporaryPlugIsFiniteRefundsOnDisconnectAndExpires()
        {
            var sim = NavigationPhaseTests.Create(); sim.Crew[0].Position = VoyageSimulation.PlugLocker;
            sim.DeckCommand(10, DeckAction.TakePlug); Assert.AreEqual(1, sim.Opportunity.PlugsAvailable);
            sim.Leave(10); Assert.AreEqual(2, sim.Opportunity.PlugsAvailable); sim.Join(10);
            sim.Crew[0].Position = VoyageSimulation.PlugLocker; Wait(sim); sim.DeckCommand(10, DeckAction.TakePlug);
            sim.State.Leak = 1; sim.Crew[0].Position = new Vector2(3, -3); Wait(sim); sim.Interact(10, 4);
            Assert.AreEqual(20, sim.Opportunity.PlugRemaining); Assert.AreEqual(1, sim.State.Leak);
            float health = sim.State.Health; Wait(sim, 1); Assert.AreEqual(health, sim.State.Health);
            Wait(sim, 20); Assert.LessOrEqual(sim.Opportunity.PlugRemaining, 0); Assert.Less(sim.State.Health, health);
        }
        [Test] public void NoLeakDoesNotConsumePlug()
        {
            var sim = NavigationPhaseTests.Create(); sim.Crew[0].Position = VoyageSimulation.PlugLocker; sim.DeckCommand(10, DeckAction.TakePlug);
            sim.Crew[0].Position = new Vector2(3, -3); Wait(sim); sim.Interact(10, 4);
            Assert.AreEqual(ServiceItem.TemporaryPlug, sim.Crew[0].Carrying); Assert.AreEqual(0, sim.Opportunity.PlugsUsed);
        }
        [Test] public void SeaChestNeedsSlowNearbyShipAndSettlesOnlyOnce()
        {
            var sim = NavigationPhaseTests.Create(); sim.Opportunity.ChestAvailable = true; sim.Crew[0].Position = VoyageSimulation.RescueRope;
            sim.DeckCommand(10, DeckAction.HaulChest); Assert.IsFalse(sim.Opportunity.ChestClaimed);
            sim.State.Position = sim.Opportunity.ChestPosition; sim.State.Speed = 3; Wait(sim); sim.DeckCommand(10, DeckAction.HaulChest);
            Assert.IsFalse(sim.Opportunity.ChestClaimed);
            sim.State.Speed = 0; Wait(sim); sim.DeckCommand(10, DeckAction.HaulChest);
            Assert.IsTrue(sim.Opportunity.ChestClaimed); Assert.AreEqual(30, sim.Opportunity.ChestReward); Assert.AreEqual(0, sim.State.Wallet);
            Wait(sim); sim.DeckCommand(10, DeckAction.HaulChest); Assert.AreEqual(30, sim.Opportunity.ChestReward);
            sim.State.Position = VoyageSimulation.Destination; sim.Crew[0].AtHelm = true; sim.HelmAction(10);
            Assert.AreEqual(30, sim.State.LastEventBonus);
        }
        [Test] public void WorkAgreementCleansButNeverCreatesTicketCash()
        {
            var sim = NavigationPhaseTests.Create(); sim.State.Phase = VoyagePhase.Boarding; sim.Crew[0].Position = new Vector2(-3,-4);
            sim.Interact(10, 0); sim.State.Phase = VoyagePhase.Sailing; sim.Passengers[0].TicketChecked = true;
            sim.Deck.Spill = true; sim.Deck.SpillPosition = new Vector2(-1.9f,-1.5f);
            sim.Crew[0].Position = new Vector2(-3,-1.5f); Wait(sim); sim.DeckCommand(10, DeckAction.WorkFare, 0);
            Wait(sim, 7); Assert.IsTrue(sim.Passengers[0].WorkedFare); Assert.IsFalse(sim.Deck.Spill);
            Assert.AreEqual(1, sim.Opportunity.WorkedFares);
            sim.State.Position = VoyageSimulation.Destination; sim.Crew[0].AtHelm = true; sim.HelmAction(10);
            Assert.AreEqual(120, sim.State.LastTickets);
        }
        [Test] public void MigrationKeepsPlugCountdownAndClaimedChest()
        {
            var sim = NavigationPhaseTests.Create(); sim.Opportunity.PlugRemaining = 7; sim.Opportunity.ChestClaimed = true; sim.Opportunity.ChestReward = 30;
            var target = NavigationPhaseTests.Create(); target.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsTrue(sim.Opportunity.Equals(target.Opportunity));
        }
    }
}
