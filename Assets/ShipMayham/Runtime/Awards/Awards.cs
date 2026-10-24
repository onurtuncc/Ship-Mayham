using System.Collections.Generic;

namespace ShipMayham
{
    /// <summary>A light-hearted highlight of the voyage: who did a thing the most, and how often.</summary>
    public readonly struct Award
    {
        public readonly string Title, Unit;
        public readonly int Value;
        /// <summary>Crew slots that share the award (several on a tie).</summary>
        public readonly int[] Slots;

        public Award(string title, string unit, int value, int[] slots) { Title = title; Unit = unit; Value = value; Slots = slots; }
    }

    /// <summary>
    /// Picks the awards shown in the voyage report. An award needs a minimum amount to count, goes to whoever has the most (ties
    /// share it), and only the first few by priority are shown. There is no ranking: a crew that did nothing special gets none.
    /// </summary>
    public static class Awards
    {
        public const int MaxShown = 4;

        private readonly struct Rule
        {
            public readonly string Title, Unit;
            public readonly int Threshold;
            public readonly System.Func<CrewTally, int> Stat;
            public Rule(string title, string unit, int threshold, System.Func<CrewTally, int> stat) { Title = title; Unit = unit; Threshold = threshold; Stat = stat; }
        }

        // In priority order: the funniest first, so a busy voyage still shows the stories worth telling.
        private static readonly Rule[] Rules =
        {
            new Rule("Passenger Hurler", "passengers launched", 2, t => t.Throws),
            new Rule("Sharpshooter", "cannon hits", 2, t => t.Hits),
            new Rule("Fix-It Hero", "repairs", 3, t => t.Repairs),
            new Rule("Tray Master", "orders served", 3, t => t.Orders),
            new Rule("Fare Collector", "fares collected", 2, t => t.Fares),
            new Rule("Dock Hand", "deliveries", 3, t => t.Deliveries),
            new Rule("Steady Hand", "seconds at the helm", 60, t => t.HelmSeconds),
        };

        /// <summary>Awards for the crew members marked active.</summary>
        public static List<Award> Compute(IReadOnlyList<CrewTally> tallies, IReadOnlyList<bool> active)
        {
            var result = new List<Award>();
            foreach (var rule in Rules)
            {
                int best = 0;
                for (int i = 0; i < tallies.Count; i++) if (active[i]) best = System.Math.Max(best, rule.Stat(tallies[i]));
                if (best < rule.Threshold) continue;
                var winners = new List<int>();
                for (int i = 0; i < tallies.Count; i++) if (active[i] && rule.Stat(tallies[i]) == best) winners.Add(i);
                result.Add(new Award(rule.Title, rule.Unit, best, winners.ToArray()));
                if (result.Count == MaxShown) break;
            }
            return result;
        }
    }
}
