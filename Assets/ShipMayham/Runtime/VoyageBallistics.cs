using UnityEngine;

namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        public const float CannonballSpeed = 45, CannonballGravity = 4, CannonballLifetime = CannonRange / CannonballSpeed;

        private static CannonballState LaunchBall(Vector3 origin, Vector3 velocity) => new CannonballState
        { Active = true, Position = origin, Velocity = velocity, Remaining = CannonballLifetime };

        private void TickCannonballs(float dt, Vector2 previousShip, Vector2 previousPirate)
        {
            float hit = CannonProgression.HitDamage(State.GunneryLevel);
            TickBall(ref Encounter.CrewBall, false, dt, previousShip, previousPirate, hit);
            float volley = CannonProgression.VolleyHitDamage(State.GunneryLevel);
            TickBall(ref Encounter.CrewBallB, false, dt, previousShip, previousPirate, volley);
            TickBall(ref Encounter.CrewBallC, false, dt, previousShip, previousPirate, volley);
            TickBall(ref Encounter.PirateBall, true, dt, previousShip, previousPirate);
        }

        private void TickBall(ref CannonballState ball, bool enemy, float dt, Vector2 previousShip, Vector2 previousPirate, float damage = 0)
        {
            if (!ball.Active) return;
            dt = Mathf.Min(dt, ball.Remaining);
            if (dt <= 0) { ball.Active = false; return; }
            Vector3 from = ball.Position;
            Vector3 to = from + ball.Velocity * dt + Vector3.down * (.5f * CannonballGravity * dt * dt);
            ball.Velocity += Vector3.down * (CannonballGravity * dt);
            ball.Remaining = Mathf.Max(0, ball.Remaining - dt);
            float hit = 0;
            bool target;
            if (enemy)
            {
                Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0, State.Heading, 0));
                Vector3 localFrom = inverse * (from - new Vector3(previousShip.x, 0, previousShip.y));
                Vector3 localTo = inverse * (to - new Vector3(State.Position.x, 0, State.Position.y));
                target = BallisticCollision.Box(localFrom, localTo, new Vector3(-5.4f, .1f, -8.4f), new Vector3(5.4f, 3.2f, 8.4f), out hit);
            }
            else
            {
                // Relative motion sweeps the moving pirate as well as the round.
                Vector3 relativeFrom = from - new Vector3(previousPirate.x, 1.5f, previousPirate.y);
                Vector3 relativeTo = to - new Vector3(Encounter.PiratePosition.x, 1.5f, Encounter.PiratePosition.y);
                target = Encounter.Pirate == PiratePhase.Attacking && BallisticCollision.Sphere(relativeFrom, relativeTo, Vector3.zero, 3.5f, out hit);
            }
            float water = from.y <= 0 ? 0 : to.y <= 0 ? from.y / (from.y - to.y) : float.PositiveInfinity;
            if (target && hit <= water)
            {
                ball.Position = Vector3.LerpUnclamped(from, to, hit);
                ball.Active = false;
                if (enemy)
                {
                    Encounter.EnemyShotTo = ball.Position;
                    Encounter.PirateHits++;
                    DamageHull(8);
                    if (Encounter.PirateHits == 1) CutPower();
                }
                else
                {
                    Encounter.ShotTo = ball.Position;
                    Encounter.Hits++;
                    Tally(crewBallOwner, TallyKind.Hits);
                    Encounter.PirateHealth = Mathf.Max(0, Encounter.PirateHealth - damage);
                    if (Encounter.PirateHealth == 0) { Encounter.Pirate = PiratePhase.Defeated; Encounter.Bounty = 50; }
                }
                return;
            }
            ball.Position = water <= 1 ? Vector3.LerpUnclamped(from, to, water) : to;
            if (water <= 1 || ball.Remaining == 0) ball.Active = false;
        }
    }
}
