using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Cast, wait for the float to dip, hook the fish with good timing, then reel: hold to lift the green bar, let go to lower it, and keep
    /// it over the struggling fish until the catch meter fills. Striking early scares the fish away; a fish that is left alone gets away.
    /// </summary>
    public sealed class FishingGame
    {
        public enum Phase : byte { Waiting, Bite, Reeling, Caught, Lost }

        public const float BiteWindowSeconds = 1f, ReelTimeLimit = 30f, BarSize = .22f;

        public Phase State { get; private set; } = Phase.Waiting;
        public FishKind Fish { get; }
        public float WaitLeft { get; private set; }
        public float BiteLeft { get; private set; }
        public float Bar { get; private set; } = .3f;
        public float FishPosition { get; private set; } = .5f;
        public float Progress { get; private set; } = .3f;
        public float Elapsed { get; private set; }
        public bool Finished => State == Phase.Caught || State == Phase.Lost;
        public bool Succeeded => State == Phase.Caught;
        public string LostReason { get; private set; }

        private SeedRandom random;
        private float barVelocity, fishTarget = .5f, retarget;
        private readonly float fishSpeed, fishJitter;

        public FishingGame(uint seed, float difficulty)
        {
            random = new SeedRandom(seed);
            WaitLeft = random.Range(2f, 5f);
            float roll = random.Value();
            Fish = roll < .6f ? FishKind.Small : roll < .85f ? FishKind.Big : FishKind.Boot;
            float hard = Mathf.Clamp01(difficulty);
            // A big fish fights hard, a boot hardly at all.
            fishSpeed = Fish == FishKind.Big ? Mathf.Lerp(.55f, .8f, hard) : Fish == FishKind.Small ? Mathf.Lerp(.35f, .55f, hard) : .15f;
            fishJitter = Fish == FishKind.Big ? .9f : Fish == FishKind.Small ? .6f : .25f;
        }

        /// <summary>Strike on the bite to hook the fish; strike too early and the fish is gone.</summary>
        public void Hook()
        {
            if (Finished) return;
            if (State == Phase.Waiting) { Lose("You struck too early and scared it off."); return; }
            if (State != Phase.Bite) return;
            State = Phase.Reeling;
            Elapsed = 0;
            retarget = 0;
        }

        public void Tick(float dt, bool holding)
        {
            if (Finished || dt <= 0) return;
            switch (State)
            {
                case Phase.Waiting:
                    WaitLeft -= dt;
                    if (WaitLeft <= 0) { State = Phase.Bite; BiteLeft = BiteWindowSeconds; }
                    break;
                case Phase.Bite:
                    BiteLeft -= dt;
                    if (BiteLeft <= 0) Lose("It took the bait and got away.");
                    break;
                case Phase.Reeling:
                    Reel(dt, holding);
                    break;
            }
        }

        private void Reel(float dt, bool holding)
        {
            Elapsed += dt;
            barVelocity += (holding ? 2.6f : -2.2f) * dt;
            barVelocity = Mathf.Clamp(barVelocity, -1.4f, 1.4f);
            Bar += barVelocity * dt;
            if (Bar < 0) { Bar = 0; barVelocity = Mathf.Max(0, barVelocity * -.3f); }
            if (Bar > 1) { Bar = 1; barVelocity = Mathf.Min(0, barVelocity * -.3f); }

            retarget -= dt;
            if (retarget <= 0) { fishTarget = random.Range(.1f, .9f); retarget = random.Range(.4f, 1.1f) / Mathf.Max(.3f, fishJitter); }
            FishPosition = Mathf.MoveTowards(FishPosition, fishTarget, fishSpeed * dt);

            bool inside = Mathf.Abs(FishPosition - Bar) <= BarSize * .5f;
            Progress += (inside ? .26f : -.17f) * dt;
            if (Progress >= 1f) { Progress = 1f; State = Phase.Caught; }
            else if (Progress <= 0f) Lose("The line went slack and the fish swam off.");
            else if (Elapsed >= ReelTimeLimit) Lose("The fish outlasted you.");
        }

        private void Lose(string reason)
        {
            State = Phase.Lost;
            LostReason = reason;
        }

        public int Score => !Succeeded ? 0 : Fish == FishKind.Big ? 100 : Fish == FishKind.Small ? 60 : 20;
    }
}
