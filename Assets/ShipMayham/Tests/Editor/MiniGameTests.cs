using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    /// <summary>The rules of the four skill games, played without any screen.</summary>
    public sealed class MiniGameTests
    {
        // ------------------------------------------------------------ the shared dice

        [Test]
        public void TheSeededGeneratorIsRepeatableAndSpread()
        {
            var a = new SeedRandom(42);
            var b = new SeedRandom(42);
            var c = new SeedRandom(43);
            bool differs = false;
            float sum = 0;
            for (int i = 0; i < 1000; i++)
            {
                float x = a.Value();
                Assert.AreEqual(x, b.Value());
                Assert.That(x, Is.InRange(0f, .99999f));
                if (c.Value() != x) differs = true;
                sum += x;
            }
            Assert.IsTrue(differs);
            Assert.AreEqual(.5f, sum / 1000f, .05f);
            Assert.That(new SeedRandom(0).Value(), Is.InRange(0f, 1f), "a zero seed still works");
            for (int i = 0; i < 100; i++) Assert.That(new SeedRandom((uint)i + 1).Range(3, 7), Is.InRange(3, 6));
        }

        // ------------------------------------------------------------ lockpick

        [Test]
        public void TheSweetSpotIsSeededAndInsideTheDial()
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                var game = new LockpickGame(seed, .5f);
                Assert.That(game.SweetAngle, Is.InRange(-70f, 70f));
                Assert.AreEqual(game.SweetAngle, new LockpickGame(seed, .5f).SweetAngle);
            }
            Assert.AreNotEqual(new LockpickGame(1, 0).SweetAngle, new LockpickGame(2, 0).SweetAngle);
            Assert.Greater(new LockpickGame(1, 0).Tolerance, new LockpickGame(1, 1).Tolerance, "harder locks are pickier");
        }

        [Test]
        public void TheCylinderTurnsFurtherTheCloserThePickIs()
        {
            var game = new LockpickGame(7, .3f);
            Assert.AreEqual(1f, game.MaxTurnAt(game.SweetAngle), .001f);
            Assert.AreEqual(1f, game.MaxTurnAt(game.SweetAngle + game.Tolerance * .9f), .001f);
            float near = game.MaxTurnAt(game.SweetAngle + game.Tolerance * 2f);
            float far = game.MaxTurnAt(game.SweetAngle + game.Tolerance * 3f);
            Assert.Less(near, 1f);
            Assert.Less(far, near);
            Assert.AreEqual(0f, game.MaxTurnAt(game.SweetAngle + game.Tolerance * 5f), .001f);
        }

        [Test]
        public void PickingAtTheSweetSpotOpensTheLockCleanlyAndQuickly()
        {
            var game = new LockpickGame(11, .3f);
            for (float t = 0; t < 3f && !game.Finished; t += .02f) game.Tick(game.SweetAngle, true, .02f);
            Assert.IsTrue(game.Opened);
            Assert.AreEqual(0, game.PicksBroken);
            Assert.Less(game.Elapsed, LockpickGame.TurnSeconds + .1f);
            Assert.AreEqual(100, game.Score);
        }

        [Test]
        public void ForcingAJammedLockSnapsPicksUntilTheyRunOut()
        {
            var game = new LockpickGame(11, .3f);
            float wrong = game.SweetAngle > 0 ? -80f : 80f;
            int broke = 0;
            for (float t = 0; t < 30f && !game.Finished; t += .02f)
            {
                game.Tick(wrong, true, .02f);
                if (game.PickJustBroke) { broke++; Assert.AreEqual(0f, game.Turn, "a broken pick lets the cylinder spring back"); }
            }
            Assert.AreEqual(LockpickGame.MaxPicks, broke);
            Assert.IsTrue(game.Failed);
            Assert.IsFalse(game.Opened);
            Assert.AreEqual(0, game.Score);
            game.Tick(game.SweetAngle, true, .5f);
            Assert.IsFalse(game.Opened, "a finished game ignores input");
        }

        [Test]
        public void ReleasingTheTurnRelaxesTheLockAndTheStrain()
        {
            var game = new LockpickGame(11, .3f);
            float wrong = game.SweetAngle > 0 ? -80f : 80f;
            for (int i = 0; i < 3; i++) game.Tick(wrong, true, .1f);
            Assert.Greater(game.Tension, 0f);
            float strain = game.Tension;
            game.Tick(wrong, false, .3f);
            Assert.Less(game.Tension, strain);
            game.Tick(wrong, false, 1f);
            Assert.AreEqual(0f, game.Turn, .001f);
            Assert.AreEqual(0f, game.Tension, .001f);
            Assert.AreEqual(LockpickGame.MaxPicks, game.PicksLeft, "no pick broke");
        }

        [Test]
        public void BrokenPicksAndSlownessCostScore()
        {
            var clean = new LockpickGame(5, .3f);
            for (float t = 0; t < 3f && !clean.Finished; t += .02f) clean.Tick(clean.SweetAngle, true, .02f);
            var careless = new LockpickGame(5, .3f);
            float wrong = careless.SweetAngle > 0 ? -80f : 80f;
            while (careless.PicksBroken == 0) careless.Tick(wrong, true, .02f);
            for (float t = 0; t < 3f && !careless.Finished; t += .02f) careless.Tick(careless.SweetAngle, true, .02f);
            Assert.IsTrue(careless.Opened);
            Assert.Less(careless.Score, clean.Score);
            Assert.GreaterOrEqual(careless.Score, 10);
        }

        // ------------------------------------------------------------ hammer

        [Test]
        public void TheMarkerSwingsBackAndForthInsideThePlank()
        {
            var game = new HammerGame(3, .3f);
            float min = 1, max = 0;
            for (int i = 0; i < 600; i++)
            {
                game.Tick(.02f);
                min = Mathf.Min(min, game.Marker);
                max = Mathf.Max(max, game.Marker);
                Assert.That(game.Marker, Is.InRange(0f, 1f));
            }
            Assert.Less(min, .05f);
            Assert.Greater(max, .95f);
        }

        private static void WaitForTheMarker(HammerGame game, bool inWindow)
        {
            for (int i = 0; i < 2000; i++)
            {
                if ((Mathf.Abs(game.Marker - game.WindowCenter) <= game.WindowHalf * .3f) == inWindow &&
                    (inWindow || Mathf.Abs(game.Marker - game.WindowCenter) > game.WindowHalf * 1.5f)) return;
                game.Tick(.005f);
            }
            Assert.Fail("The marker never got where the test needed it.");
        }

        [Test]
        public void ThreeCleanStrikesDriveThePlankHome()
        {
            var game = new HammerGame(9, .3f);
            for (int nail = 0; nail < HammerGame.NailsNeeded; nail++)
            {
                WaitForTheMarker(game, true);
                Assert.AreEqual(HammerGame.Strike.Perfect, game.Hit());
            }
            Assert.IsTrue(game.Succeeded);
            Assert.AreEqual(HammerGame.NailsNeeded, game.NailsDriven);
            Assert.AreEqual(0, game.Misses);
            Assert.Greater(game.Score, 90);
            Assert.AreEqual(HammerGame.Strike.Ignored, game.Hit());
        }

        [Test]
        public void ThreeMissesSplitThePlank()
        {
            var game = new HammerGame(9, .3f);
            for (int miss = 0; miss < HammerGame.MissesAllowed; miss++)
            {
                WaitForTheMarker(game, false);
                Assert.AreEqual(HammerGame.Strike.Miss, game.Hit());
            }
            Assert.IsTrue(game.Failed);
            Assert.IsFalse(game.Succeeded);
            Assert.AreEqual(0, game.Score);
        }

        [Test]
        public void EachNailMovesThePatchAndSpeedsTheSwingUp()
        {
            var game = new HammerGame(21, .3f);
            float center = game.WindowCenter;
            float before = SwingSpeed(game);
            WaitForTheMarker(game, true);
            game.Hit();
            Assert.AreNotEqual(center, game.WindowCenter);
            Assert.Greater(SwingSpeed(game), before);
        }

        private static float SwingSpeed(HammerGame game)
        {
            float start = game.Marker;
            game.Tick(.01f);
            return Mathf.Abs(game.Marker - start) / .01f;
        }

        // ------------------------------------------------------------ fishing

        private static void PlayFishing(FishingGame game, bool skilled)
        {
            float last = game.Bar;
            for (float t = 0; t < 120f && !game.Finished; t += .02f)
            {
                if (game.State == FishingGame.Phase.Bite) game.Hook();
                // Sliding-mode control: lift when the fish is above the bar, damped by how fast the bar is already moving.
                float velocity = (game.Bar - last) / .02f;
                last = game.Bar;
                bool hold = game.State == FishingGame.Phase.Reeling && skilled && game.FishPosition - game.Bar - .25f * velocity > 0;
                game.Tick(.02f, hold);
            }
        }

        [Test]
        public void ASkilledAnglerLandsEveryKindOfFish()
        {
            var seen = new HashSet<FishKind>();
            for (uint seed = 1; seed <= 120; seed++)
            {
                var game = new FishingGame(seed, .3f);
                PlayFishing(game, true);
                seen.Add(game.Fish);
                Assert.IsTrue(game.Succeeded, $"seed {seed} ({game.Fish}) was lost: {game.LostReason}");
            }
            Assert.IsTrue(seen.Contains(FishKind.Small) && seen.Contains(FishKind.Big) && seen.Contains(FishKind.Boot));
        }

        [Test]
        public void AnIdleAnglerLosesTheFish()
        {
            var game = new FishingGame(4, .3f);
            PlayFishing(game, false);
            Assert.IsTrue(game.Finished);
            Assert.IsFalse(game.Succeeded);
            Assert.IsNotEmpty(game.LostReason);
        }

        [Test]
        public void StrikingBeforeTheBiteScaresTheFishAway()
        {
            var game = new FishingGame(4, .3f);
            game.Tick(.5f, false);
            Assert.AreEqual(FishingGame.Phase.Waiting, game.State);
            game.Hook();
            Assert.AreEqual(FishingGame.Phase.Lost, game.State);
            StringAssert.Contains("early", game.LostReason);
        }

        [Test]
        public void MissingTheBiteWindowLosesTheFish()
        {
            var game = new FishingGame(4, .3f);
            for (float t = 0; t < 20f && game.State == FishingGame.Phase.Waiting; t += .02f) game.Tick(.02f, false);
            Assert.AreEqual(FishingGame.Phase.Bite, game.State);
            game.Tick(FishingGame.BiteWindowSeconds + .1f, false);
            Assert.AreEqual(FishingGame.Phase.Lost, game.State);
        }

        [Test]
        public void TheBiteComesAfterAFewSecondsAndTheFishIsDecidedByTheSeed()
        {
            for (uint seed = 1; seed < 50; seed++)
            {
                var game = new FishingGame(seed, .3f);
                Assert.That(game.WaitLeft, Is.InRange(2f, 5f));
                Assert.AreEqual(game.Fish, new FishingGame(seed, .9f).Fish, "difficulty never changes which fish it is");
            }
        }

        [Test]
        public void ABigFishStrugglesHarderThanABoot()
        {
            uint big = 0, boot = 0;
            for (uint seed = 1; seed < 300 && (big == 0 || boot == 0); seed++)
            {
                var kind = new FishingGame(seed, 1f).Fish;
                if (kind == FishKind.Big && big == 0) big = seed;
                if (kind == FishKind.Boot && boot == 0) boot = seed;
            }
            Assert.AreNotEqual(0u, big);
            Assert.AreNotEqual(0u, boot);
            Assert.AreEqual(100, SolvedScore(big));
            Assert.AreEqual(20, SolvedScore(boot));
        }

        private static int SolvedScore(uint seed)
        {
            var game = new FishingGame(seed, 1f);
            PlayFishing(game, true);
            return game.Score;
        }

        // ------------------------------------------------------------ dig

        [Test]
        public void FastTappingFillsTheHoleButSlowTappingNeverDoes()
        {
            var fast = new DigGame(.3f);
            for (float t = 0; t < 12f && !fast.Finished; t += .02f)
            {
                if (Mathf.Repeat(t, .1f) < .02f) fast.Press();
                fast.Tick(.02f);
            }
            Assert.IsTrue(fast.Succeeded);
            Assert.Greater(fast.Score, 50);

            var slow = new DigGame(.3f);
            for (float t = 0; t < 20f && !slow.Finished; t += .02f)
            {
                if (Mathf.Repeat(t, 1f) < .02f) slow.Press();
                slow.Tick(.02f);
            }
            Assert.IsTrue(slow.Failed);
            Assert.AreEqual(0, slow.Score);
        }

        [Test]
        public void TheSandSlidesBackWhenNobodyDigs()
        {
            var game = new DigGame(.3f);
            for (int i = 0; i < 4; i++) game.Press();
            float depth = game.Depth;
            game.Tick(.5f);
            Assert.Less(game.Depth, depth);
            game.Tick(5f);
            Assert.AreEqual(0f, game.Depth, .001f);
        }

        // ------------------------------------------------------------ rules and the window

        [Test]
        public void EveryGameHasAPlausibleTimeWindow()
        {
            foreach (var kind in new[] { MiniGameKind.Lockpick, MiniGameKind.Hammer, MiniGameKind.Fishing, MiniGameKind.Dig })
            {
                Assert.Greater(MiniGameRules.MinimumSeconds(kind), 0f, kind.ToString());
                Assert.Greater(MiniGameRules.MaximumSeconds(kind), MiniGameRules.MinimumSeconds(kind) * 5, kind.ToString());
            }
            Assert.AreEqual(0f, MiniGameRules.MinimumSeconds(MiniGameKind.None));
        }

        [Test]
        public void TheOverlayOpensEachGameAndFreezesNothingWithoutAHud()
        {
            var parent = new GameObject("OverlayParent", typeof(RectTransform));
            try
            {
                var overlay = new MiniGameOverlay(null, parent.transform);
                foreach (var kind in new[] { MiniGameKind.Lockpick, MiniGameKind.Hammer, MiniGameKind.Fishing, MiniGameKind.Dig })
                {
                    overlay.Open(new MiniGameStart { Kind = kind, Seed = 3, Difficulty = .2f });
                    Assert.IsTrue(overlay.IsOpen, kind.ToString());
                    Assert.AreEqual(kind, overlay.Kind);
                    overlay.Tick(.02f);
                    overlay.Abandon();
                    Assert.IsFalse(overlay.IsOpen, kind + " closes on abandon");
                }
                overlay.Open(new MiniGameStart { Kind = MiniGameKind.None });
                Assert.IsFalse(overlay.IsOpen, "an unknown game is ignored");
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
