using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>The host side of the mini-games: where they can be played, what they pay and what it refuses.</summary>
    public sealed class ActivityTests
    {
        private const ulong Host = 10, Friend = 11;
        private VoyageSimulation sim;
        private readonly List<(int slot, MiniGameStart start)> started = new List<(int, MiniGameStart)>();
        private readonly List<(int slot, string text)> notices = new List<(int, string)>();
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
                new StationDefinition(StationKind.Repair, new Vector2(4, -3)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            }, true, 5);
            sim.Join(Host);
            sim.Join(Friend);
            started.Clear();
            notices.Clear();
            sim.MiniGameStarted += (slot, start) => started.Add((slot, start));
            sim.CrewNotice += (slot, text) => notices.Add((slot, text));
            sim.Tick(.05f);
        }

        [TearDown]
        public void TearDown() => VoyageSimulation.WalkingPassengers = walkingBefore;

        private void Advance(float seconds)
        {
            for (float t = 0; t < seconds; t += .05f) sim.Tick(.05f);
        }

        private void Ashore(Vector2 spot, int slot = 0)
        {
            sim.Crew[slot].OnShore = true;
            sim.Crew[slot].Position = spot;
            Advance(.4f);
        }

        private MiniGameStart Begin(MiniGameKind kind, int target, ulong client = Host)
        {
            int before = started.Count;
            Assert.AreEqual(string.Empty, sim.ActivityCommand(client, kind, target));
            Assert.AreEqual(before + 1, started.Count, "the host opened a game");
            return started[started.Count - 1].start;
        }

        private string Finish(MiniGameKind kind, int target, MiniGameOutcome outcome, int score = 80, float afterSeconds = 4f, ulong client = Host)
        {
            Advance(afterSeconds);
            return sim.FinishMiniGame(client, new MiniGameResult { Kind = kind, Target = target, Outcome = outcome, Score = score, ElapsedMs = (int)(afterSeconds * 1000) });
        }

        // ------------------------------------------------------------ the places

        [Test]
        public void EverySpotIsOnOpenGroundAwayFromTheShopsAndTheGate()
        {
            var all = new List<Vector2>();
            foreach (var kind in new[] { MiniGameKind.Lockpick, MiniGameKind.Dig, MiniGameKind.Fishing }) all.AddRange(VoyageSimulation.SpotsOf(kind));
            for (int i = 0; i < all.Count; i++)
            {
                Assert.IsTrue(HarborTownLayout.Walkable(all[i]), "spot " + i + " is not inside a building");
                Assert.IsTrue(HarborTownLayout.Bounds.Contains(all[i]), "spot " + i + " is inside the town");
                for (int action = (int)HarborAction.BoardShip; action <= (int)HarborAction.CraneControls; action++)
                    Assert.Greater(Vector2.Distance(all[i], VoyageSimulation.HarborPoint((HarborAction)action)), VoyageSimulation.ActivityReach + 1f,
                        $"spot {i} is not on top of {(HarborAction)action}");
                for (int j = i + 1; j < all.Count; j++) Assert.Greater(Vector2.Distance(all[i], all[j]), 3f, $"spots {i} and {j} are apart");
            }
            for (int ladder = 0; ladder < VoyageSimulation.Ladders.Length; ladder++)
                foreach (var spot in all) if (VoyageSimulation.Ladders[ladder].OnQuay) Assert.Greater(Vector2.Distance(spot, VoyageSimulation.Ladders[ladder].Top), 3f);
        }

        [Test]
        public void EveryCoinIsInOpenWater()
        {
            for (int i = 0; i < VoyageSimulation.CoinSpots.Length; i++)
            {
                var spot = VoyageSimulation.CoinSpots[i];
                Assert.IsTrue(VoyageSimulation.WaterWalkable(spot), "coin " + i);
                Assert.That(spot.x, Is.InRange(VoyageSimulation.WaterMin.x, VoyageSimulation.WaterMax.x));
                Assert.That(spot.y, Is.InRange(VoyageSimulation.WaterMin.y, VoyageSimulation.WaterMax.y));
            }
            Assert.AreEqual(14, VoyageSimulation.CoinSpots.Length);
        }

        // ------------------------------------------------------------ opening a game

        [Test]
        public void ACrewMemberBesideAChestOpensTheLockpickGame()
        {
            Ashore(VoyageSimulation.ChestSpots[0] + new Vector2(.5f, 0));
            var start = Begin(MiniGameKind.Lockpick, 0);
            Assert.AreEqual(MiniGameKind.Lockpick, start.Kind);
            Assert.AreEqual(0, start.Target);
            Assert.AreNotEqual(0u, start.Seed);
            Assert.AreEqual(0, started[0].slot);
            Assert.IsTrue(sim.InMiniGame(0));
        }

        [Test]
        public void EveryGameGetsItsOwnSeed()
        {
            Ashore(VoyageSimulation.ChestSpots[0] + new Vector2(.5f, 0));
            var first = Begin(MiniGameKind.Lockpick, 0);
            Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Failed, 0);
            Advance(.5f);
            var second = Begin(MiniGameKind.Lockpick, 0);
            Assert.AreNotEqual(first.Seed, second.Seed);
        }

        [Test]
        public void AskingAgainWhileAGameIsOpenDoesNotOpenAnother()
        {
            Ashore(VoyageSimulation.DigSpots[1]);
            Begin(MiniGameKind.Dig, 1);
            Advance(.5f);
            Assert.AreEqual(string.Empty, sim.ActivityCommand(Host, MiniGameKind.Dig, 1));
            Assert.AreEqual(1, started.Count);
        }

        [TestCase(MiniGameKind.Lockpick)] [TestCase(MiniGameKind.Dig)] [TestCase(MiniGameKind.Fishing)]
        public void ThereIsNothingToDoFarFromTheSpotOrOnTheShip(MiniGameKind kind)
        {
            Assert.That(sim.ActivityCommand(Host, kind, 0), Does.Contain("ashore"), "aboard");
            Ashore(new Vector2(-14f, 0f));
            Assert.That(sim.ActivityCommand(Host, kind, 0), Does.Contain("closer"), "far away");
            Advance(.4f);
            Assert.That(sim.ActivityCommand(Host, kind, 99), Does.Contain("nothing"), "unknown spot");
            Assert.AreEqual(0, started.Count);
        }

        [Test]
        public void HandsMustBeEmptyAndNotInTheWaterOrAtSea()
        {
            Ashore(VoyageSimulation.DigSpots[0]);
            sim.Crew[0].Carrying = ServiceItem.Food;
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Dig, 0), Does.Contain("hands"));
            sim.Crew[0].Carrying = ServiceItem.None;
            sim.State.Phase = VoyagePhase.Sailing;
            Advance(.4f);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Dig, 0), Does.Contain("ashore"), "no town at sea");
            sim.State.Phase = VoyagePhase.Boarding;
            sim.Crew[0].Swimming = true;
            Advance(.4f);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Dig, 0), Does.Contain("water"));
            Assert.AreEqual(0, started.Count);
        }

        [Test]
        public void AnUnknownGameIsRefused()
        {
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.None, 0), Does.Contain("Unknown"));
            Assert.That(sim.ActivityCommand(999, MiniGameKind.Dig, 0), Does.Contain("Join"));
        }

        // ------------------------------------------------------------ the chest

        [Test]
        public void ASolvedLockPaysOnceAndTheChestStaysOpen()
        {
            Ashore(VoyageSimulation.ChestSpots[1]);
            Begin(MiniGameKind.Lockpick, 1);
            int wallet = sim.State.Wallet;
            string text = Finish(MiniGameKind.Lockpick, 1, MiniGameOutcome.Succeeded, 100);
            int paid = sim.State.Wallet - wallet;
            Assert.That(paid, Is.InRange(VoyageSimulation.ChestMinCoins, VoyageSimulation.ChestMaxCoins));
            StringAssert.Contains("+" + paid + " coins", text);
            Assert.AreEqual(1 << 1, sim.State.ChestMask);
            Assert.AreEqual(1, sim.State.Reputation, "a clean pick earns reputation");
            Advance(.5f);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Lockpick, 1), Does.Contain("already open"));
            Assert.IsTrue(sim.SpotIsFresh(MiniGameKind.Lockpick, 0));
        }

        [Test]
        public void ASloppyPickPaysWithoutReputation()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 40);
            Assert.AreEqual(0, sim.State.Reputation);
        }

        [Test]
        public void AFailedLockCostsNothingAndMayBeTriedAgain()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            int wallet = sim.State.Wallet;
            StringAssert.Contains("try again", Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Failed, 0));
            Assert.AreEqual(wallet, sim.State.Wallet);
            Assert.AreEqual(0, sim.State.ChestMask);
            Advance(.5f);
            Assert.AreEqual(string.Empty, sim.ActivityCommand(Host, MiniGameKind.Lockpick, 0));
        }

        // ------------------------------------------------------------ the checks

        [Test]
        public void AResultThatCameImpossiblyFastIsNotPaid()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            int wallet = sim.State.Wallet;
            string text = Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 100, afterSeconds: .1f);
            StringAssert.Contains("too quick", text);
            Assert.AreEqual(wallet, sim.State.Wallet);
            Assert.AreEqual(0, sim.State.ChestMask);
            Assert.IsFalse(sim.InMiniGame(0), "the game is over either way");
        }

        [Test]
        public void ResultsForTheWrongGameOrSpotOrPlayerAreIgnored()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            int wallet = sim.State.Wallet;
            Assert.AreEqual(string.Empty, Finish(MiniGameKind.Dig, 0, MiniGameOutcome.Succeeded));
            Assert.AreEqual(string.Empty, Finish(MiniGameKind.Lockpick, 1, MiniGameOutcome.Succeeded, afterSeconds: 0));
            Assert.AreEqual(string.Empty, Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, afterSeconds: 0, client: Friend));
            Assert.AreEqual(wallet, sim.State.Wallet);
            Assert.IsTrue(sim.InMiniGame(0), "still waiting for the real result");
            Assert.AreEqual(string.Empty, sim.FinishMiniGame(999, new MiniGameResult { Kind = MiniGameKind.Lockpick }));
        }

        [Test]
        public void ABrokenReportCannotBeUsedTwice()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 100);
            int wallet = sim.State.Wallet;
            Assert.AreEqual(string.Empty, Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 100, afterSeconds: .5f));
            Assert.AreEqual(wallet, sim.State.Wallet);
        }

        [Test]
        public void GivingUpPaysNothingAndFreesTheCrewMember()
        {
            Ashore(VoyageSimulation.DigSpots[0]);
            Begin(MiniGameKind.Dig, 0);
            int wallet = sim.State.Wallet;
            StringAssert.Contains("gave up", Finish(MiniGameKind.Dig, 0, MiniGameOutcome.Abandoned, 0, 1f));
            Assert.AreEqual(wallet, sim.State.Wallet);
            Assert.IsFalse(sim.InMiniGame(0));
            Assert.AreEqual(0, sim.State.DigMask);
        }

        [Test]
        public void ALeavingPlayersGameIsDropped()
        {
            Ashore(VoyageSimulation.DigSpots[0]);
            Begin(MiniGameKind.Dig, 0);
            sim.Leave(Host);
            Assert.IsFalse(sim.InMiniGame(0));
        }

        [Test]
        public void AForgottenGameExpires()
        {
            Ashore(VoyageSimulation.DigSpots[0]);
            Begin(MiniGameKind.Dig, 0);
            Advance(MiniGameRules.MaximumSeconds(MiniGameKind.Dig) + 20f);
            Assert.IsFalse(sim.InMiniGame(0));
        }

        // ------------------------------------------------------------ digging and fishing

        [Test]
        public void EverySpotYieldsAtMostOneFindAndTheLootVaries()
        {
            var outcomes = new HashSet<string>();
            for (uint seed = 1; seed <= 60; seed++)
            {
                var local = new VoyageSimulation(sim.Stations, sim.Seats, true, seed);
                local.Join(Host);
                local.Tick(.05f);
                local.Crew[0].OnShore = true;
                local.Crew[0].Position = VoyageSimulation.DigSpots[2];
                for (int i = 0; i < 10; i++) local.Tick(.05f);
                MiniGameStart start = default;
                local.MiniGameStarted += (slot, s) => start = s;
                local.ActivityCommand(Host, MiniGameKind.Dig, 2);
                for (int i = 0; i < 60; i++) local.Tick(.05f);
                int wallet = local.State.Wallet;
                string text = local.FinishMiniGame(Host, new MiniGameResult { Kind = MiniGameKind.Dig, Target = 2, Outcome = MiniGameOutcome.Succeeded, Score = 70 });
                Assert.AreEqual(1 << 2, local.State.DigMask);
                Assert.Greater(local.State.Wallet, wallet);
                outcomes.Add(text.Substring(0, text.IndexOf(' ', 2)));
            }
            Assert.GreaterOrEqual(outcomes.Count, 3, "purses, stashes, charts and boots all turn up: " + string.Join(", ", outcomes));
        }

        [Test]
        public void FishFeedTheGalleyAndTheHostDecidesWhichFish()
        {
            Ashore(VoyageSimulation.FishingSpots[0]);
            var start = Begin(MiniGameKind.Fishing, 0);
            var fish = new FishingGame(start.Seed, start.Difficulty).Fish;
            sim.State.FoodStock = 10;
            int wallet = sim.State.Wallet;
            string text = Finish(MiniGameKind.Fishing, 0, MiniGameOutcome.Succeeded, 100, 5f);
            if (fish == FishKind.Boot) { Assert.AreEqual(10, sim.State.FoodStock); Assert.AreEqual(wallet + 1, sim.State.Wallet); }
            else Assert.AreEqual(10 + (fish == FishKind.Big ? VoyageSimulation.FishFoodBig : VoyageSimulation.FishFoodSmall), sim.State.FoodStock);
            Assert.IsNotEmpty(text);
            Assert.IsTrue(sim.SpotIsFresh(MiniGameKind.Fishing, 0), "the sea never runs out of fish");
        }

        [Test]
        public void TheGalleyNeverOverflows()
        {
            Ashore(VoyageSimulation.FishingSpots[1]);
            Begin(MiniGameKind.Fishing, 1);
            sim.State.FoodStock = VoyageSimulation.SupplyCap - 1;
            Finish(MiniGameKind.Fishing, 1, MiniGameOutcome.Succeeded, 100, 5f);
            Assert.LessOrEqual(sim.State.FoodStock, VoyageSimulation.SupplyCap);
        }

        // ------------------------------------------------------------ the hammer

        private void StartLeak()
        {
            sim.State.Leak = 1;
            sim.State.RepairSteps = 0;
            sim.Crew[0].Position = sim.Stations[2].Position + Vector2.left;
            Advance(.4f);
        }

        [Test]
        public void NailingAPlankSealsTheLeak()
        {
            StartLeak();
            Begin(MiniGameKind.Hammer, 0);
            string text = Finish(MiniGameKind.Hammer, 0, MiniGameOutcome.Succeeded, 90);
            StringAssert.Contains("sealed", text);
            Assert.AreEqual(0, sim.State.Leak);
            Assert.AreEqual(1, sim.Tallies[0].Repairs);
        }

        [Test]
        public void ASplitPlankStillHoldsOneNail()
        {
            StartLeak();
            Begin(MiniGameKind.Hammer, 0);
            string text = Finish(MiniGameKind.Hammer, 0, MiniGameOutcome.Failed, 0);
            StringAssert.Contains("1/4", text);
            Assert.AreEqual(1, sim.State.RepairSteps);
            Assert.AreNotEqual(0, sim.State.Leak);
            for (int i = 0; i < 5; i++)
            {
                Advance(.5f);
                if (sim.ActivityCommand(Host, MiniGameKind.Hammer, 0) == string.Empty) Finish(MiniGameKind.Hammer, 0, MiniGameOutcome.Failed, 0, 3f);
            }
            Assert.LessOrEqual(sim.State.RepairSteps, 3, "hammering alone never completes the patch by failing");
        }

        [Test]
        public void TheHammerIsOnlyForARealLeakAtTheBenchWithNothingFasterAtHand()
        {
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Hammer, 0), Does.Contain("watertight"));
            sim.State.Leak = 1;
            sim.Crew[0].Position = new Vector2(-3, 3);
            Advance(.4f);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Hammer, 0), Does.Contain("repair bench"));
            StartLeak();
            sim.State.ReadyRepairKits = 1;
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Hammer, 0), Does.Contain("faster"));
            sim.State.ReadyRepairKits = 0;
            sim.Crew[0].OnShore = true;
            Advance(.4f);
            Assert.That(sim.ActivityCommand(Host, MiniGameKind.Hammer, 0), Does.Contain("aboard"));
            Assert.AreEqual(0, started.Count);
        }

        // ------------------------------------------------------------ sunken coins

        private void Swim(Vector2 at, int slot = 0)
        {
            sim.Crew[slot].OnShore = false;
            sim.Crew[slot].Swimming = true;
            sim.Crew[slot].Position = at;
            // Coins rest below the surface: dive to the one that is here.
            int coin = System.Array.FindIndex(VoyageSimulation.CoinSpots, spot => spot == at);
            sim.Crew[slot].Depth = coin >= 0 ? VoyageSimulation.CoinDepth(coin) : 0;
            Advance(.2f);
        }

        [Test]
        public void SwimmingOverASunkenCoinPicksItUpOnce()
        {
            int wallet = sim.State.Wallet;
            Swim(VoyageSimulation.CoinSpots[0]);
            Assert.AreEqual(wallet + VoyageSimulation.CoinValue, sim.State.Wallet);
            Assert.AreEqual(1, sim.State.CoinMask);
            Advance(1f);
            Assert.AreEqual(wallet + VoyageSimulation.CoinValue, sim.State.Wallet, "the same coin is not paid twice");
            Assert.AreEqual(1, notices.Count);
            StringAssert.Contains("5 coins", notices[0].text);
        }

        [Test]
        public void AWalkerCannotCollectCoinsFromTheDeck()
        {
            int wallet = sim.State.Wallet;
            sim.Crew[0].Position = new Vector2(0, 0);
            Advance(1f);
            Assert.AreEqual(wallet, sim.State.Wallet);
            Assert.AreEqual(0, sim.State.CoinMask);
        }

        [Test]
        public void TheLastCoinPaysABonusAndReputation()
        {
            int wallet = sim.State.Wallet;
            for (int i = 0; i < VoyageSimulation.CoinSpots.Length; i++) Swim(VoyageSimulation.CoinSpots[i]);
            Assert.AreEqual(VoyageSimulation.CoinMaskAll, sim.State.CoinMask);
            Assert.AreEqual(wallet + VoyageSimulation.CoinSpots.Length * VoyageSimulation.CoinValue + VoyageSimulation.AllCoinsBonus, sim.State.Wallet);
            Assert.AreEqual(1, sim.State.Reputation);
            StringAssert.Contains("last sunken coin", notices[notices.Count - 1].text);
        }

        [Test]
        public void NoCoinsAtSea()
        {
            sim.State.Phase = VoyagePhase.Sailing;
            int wallet = sim.State.Wallet;
            Swim(VoyageSimulation.CoinSpots[0]);
            Assert.AreEqual(wallet, sim.State.Wallet);
        }

        [Test]
        public void ANewPortBringsFreshSandChestsAndCoins()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 100);
            Swim(VoyageSimulation.CoinSpots[3]);
            Assert.AreNotEqual(0, sim.State.ChestMask);
            Assert.AreNotEqual(0, sim.State.CoinMask);
            sim.Crew[0].Swimming = false;
            sim.State.PortVisit++;                                    // arriving somewhere else is a different port
            Advance(.2f);
            Assert.AreEqual(0, sim.State.ChestMask);
            Assert.AreEqual(0, sim.State.CoinMask);
            Assert.AreEqual(0, sim.State.DigMask);
        }

        // ------------------------------------------------------------ what everyone else sees

        [Test]
        public void ThePlayerIsSeenDoingTheirGameUntilItEnds()
        {
            Ashore(VoyageSimulation.FishingSpots[0]);
            Assert.AreEqual(MiniGameKind.None, sim.Crew[0].Doing);
            Begin(MiniGameKind.Fishing, 0);
            Assert.AreEqual(MiniGameKind.Fishing, sim.Crew[0].Doing);
            Finish(MiniGameKind.Fishing, 0, MiniGameOutcome.Failed, 0, 4f);
            Assert.AreEqual(MiniGameKind.None, sim.Crew[0].Doing);
        }

        [Test]
        public void ThePlayerStandsStillWhileTheGameRuns()
        {
            Ashore(VoyageSimulation.DigSpots[0]);
            Begin(MiniGameKind.Dig, 0);
            Vector2 where = sim.Crew[0].Position;
            for (int i = 0; i < 20; i++) { sim.SetInput(Host, Vector2.right); sim.Tick(.05f); }
            Assert.AreEqual(where, sim.Crew[0].Position);
            sim.ApplyMoveCommands(Host, MoveCommandPacket.Pack(new[] { MoveCommand.Create(1, Vector2.right, false), MoveCommand.Create(2, Vector2.right, false) }));
            Assert.AreEqual(where, sim.Crew[0].Position, "movement commands are ignored too");
        }

        [Test]
        public void EveryGameHasItsOwnPoseAndTheDoingFlagTravels()
        {
            Assert.AreEqual(CrewPose.LockPicking, CrewPresentation.PoseFor(new CrewState { Active = true, Doing = MiniGameKind.Lockpick }, 0));
            Assert.AreEqual(CrewPose.Digging, CrewPresentation.PoseFor(new CrewState { Active = true, Doing = MiniGameKind.Dig }, 3f), "digging beats walking");
            Assert.AreEqual(CrewPose.FishingIdle, CrewPresentation.PoseFor(new CrewState { Active = true, Doing = MiniGameKind.Fishing }, 0));
            Assert.AreEqual(CrewPose.Hammering, CrewPresentation.PoseFor(new CrewState { Active = true, Doing = MiniGameKind.Hammer }, 0));
            var busy = new CrewState { Active = true, Doing = MiniGameKind.Dig };
            Assert.AreNotEqual(busy, new CrewState { Active = true });
        }

        // ------------------------------------------------------------ network and migration

        [Test]
        public void TheMasksSurviveHostMigration()
        {
            Ashore(VoyageSimulation.ChestSpots[0]);
            Begin(MiniGameKind.Lockpick, 0);
            Finish(MiniGameKind.Lockpick, 0, MiniGameOutcome.Succeeded, 100);
            Swim(VoyageSimulation.CoinSpots[2], 1);
            var restored = new VoyageSimulation(sim.Stations, sim.Seats, true);
            restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.AreEqual(sim.State.ChestMask, restored.State.ChestMask);
            Assert.AreEqual(sim.State.CoinMask, restored.State.CoinMask);
            Assert.AreEqual(sim.State.ActivityPortKey, restored.State.ActivityPortKey);
        }

        // ------------------------------------------------------------ what the player sees

        [Test]
        public void ThePromptsExplainEachGameAndBlockFullHands()
        {
            Assert.AreEqual("Pick the lock", PromptCatalog.ForActivity(MiniGameKind.Lockpick, default).Action);
            Assert.AreEqual("Dig", PromptCatalog.ForActivity(MiniGameKind.Dig, default).Action);
            Assert.AreEqual("Cast a line", PromptCatalog.ForActivity(MiniGameKind.Fishing, default).Action);
            Assert.AreEqual(PromptTone.Blocked, PromptCatalog.ForActivity(MiniGameKind.Dig, new CrewState { Carrying = ServiceItem.Food }).Tone);
            StringAssert.Contains("Nail", PromptCatalog.ForHammer(default).Action);
            var target = InteractionTarget.Activity(MiniGameKind.Fishing, 2, default, default);
            Assert.AreEqual(MiniGameKind.Fishing, target.ActivityKind);
            Assert.AreEqual(2, target.ActivitySpot);
            Assert.AreEqual(MiniGameKind.None, InteractionTarget.None.ActivityKind);
        }
    }
}
