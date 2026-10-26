using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>The voyage chain: islands and their unlocks, the destination choice, captain's tasks, the flagship and the victory.</summary>
    public sealed class CampaignTests
    {
        private const ulong Host = 10, Mate = 11;
        private VoyageSimulation sim;
        private bool walkingBefore;
        private readonly List<string> notices = new List<string>();

        [SetUp]
        public void SetUp()
        {
            walkingBefore = VoyageSimulation.WalkingPassengers;
            VoyageSimulation.WalkingPassengers = false;
            var seats = new Vector2[8];
            for (int i = 0; i < 8; i++) seats[i] = new Vector2(i % 2 == 0 ? -1.9f : 1.9f, -1.5f + i / 2 * 2.1f);
            sim = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
                new StationDefinition(StationKind.Cannon, new Vector2(4, 6)),
                new StationDefinition(StationKind.Power, new Vector2(4, 0)),
            }, seats, true);
            sim.Join(Host); sim.Join(Mate);
            notices.Clear();
            sim.CrewNotice += (slot, text) => notices.Add(text);
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        private void Tick(float seconds = .1f) { for (float t = 0; t < seconds; t += .02f) sim.Tick(.02f); }

        private void AtOffice(ulong client = Mate)
        {
            int slot = sim.FindCrew(client);
            sim.Crew[slot].OnShore = true;
            sim.Crew[slot].Position = VoyageSimulation.HarborPoint(HarborAction.HarborMaster);
            Tick(.4f);
        }

        /// <summary>Docks at the current target and resets for the next voyage, like the crew pressing the manifest.</summary>
        private void SailToTargetAndBack()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = VoyageSimulation.DestinationPoint(sim.State);
            sim.State.Speed = 0;
            sim.Crew[0].AtHelm = true;
            sim.HelmAction(Host);
            Assert.AreEqual(VoyagePhase.Arrived, sim.State.Phase);
            sim.Crew[0].AtHelm = false;
        }

        private void NextVoyage()
        {
            sim.Crew[0].Position = sim.Stations[0].Position + Vector2.right;
            Tick(.4f);
            Assert.That(sim.Interact(Host, 0), Does.Contain("Back at the departure port"));
        }

        // -------------------------------------------------------------------- the data

        [Test] public void EveryIslandIsNamedAndSpacedOut()
        {
            Assert.AreEqual(6, Campaign.Count);
            for (int i = 0; i < Campaign.Count; i++)
            {
                Assert.IsNotEmpty(Campaign.Name(i));
                for (int j = i + 1; j < Campaign.Count; j++)
                {
                    float distance = Vector2.Distance(Campaign.World(i), Campaign.World(j));
                    Assert.Greater(distance, 150f, $"{i} and {j} are too close to tell apart at sea");
                }
            }
        }

        [Test] public void TheTwoStarterIslandsKeepTheirOldPlaces()
        {
            Assert.AreEqual(Vector2.zero, Campaign.World(Campaign.BrassQuay));
            Assert.AreEqual(VoyageSimulation.Destination, Campaign.World(Campaign.NorthHaven));
        }

        [Test] public void TheWorldBoundsEnclosEveryIsland()
        {
            for (int i = 0; i < Campaign.Count; i++)
            {
                Assert.Greater(Campaign.World(i).x, Campaign.WorldMin.x + 100f);
                Assert.Less(Campaign.World(i).x, Campaign.WorldMax.x - 100f);
                Assert.Greater(Campaign.World(i).y, Campaign.WorldMin.y + 100f);
                Assert.Less(Campaign.World(i).y, Campaign.WorldMax.y - 100f);
            }
        }

        [Test] public void IslandsOpenOneTaskAtATime()
        {
            Assert.IsTrue(Campaign.Unlocked(Campaign.NorthHaven, 0, 0, false));
            Assert.IsFalse(Campaign.Unlocked(Campaign.CoralCay, 0, 99, false), "reputation alone is not enough");
            int haven = 1 << Campaign.NorthHaven;
            Assert.IsFalse(Campaign.Unlocked(Campaign.CoralCay, haven, 0, false), "and tokens alone are not enough");
            Assert.IsTrue(Campaign.Unlocked(Campaign.CoralCay, haven, 2, false));
            Assert.IsTrue(Campaign.Unlocked(Campaign.SmugglersCove, haven, 2, false));
            Assert.IsFalse(Campaign.Unlocked(Campaign.StormReef, haven, 9, false));
            Assert.IsTrue(Campaign.Unlocked(Campaign.StormReef, haven | 1 << Campaign.CoralCay, 5, false));
            int four = haven | 1 << Campaign.CoralCay | 1 << Campaign.SmugglersCove | 1 << Campaign.StormReef;
            Assert.IsFalse(Campaign.Unlocked(Campaign.SkullAtoll, four, 8, false));
            Assert.IsTrue(Campaign.Unlocked(Campaign.SkullAtoll, four, 9, false));
        }

        [Test] public void AfterTheVictoryEveryIslandIsOpen()
        {
            for (int i = 0; i < Campaign.Count; i++) Assert.IsTrue(Campaign.Unlocked(i, 0, 0, true));
            Assert.AreEqual(string.Empty, Campaign.LockReason(Campaign.SkullAtoll, 0, 0, true));
        }

        [Test] public void LockReasonsSayWhatIsMissing()
        {
            StringAssert.Contains("task", Campaign.LockReason(Campaign.CoralCay, 0, 9, false));
            StringAssert.Contains("reputation", Campaign.LockReason(Campaign.CoralCay, 1 << Campaign.NorthHaven, 0, false));
            Assert.AreEqual(string.Empty, Campaign.LockReason(Campaign.NorthHaven, 0, 0, false));
        }

        [Test] public void TheDefaultCourseLeadsOnwardAndNeverToWhereYouAre()
        {
            Assert.AreEqual(Campaign.NorthHaven, Campaign.DefaultTarget(Campaign.BrassQuay, 0, 0, false));
            Assert.AreEqual(Campaign.BrassQuay, Campaign.DefaultTarget(Campaign.NorthHaven, 1 << Campaign.NorthHaven, 0, false), "nothing new is open yet");
            int haven = 1 << Campaign.NorthHaven;
            Assert.AreEqual(Campaign.CoralCay, Campaign.DefaultTarget(Campaign.NorthHaven, haven, 2, false));
            Assert.AreEqual(Campaign.SmugglersCove, Campaign.DefaultTarget(Campaign.CoralCay, haven | 1 << Campaign.CoralCay, 2, false));
            for (int from = 0; from < Campaign.Count; from++) Assert.AreNotEqual(from, Campaign.DefaultTarget(from, 0, 0, true));
        }

        [Test] public void TheLogAsksForTheNextTaskThenTheFinale()
        {
            StringAssert.Contains("North Haven", Campaign.Objective(0, 0, false));
            StringAssert.Contains("Coral Cay", Campaign.Objective(1 << Campaign.NorthHaven, 2, false));
            int all = 1 << Campaign.NorthHaven | 1 << Campaign.CoralCay | 1 << Campaign.SmugglersCove | 1 << Campaign.StormReef;
            StringAssert.Contains("flagship", Campaign.Objective(all, 9, false));
            StringAssert.Contains("sunk", Campaign.Objective(all | 1 << Campaign.SkullAtoll, 9, true));
            StringAssert.Contains("out of reach", Campaign.Objective(1 << Campaign.NorthHaven, 0, false));
        }

        // -------------------------------------------------------------------- the rules

        [Test] public void ANewGameStartsAtTheHomeQuayBoundForNorthHaven()
        {
            Assert.AreEqual(Campaign.BrassQuay, sim.State.Island);
            Assert.AreEqual(Campaign.NorthHaven, sim.State.Target);
            Assert.AreEqual(VoyageSimulation.Destination, sim.DestinationPoint());
            Assert.AreEqual(Vector2.zero, sim.OriginPoint());
            Assert.AreEqual(new Vector3(-14, 0, 0), VoyageSimulation.HarborOrigin(sim.State));
        }

        [Test] public void TheShipStaysAtTheIslandItReached()
        {
            SailToTargetAndBack();
            Assert.AreEqual(Campaign.NorthHaven, VoyageSimulation.DockedIsland(sim.State));
            Assert.AreEqual(new Vector3(-14, 0, 220), VoyageSimulation.HarborOrigin(sim.State));
            NextVoyage();
            Assert.AreEqual(VoyagePhase.Boarding, sim.State.Phase);
            Assert.AreEqual(Campaign.NorthHaven, sim.State.Island);
            Assert.AreEqual(VoyageSimulation.Destination, sim.State.Position, "no teleport back to the start");
            Assert.AreEqual(Campaign.BrassQuay, sim.State.Target, "the way back is the only open course for now");
            Assert.AreEqual(new Vector3(-14, 0, 220), VoyageSimulation.HarborOrigin(sim.State));
        }

        [Test] public void ADestinationIsChosenAtTheHarborMasterOnly()
        {
            sim.State.TaskMask = 1 << Campaign.NorthHaven; sim.State.Reputation = 2;
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.BrassQuay;
            Assert.That(sim.SelectDestination(Mate, Campaign.CoralCay), Does.Contain("harbor master"), "not ashore");
            AtOffice();
            Assert.That(sim.SelectDestination(Mate, Campaign.CoralCay), Does.StartWith("Course set"));
            Assert.AreEqual(Campaign.CoralCay, sim.State.Target);
            sim.Crew[1].Position = VoyageSimulation.HarborPoint(HarborAction.BuyFood);
            Tick(.4f);
            Assert.That(sim.SelectDestination(Mate, Campaign.BrassQuay), Does.Contain("harbor master"), "too far from the office");
            Assert.AreEqual(Campaign.CoralCay, sim.State.Target);
        }

        [Test] public void LockedIslandsAndTheCurrentOneAreRefused()
        {
            AtOffice();
            Assert.That(sim.SelectDestination(Mate, Campaign.SkullAtoll), Does.Contain("out of reach"));
            Assert.That(sim.SelectDestination(Mate, Campaign.BrassQuay), Does.Contain("already here"));
            Assert.That(sim.SelectDestination(Mate, 99), Does.Contain("Unknown"));
            Assert.That(sim.SelectDestination(999, Campaign.NorthHaven), Does.Contain("Join"));
            Assert.AreEqual(Campaign.NorthHaven, sim.State.Target);
        }

        [Test] public void TheCourseIsFixedOnceThePassengersBoardOrTheShipSails()
        {
            sim.State.TaskMask = 1 << Campaign.NorthHaven; sim.State.Reputation = 2;
            AtOffice();
            sim.State.Boarded = 4;
            Assert.That(sim.SelectDestination(Mate, Campaign.CoralCay), Does.Contain("before boarding"));
            sim.State.Boarded = 0; sim.State.Phase = VoyagePhase.Sailing;
            Assert.That(sim.SelectDestination(Mate, Campaign.CoralCay), Does.Contain("new voyage"));
            Assert.AreEqual(Campaign.NorthHaven, sim.State.Target);
        }

        [Test] public void FinishingTheHarborJobEarnsTheFirstToken()
        {
            SailToTargetAndBack();
            int wallet = sim.State.Wallet, reputation = sim.State.Reputation;
            sim.State.DockJobPortKey = sim.State.PortVisit + 1;       // the same mark a finished job leaves
            Tick();
            Assert.IsTrue(Campaign.Has(sim.State.TaskMask, Campaign.NorthHaven));
            Assert.AreEqual(wallet + Campaign.TaskCoinReward, sim.State.Wallet);
            Assert.AreEqual(reputation + Campaign.TaskReputationReward, sim.State.Reputation);
            Assert.AreEqual(1, notices.FindAll(n => n.Contains("Captain's task")).Count / 2, "both crew members hear it once");
            Tick(2f);
            Assert.AreEqual(wallet + Campaign.TaskCoinReward, sim.State.Wallet, "paid once");
        }

        [Test] public void TheTaskOfAFinishedIslandOpensTheNextOnes()
        {
            SailToTargetAndBack();
            sim.State.Reputation = 1;
            sim.State.DockJobPortKey = sim.State.PortVisit + 1;
            Tick();
            Assert.IsTrue(notices.Exists(n => n.Contains("Coral Cay is open")), string.Join(" | ", notices));
            Assert.IsTrue(Campaign.Unlocked(Campaign.CoralCay, sim.State.TaskMask, sim.State.Reputation, false));
        }

        [Test] public void PearlsAtTheCayAreSixSunkenCoinsInOneVisit()
        {
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.CoralCay; sim.State.TaskMask = 1 << Campaign.NorthHaven; sim.State.Reputation = 2;
            SailToTargetAndBack();
            Tick(.1f);
            sim.State.CoinMask = (1 << (Campaign.PearlsNeeded - 1)) - 1;
            Tick(.1f);
            Assert.IsFalse(Campaign.Has(sim.State.TaskMask, Campaign.CoralCay));
            sim.State.CoinMask = (1 << Campaign.PearlsNeeded) - 1;
            Tick(.1f);
            Assert.IsTrue(Campaign.Has(sim.State.TaskMask, Campaign.CoralCay));
        }

        [Test] public void TheCoveWantsBothChestsOpened()
        {
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.SmugglersCove; sim.State.TaskMask = 1 << Campaign.NorthHaven; sim.State.Reputation = 2;
            SailToTargetAndBack();
            Tick(.1f);
            sim.State.ChestMask = 1;
            Tick(.1f);
            Assert.IsFalse(Campaign.Has(sim.State.TaskMask, Campaign.SmugglersCove));
            sim.State.ChestMask = 3;
            Tick(.1f);
            Assert.IsTrue(Campaign.Has(sim.State.TaskMask, Campaign.SmugglersCove));
        }

        [Test] public void TheReefTaskNeedsAHullThatSurvivedTheStorm()
        {
            sim.State.Island = Campaign.CoralCay; sim.State.Target = Campaign.StormReef;
            sim.State.TaskMask = 1 << Campaign.NorthHaven | 1 << Campaign.CoralCay; sim.State.Reputation = 5;
            sim.State.Health = Campaign.StormHullNeeded - 5;
            SailToTargetAndBack();
            Tick(.1f);
            Assert.IsFalse(Campaign.Has(sim.State.TaskMask, Campaign.StormReef), "battered");
        }

        [Test] public void ArrivingAtTheReefWithAGoodHullCompletesIt()
        {
            sim.State.Island = Campaign.CoralCay; sim.State.Target = Campaign.StormReef;
            sim.State.TaskMask = 1 << Campaign.NorthHaven | 1 << Campaign.CoralCay; sim.State.Reputation = 5;
            sim.State.Health = Campaign.StormHullNeeded + 10;
            SailToTargetAndBack();
            Tick(.1f);
            Assert.IsTrue(Campaign.Has(sim.State.TaskMask, Campaign.StormReef));
        }

        [Test] public void TasksAreOnlyCheckedAtTheirOwnIsland()
        {
            SailToTargetAndBack();                                     // at North Haven
            sim.State.CoinMask = 0x3fff; sim.State.ChestMask = 3;
            Tick(.2f);
            Assert.IsFalse(Campaign.Has(sim.State.TaskMask, Campaign.CoralCay));
            Assert.IsFalse(Campaign.Has(sim.State.TaskMask, Campaign.SmugglersCove));
        }

        [Test] public void ArrivingAtANewIslandPaysAFareBonusAndADiscoveryReward()
        {
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.CoralCay; sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = Campaign.World(Campaign.CoralCay);
            sim.Crew[0].AtHelm = true;
            sim.HelmAction(Host);
            Assert.AreEqual(Campaign.Island(Campaign.CoralCay).FareBonus + Campaign.DiscoveryReward, sim.State.LastRouteBonus);
            Assert.IsTrue(Campaign.Has(sim.State.VisitedMask, Campaign.CoralCay));
            Assert.AreEqual(1, sim.State.PortVisit);
        }

        [Test] public void ComingBackPaysOnlyTheFare()
        {
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.CoralCay; sim.State.Phase = VoyagePhase.Sailing;
            sim.State.VisitedMask |= 1 << Campaign.CoralCay;
            sim.State.Position = Campaign.World(Campaign.CoralCay);
            sim.Crew[0].AtHelm = true;
            sim.HelmAction(Host);
            Assert.AreEqual(Campaign.Island(Campaign.CoralCay).FareBonus, sim.State.LastRouteBonus);
        }

        [Test] public void LootAndPortJobsAreNotRenewedByStartingANewVoyageAtTheSameIsland()
        {
            SailToTargetAndBack();
            sim.State.ChestMask = 1; sim.State.ActivityPortKey = sim.State.PortVisit + 2; sim.State.CoinMask = 5;
            sim.State.DockJobPortKey = sim.State.PortVisit + 1;
            NextVoyage();
            Tick(.2f);
            Assert.AreEqual(1, sim.State.ChestMask);
            Assert.AreEqual(5, sim.State.CoinMask);
            Assert.AreEqual(sim.State.PortVisit + 1, sim.State.DockJobPortKey, "this stay's job stays done");
        }

        [Test] public void AFailedVoyageStartsAgainFromTheIslandItLeft()
        {
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.BrassQuay;
            sim.State.Phase = VoyagePhase.Failed;
            sim.Crew[0].Position = sim.Stations[0].Position + Vector2.right;
            Tick(.4f);
            sim.Interact(Host, 0);
            Assert.AreEqual(Campaign.NorthHaven, sim.State.Island);
            Assert.AreEqual(VoyageSimulation.Destination, sim.State.Position);
        }

        [Test] public void ProgressSurvivesNewVoyagesAndMigration()
        {
            sim.State.TaskMask = 6; sim.State.VisitedMask = 15; sim.State.PortVisit = 7; sim.State.Cycle = 2; sim.State.Victory = true;
            SailToTargetAndBack();
            NextVoyage();
            Assert.AreEqual(6, sim.State.TaskMask); Assert.AreEqual(2, sim.State.Cycle); Assert.IsTrue(sim.State.Victory);
            Assert.AreEqual(8, sim.State.PortVisit);
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true);
            restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.AreEqual(sim.State.Island, restored.State.Island);
            Assert.AreEqual(sim.State.Target, restored.State.Target);
            Assert.AreEqual(6, restored.State.TaskMask);
            Assert.AreEqual(15 | 1 << sim.State.Island, restored.State.VisitedMask | 1 << sim.State.Island);
        }

        [Test] public void TheCareerSaveKeepsTheChainAndRejectsGarbage()
        {
            var state = new VoyageState { Island = 3, TaskMask = 0xffff, VisitedMask = 0xffff, PortVisit = 9, Cycle = 1, Victory = true, Reputation = 7 };
            var career = CareerProgress.Capture(state);
            Assert.AreEqual(3, career.Island);
            Assert.AreEqual((1 << Campaign.Count) - 1, career.TaskMask, "no bits beyond the islands that exist");
            var restored = new VoyageState();
            career.Apply(ref restored);
            Assert.AreEqual(3, restored.Island);
            Assert.AreEqual(Campaign.World(3), restored.Position);
            Assert.AreNotEqual(3, restored.Target);
            Assert.IsTrue(restored.Victory);
            Assert.AreEqual(1, restored.Cycle);
            career.Island = 99;
            career.Apply(ref restored);
            Assert.AreEqual(0, restored.Island);
        }

        [Test] public void AnOldCareerSaveStartsAtTheHomeQuay()
        {
            Assert.IsTrue(CareerProgressStore.TryDecode("{\"Version\":2,\"Wallet\":55}", out var progress));
            var state = new VoyageState();
            progress.Apply(ref state);
            Assert.AreEqual(Campaign.BrassQuay, state.Island);
            Assert.AreEqual(Campaign.NorthHaven, state.Target);
            Assert.AreEqual(Vector2.zero, state.Position);
            Assert.AreEqual(Campaign.StarterIslands, state.VisitedMask);
        }

        [Test] public void TheStormyIslandForcesTheStormContractAndOnlyThat()
        {
            sim.State.Island = Campaign.CoralCay; sim.State.Target = Campaign.NorthHaven;
            sim.State.TaskMask = 1 << Campaign.NorthHaven | 1 << Campaign.CoralCay; sim.State.Reputation = 5;
            AtOffice();
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
            Assert.That(sim.SelectDestination(Mate, Campaign.StormReef), Does.StartWith("Course set"));
            Assert.AreEqual(RouteProfile.StormPassage, sim.State.Route);
            Tick(.4f);
            Assert.That(sim.SelectRoute(Mate, RouteProfile.CoastalFerry), Does.Contain("always stormy"));
            Assert.AreEqual(RouteProfile.StormPassage, sim.State.Route);
            Tick(.4f);
            Assert.That(sim.SelectDestination(Mate, Campaign.NorthHaven), Does.StartWith("Course set"));
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route, "the storm contract ends with the storm island");
        }

        [Test] public void AMarketRunContractIsKeptWhenTheCourseChangesBetweenCalmIslands()
        {
            sim.State.Island = Campaign.NorthHaven; sim.State.Target = Campaign.BrassQuay;
            sim.State.TaskMask = 1 << Campaign.NorthHaven; sim.State.Reputation = 4;
            AtOffice();
            Assert.That(sim.SelectRoute(Mate, RouteProfile.MarketRun), Does.StartWith("Market Run"));
            Tick(.4f);
            sim.SelectDestination(Mate, Campaign.CoralCay);
            Assert.AreEqual(RouteProfile.MarketRun, sim.State.Route);
        }

        [Test] public void ANewVoyageToTheReefStartsWithTheStormContract()
        {
            sim.State.Island = Campaign.CoralCay; sim.State.Target = Campaign.StormReef; sim.State.Phase = VoyagePhase.Sailing;
            sim.State.TaskMask = 1 << Campaign.NorthHaven | 1 << Campaign.CoralCay; sim.State.Reputation = 9;
            sim.State.Position = Campaign.World(Campaign.StormReef);
            sim.Crew[0].AtHelm = true;
            sim.HelmAction(Host);
            sim.Crew[0].AtHelm = false;
            NextVoyage();
            Assert.AreEqual(Campaign.StormReef, sim.State.Island);
            Assert.AreNotEqual(Campaign.StormReef, sim.State.Target);
            Assert.AreEqual(RouteProfile.CoastalFerry, sim.State.Route);
        }

        // -------------------------------------------------------------------- the finale

        private void SetUpFinale()
        {
            sim.State.Island = Campaign.StormReef; sim.State.Target = Campaign.SkullAtoll; sim.State.Reputation = 9;
            sim.State.TaskMask = 1 << Campaign.NorthHaven | 1 << Campaign.CoralCay | 1 << Campaign.SmugglersCove | 1 << Campaign.StormReef;
            sim.State.Position = Campaign.World(Campaign.StormReef);
            AtOffice();
            sim.State.Phase = VoyagePhase.Boarding;
        }

        [Test] public void TheFlagshipWaitsAtTheLairAndOnlyThere()
        {
            Assert.IsFalse(VoyageSimulation.FlagshipVoyage(sim.State));
            SetUpFinale();
            Assert.IsTrue(VoyageSimulation.FlagshipVoyage(sim.State));
            Assert.That(sim.SelectDestination(Mate, Campaign.SkullAtoll), Does.StartWith("Course set"));
            Assert.IsTrue(sim.Encounter.Boss, "the encounter is switched to the flagship");
            Tick(.4f);
            sim.SelectDestination(Mate, Campaign.CoralCay);
            Assert.IsFalse(sim.Encounter.Boss, "and back to an ordinary raider");
        }

        [Test] public void TheFlagshipIsTougherAndNeverLeaves()
        {
            SetUpFinale();
            sim.SelectDestination(Mate, Campaign.SkullAtoll);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Elapsed = VoyageSimulation.BossArrivalSeconds + 1;
            sim.Tick(.05f);
            Assert.AreEqual(PiratePhase.Warning, sim.Encounter.Pirate);
            Tick(7f);
            Assert.AreEqual(PiratePhase.Attacking, sim.Encounter.Pirate);
            sim.Encounter.AttackRemaining = 0;
            Tick(1f);
            Assert.AreEqual(PiratePhase.Attacking, sim.Encounter.Pirate, "an ordinary raider would have given up by now");
        }

        [Test] public void FlagshipHitsTakeAFractionOfTheHealth()
        {
            SetUpFinale();
            sim.SelectDestination(Mate, Campaign.SkullAtoll);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Encounter.Pirate = PiratePhase.Attacking;
            sim.Encounter.PiratePosition = new Vector2(0, 100);
            sim.Encounter.CrewBall = new CannonballState { Active = true, Position = new Vector3(0, 1.5f, 85), Velocity = Vector3.forward * 400, Remaining = 1 };
            sim.Tick(.1f);
            Assert.AreEqual(1, sim.Encounter.Hits);
            float taken = 100 - sim.Encounter.PirateHealth;
            Assert.AreEqual(CannonProgression.BaseHitDamage / VoyageSimulation.BossToughness, taken, .1f);
        }

        [Test] public void TheHarborStaysShutWhileTheFlagshipLives()
        {
            SetUpFinale();
            sim.SelectDestination(Mate, Campaign.SkullAtoll);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = Campaign.World(Campaign.SkullAtoll); sim.State.Speed = 0;
            sim.Crew[0].AtHelm = true;
            Assert.That(sim.HelmAction(Host), Does.Contain("flagship"));
            Assert.AreEqual(VoyagePhase.Sailing, sim.State.Phase);
            sim.Encounter.Pirate = PiratePhase.Defeated;
            Assert.That(sim.HelmAction(Host), Does.StartWith("Docked"));
        }

        [Test] public void SinkingTheFlagshipAndDockingWinsTheGame()
        {
            SetUpFinale();
            sim.SelectDestination(Mate, Campaign.SkullAtoll);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = Campaign.World(Campaign.SkullAtoll); sim.State.Speed = 0;
            sim.Encounter.Pirate = PiratePhase.Defeated; sim.Encounter.Bounty = VoyageSimulation.BossBounty;
            sim.Crew[0].AtHelm = true;
            sim.HelmAction(Host);
            Assert.IsTrue(sim.State.Victory);
            Assert.IsTrue(Campaign.Has(sim.State.TaskMask, Campaign.SkullAtoll));
            Assert.AreEqual(VoyageSimulation.BossBounty, sim.State.LastBounty);
            for (int i = 0; i < Campaign.Count; i++) Assert.IsTrue(Campaign.Unlocked(i, sim.State.TaskMask, sim.State.Reputation, sim.State.Victory));
        }

        [Test] public void AfterTheVictoryTheLairHasNoFlagshipUntilANewCycle()
        {
            SetUpFinale();
            sim.State.Victory = true;
            Assert.IsFalse(VoyageSimulation.FlagshipVoyage(sim.State));
            sim.State.Victory = false;
            sim.State.Victory = true;
            sim.State.Island = Campaign.SkullAtoll; sim.State.Target = Campaign.BrassQuay; sim.State.Phase = VoyagePhase.Boarding;
            Assert.That(sim.StartNewCycle(Mate), Does.Contain("cycle 1"));
            Assert.IsFalse(sim.State.Victory);
            Assert.AreEqual(0, sim.State.TaskMask);
            Assert.AreEqual(1, sim.State.Cycle);
            Assert.That(sim.StartNewCycle(Mate), Does.Contain("Sink"), "only once per victory");
        }

        [Test] public void ANewCycleNeedsAVictoryAndAnEmptyShip()
        {
            Assert.That(sim.StartNewCycle(Mate), Does.Contain("Sink"));
            sim.State.Victory = true; sim.State.Boarded = 2;
            Assert.That(sim.StartNewCycle(Mate), Does.Contain("before boarding"));
            Assert.That(sim.StartNewCycle(999), Does.Contain("Join"));
            Assert.AreEqual(0, sim.State.Cycle);
        }

        [Test] public void LaterCyclesMakeTheFlagshipHarder()
        {
            SetUpFinale();
            sim.State.Cycle = 2;
            sim.SelectDestination(Mate, Campaign.SkullAtoll);
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Encounter.Pirate = PiratePhase.Attacking;
            sim.Encounter.PiratePosition = new Vector2(0, 100);
            sim.Encounter.CrewBall = new CannonballState { Active = true, Position = new Vector3(0, 1.5f, 85), Velocity = Vector3.forward * 400, Remaining = 1 };
            sim.Tick(.1f);
            float taken = 100 - sim.Encounter.PirateHealth;
            Assert.AreEqual(CannonProgression.BaseHitDamage / (VoyageSimulation.BossToughness + VoyageSimulation.BossCycleToughness * 2), taken, .1f);
        }

        // -------------------------------------------------------------------- the sea around

        [Test] public void ShoalsLieBetweenTheTwoIslandsOfTheRoute()
        {
            var buffer = new Shoal[HazardLogic.MaxShoals];
            Vector2 from = Campaign.World(Campaign.NorthHaven), to = Campaign.World(Campaign.CoralCay);
            int count = HazardLogic.Generate(7, RouteProfile.StormPassage, from, to, buffer);
            Assert.Greater(count, 0);
            Vector2 direction = (to - from).normalized;
            float length = Vector2.Distance(from, to);
            for (int i = 0; i < count; i++)
            {
                float along = Vector2.Dot(buffer[i].Center - from, direction);
                Assert.Greater(along, HazardLogic.SafeFromStart - 1f);
                Assert.Less(along, length - HazardLogic.SafeFromPort + 1f);
                Assert.GreaterOrEqual(Vector2.Distance(buffer[i].Center, from), HazardLogic.SafeFromStart + buffer[i].Radius - .01f);
                Assert.GreaterOrEqual(Vector2.Distance(buffer[i].Center, to), HazardLogic.SafeFromPort + buffer[i].Radius - .01f);
            }
        }

        [Test] public void TheOriginalRouteStillLaysTheSameShoals()
        {
            var a = new Shoal[HazardLogic.MaxShoals]; var b = new Shoal[HazardLogic.MaxShoals];
            int na = HazardLogic.Generate(42, RouteProfile.StormPassage, a);
            int nb = HazardLogic.Generate(42, RouteProfile.StormPassage, Vector2.zero, VoyageSimulation.Destination, b);
            Assert.AreEqual(na, nb);
            for (int i = 0; i < na; i++) Assert.AreEqual(a[i].Center, b[i].Center);
        }

        [Test] public void PassageGatesFollowTheRoute()
        {
            Vector2 from = Campaign.World(Campaign.NorthHaven), to = Campaign.World(Campaign.CoralCay);
            Vector2 direction = (to - from).normalized;
            Vector2 safe = VoyageSimulation.CourseGate(PassageCourse.Safe, 0, from, to);
            Assert.Less(Vector2.Dot(safe - from, direction), Vector2.Distance(from, to) * .2f);
            Assert.AreEqual(70f, Vector2.Distance(from + direction * Vector2.Dot(safe - from, direction), safe), .01f);
            Assert.AreEqual(VoyageSimulation.CourseGate(PassageCourse.Safe, 1), VoyageSimulation.CourseGate(PassageCourse.Safe, 1, Vector2.zero, VoyageSimulation.Destination));
        }

        [Test] public void TheChestDriftsAlongTheRoute()
        {
            sim.State.Island = Campaign.CoralCay; sim.State.Target = Campaign.StormReef; sim.State.Phase = VoyagePhase.Arrived;
            sim.Crew[0].Position = sim.Stations[0].Position + Vector2.right;
            Tick(.4f);
            sim.State.Phase = VoyagePhase.Arrived;
            sim.Interact(Host, 0);
            Vector2 chest = sim.Opportunity.ChestPosition;
            Vector2 from = sim.OriginPoint(), to = sim.DestinationPoint();
            Assert.Greater(Vector2.Dot(chest - from, (to - from).normalized), Vector2.Distance(from, to) * .4f);
            Assert.Less(Vector2.Dot(chest - from, (to - from).normalized), Vector2.Distance(from, to) * .8f);
        }

        [Test] public void TheShipCanSailTheWholeChain()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            sim.State.Position = Campaign.World(Campaign.SkullAtoll) + new Vector2(0, 100);
            sim.Tick(.05f);
            Assert.Greater(sim.State.Position.y, 900f, "the old 250 m limit is gone");
            sim.State.Position = new Vector2(5000, -5000);
            sim.Tick(.05f);
            Assert.LessOrEqual(sim.State.Position.x, Campaign.WorldMax.x);
            Assert.GreaterOrEqual(sim.State.Position.y, Campaign.WorldMin.y);
        }
    }
}
