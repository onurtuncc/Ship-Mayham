using UnityEngine;

namespace ShipMayham
{
    /// <summary>Digging for buried treasure: tap fast to fill the hole, because the sand keeps sliding back in.</summary>
    public sealed class DigGame
    {
        public const float TimeLimit = 12f, PerPress = .095f, Slide = .2f;

        public float Depth { get; private set; }
        public float Elapsed { get; private set; }
        public int Presses { get; private set; }
        public bool Succeeded => Depth >= 1f;
        public bool Failed => !Succeeded && Elapsed >= TimeLimit;
        public bool Finished => Succeeded || Failed;

        private readonly float slide;

        public DigGame(float difficulty) { slide = Mathf.Lerp(Slide * .8f, Slide * 1.3f, Mathf.Clamp01(difficulty)); }

        public void Press()
        {
            if (Finished) return;
            Presses++;
            Depth = Mathf.Min(1f, Depth + PerPress);
        }

        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            Elapsed += dt;
            Depth = Mathf.Max(0, Depth - slide * dt);
        }

        public int Score => !Succeeded ? 0 : Mathf.Clamp(100 - Mathf.RoundToInt(Mathf.Max(0, Elapsed - 4f) * 8f), 20, 100);
    }
}
