using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class VoyageHarborTests
    {
        private VoyageSimulation sim;
        private const ulong Host = 10, Client = 11;
        [SetUp] public void Setup()
        {
            var seats = new Vector2[8];
            for (int i = 0; i < 8; i++) seats[i] = new Vector2(i % 2 == 0 ? -1.9f : 1.9f, -1.5f + i / 2 * 2.1f);
            sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
                new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4,0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4,3)),
                new StationDefinition(StationKind.Cannon, new Vector2(4,6)),
                new StationDefinition(StationKind.Power, new Vector2(4,0)) }, seats, true);
            sim.Join(Host); sim.Join(Client);
        }
        private void Advance(float seconds = .4f)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .02f); i++) sim.Tick(.02f);
        }
        private void Use(int station)
        {
            sim.Crew[0].Position = sim.Stations[station].Position + Vector2.right;
            Advance(); sim.Interact(Host, station);
        }
        private void Harbor(HarborAction action, ulong client = Client)
        {
            sim.Crew[sim.FindCrew(client)].Position = VoyageSimulation.HarborPoint(action) + (action >= HarborAction.BuyFood ? Vector2.right * 1.2f : Vector2.zero);
            Advance(); sim.HarborCommand(client, action);
        }
        private void Depart() { Use(0); Use(1); sim.HelmAction(Host); }
        private void Arrive() { sim.State.Position = VoyageSimulation.Destination; sim.HelmAction(Host); }

        [Test] public void GangwayTransfersCoordinatesAndStopsOldInput()
        {
            sim.SetInput(Client, Vector2.up);
            Harbor(HarborAction.GoAshore);
            Assert.IsTrue(sim.Crew[1].OnShore);
            Assert.AreEqual(VoyageSimulation.HarborPoint(HarborAction.BoardShip), sim.Crew[1].Position);
            Advance(); Assert.AreEqual(new Vector2(5,-6), sim.Crew[1].Position);
            Harbor(HarborAction.BoardShip);
            Assert.IsFalse(sim.Crew[1].OnShore);
            Assert.AreEqual(VoyageSimulation.ShipGangway, sim.Crew[1].Position);
        }
        [Test] public void GangwayRejectsUnknownFarAndInvalidCommands()
        {
            sim.HarborCommand(999, HarborAction.GoAshore);
            sim.HarborCommand(Client, HarborAction.GoAshore);
            sim.Crew[1].Position = VoyageSimulation.ShipGangway;
            sim.HarborCommand(Client, (HarborAction)255);
            Assert.IsFalse(sim.Crew[1].OnShore);
            Assert.AreEqual(0, sim.CrewAshore);
        }
        [Test] public void ShoreCommandsCannotBeSentFromDeck()
        {
            sim.State.Wallet = 100;
            Harbor(HarborAction.BuyFood);
            Harbor(HarborAction.MaintainHull);
            Harbor(HarborAction.BoardShip);
            Assert.AreEqual(100, sim.State.Wallet);
            Assert.AreEqual(16, sim.State.FoodStock);
            Assert.AreEqual(0, sim.State.HullShield);
            Assert.IsFalse(sim.Crew[1].OnShore);
        }
        [Test] public void EmptyHandsAndGroundingAreRequiredForGangway()
        {
            sim.Crew[1].Carrying = ServiceItem.Drink;
            Harbor(HarborAction.GoAshore); Assert.IsFalse(sim.Crew[1].OnShore);
            sim.Crew[1].Carrying = ServiceItem.None;
            Advance(); sim.Jump(Client); sim.HarborCommand(Client, HarborAction.GoAshore);
            Assert.IsFalse(sim.Crew[1].OnShore);
            Advance(1); sim.Crew[1].AtCannon = true;
            Harbor(HarborAction.GoAshore); Assert.IsFalse(sim.Crew[1].OnShore);
        }
        [Test] public void PassengerCannotBeTakenAshore()
        {
            Use(0); sim.Crew[0].Position = sim.Seats[0] + Vector2.right; Advance();
            sim.PassengerCommand(Host, 0, PassengerAction.CarryOrDrop, 0);
            Harbor(HarborAction.GoAshore, Host);
            Assert.IsFalse(sim.Crew[0].OnShore);
            Assert.AreEqual(0, sim.CarriedPassenger(0));
        }
        [Test] public void DepartureWaitsForCrewAndDisconnectUnblocksIt()
        {
            Harbor(HarborAction.GoAshore); Depart();
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
            Assert.That(sim.HelmAction(Host), Does.Contain("ashore"));
            sim.Leave(Client); sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Sailing, sim.State.Phase);
            sim.Join(Client); Assert.IsFalse(sim.Crew[1].OnShore);
        }
        [Test] public void HarborIsClosedAtSeaAndAfterFailure()
        {
            Depart(); Harbor(HarborAction.GoAshore);
            Assert.IsFalse(sim.Crew[1].OnShore);
            sim.State.Health = .001f; sim.State.Leak = 1; Advance();
            Assert.AreEqual(VoyagePhase.Failed, sim.State.Phase);
            Harbor(HarborAction.GoAshore); Assert.IsFalse(sim.Crew[1].OnShore);
        }
        [Test] public void ShoreMovementIsBoundedAndStillSupportsJumping()
        {
            Harbor(HarborAction.GoAshore); sim.Jump(Client);
            sim.SetInput(Client, Vector2.right); sim.Tick(.1f);
            Assert.Greater(sim.Crew[1].JumpHeight, 0);
            Assert.LessOrEqual(sim.Crew[1].Position.x, HarborTownLayout.Bounds.xMax);
            sim.Crew[1].Position = new Vector2(2, HarborTownLayout.Bounds.yMax - .01f);
            sim.SetInput(Client, Vector2.up); sim.Tick(.1f);
            Assert.LessOrEqual(sim.Crew[1].Position.y, HarborTownLayout.Bounds.yMax);
        }
        [Test] public void ShorePlayerCannotUseShipOrPassengerCommandsAtMatchingCoordinates()
        {
            Use(0); Harbor(HarborAction.GoAshore);
            sim.Crew[1].Position = sim.Stations[1].Position;
            sim.Interact(Client, 1); Assert.IsFalse(sim.Crew[1].AtHelm);
            sim.Crew[1].Position = sim.Seats[0]; Advance();
            sim.PassengerCommand(Client, 0, PassengerAction.CheckTicket, 0);
            Assert.IsFalse(sim.Passengers[0].TicketChecked);
        }
        [Test] public void PoorOrDistantShopperCannotSpendOrCreateStock()
        {
            Harbor(HarborAction.GoAshore); Harbor(HarborAction.BuyFood);
            Assert.AreEqual(16, sim.State.FoodStock);
            sim.State.Wallet = 100;
            sim.Crew[1].Position = new Vector2(5, 10); Advance();
            sim.HarborCommand(Client, HarborAction.BuyFood);
            Assert.AreEqual(100, sim.State.Wallet);
            Assert.AreEqual(16, sim.State.FoodStock);
        }
        [Test] public void SupplyPurchaseUsesSharedWalletAndRejectsImmediateRepeat()
        {
            sim.State.Wallet = 100; Harbor(HarborAction.GoAshore); Harbor(HarborAction.BuyFood);
            sim.HarborCommand(Client, HarborAction.BuyFood);
            Assert.AreEqual(24, sim.State.FoodStock); Assert.AreEqual(88, sim.State.Wallet);
            Harbor(HarborAction.BuyDrink);
            Assert.AreEqual(24, sim.State.DrinkStock); Assert.AreEqual(76, sim.State.Wallet);
        }
        [Test] public void SupplyCapIncludesItemsCarriedByOtherPlayers()
        {
            sim.State.Wallet = 100;
            sim.State.FoodStock = 24; Use(2); // 23 in storage plus one in hand.
            Harbor(HarborAction.GoAshore); Harbor(HarborAction.BuyFood);
            Assert.AreEqual(31, sim.State.FoodStock);
            Harbor(HarborAction.BuyFood);
            Assert.AreEqual(88, sim.State.Wallet);
            Use(2); Assert.AreEqual(32, sim.State.FoodStock);
        }
        [Test] public void ConcurrentPurchasesCannotOverdrawWallet()
        {
            sim.State.Wallet = 12;
            Harbor(HarborAction.GoAshore); Harbor(HarborAction.GoAshore, Host);
            Harbor(HarborAction.BuyFood); Harbor(HarborAction.BuyDrink, Host);
            Assert.AreEqual(0, sim.State.Wallet);
            Assert.AreEqual(24, sim.State.FoodStock); Assert.AreEqual(16, sim.State.DrinkStock);
        }
        [Test] public void MaintenanceBuysOnceAndAbsorbsLeakThenPirateDamage()
        {
            sim.State.Wallet = 100; Harbor(HarborAction.GoAshore); Harbor(HarborAction.MaintainHull);
            Harbor(HarborAction.MaintainHull);
            Assert.AreEqual(70, sim.State.Wallet); Assert.AreEqual(20, sim.State.HullShield);
            Harbor(HarborAction.BoardShip); Depart();
            sim.State.Leak = 1; sim.Tick(.1f);
            Assert.AreEqual(100, sim.State.Health); Assert.Less(sim.State.HullShield, 20);
            sim.State.Leak = 0; sim.State.HullShield = 3;
            sim.Encounter.Pirate = PiratePhase.Attacking;
            sim.Encounter.AttackRemaining = 30; sim.Encounter.EnemyShotRemaining = .01f;
            sim.Encounter.PiratePosition = new Vector2(26, 25); sim.Tick(.1f);
            Assert.AreEqual(3, sim.State.HullShield, "Firing cannot damage the shield before impact.");
            Advance(1);
            Assert.AreEqual(0, sim.State.HullShield); Assert.AreEqual(95, sim.State.Health);
        }
        [Test] public void ArrivalSnapsToBerthAndResetWaitsForShoreCrew()
        {
            Depart(); sim.State.Position = VoyageSimulation.Destination + Vector2.right * 5; sim.State.Heading = 120;
            sim.HelmAction(Host);
            Assert.AreEqual(VoyageSimulation.Destination, sim.State.Position); Assert.AreEqual(0, sim.State.Heading);
            Assert.AreEqual(new Vector3(-14, 0, 220), VoyageSimulation.HarborOrigin(sim.State));
            Harbor(HarborAction.GoAshore); Use(0);
            Assert.AreEqual(1, sim.State.Voyage);
            Harbor(HarborAction.BoardShip); Use(0);
            Assert.AreEqual(2, sim.State.Voyage); Assert.AreEqual(Vector2.zero, sim.State.Position);
        }
        [Test] public void ArrivalPurchasesSurviveNewVoyageAndFreeBaselinePreventsSoftlock()
        {
            Depart(); Arrive(); sim.State.Wallet = 100;
            Harbor(HarborAction.GoAshore); Harbor(HarborAction.BuyFood); Harbor(HarborAction.MaintainHull);
            sim.State.DrinkStock = 0;
            Harbor(HarborAction.BoardShip); Use(0);
            Assert.AreEqual(24, sim.State.FoodStock); Assert.AreEqual(16, sim.State.DrinkStock);
            Assert.AreEqual(20, sim.State.HullShield); Assert.AreEqual(58, sim.State.Wallet);
        }
        [Test] public void SnapshotEqualityIncludesShoreAndHullProtection()
        {
            var crew = sim.Crew[1]; Harbor(HarborAction.GoAshore);
            Assert.IsFalse(crew.Equals(sim.Crew[1]));
            var voyage = sim.State; sim.State.HullShield = 20;
            Assert.IsFalse(voyage.Equals(sim.State));
        }
        [Test] public void RouteContractRequiresShoreAndLocksWithPassengerManifest()
        {
            sim.State.Reputation = 5;
            sim.SelectRoute(Client, RouteProfile.MarketRun);
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
            Harbor(HarborAction.GoAshore);
            sim.SelectRoute(Client, (RouteProfile)255);
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
            sim.SelectRoute(Client, RouteProfile.MarketRun);
            Assert.AreEqual(RouteProfile.MarketRun, sim.State.Route);
            Harbor(HarborAction.BoardShip); Use(0); Harbor(HarborAction.GoAshore);
            sim.SelectRoute(Client, RouteProfile.StormPassage);
            Assert.AreEqual(RouteProfile.MarketRun, sim.State.Route);
        }
        [Test] public void MarketContractRewardsServiceAndPersistsReputation()
        {
            sim.State.Reputation = 2;
            Harbor(HarborAction.GoAshore);
            sim.SelectRoute(Client, RouteProfile.MarketRun);
            Harbor(HarborAction.BoardShip);
            Depart(); sim.State.Served = 3; Arrive();
            Assert.AreEqual(35, sim.State.LastRouteBonus);
            Assert.AreEqual(2, sim.State.LastReputation);
            Assert.AreEqual(4, sim.State.Reputation);
            Use(0);
            Assert.AreEqual(4, sim.State.Reputation);
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
        }
        [Test] public void StormContractAcceleratesHazardsAndRewardsSafeArrival()
        {
            sim.State.Reputation = 5;
            sim.Join(901); sim.Join(902);                   // a full crew keeps the base event timetable
            Harbor(HarborAction.GoAshore);
            sim.SelectRoute(Client, RouteProfile.StormPassage);
            Harbor(HarborAction.BoardShip);
            Depart(); Advance(12.1f);
            Assert.AreEqual(VoyageEventPhase.Active, sim.Event.Phase);
            Assert.That(sim.Event.Remaining, Is.LessThanOrEqualTo(24));
            Advance(4.1f);
            Assert.AreEqual(1, sim.State.Leak);
            sim.State.Leak = 0; sim.State.Health = 75; Arrive();
            Assert.AreEqual(50, sim.State.LastRouteBonus);
            Assert.AreEqual(3, sim.State.LastReputation);
        }
        [TestCase(RouteProfile.MarketRun, 2)]
        [TestCase(RouteProfile.StormPassage, 5)]
        public void RouteUnlockIsAuthoritativeAndDoesNotSpendReputation(RouteProfile route, int required)
        {
            Harbor(HarborAction.GoAshore);
            sim.State.Reputation = required - 1;
            sim.State.Wallet = 100;
            var before = sim.Event;
            StringAssert.Contains($"{required} reputation", sim.SelectRoute(Client, route));
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
            Assert.AreEqual(before.NextIn, sim.Event.NextIn);

            sim.State.Reputation = required;
            sim.SelectRoute(Client, route);
            Assert.AreEqual(route, sim.State.Route);
            Assert.AreEqual(required, sim.State.Reputation);
            Assert.AreEqual(100, sim.State.Wallet);
            sim.SelectRoute(Client, RouteProfile.CoastalFerry);
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
        }

        [Test] public void FirstCargoRelayCanFinishTheMarketUnlockWithoutStartingARoute()
        {
            sim.State.Reputation = 1;
            Harbor(HarborAction.GoAshore);
            sim.SelectRoute(Client, RouteProfile.MarketRun);
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
            sim.ToggleDockJob(Client);
            for (int crate = 0; crate < VoyageSimulation.DockJobGoal; crate++)
            {
                Harbor(HarborAction.BuyFood);
                Harbor(HarborAction.MaintainHull);
            }
            Assert.AreEqual(2, sim.State.Reputation);
            sim.SelectRoute(Client, RouteProfile.MarketRun);
            Assert.AreEqual(RouteProfile.MarketRun, sim.State.Route);
        }

        [Test] public void CargoRelaySupportsParallelCrewAndRewardsSharedProgress()
        {
            Harbor(HarborAction.GoAshore);
            Harbor(HarborAction.GoAshore, Host);
            sim.ToggleDockJob(Client);
            Harbor(HarborAction.BuyFood);
            Harbor(HarborAction.BuyFood, Host);
            Assert.AreEqual(ServiceItem.Cargo, sim.Crew[0].Carrying);
            Assert.AreEqual(ServiceItem.Cargo, sim.Crew[1].Carrying);
            Harbor(HarborAction.MaintainHull);
            Harbor(HarborAction.MaintainHull, Host);
            Harbor(HarborAction.BuyFood);
            Harbor(HarborAction.MaintainHull);
            Assert.IsFalse(sim.State.DockJobActive);
            Assert.AreEqual(3, sim.State.DockJobProgress);
            Assert.AreEqual(24, sim.State.Wallet);
            Assert.AreEqual(1, sim.State.Reputation);
            Assert.AreEqual(1, sim.State.DockJobsCompleted);
            sim.ToggleDockJob(Client);
            Assert.IsFalse(sim.State.DockJobActive);
        }
        [Test] public void CargoRelayCanBeCancelledOnlyAfterCarriedCratesReturn()
        {
            Harbor(HarborAction.GoAshore);
            sim.ToggleDockJob(Client);
            Harbor(HarborAction.BuyFood);
            sim.ToggleDockJob(Client);
            Assert.IsTrue(sim.State.DockJobActive);
            Assert.AreEqual(ServiceItem.Cargo, sim.Crew[1].Carrying);
            Harbor(HarborAction.BuyFood);
            Assert.AreEqual(ServiceItem.None, sim.Crew[1].Carrying);
            sim.ToggleDockJob(Client);
            Assert.IsFalse(sim.State.DockJobActive);
            Assert.AreEqual(0, sim.State.DockJobProgress);
        }
        [Test] public void PaintRequiresShoreAndReputationAndSurvivesNewVoyage()
        {
            sim.State.Reputation = 5;
            sim.SelectLivery(Client, ShipLivery.Sunset);
            Assert.AreEqual(ShipLivery.Classic, sim.State.Livery);
            Harbor(HarborAction.GoAshore);
            sim.State.Reputation = 1;
            sim.SelectLivery(Client, ShipLivery.Seafoam);
            Assert.AreEqual(ShipLivery.Classic, sim.State.Livery);
            sim.State.Reputation = 5;
            sim.SelectLivery(Client, (ShipLivery)255);
            Assert.AreEqual(ShipLivery.Classic, sim.State.Livery);
            sim.SelectLivery(Client, ShipLivery.Sunset);
            Assert.AreEqual(ShipLivery.Sunset, sim.State.Livery);
            Assert.AreEqual(0, sim.State.Wallet);
            Harbor(HarborAction.BoardShip);
            Depart(); Arrive(); Use(0);
            Assert.AreEqual(ShipLivery.Sunset, sim.State.Livery);
        }

        [Test] public void CargoCannotBeDeliveredRemotelyOrRepeatedDuringCooldown()
        {
            Harbor(HarborAction.GoAshore);
            sim.ToggleDockJob(Client);
            Harbor(HarborAction.BuyFood);
            Advance();
            sim.HarborCommand(Client, HarborAction.MaintainHull);
            Assert.AreEqual(0, sim.State.DockJobProgress);
            Assert.AreEqual(ServiceItem.Cargo, sim.Crew[1].Carrying);
            Harbor(HarborAction.MaintainHull);
            Assert.AreEqual(1, sim.State.DockJobProgress);
            sim.Crew[1].Position = VoyageSimulation.HarborPoint(HarborAction.BuyFood) + Vector2.right;
            sim.HarborCommand(Client, HarborAction.BuyFood);
            Assert.AreEqual(ServiceItem.None, sim.Crew[1].Carrying);
        }

        [Test] public void FourCrewCannotTakeMoreCratesThanTheJobNeeds()
        {
            sim.Join(12); sim.Join(13);
            Harbor(HarborAction.GoAshore);
            sim.ToggleDockJob(Client);
            foreach (ulong id in new ulong[] { Host, 12, 13 }) Harbor(HarborAction.GoAshore, id);
            foreach (ulong id in new ulong[] { Host, Client, 12, 13 }) Harbor(HarborAction.BuyFood, id);
            Assert.AreEqual(ServiceItem.None, sim.Crew[3].Carrying);
            foreach (ulong id in new ulong[] { Host, Client, 12 }) Harbor(HarborAction.MaintainHull, id);
            Assert.IsFalse(sim.State.DockJobActive);
            foreach (var member in sim.Crew) Assert.AreEqual(ServiceItem.None, member.Carrying);
            foreach (ulong id in new ulong[] { Host, Client, 12, 13 }) Harbor(HarborAction.BoardShip, id);
            Assert.AreEqual(0, sim.CrewAshore);
            Assert.AreEqual(24, sim.State.Wallet);
        }

        [Test] public void ActiveCargoRelayLocksManifestButNewPortOffersAnotherJob()
        {
            Harbor(HarborAction.GoAshore);
            sim.ToggleDockJob(Client);
            Harbor(HarborAction.BoardShip);
            Use(0);
            Assert.AreEqual(0, sim.State.Boarded);
            Harbor(HarborAction.GoAshore);
            sim.ToggleDockJob(Client);
            Harbor(HarborAction.BoardShip);
            Depart(); Arrive();
            Harbor(HarborAction.GoAshore);
            sim.ToggleDockJob(Client);
            Assert.IsTrue(sim.State.DockJobActive);
            Assert.AreEqual(0, sim.State.DockJobProgress);
        }
    }
}
