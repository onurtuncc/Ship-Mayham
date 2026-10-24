using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// How much pressure a crew of a given size gets. Four crew members face the full voyage; fewer face less of the
    /// events and pirate threat so a solo player or a pair can still keep up. Passengers and the economy do not change.
    /// The factors are first guesses to be tuned after played sessions.
    /// </summary>
    public static class Workload
    {
        public const int FullCrew = VoyageSimulation.MaxCrew;
        public const float Full = 1f, ThreeCrew = .8f, TwoCrew = .6f, Solo = .45f;

        /// <summary>The pressure factor for <paramref name="crew"/> active crew members; below 1, delays grow by 1 / factor.</summary>
        public static float ScaleFor(int crew)
        {
            if (crew >= FullCrew) return Full;
            switch (crew)
            {
                case 3: return ThreeCrew;
                case 2: return TwoCrew;
                default: return Solo;
            }
        }

        /// <summary>A delay stretched for a smaller crew.</summary>
        public static float Stretch(float seconds, float scale) => seconds / Mathf.Max(.1f, scale);
    }
}
