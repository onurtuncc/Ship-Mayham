using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// A classic lock: move the pick until the lock gives, then turn. The nearer the pick is to the hidden sweet spot the further
    /// the cylinder turns before it jams; keep forcing a jammed lock and the pick strains and snaps. Three picks, then the lock wins.
    /// </summary>
    public sealed class LockpickGame
    {
        public const int MaxPicks = 3;
        public const float AngleRange = 90f, TurnSeconds = 1.1f, ReleaseSeconds = .4f, BreakSeconds = .8f;

        /// <summary>Where the lock gives, in degrees from the middle (-70..70).</summary>
        public readonly float SweetAngle;
        /// <summary>How far off the pick may be and still turn the whole way.</summary>
        public readonly float Tolerance;

        public int PicksLeft { get; private set; } = MaxPicks;
        public int PicksBroken => MaxPicks - PicksLeft;
        public float Turn { get; private set; }
        public float Tension { get; private set; }
        public float Elapsed { get; private set; }
        public bool Opened { get; private set; }
        public bool Failed { get; private set; }
        public bool Finished => Opened || Failed;
        /// <summary>Raised for exactly one tick after a pick snaps, so the view can play the sound.</summary>
        public bool PickJustBroke { get; private set; }

        public LockpickGame(uint seed, float difficulty)
        {
            var random = new SeedRandom(seed);
            SweetAngle = random.Range(-70f, 70f);
            Tolerance = Mathf.Lerp(15f, 6f, Mathf.Clamp01(difficulty));
        }

        /// <summary>How far the cylinder may turn with the pick at <paramref name="pickAngle"/>: 1 within the tolerance, less and less beyond it.</summary>
        public float MaxTurnAt(float pickAngle)
        {
            float distance = Mathf.Abs(Mathf.Clamp(pickAngle, -AngleRange, AngleRange) - SweetAngle);
            if (distance <= Tolerance) return 1f;
            return 1f - Mathf.Clamp01((distance - Tolerance) / (Tolerance * 3.5f));
        }

        public void Tick(float pickAngle, bool turning, float dt)
        {
            PickJustBroke = false;
            if (Finished || dt <= 0) return;
            Elapsed += dt;
            float limit = MaxTurnAt(pickAngle);
            if (!turning)
            {
                Turn = Mathf.MoveTowards(Turn, 0, dt / ReleaseSeconds);
                Tension = Mathf.MoveTowards(Tension, 0, dt * 2f);
                return;
            }
            Turn = Mathf.MoveTowards(Turn, 1, dt / TurnSeconds);
            if (Turn > limit)
            {
                // Jammed: the pick strains harder the further the lock is from giving.
                Turn = limit;
                Tension += dt / BreakSeconds * (1.6f - limit);
            }
            else Tension = Mathf.MoveTowards(Tension, 0, dt * .8f);
            if (Turn >= 1f) { Opened = true; return; }
            if (Tension >= 1f) BreakPick();
        }

        private void BreakPick()
        {
            PicksLeft--;
            PickJustBroke = true;
            Turn = 0;
            Tension = 0;
            if (PicksLeft <= 0) Failed = true;
        }

        /// <summary>100 for a lock opened with every pick intact and quickly, less for each broken pick and a slow hand.</summary>
        public int Score => !Opened ? 0 : Mathf.Clamp(100 - PicksBroken * 30 - Mathf.RoundToInt(Mathf.Max(0, Elapsed - 6f) * 2f), 10, 100);
    }
}
