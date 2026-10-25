using UnityEngine;

namespace ShipMayham
{
    /// <summary>The short skill games that settle a problem or a prize instead of a button press.</summary>
    public enum MiniGameKind : byte { None, Lockpick, Hammer, Fishing, Dig }

    public enum MiniGameOutcome : byte { Abandoned, Failed, Succeeded }

    public enum FishKind : byte { Small, Big, Boot }

    /// <summary>What a client reports when a mini-game ends. The host decides what it is worth.</summary>
    public struct MiniGameResult
    {
        public MiniGameKind Kind;
        /// <summary>Which chest, dig spot, fishing spot or repair bench the game was played at.</summary>
        public int Target;
        public MiniGameOutcome Outcome;
        /// <summary>0-100: how cleanly it went (fewer broken picks, more perfect strikes, a big fish).</summary>
        public int Score;
        public int ElapsedMs;
        /// <summary>For fishing: what was on the line.</summary>
        public FishKind Fish;
    }

    /// <summary>A tiny seeded generator so a game played on the client is the same game the host started.</summary>
    public struct SeedRandom
    {
        private uint state;

        public SeedRandom(uint seed) { state = seed == 0 ? 0x9E3779B9u : seed; Next(); Next(); }

        public uint Next()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state;
        }

        /// <summary>A number in [0, 1).</summary>
        public float Value() => (Next() & 0xFFFFFF) / 16777216f;

        public float Range(float min, float max) => Mathf.Lerp(min, max, Value());

        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(Next() % (uint)Mathf.Max(1, maxExclusive - minInclusive));
    }

    /// <summary>The rules every mini-game shares: time limits and how long a plausible game takes (anything faster was not played).</summary>
    public static class MiniGameRules
    {
        public static float MinimumSeconds(MiniGameKind kind)
        {
            switch (kind)
            {
                case MiniGameKind.Lockpick: return 1.2f;
                case MiniGameKind.Hammer: return 2.5f;
                case MiniGameKind.Fishing: return 3f;
                case MiniGameKind.Dig: return 1.5f;
                default: return 0;
            }
        }

        public static float MaximumSeconds(MiniGameKind kind)
        {
            switch (kind)
            {
                case MiniGameKind.Lockpick: return 120f;
                case MiniGameKind.Hammer: return 60f;
                case MiniGameKind.Fishing: return 90f;
                case MiniGameKind.Dig: return 40f;
                default: return 0;
            }
        }
    }
}
