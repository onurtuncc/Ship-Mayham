using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class VoyagePassengerTests
    {
        private VoyageSimulation sim;
        private const ulong Host = 10, Other = 11;
        [SetUp]
        public void Setup()
        {
            sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
                new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4,0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4,3)) },
                new[] { new Vector2(-1.9f,-1.5f), new Vector2(1.9f,-1.5f), new Vector2(-1.9f,.6f), new Vector2(1.9f,.6f),
                    new Vector2(-1.9f,2.7f), new Vector2(1.9f,2.7f), new Vector2(-1.9f,4.8f), new Vector2(1.9f,4.8f) });
            sim.Join(Host); sim.Join(Other);
            Use(0);
        }
        private void Advance(float seconds)
        {
            for (int n = 0; n < Mathf.CeilToInt(seconds / .02f); n++) sim.Tick(.02f);
        }
        private void Use(int target, ulong client = Host)
        {
            sim.Crew[sim.FindCrew(client)].Position = target < sim.Stations.Length ? sim.Stations[target].Position + Vector2.right :
                new Vector2(sim.Passengers[target - sim.Stations.Length].Position.x - 1, sim.Passengers[target - sim.Stations.Length].Position.z);
            Advance(.4f); sim.Interact(client, target);
        }
        private void Near(int passenger, ulong client = Host)
        {
            var p = sim.Passengers[passenger];
            sim.Crew[sim.FindCrew(client)].Position = new Vector2(p.Position.x - 1, p.Position.z);
            Advance(.4f);
        }
        private void Command(int p, PassengerAction action, ulong client = Host, float yaw = 0)
        {
            Advance(.4f); sim.PassengerCommand(client, p, action, yaw);
        }
        private void Collect(int passenger)
        {
            Near(passenger); Command(passenger, PassengerAction.CheckTicket); Command(passenger, PassengerAction.CheckTicket);
        }
        private void Arrive()
        {
            Use(1); sim.HelmAction(Host);
            sim.State.Position = VoyageSimulation.Destination;
            sim.HelmAction(Host);
        }
        private void ThrowOverboard()
        {
            Near(0); Command(0, PassengerAction.CarryOrDrop);
            sim.Crew[0].Position = new Vector2(-5, -1.5f);
            Command(-1, PassengerAction.Throw, yaw: -90);
            Advance(.1f);
        }

        [Test] public void OneUnpaidPassengerAndStableSeatPositionsAreCreated()
        {
            Assert.IsFalse(sim.Passengers[0].FarePaid);
            for (int i = 1; i < 4; i++) Assert.IsTrue(sim.Passengers[i].FarePaid);
            Assert.IsFalse(sim.Passengers[0].TicketChecked);
            Assert.AreEqual(new Vector3(-1.9f,.5f,-1.5f), sim.Passengers[0].Position);
            Assert.AreEqual(-1, sim.Passengers[0].CarrierSlot);
        }
        [Test] public void CheckThenCollectChargesOnlyOnceAndDefersPaymentUntilArrival()
        {
            Near(0); Command(0, PassengerAction.CheckTicket);
            Assert.IsTrue(sim.Passengers[0].TicketChecked);
            Assert.IsFalse(sim.Passengers[0].FarePaid);
            Command(0, PassengerAction.CheckTicket); Command(0, PassengerAction.CheckTicket);
            Assert.IsTrue(sim.Passengers[0].FarePaid);
            Assert.AreEqual(1, sim.State.CheckedTickets);
            Assert.AreEqual(1, sim.State.CollectedFares);
            Assert.AreEqual(0, sim.State.Wallet);
            Arrive();
            Assert.AreEqual(160, sim.State.LastTickets);
            int wallet = sim.State.Wallet;
            sim.HelmAction(Host);
            Assert.AreEqual(wallet, sim.State.Wallet);
        }
        [Test] public void PrepaidPassengerIsNotChargedAgain()
        {
            Near(1); Command(1, PassengerAction.CheckTicket); Command(1, PassengerAction.CheckTicket);
            Assert.AreEqual(1, sim.State.CheckedTickets);
            Assert.AreEqual(0, sim.State.CollectedFares);
        }
        [Test] public void UnpaidFareIsExcludedFromArrivalIncome()
        {
            Arrive();
            Assert.AreEqual(120, sim.State.LastTickets);
            Assert.AreEqual(1, sim.State.LastUnpaid);
        }
        [Test] public void CommandsRejectUnknownDistantInvalidAndAirbornePlayers()
        {
            sim.PassengerCommand(999, 0, PassengerAction.CheckTicket, 0);
            sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, float.NaN);
            sim.PassengerCommand(Host, 100, PassengerAction.CarryOrDrop, 0);
            Advance(.4f); sim.PassengerCommand(Host, 0, PassengerAction.CheckTicket, 0);
            Assert.IsFalse(sim.Passengers[0].TicketChecked);
            Near(0); sim.Jump(Host); sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Advance(.8f); sim.PassengerCommand(Host, 0, (PassengerAction)255, 0);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
        }
        [Test] public void TwoPlayersCannotChargeOrCarrySamePassengerTwice()
        {
            Collect(0); Near(0, Other); Command(0, PassengerAction.CheckTicket, Other);
            Assert.AreEqual(1, sim.State.CollectedFares);
            Near(0); Command(0, PassengerAction.CarryOrDrop);
            Command(0, PassengerAction.CarryOrDrop, Other);
            Assert.AreEqual(0, sim.Passengers[0].CarrierSlot);
            Assert.AreEqual(0, sim.CarriedPassenger(0));
            Assert.AreEqual(-1, sim.CarriedPassenger(1));
            Command(-1, PassengerAction.Throw, Other);
            Assert.AreEqual(PassengerMotion.Carried, sim.Passengers[0].Motion);
        }
        [Test] public void CarryRequiresEmptyHandsAndBlocksStations()
        {
            Use(2); Near(0); Command(0, PassengerAction.CarryOrDrop);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Use(2); Near(0); Command(0, PassengerAction.CarryOrDrop);
            Use(1); Assert.IsFalse(sim.Crew[0].AtHelm);
            Use(3); Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
        }
        [Test] public void CarryFollowsLookAndJumpAndSlowsMovement()
        {
            Near(0); Command(0, PassengerAction.CarryOrDrop);
            sim.Crew[0].Position = new Vector2(0, -5);
            sim.AimPassenger(Host, 90); sim.Jump(Host);
            sim.SetInput(Host, Vector2.right);
            for (int warmUp = 0; warmUp < 3; warmUp++) { sim.SetInput(Host, Vector2.right); sim.Tick(.1f); }   // acceleration ramps; measure at the settled speed
            float before = sim.Crew[0].Position.x;
            sim.SetInput(Host, Vector2.right);
            sim.Tick(.1f);
            Assert.That(sim.Crew[0].Position.x - before, Is.EqualTo(CrewKinematics.WalkSpeed * .7f * .1f).Within(.001f));
            Assert.Greater(sim.Passengers[0].Position.y, 1.1f);
            Assert.Greater(sim.Passengers[0].Position.x, sim.Crew[0].Position.x + .9f);
        }
        [Test] public void PutDownPassengerStaysWhereTheyWereLeftUntilTheyStandUp()
        {
            Near(0); Command(0, PassengerAction.CarryOrDrop);
            sim.Crew[0].Position = new Vector2(0, -5);
            Command(-1, PassengerAction.CarryOrDrop);
            Assert.AreEqual(PassengerMotion.Standing, sim.Passengers[0].Motion);
            Assert.AreEqual(-1, sim.Passengers[0].CarrierSlot);
            Assert.AreNotEqual(new Vector3(sim.Seats[0].x, .5f, sim.Seats[0].y), sim.Passengers[0].Position);
            Advance(VoyageSimulation.StandingSeconds + VoyageSimulation.ReturnTimeoutSeconds + .2f);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Use(2); Use(sim.Stations.Length);
            Assert.AreEqual(1, sim.State.Served, "served once they sit down again");
            Assert.AreEqual(PassengerReaction.Thanks, sim.Passengers[0].Reaction);
        }
        [Test] public void ThrowInsideDeckLandsWithoutRescueCost()
        {
            Near(0); Command(0, PassengerAction.CarryOrDrop);
            sim.Crew[0].Position = new Vector2(0, -6);
            Command(-1, PassengerAction.Throw);
            Assert.AreEqual(PassengerMotion.Airborne, sim.Passengers[0].Motion);
            Advance(1);
            Assert.AreEqual(PassengerMotion.Landing, sim.Passengers[0].Motion, "lies on the deck first");
            Assert.AreEqual(0, sim.State.RescueCount);
            Assert.That(sim.Passengers[0].Position.y, Is.EqualTo(.5f));
            Advance(VoyageSimulation.LandingSeconds + VoyageSimulation.DizzySeconds + VoyageSimulation.ReturnTimeoutSeconds);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
        }
        [Test] public void OverboardRescueIsTimedCostsOnceAndRestoresOriginalSeat()
        {
            ThrowOverboard();
            Assert.AreEqual(PassengerMotion.Rescuing, sim.Passengers[0].Motion);
            Assert.AreEqual(1, sim.State.RescueCount);
            Assert.AreEqual(75, sim.Passengers[0].Satisfaction);
            Advance(4);
            Assert.AreEqual(PassengerMotion.Rescuing, sim.Passengers[0].Motion);
            Advance(4.5f);
            Assert.AreEqual(PassengerMotion.Dizzy, sim.Passengers[0].Motion, "hauled aboard, not teleported");
            Advance(VoyageSimulation.DizzySeconds + VoyageSimulation.ReturnTimeoutSeconds);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.AreEqual(new Vector3(sim.Seats[0].x,.5f,sim.Seats[0].y), sim.Passengers[0].Position);
            Assert.AreEqual(1, sim.State.RescueCount);
            Arrive(); Assert.AreEqual(15, sim.State.LastRescueCosts);
        }
        [Test] public void ArrivalDuringRescueExcludesPassengerAndCannotPayLater()
        {
            Collect(0); ThrowOverboard(); Arrive();
            Assert.AreEqual(120, sim.State.LastTickets);
            Assert.AreEqual(1, sim.State.LastMissing);
            int wallet = sim.State.Wallet;
            Advance(10);
            Assert.AreEqual(wallet, sim.State.Wallet);
            Assert.AreEqual(120, sim.State.LastTickets);
        }
        [Test] public void DisconnectReturnsPassengerWithoutChargingRescue()
        {
            Near(0); Command(0, PassengerAction.CarryOrDrop); sim.Leave(Host);
            Assert.AreEqual(PassengerMotion.OnDeck, sim.Passengers[0].Motion);
            Assert.AreEqual(-1, sim.Passengers[0].CarrierSlot);
            Assert.AreEqual(0, sim.State.RescueCount);
            sim.Join(Host); Near(0); Command(0, PassengerAction.CarryOrDrop);
            Assert.AreEqual(0, sim.CarriedPassenger(0));
        }
        [Test] public void NewVoyageClearsTicketsAndRescuesAndRotatesUnpaidPassenger()
        {
            Collect(0); ThrowOverboard(); Advance(9); Arrive(); Use(0); Use(0);
            Assert.AreEqual(2, sim.State.Voyage);
            Assert.AreEqual(0, sim.State.CheckedTickets);
            Assert.AreEqual(0, sim.State.CollectedFares);
            Assert.AreEqual(0, sim.State.RescueCount);
            Assert.IsTrue(sim.Passengers[0].FarePaid);
            Assert.IsFalse(sim.Passengers[1].FarePaid);
            Assert.IsFalse(sim.Passengers[0].TicketChecked);
        }
        [Test] public void PassengerSnapshotEqualityIncludesTicketAndMotionState()
        {
            var before = sim.Passengers[0];
            Near(0); Command(0, PassengerAction.CheckTicket);
            Assert.IsFalse(before.Equals(sim.Passengers[0]));
            var checkedTicket = sim.Passengers[0];
            Command(0, PassengerAction.CarryOrDrop);
            Assert.IsFalse(checkedTicket.Equals(sim.Passengers[0]));
        }
    }
}
