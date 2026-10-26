using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// The voyage chain in the host's rules: where the ship leaves from and sails to, which island it may choose next, the
    /// captain's tasks that earn tokens, the finale and the victory. The data lives in <see cref="Campaign"/>.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>Health of the pirate flagship is shown as a percentage of 100; this many times tougher than an ordinary raider.</summary>
        public const float BossToughness = 2.2f, BossCycleToughness = .35f;
        public const float BossArrivalSeconds = 12f, BossShotIntervalFactor = .65f;
        public const int BossBounty = 150;

        /// <summary>The island where the ship lies: the one it left while boarding or sailing, the one it reached after arriving.</summary>
        public static int DockedIsland(in VoyageState state) => state.Phase == VoyagePhase.Arrived ? state.Target : state.Island;

        public static Vector2 OriginPoint(in VoyageState state) => Campaign.World(state.Island);

        public static Vector2 DestinationPoint(in VoyageState state) => Campaign.World(state.Target);

        public Vector2 OriginPoint() => OriginPoint(State);

        public Vector2 DestinationPoint() => DestinationPoint(State);

        /// <summary>World position of the place the harbor town hangs from while the ship lies at <see cref="DockedIsland"/>.</summary>
        public static Vector3 IslandHarborOrigin(int island)
        {
            Vector2 world = Campaign.World(island);
            return new Vector3(world.x - 14f, 0, world.y);
        }

        /// <summary>Whether the pirate flagship is the voyage's threat: the final island, until the Black Pennant is sunk.</summary>
        public static bool FlagshipVoyage(in VoyageState state) => state.Target == Campaign.SkullAtoll && !state.Victory;

        private static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        /// <summary>Picks the next island at the harbor master's office. Only while boarding, before passengers are taken on.</summary>
        public string SelectDestination(ulong clientId, int island)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby) return "Join the crew first.";
            if (!Campaign.Valid(island)) return "Unknown island.";
            if (State.Phase != VoyagePhase.Boarding) return "Choose the next island once the ship is ready for a new voyage.";
            if (State.Boarded > 0) return "Choose the destination before boarding passengers.";
            if (!Crew[slot].OnShore || Vector2.Distance(Crew[slot].Position, HarborPoint(HarborAction.HarborMaster)) > InteractionRange + 1f)
                return "Ask the harbor master to plot a course.";
            if (island == State.Island) return "You are already here.";
            string locked = Campaign.LockReason(island, State.TaskMask, State.Reputation, State.Victory);
            if (locked.Length > 0) return $"{Campaign.Name(island)} is out of reach. {locked}";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            int previous = State.Target;
            State.Target = island;
            ApplyIslandRoute(previous);
            ResetEncounterForTarget();
            return $"Course set for {Campaign.Name(island)}.";
        }

        /// <summary>Arriving at the pirates' lair means the flagship; every other voyage has an ordinary raider.</summary>
        private void ResetEncounterForTarget()
        {
            if (State.Phase != VoyagePhase.Boarding) return;
            Encounter.Boss = FlagshipVoyage(State);
        }

        /// <summary>Called when a voyage ends in success: records the arrival and what it earned.</summary>
        private int ArrivalBonus()
        {
            int island = State.Target;
            int bonus = Campaign.Island(island).FareBonus;
            State.VisitedMask |= 1 << State.Island;
            if (!Campaign.Has(State.VisitedMask, island))
            {
                State.VisitedMask |= 1 << island;
                bonus += Campaign.DiscoveryReward;
            }
            State.PortVisit++;
            if (island == Campaign.SkullAtoll && Encounter.Boss && Encounter.Pirate == PiratePhase.Defeated)
            {
                State.Victory = true;
                State.TaskMask |= 1 << Campaign.SkullAtoll;
            }
            return bonus;
        }

        /// <summary>Starts again from the home quay with a harder flagship; ship, coins and reputation are kept.</summary>
        public string StartNewCycle(ulong clientId)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby) return "Join the crew first.";
            if (!State.Victory) return "Sink the flagship first.";
            if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Arrived) return "Wait until the ship is in port.";
            if (State.Boarded > 0) return "Start the new cycle before boarding passengers.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            State.Victory = false;
            State.TaskMask = 0;
            State.Cycle++;
            State.Target = Campaign.DefaultTarget(DockedIsland(State), State.TaskMask, State.Reputation, false);
            if (State.Phase == VoyagePhase.Boarding) ResetEncounterForTarget();
            return $"A new cycle begins. The Black Pennant is back and meaner (cycle {State.Cycle}).";
        }

        private void TickCampaign()
        {
            if (!HarborOpen || InLobby) return;
            int island = DockedIsland(State);
            if (Campaign.Has(State.TaskMask, island)) return;
            bool done;
            switch (Campaign.Island(island).Task)
            {
                case IslandTask.HarborJob: done = State.DockJobPortKey == CurrentPortKey; break;
                case IslandTask.Pearls: done = CountBits(State.CoinMask) >= Campaign.PearlsNeeded; break;
                case IslandTask.Chests: done = CountBits(State.ChestMask) >= Campaign.ChestsNeeded; break;
                case IslandTask.StormPassage: done = State.Phase == VoyagePhase.Arrived && State.Health >= Campaign.StormHullNeeded; break;
                default: done = false; break;
            }
            if (done) CompleteTask(island);
        }

        private void CompleteTask(int island)
        {
            var before = State;
            State.TaskMask |= 1 << island;
            State.Wallet += Campaign.TaskCoinReward;
            State.Reputation += Campaign.TaskReputationReward;
            string text = $"Captain's task done at {Campaign.Name(island)}! +{Campaign.TaskCoinReward} coins and +{Campaign.TaskReputationReward} reputation.";
            for (int i = 0; i < Campaign.Count; i++)
            {
                bool wasOpen = Campaign.Unlocked(i, before.TaskMask, before.Reputation, before.Victory);
                if (!wasOpen && Campaign.Unlocked(i, State.TaskMask, State.Reputation, State.Victory)) text += $" {Campaign.Name(i)} is open.";
            }
            if (Campaign.Tokens(State.TaskMask) >= Campaign.TokensForFinale && !Campaign.Has(State.TaskMask, Campaign.SkullAtoll))
                text += " Every task is done: the pirates' lair awaits.";
            for (int i = 0; i < MaxCrew; i++) if (Crew[i].Active && Crew[i].ClientId != ulong.MaxValue) CrewNotice?.Invoke(i, text);
        }

        /// <summary>Where the opportunity chest drifts: a bit more than halfway along the route, off to the west.</summary>
        private Vector2 RouteChestPosition()
        {
            Vector2 from = OriginPoint(), to = DestinationPoint();
            Vector2 along = to - from;
            if (along.sqrMagnitude < 1) return from;
            Vector2 direction = along.normalized;
            Vector2 left = new Vector2(-direction.y, direction.x);
            return from + along * .57f + left * 25f;
        }
    }
}
