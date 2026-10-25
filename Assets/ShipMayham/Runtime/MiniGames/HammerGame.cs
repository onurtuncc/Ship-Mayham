using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Nailing a plank over a leak: a marker swings across the plank, strike when it is over the green patch. Three nails hold the
    /// plank; three misses and the plank splits. Each nail moves the patch and speeds the swing up a little.
    /// </summary>
    public sealed class HammerGame
    {
        public const int NailsNeeded = 3, MissesAllowed = 3;
        public const float BaseSpeed = 1.1f, SpeedUpPerNail = .3f;

        public float Marker { get; private set; }
        public float WindowCenter { get; private set; } = .5f;
        public float WindowHalf { get; private set; }
        public int NailsDriven { get; private set; }
        public int Misses { get; private set; }
        public int Perfects { get; private set; }
        public float Elapsed { get; private set; }
        public bool Succeeded => NailsDriven >= NailsNeeded;
        public bool Failed => !Succeeded && Misses >= MissesAllowed;
        public bool Finished => Succeeded || Failed;

        private SeedRandom random;
        private readonly float baseHalf;
        private float direction = 1;

        public enum Strike { Ignored, Miss, Hit, Perfect }

        public HammerGame(uint seed, float difficulty)
        {
            random = new SeedRandom(seed);
            baseHalf = Mathf.Lerp(.14f, .08f, Mathf.Clamp01(difficulty));
            WindowHalf = baseHalf;
            Marker = random.Range(0f, 1f);
            MoveWindow();
        }

        private float Speed => BaseSpeed + SpeedUpPerNail * NailsDriven;

        private void MoveWindow() => WindowCenter = random.Range(.22f, .78f);

        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            Elapsed += dt;
            Marker += direction * Speed * dt;
            if (Marker >= 1f) { Marker = 1f; direction = -1; }
            else if (Marker <= 0f) { Marker = 0f; direction = 1; }
        }

        public Strike Hit()
        {
            if (Finished) return Strike.Ignored;
            float distance = Mathf.Abs(Marker - WindowCenter);
            if (distance > WindowHalf) { Misses++; return Strike.Miss; }
            NailsDriven++;
            bool perfect = distance <= WindowHalf * .35f;
            if (perfect) Perfects++;
            MoveWindow();
            WindowHalf = Mathf.Max(.05f, baseHalf - .01f * NailsDriven);
            return perfect ? Strike.Perfect : Strike.Hit;
        }

        public int Score => !Succeeded ? 0 : Mathf.Clamp(60 + Perfects * 15 - Misses * 15, 10, 100);
    }
}
