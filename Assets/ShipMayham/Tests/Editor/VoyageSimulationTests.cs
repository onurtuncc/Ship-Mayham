using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class VoyageSimulationTests
    {
        private VoyageSimulation sim;
        private const ulong Host = 10, Client = 11;

        [SetUp]
        public void SetUp()
        {
            sim = new VoyageSimulation(new[] {
                new StationDefinition(StationKind.Manifest, new Vector2(-4,-4)),
                new StationDefinition(StationKind.Helm, new Vector2(0,6.8f)),
                new StationDefinition(StationKind.Food, new Vector2(-4,0)),
                new StationDefinition(StationKind.Drink, new Vector2(-4,3)),
                new StationDefinition(StationKind.Repair, new Vector2(4,-3)),
                new StationDefinition(StationKind.EngineUpgrade, new Vector2(3.8f,-7.8f)),
                new StationDefinition(StationKind.CapacityUpgrade, new Vector2(0,-7.8f)),
                new StationDefinition(StationKind.ComfortUpgrade, new Vector2(-3.8f,-7.8f)) },
                new[] { new Vector2(-1.9f,-1.5f), new Vector2(1.9f,-1.5f), new Vector2(-1.9f,.6f), new Vector2(1.9f,.6f),
                    new Vector2(-1.9f,2.7f), new Vector2(1.9f,2.7f), new Vector2(-1.9f,4.8f), new Vector2(1.9f,4.8f) });
            sim.Join(Host);
        }

        [Test] public void CrewAcceleratesAndStopsInsteadOfTeleportingBetweenSpeeds()
        {
            int slot = sim.FindCrew(Host);
            sim.Crew[slot].Position = new Vector2(0, -5);
            sim.SetInput(Host, Vector2.right);
            sim.Tick(.02f);
            float first = sim.Crew[slot].Position.x;
            Assert.Greater(first, 0);
            Assert.Less(first, CrewKinematics.WalkSpeed * .02f * .5f, "The first tick must be a fraction of full speed.");
            for (int i = 0; i < 10; i++) { sim.SetInput(Host, Vector2.right); sim.Tick(.02f); }
            float cruising = sim.Crew[slot].Position.x;
            sim.SetInput(Host, Vector2.zero);
            sim.Tick(.02f);
            Assert.Greater(sim.Crew[slot].Position.x, cruising, "Releasing the stick still slides a little.");
            for (int i = 0; i < 12; i++) sim.Tick(.02f);
            float settled = sim.Crew[slot].Position.x;
            sim.Tick(.02f);
            Assert.AreEqual(settled, sim.Crew[slot].Position.x, 1e-4f, "...and then stops.");
        }

        [Test] public void WalkingIntoAStationStopsWithoutBuildingUpSpeedAgainstIt()
        {
            int slot = sim.FindCrew(Host);
            sim.Crew[slot].Position = new Vector2(-2.6f, 0);        // Food counter at (-4, 0)
            for (int i = 0; i < 60; i++) { sim.SetInput(Host, Vector2.left); sim.Tick(.02f); }
            float blocked = sim.Crew[slot].Position.x;
            sim.SetInput(Host, Vector2.right);
            sim.Tick(.02f);
            Assert.Greater(sim.Crew[slot].Position.x, blocked, "Reversing must respond at once; no stored-up speed against the wall.");
            Assert.Less(sim.Crew[slot].Position.x - blocked, CrewKinematics.WalkSpeed * .02f * .6f);
        }

        [Test] public void ShipSpeedAndHeadingEaseInAtTheHelm()
        {
            sim.Crew[0].Position = sim.Stations[1].Position + Vector2.right;
            sim.Interact(Host, 1);
            Assert.IsTrue(sim.Crew[0].AtHelm);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.SetInput(Host, new Vector2(1, 1));
            sim.Tick(.02f);
            Assert.Less(sim.State.Speed, .2f, "Speed builds gradually.");
            Assert.Less(sim.State.Heading, .5f, "The rudder builds up instead of snapping to the full turn rate.");
            for (int i = 0; i < 75; i++) { sim.SetInput(Host, new Vector2(1, 1)); sim.Tick(.02f); }
            Assert.Greater(sim.State.Speed, 3f);
            Assert.Greater(sim.State.Heading, 20f);
            float heading = sim.State.Heading;
            sim.SetInput(Host, new Vector2(0, 1));
            sim.Tick(.02f);
            Assert.Greater(sim.State.Heading, heading, "The ship keeps swinging briefly after the wheel is released.");
        }

        private void Advance(float seconds)
        {
            for (int n = 0; n < Mathf.CeilToInt(seconds / .05f); n++) sim.Tick(.05f);
        }

        private string Use(int station, ulong client = Host)
        {
            int slot = sim.FindCrew(client);
            sim.Crew[slot].Position = station < sim.Stations.Length ? sim.Stations[station].Position + Vector2.right : sim.Seats[station - sim.Stations.Length] + Vector2.right;
            Advance(.4f);
            return sim.Interact(client, station);
        }

        private void Depart()
        {
            Use(0);
            Use(1);
            sim.HelmAction(Host);
        }

        [Test] public void CrewLimitAndDuplicateJoinAreEnforced()
        {
            Assert.AreEqual(0, sim.Join(Host));
            Assert.AreEqual(1, sim.Join(11));
            Assert.AreEqual(2, sim.Join(12));
            Assert.AreEqual(3, sim.Join(13));
            Assert.AreEqual(-1, sim.Join(14));
            sim.Leave(11);
            Assert.AreEqual(1, sim.Join(14));
        }

        [Test] public void CannotDepartWithoutPassengers()
        {
            Use(1);
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
        }

        [Test] public void RemoteOrUnknownPlayerCannotBoardPassengers()
        {
            sim.Interact(Host, 0);
            sim.Interact(999, 0);
            sim.Interact(Host, 999);
            Assert.AreEqual(0, sim.State.Boarded);
        }

        [Test] public void ManifestDoesNotDuplicatePassengers()
        {
            Use(0); Use(0);
            Assert.AreEqual(4, sim.State.Boarded);
            Assert.IsFalse(sim.Passengers[4].Active);
        }

        [Test] public void OnlyOnePlayerCanUseHelm()
        {
            sim.Join(Client);
            Use(1); Use(1, Client);
            Assert.IsTrue(sim.Crew[0].AtHelm);
            Assert.IsFalse(sim.Crew[1].AtHelm);
            sim.Leave(Host);
            Use(1, Client);
            Assert.IsTrue(sim.Crew[1].AtHelm);
        }

        [Test] public void IncorrectOrderDoesNotConsumeItemOrPay()
        {
            Use(0); Use(3); Use(8);
            Assert.AreEqual(ServiceItem.Drink, sim.Crew[0].Carrying);
            Assert.AreEqual(ServiceItem.Food, sim.Passengers[0].Request);
            Assert.AreEqual(0, sim.State.Served);
        }

        [Test] public void CorrectOrderIsPaidOnceAndResetsRequest()
        {
            Use(0); Use(2); Use(8); Use(8);
            Assert.AreEqual(1, sim.State.Served);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            Assert.AreEqual(ServiceItem.None, sim.Passengers[0].Request);
            Assert.AreEqual(15, sim.State.FoodStock);
            Assert.AreEqual(0, sim.State.Wallet, "Income settles on arrival, not when serving.");
        }

        [Test] public void SecondPlayerCannotServeAlreadyFulfilledOrder()
        {
            sim.Join(Client);
            Use(0); Use(2); Use(2, Client); Use(8); Use(8, Client);
            Assert.AreEqual(1, sim.State.Served);
            Assert.AreEqual(ServiceItem.Food, sim.Crew[1].Carrying);
        }

        [Test] public void DisconnectReturnsHeldStockAndReleasesHelm()
        {
            Use(2); Use(1);
            sim.Leave(Host);
            Assert.AreEqual(16, sim.State.FoodStock);
            Assert.IsFalse(sim.Crew[0].Active);
            Assert.AreEqual(0, sim.State.Speed);
        }

        [Test] public void StockCannotBecomeNegative()
        {
            sim.State.FoodStock = 0;
            Use(2);
            Assert.AreEqual(ServiceItem.None, sim.Crew[0].Carrying);
            Assert.AreEqual(0, sim.State.FoodStock);
        }

        [Test] public void LeakDamagesHullAndFourPatchesStopIt()
        {
            Depart();
            Advance(27);
            Assert.AreEqual(1, sim.State.Leak);
            Assert.Less(sim.State.Health, 100);
            sim.Interact(Host, 1); // release helm
            for (int i = 0; i < 4; i++) Use(4);
            Assert.AreEqual(0, sim.State.Leak);
            float health = sim.State.Health;
            Advance(5);
            Assert.AreEqual(health, sim.State.Health);
        }

        [Test] public void CannotDockFarAwayOrTooFast()
        {
            Depart(); sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Sailing, sim.State.Phase);
            sim.State.Position = VoyageSimulation.Destination;
            sim.State.Speed = 4;
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Sailing, sim.State.Phase);
        }

        [Test] public void FullVoyageThroughThrottleBrakeAndSettlementPaysOnlyOnce()
        {
            Use(0); Use(2); Use(8); Use(1); sim.HelmAction(Host);
            for (int i = 0; i < 2000 && sim.State.Position.y < 211; i++)
            {
                sim.SetInput(Host, Vector2.up);
                sim.Tick(.05f);
            }
            Assert.GreaterOrEqual(sim.State.Position.y, 211);
            for (int i = 0; i < 30; i++) { sim.SetInput(Host, Vector2.down); sim.Tick(.05f); }
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase);
            Assert.AreEqual(120, sim.State.LastTickets, "One unpaid passenger was not checked/charged on this voyage.");
            Assert.AreEqual(8, sim.State.LastServices);
            Assert.Greater(sim.State.Wallet, 0);
            int wallet = sim.State.Wallet;
            sim.HelmAction(Host);
            Assert.AreEqual(wallet, sim.State.Wallet);
            Use(5);
            Assert.AreEqual(1, sim.State.EngineLevel);
            Assert.AreEqual(wallet - 100, sim.State.Wallet);
            Use(0);
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
            Assert.AreEqual(2, sim.State.Voyage);
            Assert.AreEqual(1, sim.State.EngineLevel);
            Assert.AreEqual(0, sim.State.Boarded);
        }

        [Test] public void FailedVoyageCanRestartWithoutDebtSoftlock()
        {
            Depart();
            sim.State.Leak = 1;
            sim.State.Health = .01f;
            sim.Tick(.05f);
            Assert.AreEqual(VoyagePhase.Failed, sim.State.Phase);
            Assert.AreEqual(0, sim.State.Wallet);
            Assert.AreEqual(0, sim.State.LastTickets);
            Use(0); Use(0);
            Assert.AreEqual(4, sim.State.Boarded);
            Assert.AreEqual(100, sim.State.Health);
        }

        [Test] public void UpgradeRequiresMoneyPortAndRespectsCap()
        {
            Use(5);
            Assert.AreEqual(0, sim.State.EngineLevel);
            sim.State.Wallet = 1000;
            Use(5); Use(5); Use(5);
            Assert.AreEqual(2, sim.State.EngineLevel);
            Assert.AreEqual(700, sim.State.Wallet);
            Depart(); sim.Interact(Host, 1); Use(6);
            Assert.AreEqual(0, sim.State.CapacityLevel);
        }

        [Test] public void CapacityUpgradeAddsPassengersAtNextBoarding()
        {
            sim.State.Wallet = 1000;
            Use(6); Use(6); Use(0);
            Assert.AreEqual(8, sim.State.Boarded);
            Assert.IsTrue(sim.Passengers[7].Active);
        }

        [Test] public void JumpHasOneMeterArcLandsAndRejectsAirborneSpam()
        {
            sim.Jump(Host);
            Assert.AreEqual(VoyageSimulation.JumpSpeed, sim.Crew[0].VerticalSpeed);
            float peak = 0;
            for (int frame = 0; frame < 12; frame++)
            {
                sim.Tick(.05f);
                peak = Mathf.Max(peak, sim.Crew[0].JumpHeight);
                float velocity = sim.Crew[0].VerticalSpeed;
                sim.Jump(Host);
                Assert.AreEqual(velocity, sim.Crew[0].VerticalSpeed);
            }
            Assert.That(peak, Is.InRange(.95f, 1.01f));
            Advance(.2f);
            Assert.AreEqual(0, sim.Crew[0].JumpHeight);
            Assert.AreEqual(0, sim.Crew[0].VerticalSpeed);
            sim.Jump(Host);
            Assert.AreEqual(VoyageSimulation.JumpSpeed, sim.Crew[0].VerticalSpeed);
        }

        [Test] public void JumpAllowsHorizontalMovementButRetainsDeckBounds()
        {
            sim.Crew[0].Position = new Vector2(-5, -6);
            sim.Jump(Host);
            sim.SetInput(Host, Vector2.left);
            Advance(.2f);
            Assert.Less(sim.Crew[0].Position.x, -5);
            Assert.GreaterOrEqual(sim.Crew[0].Position.x, -VoyageSimulation.DeckBounds.x);
            Assert.Greater(sim.Crew[0].JumpHeight, 0);
        }

        [Test] public void HelmAndUnknownClientsCannotJump()
        {
            Use(1);
            sim.Jump(Host); sim.Jump(999);
            Advance(.2f);
            Assert.AreEqual(0, sim.Crew[0].JumpHeight);
            Assert.AreEqual(0, sim.Crew[0].VerticalSpeed);
            Assert.IsTrue(sim.Crew[0].AtHelm);
        }

        [Test] public void AirborneCrewCannotClaimStationUntilLanding()
        {
            sim.Crew[0].Position = sim.Stations[1].Position + Vector2.down;
            sim.Jump(Host);
            sim.Interact(Host, 1);
            Assert.IsFalse(sim.Crew[0].AtHelm);
            Advance(.8f);
            sim.Interact(Host, 1);
            Assert.IsTrue(sim.Crew[0].AtHelm);
        }

        [Test] public void JumpKeepsCarriedItemAndStock()
        {
            Use(3);
            sim.Jump(Host); Advance(.3f);
            Assert.AreEqual(ServiceItem.Drink, sim.Crew[0].Carrying);
            Assert.AreEqual(15, sim.State.DrinkStock);
        }

        [Test] public void DisconnectAndNewVoyageClearJumpState()
        {
            sim.Join(Client); sim.Jump(Client); Advance(.2f);
            sim.Leave(Client); sim.Join(Client);
            Assert.AreEqual(0, sim.Crew[1].JumpHeight);
            Assert.AreEqual(0, sim.Crew[1].VerticalSpeed);
            Depart(); sim.State.Position = VoyageSimulation.Destination; sim.HelmAction(Host);
            sim.Jump(Client);
            Use(0); // Another crew member can reset the voyage while this crew member is airborne.
            Assert.AreEqual(2, sim.State.Voyage);
            Assert.AreEqual(0, sim.Crew[1].JumpHeight);
            Assert.AreEqual(0, sim.Crew[1].VerticalSpeed);
        }

        [Test] public void JumpChangesParticipateInNetworkSnapshotEquality()
        {
            var before = sim.Crew[0];
            sim.Jump(Host);
            Assert.IsFalse(before.Equals(sim.Crew[0]));
            var afterTakeoff = sim.Crew[0];
            sim.Tick(.05f);
            Assert.IsFalse(afterTakeoff.Equals(sim.Crew[0]));
        }

        [Test] public void CrewMovementIsBoundedAndExpiredInputsStopWalking()
        {
            for (int i = 0; i < 100; i++) { sim.SetInput(Host, Vector2.left * 100); sim.Tick(.1f); }
            Assert.GreaterOrEqual(sim.Crew[0].Position.x, -VoyageSimulation.DeckBounds.x);
            Advance(1);
            Vector2 stopped = sim.Crew[0].Position;
            Advance(1);
            Assert.AreEqual(stopped, sim.Crew[0].Position);
            sim.SetInput(Host, new Vector2(float.NaN, float.PositiveInfinity));
            Advance(.1f);
            Assert.IsFalse(float.IsNaN(sim.Crew[0].Position.x));
        }
    }
}
