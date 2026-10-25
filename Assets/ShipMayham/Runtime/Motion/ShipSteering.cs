using UnityEngine;

namespace ShipMayham
{
    /// <summary>The ship's helm and hull motion: what <see cref="ShipSteering"/> advances. Replicated inside VoyageState.</summary>
    public struct ShipMotion
    {
        /// <summary>Engine order lever, -<see cref="ShipSteering.MaxAstern"/> (full astern) to 1 (full ahead). Stays where it is set.</summary>
        public float Throttle;
        /// <summary>Rudder angle, -1 (hard to port) to 1 (hard to starboard).</summary>
        public float Rudder;
        /// <summary>Speed through the water along the keel (m/s, negative going astern).</summary>
        public float Speed;
        /// <summary>Sideways slip (m/s, positive to starboard): a turning hull skids outward before the water grips it.</summary>
        public float Drift;
        /// <summary>Rate of turn (degrees per second, positive clockwise seen from above).</summary>
        public float YawRate;
    }

    /// <summary>
    /// Ship handling modelled on how a real hull behaves, tuned for play:
    /// <list type="bullet">
    /// <item>W/S move an engine-order lever that stays where it is set (a telegraph, not a gas pedal). Thrust works against
    /// linear plus quadratic water resistance, so a ship gathers way slowly, coasts for a long time and needs astern to stop.</item>
    /// <item>A/D turn the wheel; the rudder follows at a limited rate and eases back amidships when the wheel is let go.</item>
    /// <item>The rudder only bites with water flowing past it: from the ship's own way and from the propeller wash while the
    /// engine pushes ahead. A drifting ship barely answers the helm; going astern the steering reverses.</item>
    /// <item>Rate of turn follows the rudder through the first-order Nomoto model (T·r' + r = K·δ): the bow takes a moment to
    /// start swinging and keeps swinging after the rudder is centred, so turns are begun early and checked with opposite helm.</item>
    /// <item>A turning hull skids outward and loses speed.</item>
    /// </list>
    /// Pure and deterministic; VoyageSimulation owns the state.
    /// </summary>
    public static class ShipSteering
    {
        public const float LeverRate = .55f;            // lever travel per second while W/S is held
        public const float MaxAstern = .6f;             // the lever's astern end
        public const float AsternThrust = .6f;          // astern pushes weaker than ahead (propellers are built for ahead)
        public const float WheelRate = 1.5f;            // rudder travel per second while A/D is held
        public const float WheelReturn = .9f;           // rudder travel per second back amidships when the wheel is let go
        public const float MaxRudderDegrees = 35f;      // for the HUD and the wheel model
        public const float LinearDrag = .12f;           // 1/s
        public const float QuadraticDrag = .035f;       // 1/m
        public const float TurnRateGain = 16f;          // K: degrees per second at full rudder and full flow
        public const float TurnLag = 1.8f;              // T: seconds for the turn rate to settle on the rudder
        public const float FullFlowSpeed = 3f;          // own speed (m/s) at which the rudder has full authority
        public const float PropWash = .35f;             // rudder authority from the propeller wash at full ahead
        public const float DriftGain = .45f;            // how strongly a turn throws the hull sideways
        public const float DriftDamping = 1.2f;         // 1/s: how fast the water stops a sideways skid
        public const float TurnSpeedLoss = .3f;         // speed lost per radian of turn

        /// <summary>Forward thrust (m/s²) at full ahead that makes <paramref name="maxSpeed"/> the top speed against the drag.</summary>
        public static float Thrust(float maxSpeed) => LinearDrag * maxSpeed + QuadraticDrag * maxSpeed * maxSpeed;

        /// <summary>
        /// Advances one step. <paramref name="manned"/> false means nobody holds the helm: the lever eases back to stop and the
        /// rudder to amidships while the ship carries its way. Returns the heading change in degrees (clockwise).
        /// </summary>
        public static float Step(ref ShipMotion m, Vector2 input, bool manned, float maxSpeed, float dt)
        {
            if (dt <= 0) return 0;
            input = Vector2.ClampMagnitude(input, 1);
            if (!manned) input = Vector2.zero;
            // The lever stays where it is put; an empty helm eases it to stop.
            m.Throttle = manned
                ? Mathf.Clamp(m.Throttle + input.y * LeverRate * dt, -MaxAstern, 1)
                : Mathf.MoveTowards(m.Throttle, 0, LeverRate * dt);
            m.Rudder = Mathf.Abs(input.x) > .05f
                ? Mathf.Clamp(m.Rudder + input.x * WheelRate * dt, -1, 1)
                : Mathf.MoveTowards(m.Rudder, 0, WheelReturn * dt);

            // Way: thrust against linear and quadratic water resistance, plus the drag of a hull swinging through the water.
            float thrust = Thrust(maxSpeed) * (m.Throttle >= 0 ? m.Throttle : m.Throttle * AsternThrust);
            float yawRadians = m.YawRate * Mathf.Deg2Rad;
            float resistance = LinearDrag * m.Speed + QuadraticDrag * m.Speed * Mathf.Abs(m.Speed) + TurnSpeedLoss * Mathf.Abs(yawRadians) * m.Speed;
            m.Speed += (thrust - resistance) * dt;
            if (Mathf.Abs(m.Speed) < .01f && Mathf.Abs(m.Throttle) < .01f) m.Speed = 0;

            // Steerage: water past the rudder from the ship's way and, going ahead, from the propeller.
            float flow = Mathf.Clamp(Mathf.Abs(m.Speed) / FullFlowSpeed, 0, 1) + PropWash * Mathf.Max(0, m.Throttle);
            float direction = m.Speed >= -.05f ? 1 : -1;
            float target = TurnRateGain * m.Rudder * Mathf.Min(flow, 1.25f) * direction;
            // Nomoto first order: the rate of turn approaches what the rudder asks for with time constant T.
            m.YawRate += (target - m.YawRate) * (1 - Mathf.Exp(-dt / TurnLag));

            // The hull skids outward in a turn and the water damps the skid.
            m.Drift += (-DriftGain * m.Speed * yawRadians - DriftDamping * m.Drift) * dt;
            if (Mathf.Abs(m.Drift) < .001f && Mathf.Abs(m.YawRate) < .01f) m.Drift = 0;
            return m.YawRate * dt;
        }

        /// <summary>World-plane velocity (x, z) for a heading in degrees.</summary>
        public static Vector2 Velocity(in ShipMotion m, float headingDegrees)
        {
            float h = headingDegrees * Mathf.Deg2Rad;
            var forward = new Vector2(Mathf.Sin(h), Mathf.Cos(h));
            var starboard = new Vector2(Mathf.Cos(h), -Mathf.Sin(h));
            return forward * m.Speed + starboard * m.Drift;
        }

        /// <summary>Lever position as an engine order for the HUD.</summary>
        public static string EngineOrder(float throttle)
        {
            if (throttle <= -.45f) return "FULL ASTERN";
            if (throttle < -.05f) return "ASTERN";
            if (throttle < .05f) return "STOP";
            if (throttle < .4f) return "SLOW AHEAD";
            if (throttle < .8f) return "HALF AHEAD";
            return "FULL AHEAD";
        }
    }
}
