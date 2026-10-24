using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ShipMayham.Tests
{
    public sealed class HazardTests
    {
        private const ulong Host = 10;
        private VoyageSimulation sim;

        [SetUp]
        public void SetUp() => sim = NewSim();

        private static VoyageSimulation NewSim(uint seed = 7)
        {
            var created = new VoyageSimulation(new[]
            {
                new StationDefinition(StationKind.Manifest, new Vector2(-4, -4)),
                new StationDefinition(StationKind.Helm, new Vector2(0, 6.8f)),
            }, new[]
            {
                new Vector2(-1.9f, -1.5f), new Vector2(1.9f, -1.5f), new Vector2(-1.9f, .6f), new Vector2(1.9f, .6f),
                new Vector2(-1.9f, 2.7f), new Vector2(1.9f, 2.7f), new Vector2(-1.9f, 4.8f), new Vector2(1.9f, 4.8f)
            }, false, seed);
            created.Join(Host);
            return created;
        }

        private static void Sail(VoyageSimulation target, RouteProfile route)
        {
            target.State.Route = route;
            target.State.Phase = VoyagePhase.Sailing;
            target.Tick(.05f);              // lays out the shoals and schedules the first wave
        }

        private static void Run(VoyageSimulation target, float seconds)
        {
            for (float t = 0; t < seconds; t += .1f) target.Tick(.1f);
        }

        // ------------------------------------------------------------ layout

        [Test]
        public void LayoutIsDeterministicAndSeedDependent()
        {
            var a = new Shoal[HazardLogic.MaxShoals];
            var b = new Shoal[HazardLogic.MaxShoals];
            var c = new Shoal[HazardLogic.MaxShoals];
            int na = HazardLogic.Generate(42, RouteProfile.StormPassage, a);
            int nb = HazardLogic.Generate(42, RouteProfile.StormPassage, b);
            int nc = HazardLogic.Generate(43, RouteProfile.StormPassage, c);
            Assert.AreEqual(na, nb);
            for (int i = 0; i < na; i++) { Assert.AreEqual(a[i].Center, b[i].Center); Assert.AreEqual(a[i].Radius, b[i].Radius); }
            bool differs = na != nc;
            for (int i = 0; i < Mathf.Min(na, nc) && !differs; i++) differs = a[i].Center != c[i].Center;
            Assert.IsTrue(differs, "another seed gives another layout");
        }

        [TestCase(RouteProfile.CoastalFerry, 3)]
        [TestCase(RouteProfile.MarketRun, 4)]
        [TestCase(RouteProfile.StormPassage, 6)]
        public void StormierRoutesHaveMoreShoals(RouteProfile route, int expected)
        {
            var buffer = new Shoal[HazardLogic.MaxShoals];
            for (uint seed = 1; seed <= 100; seed++)
                Assert.AreEqual(expected, HazardLogic.Generate(seed * 7919, route, buffer), "seed " + seed);
        }

        [Test]
        public void HarborEntrancesStayClearAndGapsStayWideEnoughToSail()
        {
            var buffer = new Shoal[HazardLogic.MaxShoals];
            foreach (RouteProfile route in System.Enum.GetValues(typeof(RouteProfile)))
            {
                for (uint seed = 1; seed <= 200; seed++)
                {
                    int count = HazardLogic.Generate(seed * 104729, route, buffer);
                    for (int i = 0; i < count; i++)
                    {
                        var shoal = buffer[i];
                        Assert.GreaterOrEqual(Vector2.Distance(shoal.Center, Vector2.zero) - shoal.Radius, HazardLogic.SafeFromStart - 1f);
                        Assert.GreaterOrEqual(Vector2.Distance(shoal.Center, VoyageSimulation.Destination) - shoal.Radius, HazardLogic.SafeFromPort - 1f);
                        Assert.LessOrEqual(Mathf.Abs(shoal.Center.x), HazardLogic.MaxOffset);
                        Assert.That(shoal.Radius, Is.InRange(HazardLogic.MinRadius, HazardLogic.MaxRadius));
                        for (int j = i + 1; j < count; j++)
                            Assert.GreaterOrEqual(Vector2.Distance(shoal.Center, buffer[j].Center) - shoal.Radius - buffer[j].Radius, HazardLogic.MinGap - .01f);
                    }
                }
            }
        }

        [Test]
        public void VoyageSeedsDifferPerVoyageAndAreNeverZero()
        {
            var seen = new HashSet<uint>();
            for (int voyage = 1; voyage <= 50; voyage++)
            {
                uint seed = HazardLogic.VoyageSeed(1234, voyage);
                Assert.AreNotEqual(0u, seed);
                Assert.IsTrue(seen.Add(seed), "voyage " + voyage);
            }
            Assert.AreNotEqual(0u, HazardLogic.VoyageSeed(0, 0));
        }

        [Test]
        public void ShoalSetMatchesGenerateAndCaches()
        {
            var buffer = new Shoal[HazardLogic.MaxShoals];
            int expected = HazardLogic.Generate(99, RouteProfile.MarketRun, buffer);
            int count = ShoalSet.For(99, RouteProfile.MarketRun);
            Assert.AreEqual(expected, count);
            for (int i = 0; i < count; i++) Assert.AreEqual(buffer[i].Center, ShoalSet.Shoals[i].Center);
            Assert.AreEqual(count, ShoalSet.For(99, RouteProfile.MarketRun));
            Assert.AreNotEqual(ShoalSet.For(99, RouteProfile.StormPassage), ShoalSet.For(99, RouteProfile.CoastalFerry));
        }

        // ------------------------------------------------------------ rules

        [Test]
        public void ShoalDamageGrowsWithSpeedAndNeverVanishes()
        {
            Assert.AreEqual(2f, HazardLogic.ShoalDamage(0), .001f);
            Assert.Greater(HazardLogic.ShoalDamage(8), HazardLogic.ShoalDamage(4));
            Assert.AreEqual(2 * HazardLogic.ShoalDamage(4), HazardLogic.ShoalDamage(8), .001f);
        }

        [Test]
        public void WaveSafetyUsesTheShortestAngleAcrossNorth()
        {
            Assert.IsTrue(HazardLogic.WaveSafe(350, 10));
            Assert.IsTrue(HazardLogic.WaveSafe(90, 90 + HazardLogic.WaveSafeAngle));
            Assert.IsFalse(HazardLogic.WaveSafe(90, 90 + HazardLogic.WaveSafeAngle + 5));
            Assert.IsFalse(HazardLogic.WaveSafe(0, 180));
            Assert.Greater(HazardLogic.TurnToFace(0, 80), 0);
            Assert.Less(HazardLogic.TurnToFace(0, 280), 0);
        }

        [Test]
        public void WaveBearingsAreCompassPointsAndRepeatable()
        {
            for (int i = 0; i < 40; i++)
            {
                float bearing = HazardLogic.WaveBearing(555, i);
                Assert.AreEqual(0f, bearing % 45f, .001f);
                Assert.That(bearing, Is.InRange(0f, 315f));
                Assert.AreEqual(bearing, HazardLogic.WaveBearing(555, i));
                Assert.That(HazardLogic.WaveInterval(RouteProfile.StormPassage, 555, i), Is.InRange(45f, 70f));
                Assert.That(HazardLogic.WaveInterval(RouteProfile.MarketRun, 555, i), Is.InRange(75f, 105f));
            }
            Assert.AreEqual("N", HazardLogic.Compass(0));
            Assert.AreEqual("NE", HazardLogic.Compass(45));
            Assert.AreEqual("NW", HazardLogic.Compass(315));
            Assert.AreEqual("N", HazardLogic.Compass(359));
        }

        [Test]
        public void NearestAheadSeesShoalsInFrontAndIgnoresBehindOrFarOnes()
        {
            var shoals = new[] { new Shoal(new Vector2(0, 40), 6) };
            Assert.AreEqual(0, HazardLogic.NearestAhead(Vector2.zero, 0, shoals, 1, out float gap));
            Assert.AreEqual(40 - 6 - HazardLogic.ShipRadius, gap, .01f);
            Assert.AreEqual(-1, HazardLogic.NearestAhead(Vector2.zero, 180, shoals, 1, out _), "behind");
            Assert.AreEqual(-1, HazardLogic.NearestAhead(new Vector2(0, -40), 0, shoals, 1, out _), "too far");
            Assert.AreEqual(0, HazardLogic.NearestAhead(new Vector2(0, 30), 180, shoals, 1, out _), "close behind is still a danger");
            Assert.AreEqual(-1, HazardLogic.NearestAhead(Vector2.zero, 0, shoals, 0, out _));
        }

        // ------------------------------------------------------------ simulation

        [Test]
        public void ShoalLayoutAppearsWhenSailingAndMatchesTheSeed()
        {
            Assert.AreEqual(0, sim.ShoalTotal, "nothing in the harbor");
            Sail(sim, RouteProfile.StormPassage);
            var buffer = new Shoal[HazardLogic.MaxShoals];
            int expected = HazardLogic.Generate(sim.Hazards.Seed, RouteProfile.StormPassage, buffer);
            Assert.AreEqual(expected, sim.ShoalTotal);
            Assert.AreEqual(buffer[0].Center, sim.ShoalAt(0).Center);
        }

        [Test]
        public void EachVoyageGetsItsOwnSeedAndStartsClean()
        {
            uint first = sim.Hazards.Seed;
            Assert.AreEqual(HazardLogic.VoyageSeed(7, 1), first);
            Sail(sim, RouteProfile.StormPassage);
            sim.State.Health = 0;
            sim.Tick(.1f);                                   // the ship is lost
            Assert.AreEqual(VoyagePhase.Failed, sim.State.Phase);
            Assert.AreEqual(first, sim.Hazards.Seed, "the seed lives as long as the voyage report");
            for (int i = 0; i < 5; i++) sim.Tick(.1f);
            sim.Crew[0].Position = new Vector2(-3, -3.5f);
            Assert.That(sim.Interact(Host, 0), Does.StartWith("Back at the departure port"));
            Assert.AreEqual(HazardLogic.VoyageSeed(7, 2), sim.Hazards.Seed);
            Assert.AreNotEqual(first, sim.Hazards.Seed);
            Assert.AreEqual(0, sim.Hazards.ShoalHits);
            Assert.AreEqual(WavePhase.Idle, sim.Hazards.Wave);
        }

        [Test]
        public void RunningIntoAShoalHurtsPushesBackAndSlows()
        {
            Sail(sim, RouteProfile.StormPassage);
            var shoal = sim.ShoalAt(0);
            sim.State.Heading = 180;
            sim.State.Speed = 6;
            sim.State.Position = shoal.Center + new Vector2(0, shoal.Radius + 1f);
            float before = sim.State.Health;
            sim.Tick(.1f);
            Assert.Less(sim.State.Health, before - 5f, "speed 6 costs about 15");
            Assert.AreEqual(1, sim.Hazards.ShoalHits);
            Assert.GreaterOrEqual(Vector2.Distance(sim.State.Position, shoal.Center), shoal.Radius + HazardLogic.ShipRadius - .01f);
            Assert.Less(sim.State.Speed, 6 * .5f);
        }

        [Test]
        public void FasterShipsLoseMoreHull()
        {
            float Loss(float speed)
            {
                var local = NewSim();
                Sail(local, RouteProfile.StormPassage);
                var shoal = local.ShoalAt(0);
                local.State.Heading = 180;
                local.State.Speed = speed;
                local.State.Position = shoal.Center + new Vector2(0, shoal.Radius + 1f);
                float before = local.State.Health;
                local.Tick(.1f);
                return before - local.State.Health;
            }
            Assert.Greater(Loss(8), Loss(3));
        }

        [Test]
        public void ScrapingAlongARockDoesNotDamageEveryFrame()
        {
            Sail(sim, RouteProfile.StormPassage);
            var shoal = sim.ShoalAt(0);
            sim.State.Heading = 180;
            for (int i = 0; i < 8; i++)
            {
                sim.State.Speed = 5;
                sim.State.Position = shoal.Center + new Vector2(0, shoal.Radius + 1f);
                sim.Tick(.1f);
            }
            Assert.AreEqual(1, sim.Hazards.ShoalHits, "0.8 s is inside the cooldown");
            sim.State.Speed = 5;
            sim.State.Position = shoal.Center + new Vector2(0, shoal.Radius + 1f);
            Run(sim, HazardLogic.HitCooldown + .2f);
            sim.State.Speed = 5;
            sim.State.Position = shoal.Center + new Vector2(0, shoal.Radius + 1f);
            sim.Tick(.1f);
            Assert.AreEqual(2, sim.Hazards.ShoalHits);
        }

        [Test]
        public void ShoalsSinkThePlayerShipWhenTheHullIsGone()
        {
            Sail(sim, RouteProfile.StormPassage);
            var shoal = sim.ShoalAt(0);
            sim.State.Health = 1;
            sim.State.HullShield = 0;
            sim.State.Heading = 180;
            sim.State.Speed = 8;
            sim.State.Position = shoal.Center + new Vector2(0, shoal.Radius + 1f);
            sim.Tick(.1f);
            Assert.AreEqual(VoyagePhase.Failed, sim.State.Phase);
            Assert.AreEqual(0, sim.ShoalTotal, "hazards are cleared with the voyage");
            Assert.AreEqual(WavePhase.Idle, sim.Hazards.Wave);
        }

        [Test]
        public void CoastalFerryHasNoWaves()
        {
            Sail(sim, RouteProfile.CoastalFerry);
            Assert.Less(sim.Hazards.NextWaveAt, 0);
            Run(sim, 40);
            Assert.AreEqual(WavePhase.Idle, sim.Hazards.Wave);
            Assert.AreEqual(0, sim.Hazards.WaveIndex);
        }

        [Test]
        public void StormWavesWarnFiveSecondsAhead()
        {
            Sail(sim, RouteProfile.StormPassage);
            Assert.AreEqual(sim.State.Elapsed + 30f - .05f, sim.Hazards.NextWaveAt, .2f);
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            Assert.AreEqual(WavePhase.Warning, sim.Hazards.Wave);
            Assert.AreEqual(HazardLogic.WaveWarningSeconds, sim.Hazards.WaveRemaining, .01f);
            Assert.AreEqual(HazardLogic.WaveBearing(sim.Hazards.Seed, 0), sim.Hazards.WaveBearing);
        }

        [Test]
        public void FacingTheWaveAvoidsTheDamage()
        {
            Sail(sim, RouteProfile.StormPassage);
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            sim.State.Heading = sim.Hazards.WaveBearing;
            sim.State.Speed = 0;
            float before = sim.State.Health;
            Run(sim, HazardLogic.WaveWarningSeconds + .3f);
            Assert.AreEqual(WavePhase.Idle, sim.Hazards.Wave);
            Assert.AreEqual(1, sim.Hazards.WavesDodged);
            Assert.AreEqual(0, sim.Hazards.WavesHit);
            Assert.AreEqual(before, sim.State.Health, .01f);
            Assert.AreEqual(1, sim.Hazards.WaveIndex);
            Assert.Greater(sim.Hazards.NextWaveAt, sim.State.Elapsed);
        }

        [Test]
        public void TurningAwayTakesTheHitAndLosesSpeed()
        {
            Sail(sim, RouteProfile.StormPassage);
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            sim.State.Heading = Mathf.Repeat(sim.Hazards.WaveBearing + 180f, 360f);
            sim.State.Speed = 0;
            float before = sim.State.Health;
            Run(sim, HazardLogic.WaveWarningSeconds + .3f);
            Assert.AreEqual(1, sim.Hazards.WavesHit);
            Assert.AreEqual(before - HazardLogic.WaveDamage, sim.State.Health, .01f);
        }

        [Test]
        public void HullShieldAbsorbsWaveDamage()
        {
            Sail(sim, RouteProfile.StormPassage);
            sim.State.HullShield = 20;
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            sim.State.Heading = Mathf.Repeat(sim.Hazards.WaveBearing + 180f, 360f);
            float before = sim.State.Health;
            Run(sim, HazardLogic.WaveWarningSeconds + .3f);
            Assert.AreEqual(before, sim.State.Health, .01f);
            Assert.Less(sim.State.HullShield, 20);
        }

        [Test]
        public void WavesWaitWhileTheShipIsNearThePort()
        {
            Sail(sim, RouteProfile.StormPassage);
            sim.State.Position = VoyageSimulation.Destination - new Vector2(0, 20);
            sim.State.Speed = 0;
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            Assert.AreEqual(WavePhase.Idle, sim.Hazards.Wave);
            Assert.Greater(sim.Hazards.NextWaveAt, sim.State.Elapsed);
        }

        // ------------------------------------------------------------ migration and network state

        [Test]
        public void MigrationKeepsTheLayoutAndTheWaveTimer()
        {
            Sail(sim, RouteProfile.StormPassage);
            sim.Hazards.NextWaveAt = sim.State.Elapsed;
            sim.Tick(.1f);
            Run(sim, 1);
            byte[] data = sim.ExportMigrationData();

            var restored = NewSim(999);
            restored.ImportMigrationData(data);
            Assert.AreEqual(sim.Hazards, restored.Hazards);
            Assert.AreEqual(sim.ShoalTotal, restored.ShoalTotal);
            for (int i = 0; i < sim.ShoalTotal; i++) Assert.AreEqual(sim.ShoalAt(i).Center, restored.ShoalAt(i).Center);
            Assert.AreEqual(WavePhase.Warning, restored.Hazards.Wave);
            float remaining = restored.Hazards.WaveRemaining;
            Run(restored, .5f);
            Assert.Less(restored.Hazards.WaveRemaining, remaining, "the warning keeps counting down");
        }

        [Test]
        public void MigrationOutsideSailingHasNoShoals()
        {
            byte[] data = sim.ExportMigrationData();
            var restored = NewSim(5);
            restored.ImportMigrationData(data);
            Assert.AreEqual(0, restored.ShoalTotal);
        }

        [Test]
        public void HazardStateSerializesAllFields()
        {
            var state = new HazardState
            {
                Seed = 77, Wave = WavePhase.Warning, WaveRemaining = 3.5f, WaveBearing = 135, NextWaveAt = 61.5f,
                WaveIndex = 2, ShoalHits = 3, WavesDodged = 1, WavesHit = 1
            };
            using (var writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(state);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out HazardState copy);
                    Assert.AreEqual(state, copy);
                }
            }
        }

        // ------------------------------------------------------------ feedback and tips

        [Test]
        public void FeedbackReportsWarningsOnlyWhileSailing()
        {
            var before = FeedbackSnapshot.Create();
            var after = FeedbackSnapshot.Create();
            before.Valid = after.Valid = true;
            before.Phase = after.Phase = VoyagePhase.Sailing;
            after.WaveWarning = true; after.ShoalNear = true;
            var events = new List<FeedbackEvent>();
            FeedbackDetector.Detect(before, after, events);
            Assert.IsTrue(events.Exists(e => e.Kind == FeedbackKind.WaveWarning));
            Assert.IsTrue(events.Exists(e => e.Kind == FeedbackKind.ShoalAhead));

            events.Clear();
            before = after; after.WaveWarning = false; after.WavesDodged = 1;
            FeedbackDetector.Detect(before, after, events);
            Assert.IsTrue(events.Exists(e => e.Kind == FeedbackKind.WaveDodged));

            events.Clear();
            before.Phase = VoyagePhase.Boarding;
            FeedbackDetector.Detect(before, after, events);
            Assert.IsFalse(events.Exists(e => e.Kind == FeedbackKind.WaveDodged || e.Kind == FeedbackKind.ShoalAhead));
        }

        [Test]
        public void HazardTipIsUrgentAndOnlyWhileSailingAboard()
        {
            var c = new TipContext { InVoyage = true, Phase = VoyagePhase.Sailing, HazardWarning = true };
            Assert.IsTrue(TipLogic.Applies(TipId.Hazard, c));
            Assert.IsTrue(TipLogic.IsUrgent(TipId.Hazard));
            c.OnShore = true;
            Assert.IsFalse(TipLogic.Applies(TipId.Hazard, c));
            c.OnShore = false; c.HazardWarning = false;
            Assert.IsFalse(TipLogic.Applies(TipId.Hazard, c));
        }

        [Test]
        public void HazardTipOutranksTheCalmSignalsTip()
        {
            var c = new TipContext { InVoyage = true, Phase = VoyagePhase.Sailing, HazardWarning = true };
            Assert.AreEqual(TipId.Hazard, TipLogic.Pick(c, 0));
        }
    }
}
