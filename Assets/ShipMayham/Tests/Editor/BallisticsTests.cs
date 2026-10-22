using NUnit.Framework;
using UnityEngine;

namespace ShipMayham.Tests
{
    public class BallisticsTests
    {
        private VoyageSimulation sim;
        private static VoyageSimulation Create() => new VoyageSimulation(new[] {
            new StationDefinition(StationKind.Cannon, Vector2.zero),
            new StationDefinition(StationKind.Power, new Vector2(4, 0)) }, new Vector2[8]);
        private static void Advance(VoyageSimulation s, float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) s.Tick(.05f); }
        private static Vector2 Aim => new Vector2(Mathf.Atan2(26, 25) * Mathf.Rad2Deg, 0);

        [SetUp] public void Setup()
        {
            sim = Create(); sim.Join(10, "gunner"); sim.Crew[0].AtCannon = true;
            sim.Crew[0].Position = Vector2.zero;
            sim.State.Phase = VoyagePhase.Sailing;
            sim.Encounter.Pirate = PiratePhase.Attacking;
            sim.Encounter.PiratePosition = new Vector2(26, 25);
            sim.Encounter.AttackRemaining = sim.Encounter.EnemyShotRemaining = 100;
        }

        [Test] public void CrewDamageWaitsForFlightAndConsumesOnlyOneRound()
        {
            sim.FireCannon(10, Aim);
            Assert.AreEqual(100, sim.Encounter.PirateHealth);
            Assert.AreEqual(0, sim.Encounter.Hits);
            Assert.IsTrue(sim.Encounter.CrewBall.Active);
            Vector3 origin = sim.Encounter.CrewBall.Position;
            sim.FireCannon(10, Aim);
            Advance(sim, .1f);
            Assert.AreEqual(11, sim.Encounter.Ammo);
            Assert.Greater(Vector3.Distance(origin, sim.Encounter.CrewBall.Position), 4);
            Assert.Less(sim.Encounter.CrewBall.Velocity.y, 0);
            Advance(sim, .8f);
            Assert.AreEqual(1, sim.Encounter.Hits);
            Assert.AreEqual(66, sim.Encounter.PirateHealth);
            Assert.IsFalse(sim.Encounter.CrewBall.Active);
            Advance(sim, 1);
            Assert.AreEqual(1, sim.Encounter.Hits);
        }

        [Test] public void TargetThatMovesOutOfTheTrajectoryIsNotHit()
        {
            sim.FireCannon(10, Aim);
            sim.Encounter.PiratePosition = new Vector2(-26, 25);
            Advance(sim, 1.2f);
            Assert.AreEqual(0, sim.Encounter.Hits);
            Assert.IsFalse(sim.Encounter.CrewBall.Active);
        }

        [Test] public void SegmentSweepCannotTunnelThroughPirate()
        {
            sim.Encounter.CrewBall = new CannonballState { Active = true, Position = new Vector3(26, 1.5f, 15), Velocity = Vector3.forward * 400, Remaining = 1 };
            sim.Tick(.1f);
            Assert.AreEqual(1, sim.Encounter.Hits);
            Assert.IsFalse(sim.Encounter.CrewBall.Active);
        }

        [Test] public void WaterWinsIfItIsReachedBeforeTarget()
        {
            sim.Encounter.CrewBall = new CannonballState { Active = true, Position = new Vector3(26, .01f, 15), Velocity = new Vector3(0, -2, 100), Remaining = 1 };
            sim.Tick(.1f);
            Assert.AreEqual(0, sim.Encounter.Hits);
            Assert.IsFalse(sim.Encounter.CrewBall.Active);
            Assert.That(sim.Encounter.CrewBall.Position.y, Is.EqualTo(0).Within(.0001f));
        }

        [Test] public void EnemyDamageAndFirstHitPowerFaultWaitForImpact()
        {
            sim.Encounter.EnemyShotRemaining = .01f;
            sim.Tick(.05f);
            Assert.AreEqual(1, sim.Encounter.PirateShotsFired);
            Assert.AreEqual(100, sim.State.Health);
            Assert.IsTrue(sim.Encounter.PirateBall.Active);
            Assert.IsFalse(sim.Encounter.PowerOut);
            Advance(sim, 1);
            Assert.AreEqual(1, sim.Encounter.PirateHits);
            Assert.AreEqual(92, sim.State.Health);
            Assert.IsTrue(sim.Encounter.PowerOut);
            Assert.IsFalse(sim.Encounter.PirateBall.Active);
        }

        [Test] public void ShipCanDodgeAnAlreadyLaunchedEnemyRound()
        {
            sim.Encounter.EnemyShotRemaining = .01f;
            sim.Tick(.05f);
            sim.State.Position = new Vector2(70, 0);
            Advance(sim, 2.2f);
            Assert.AreEqual(0, sim.Encounter.PirateHits);
            Assert.AreEqual(100, sim.State.Health);
            Assert.IsFalse(sim.Encounter.PirateBall.Active);
            sim.State.Position = Vector2.zero;
            sim.Encounter.PiratePosition = new Vector2(26, 25);
            sim.Encounter.EnemyShotRemaining = .01f;
            Advance(sim, 1.1f);
            Assert.AreEqual(2, sim.Encounter.PirateShotsFired);
            Assert.AreEqual(1, sim.Encounter.PirateHits);
            Assert.IsTrue(sim.Encounter.PowerOut, "The first actual hit, not the first attempted shot, cuts power.");
        }

        [Test] public void SnapshotPreservesInFlightPositionVelocityAndOneImpact()
        {
            sim.FireCannon(10, Aim); Advance(sim, .1f);
            sim.Encounter.PirateBall = new CannonballState { Active = true, Position = new Vector3(26, 2, 25), Velocity = new Vector3(-30, 0, -30), Remaining = 1.8f };
            var restored = Create(); restored.ImportMigrationData(sim.ExportMigrationData());
            Assert.IsTrue(restored.Encounter.CrewBall.Active);
            Assert.AreEqual(sim.Encounter.CrewBall.Position, restored.Encounter.CrewBall.Position);
            Assert.AreEqual(sim.Encounter.CrewBall.Velocity, restored.Encounter.CrewBall.Velocity);
            Assert.AreEqual(sim.Encounter.CrewBall.Remaining, restored.Encounter.CrewBall.Remaining);
            Assert.IsTrue(restored.Encounter.PirateBall.Active);
            Assert.AreEqual(sim.Encounter.PirateBall.Velocity, restored.Encounter.PirateBall.Velocity);
            Advance(sim, .8f); Advance(restored, .8f);
            Assert.AreEqual(1, restored.Encounter.Hits);
            Assert.AreEqual(sim.Encounter.PirateHealth, restored.Encounter.PirateHealth);
            Assert.IsFalse(restored.Encounter.CrewBall.Active);
            Assert.AreEqual(1, restored.Encounter.PirateHits);
            Assert.AreEqual(sim.State.Health, restored.State.Health);
        }

        [Test] public void EnemySweepUsesTheTranslatedRotatedShipFrame()
        {
            sim.State.Position = new Vector2(20, 40); sim.State.Heading = 90;
            sim.Encounter.PirateBall = new CannonballState
            {
                Active = true, Remaining = 1,
                Position = VoyageSimulation.DeckToWorld(sim.State, new Vector3(0, 1.5f, 20)),
                Velocity = Quaternion.Euler(0, 90, 0) * (Vector3.back * 400)
            };
            sim.Tick(.1f);
            Assert.AreEqual(1, sim.Encounter.PirateHits);
            Assert.AreEqual(92, sim.State.Health);
            Assert.IsFalse(sim.Encounter.PirateBall.Active);
        }

        [Test] public void LifetimeStopsHighShotsAndSettlementClearsBothFlights()
        {
            sim.FireCannon(10, new Vector2(-90, -25));
            Advance(sim, 2.1f);
            Assert.IsFalse(sim.Encounter.CrewBall.Active);
            Assert.AreEqual(0, sim.Encounter.Hits);
            sim.Encounter.CrewBall = sim.Encounter.PirateBall = new CannonballState { Active = true, Position = Vector3.up * 20, Remaining = 1 };
            sim.State.Health = 0; sim.Tick(.05f);
            Assert.AreEqual(VoyagePhase.Failed, sim.State.Phase);
            Assert.IsFalse(sim.Encounter.CrewBall.Active || sim.Encounter.PirateBall.Active);
        }

        [Test] public void SphereSweepCoversInsideTangentMissAndFiniteSegment()
        {
            Assert.IsTrue(BallisticCollision.Sphere(Vector3.zero, Vector3.zero, Vector3.zero, 1, out float at)); Assert.AreEqual(0, at);
            Assert.IsTrue(BallisticCollision.Sphere(new Vector3(-10, 1, 0), new Vector3(10, 1, 0), Vector3.zero, 1, out at)); Assert.That(at, Is.EqualTo(.5f).Within(.0001f));
            Assert.IsFalse(BallisticCollision.Sphere(Vector3.one * 10, Vector3.one * 11, Vector3.zero, 1, out _));
            Assert.IsFalse(BallisticCollision.Sphere(Vector3.one * 10, Vector3.one * 10, Vector3.zero, 1, out _));
            Assert.IsFalse(BallisticCollision.Sphere(new Vector3(float.NaN, 0, 0), Vector3.one, Vector3.zero, 1, out _));
        }

        [Test] public void BoxSweepCoversInsideParallelTunnelAndInvalidBounds()
        {
            Assert.IsTrue(BallisticCollision.Box(Vector3.zero, Vector3.zero, -Vector3.one, Vector3.one, out float at)); Assert.AreEqual(0, at);
            Assert.IsTrue(BallisticCollision.Box(Vector3.left * 100, Vector3.right * 100, -Vector3.one, Vector3.one, out at)); Assert.That(at, Is.EqualTo(.495f).Within(.0001f));
            Assert.IsFalse(BallisticCollision.Box(Vector3.up * 2, Vector3.up * 2 + Vector3.right, -Vector3.one, Vector3.one, out _));
            Assert.IsFalse(BallisticCollision.Box(Vector3.zero, Vector3.one, Vector3.one, -Vector3.one, out _));
        }
    }
}
