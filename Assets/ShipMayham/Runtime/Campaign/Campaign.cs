using UnityEngine;

namespace ShipMayham
{
    public enum IslandTheme : byte { Town, Reef, Cove, Storm, Skull }

    /// <summary>The captain's task of an island: what must be done there to earn a token toward the final voyage.</summary>
    public enum IslandTask : byte { None, HarborJob, Pearls, Chests, StormPassage, Boss }

    /// <summary>One stop of the voyage chain: where it lies, what it asks and what it needs before the ship may sail there.</summary>
    public readonly struct IslandDef
    {
        public readonly string Name, Blurb, TaskText;
        public readonly Vector2 World;
        public readonly IslandTheme Theme;
        public readonly IslandTask Task;
        /// <summary>Tokens (finished island tasks) and reputation needed to sail here.</summary>
        public readonly int MinTokens, MinReputation;
        /// <summary>Extra coins every successful arrival here pays (the farther, the riskier).</summary>
        public readonly int FareBonus;

        public IslandDef(string name, string blurb, Vector2 world, IslandTheme theme, IslandTask task, string taskText, int minTokens, int minReputation, int fareBonus)
        {
            Name = name; Blurb = blurb; World = world; Theme = theme; Task = task; TaskText = taskText;
            MinTokens = minTokens; MinReputation = minReputation; FareBonus = fareBonus;
        }
    }

    /// <summary>
    /// The voyage chain: six islands from the home quay to the pirates' lair. Pure data and rules, shared by the host's rules, the
    /// map and the log. An island is unlocked by the tokens earned from other islands' tasks and by reputation, so the ship
    /// travels outward instead of shuttling between two quays.
    /// </summary>
    public static class Campaign
    {
        public const int BrassQuay = 0, NorthHaven = 1, CoralCay = 2, SmugglersCove = 3, StormReef = 4, SkullAtoll = 5;
        /// <summary>Sunken coins to bring up at the Cay, chests to open at the Cove and hull needed to ride out the reef storm.</summary>
        public const int PearlsNeeded = 6, ChestsNeeded = 2, StormHullNeeded = 50;
        /// <summary>The two islands every ship knows from the start (bit mask).</summary>
        public const int StarterIslands = (1 << BrassQuay) | (1 << NorthHaven);
        public const int TaskCoinReward = 60, TaskReputationReward = 1, DiscoveryReward = 40;

        public static readonly IslandDef[] Islands =
        {
            new IslandDef("Brass Quay", "Home port. Passengers, a market and the purser's desk.", new Vector2(0, 0), IslandTheme.Town, IslandTask.None,
                string.Empty, 0, 0, 0),
            new IslandDef("North Haven", "A busy harbor town with a crane yard. Prove yourself here.", new Vector2(0, 220), IslandTheme.Town, IslandTask.HarborJob,
                "Finish a job for the harbor master", 0, 0, 0),
            new IslandDef("Coral Cay", "Shallow turquoise reef. Dive for the pearls on the seabed.", new Vector2(240, 330), IslandTheme.Reef, IslandTask.Pearls,
                $"Bring up {PearlsNeeded} pearls from the reef", 1, 2, 20),
            new IslandDef("Smugglers' Cove", "A hidden cove of locked chests and wary locals.", new Vector2(-250, 370), IslandTheme.Cove, IslandTask.Chests,
                $"Pick the locks of {ChestsNeeded} smugglers' chests", 1, 2, 30),
            new IslandDef("Storm Reef", "Jagged rock and a permanent gale. Few ships return untouched.", new Vector2(0, 520), IslandTheme.Storm, IslandTask.StormPassage,
                $"Arrive with at least {StormHullNeeded}% hull", 2, 5, 45),
            new IslandDef("Skull Atoll", "The Black Pennant's lair. Sink the flagship to end its reign.", new Vector2(0, 820), IslandTheme.Skull, IslandTask.Boss,
                "Sink the pirate flagship", 4, 9, 100),
        };

        public static int Count => Islands.Length;
        public static bool Valid(int island) => island >= 0 && island < Islands.Length;
        public static IslandDef Island(int island) => Islands[Mathf.Clamp(island, 0, Islands.Length - 1)];
        public static Vector2 World(int island) => Island(island).World;
        public static string Name(int island) => Island(island).Name;

        /// <summary>Margin around the outermost islands that the ship may still sail into.</summary>
        public const float WorldMargin = 140f;
        public static Vector2 WorldMin { get; } = ComputeBounds(false);
        public static Vector2 WorldMax { get; } = ComputeBounds(true);

        private static Vector2 ComputeBounds(bool max)
        {
            var bound = Islands[0].World;
            foreach (var island in Islands) bound = max ? Vector2.Max(bound, island.World) : Vector2.Min(bound, island.World);
            return bound + (max ? Vector2.one : -Vector2.one) * WorldMargin;
        }

        /// <summary>Whether bit <paramref name="island"/> of a mask is set.</summary>
        public static bool Has(int mask, int island) => Valid(island) && (mask & (1 << island)) != 0;

        /// <summary>Finished island tasks (the final island's task is the ending, not a token).</summary>
        public static int Tokens(int taskMask)
        {
            int count = 0;
            for (int i = 0; i < Islands.Length - 1; i++) if (Has(taskMask, i)) count++;
            return count;
        }

        /// <summary>Tokens the final voyage needs: every task before it.</summary>
        public static int TokensForFinale => SkullAtoll - 1;

        /// <summary>After a victory the whole chain is open for free sailing.</summary>
        public static bool Unlocked(int island, int taskMask, int reputation, bool free)
        {
            if (!Valid(island)) return false;
            if (free) return true;
            var def = Islands[island];
            return Tokens(taskMask) >= def.MinTokens && reputation >= def.MinReputation;
        }

        /// <summary>Why an island cannot be sailed to yet, or an empty string when it can.</summary>
        public static string LockReason(int island, int taskMask, int reputation, bool free)
        {
            if (!Valid(island)) return "Unknown island.";
            if (Unlocked(island, taskMask, reputation, free)) return string.Empty;
            var def = Islands[island];
            int tokens = Tokens(taskMask);
            if (tokens < def.MinTokens) return $"Needs {def.MinTokens} finished island task{(def.MinTokens == 1 ? "" : "s")} ({tokens} so far).";
            return $"Needs {def.MinReputation} reputation.";
        }

        /// <summary>
        /// Where the ship heads by default from <paramref name="from"/>: the first open island whose task is still undone, so
        /// progress is the easy choice, or else the first other open island.
        /// </summary>
        public static int DefaultTarget(int from, int taskMask, int reputation, bool free)
        {
            for (int i = 1; i < Islands.Length; i++)
                if (i != from && !Has(taskMask, i) && Unlocked(i, taskMask, reputation, free)) return i;
            for (int i = 0; i < Islands.Length; i++)
                if (i != from && Unlocked(i, taskMask, reputation, free)) return i;
            return from == BrassQuay ? NorthHaven : BrassQuay;
        }

        /// <summary>The story line shown in the captain's log for the current progress.</summary>
        public static string Objective(int taskMask, int reputation, bool victory)
        {
            if (victory) return "The Black Pennant is sunk. The seas are yours: sail where you like.";
            int tokens = Tokens(taskMask);
            if (tokens >= TokensForFinale && Unlocked(SkullAtoll, taskMask, reputation, victory))
                return "Every task is done. Sail to Skull Atoll and sink the pirate flagship.";
            for (int i = 1; i < SkullAtoll; i++)
            {
                if (Has(taskMask, i)) continue;
                if (Unlocked(i, taskMask, reputation, victory)) return $"{Islands[i].Name}: {Islands[i].TaskText}.";
            }
            for (int i = 1; i <= SkullAtoll; i++)
                if (!Unlocked(i, taskMask, reputation, victory)) return $"{Islands[i].Name} is still out of reach: {LockReason(i, taskMask, reputation, victory)}";
            return Islands[SkullAtoll].TaskText + ".";
        }
    }
}
