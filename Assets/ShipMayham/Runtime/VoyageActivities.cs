using System;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>What a client needs to play the game the host just opened for it.</summary>
    public struct MiniGameStart
    {
        public MiniGameKind Kind;
        public int Target;
        public uint Seed;
        public float Difficulty;
    }

    /// <summary>
    /// Things to do besides sailing: pick the locks of buried chests, dig up treasure, fish from the pier, nail a plank over a leak,
    /// and swim after coins. The host opens a mini-game for one crew member, the client plays it and reports how it went, and the
    /// host checks that it was plausible before paying anything.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        // Harbor-local spots (the same in both ports): sand to dig, chests to open, quay edge to fish from.
        public static readonly Vector2[] DigSpots = { new Vector2(-9f, -19f), new Vector2(-21f, -10f), new Vector2(-3f, 16f), new Vector2(-17f, 2f) };
        public static readonly Vector2[] ChestSpots = { new Vector2(-26f, 20f), new Vector2(-4f, -21f) };
        public static readonly Vector2[] FishingSpots = { new Vector2(5f, -14f), new Vector2(5f, 12f), new Vector2(5f, 19f) };
        // Ship-local water: the channel beside the quay, and the open bay around the hull.
        public static readonly Vector2[] CoinSpots =
        {
            new Vector2(-7.1f, -10f), new Vector2(-7.1f, 0f), new Vector2(-7.1f, 11f), new Vector2(7.6f, -4f),
            new Vector2(7.6f, 6f), new Vector2(10.5f, 0f), new Vector2(3f, -11.5f), new Vector2(-3f, 12f)
        };

        public const float ActivityReach = 1.9f, CoinReach = 1f;
        public const int CoinValue = 5, AllCoinsBonus = 25, ChestMinCoins = 25, ChestMaxCoins = 60, FishFoodSmall = 2, FishFoodBig = 5;

        private struct ActiveMiniGame
        {
            public MiniGameKind Kind;
            public int Target;
            public uint Seed;
            public float StartedAt, Difficulty;
        }

        private readonly ActiveMiniGame[] miniGames = new ActiveMiniGame[MaxCrew];
        private uint activitySeed;

        /// <summary>Raised when the host opens a mini-game for a crew slot; the session sends it to that player.</summary>
        public event Action<int, MiniGameStart> MiniGameStarted;
        /// <summary>Raised for rewards that arrive without a command (a coin picked up while swimming).</summary>
        public event Action<int, string> CrewNotice;

        public bool InMiniGame(int slot) => slot >= 0 && slot < MaxCrew && miniGames[slot].Kind != MiniGameKind.None;

        public static int CoinMaskAll => (1 << CoinSpots.Length) - 1;

        public static Vector2[] SpotsOf(MiniGameKind kind) =>
            kind == MiniGameKind.Lockpick ? ChestSpots : kind == MiniGameKind.Dig ? DigSpots : kind == MiniGameKind.Fishing ? FishingSpots : Array.Empty<Vector2>();

        public bool SpotIsFresh(MiniGameKind kind, int index) =>
            kind == MiniGameKind.Lockpick ? (State.ChestMask & (1 << index)) == 0 :
            kind == MiniGameKind.Dig ? (State.DigMask & (1 << index)) == 0 : true;

        private float ActivityDifficulty => Mathf.Clamp01((State.Voyage - 1) * .12f);

        private uint NextActivitySeed()
        {
            if (activitySeed == 0) activitySeed = HazardLogic.VoyageSeed(hazardSessionSeed, State.Voyage) | 1u;
            activitySeed = activitySeed * 1664525u + 1013904223u;
            return activitySeed == 0 ? 1u : activitySeed;
        }

        /// <summary>The Repair station the crew member is standing at, or -1.</summary>
        private int RepairBenchNear(int slot)
        {
            for (int i = 0; i < Stations.Length; i++)
                if (Stations[i].Kind == StationKind.Repair && Vector2.Distance(Crew[slot].Position, Stations[i].Position) <= InteractionRange) return i;
            return -1;
        }

        /// <summary>Opens a mini-game for the sender. The reply is empty when the game started and a short reason when it could not.</summary>
        public string ActivityCommand(ulong clientId, MiniGameKind kind, int target)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby) return "Join the crew first.";
            ref var crew = ref Crew[slot];
            if (kind == MiniGameKind.None || kind > MiniGameKind.Dig) return "Unknown activity.";
            if (crew.Swimming) return "You are in the water.";
            if (crew.AtHelm || crew.AtCannon || crew.AtCrane) return "Release your station first.";
            if (crew.JumpHeight > 0 || crew.VerticalSpeed > 0) return "Land first.";
            if (crew.Carrying != ServiceItem.None || CarriedPassenger(slot) >= 0) return "Free your hands first.";
            if (InMiniGame(slot)) return string.Empty;
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;

            if (kind == MiniGameKind.Hammer)
            {
                if (crew.OnShore) return "Go back aboard to fix the hull.";
                if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing) return "Nothing to repair now.";
                if (State.Leak == 0) return "The hull is watertight.";
                if (RepairBenchNear(slot) < 0) return "Go to the repair bench.";
                if (Emergency.ValveAdjusted || State.ReadyRepairKits > 0) return "A patch kit or the right valve fixes this faster.";
            }
            else
            {
                if (!HarborOpen || !crew.OnShore) return "Go ashore to try your luck.";
                Vector2[] spots = SpotsOf(kind);
                if (target < 0 || target >= spots.Length) return "There is nothing there.";
                if (Vector2.Distance(crew.Position, spots[target]) > ActivityReach) return "Move closer.";
                if (!SpotIsFresh(kind, target)) return kind == MiniGameKind.Lockpick ? "This chest is already open." : "Somebody dug here already.";
            }
            var start = new MiniGameStart { Kind = kind, Target = target, Seed = NextActivitySeed(), Difficulty = ActivityDifficulty };
            miniGames[slot] = new ActiveMiniGame { Kind = kind, Target = target, Seed = start.Seed, StartedAt = clock, Difficulty = start.Difficulty };
            crew.Doing = kind;
            crew.Velocity = Vector2.zero;
            MiniGameStarted?.Invoke(slot, start);
            return string.Empty;
        }

        /// <summary>Settles a mini-game: what the client reports is checked against the game the host opened.</summary>
        public string FinishMiniGame(ulong clientId, in MiniGameResult result)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !InMiniGame(slot)) return string.Empty;
            var game = miniGames[slot];
            if (game.Kind != result.Kind || game.Target != result.Target) return string.Empty;
            ClearMiniGame(slot);
            if (result.Outcome == MiniGameOutcome.Abandoned) return "You gave up.";
            float elapsed = clock - game.StartedAt;
            bool believable = elapsed >= MiniGameRules.MinimumSeconds(game.Kind) && elapsed <= MiniGameRules.MaximumSeconds(game.Kind) + 10f;
            if (result.Outcome != MiniGameOutcome.Succeeded) return FailedMiniGame(slot, game);
            if (!believable) return "That was too quick to be real.";
            return RewardMiniGame(slot, game, Mathf.Clamp(result.Score, 0, 100));
        }

        private string FailedMiniGame(int slot, in ActiveMiniGame game)
        {
            switch (game.Kind)
            {
                case MiniGameKind.Hammer:
                    if (State.Leak == 0) return "The leak is gone anyway.";
                    State.RepairSteps = Mathf.Min(3, State.RepairSteps + 1);
                    return $"The plank split, but a nail held: {State.RepairSteps}/4.";
                case MiniGameKind.Lockpick: return "The picks snapped. The chest stays shut: try again.";
                case MiniGameKind.Dig: return "The sand slid back in. Dig again!";
                default: return "It got away.";
            }
        }

        private string RewardMiniGame(int slot, in ActiveMiniGame game, int score)
        {
            var random = new SeedRandom(game.Seed ^ 0xA5A5A5A5u);
            switch (game.Kind)
            {
                case MiniGameKind.Hammer:
                    if (State.Leak == 0) return "The leak is gone anyway.";
                    Tally(slot, TallyKind.Repairs);
                    FinishLeakRepair();
                    State.Health = Mathf.Min(100, State.Health + 3);
                    return "Plank nailed down! The leak is sealed.";
                case MiniGameKind.Lockpick:
                {
                    if (!SpotIsFresh(MiniGameKind.Lockpick, game.Target)) return "This chest is already open.";
                    State.ChestMask |= 1 << game.Target;
                    int coins = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(ChestMinCoins, ChestMaxCoins, random.Value() * .5f + score / 200f)), ChestMinCoins, ChestMaxCoins);
                    State.Wallet += coins;
                    bool rep = score >= 90;
                    if (rep) State.Reputation++;
                    return $"Click! The chest opens: +{coins} coins{(rep ? " and +1 reputation for a clean pick" : string.Empty)}.";
                }
                case MiniGameKind.Dig:
                {
                    if (!SpotIsFresh(MiniGameKind.Dig, game.Target)) return "Somebody dug here already.";
                    State.DigMask |= 1 << game.Target;
                    float roll = random.Value();
                    if (roll < .5f) { int coins = random.Range(10, 26); State.Wallet += coins; return $"A coin purse! +{coins} coins."; }
                    if (roll < .8f) { int coins = random.Range(25, 46); State.Wallet += coins; return $"A pirate stash! +{coins} coins."; }
                    if (roll < .92f) { State.Wallet += 10; State.Reputation++; return "An old sea chart! +10 coins and +1 reputation."; }
                    State.Wallet += 1;
                    return "A rusty boot. +1 coin for the effort.";
                }
                case MiniGameKind.Fishing:
                {
                    var fish = new FishingGame(game.Seed, game.Difficulty).Fish;
                    if (fish == FishKind.Boot) { State.Wallet += 1; return "An old boot! +1 coin."; }
                    int food = fish == FishKind.Big ? FishFoodBig : FishFoodSmall;
                    int before = State.FoodStock;
                    State.FoodStock = Mathf.Min(SupplyCap, State.FoodStock + food);
                    return $"{(fish == FishKind.Big ? "A big fish!" : "A fish!")} +{State.FoodStock - before} food for the galley.";
                }
                default: return string.Empty;
            }
        }

        private void ClearMiniGame(int slot)
        {
            miniGames[slot] = default;
            Crew[slot].Doing = MiniGameKind.None;
        }

        private void ResetActivities()
        {
            Array.Clear(miniGames, 0, miniGames.Length);
            for (int i = 0; i < MaxCrew; i++) Crew[i].Doing = MiniGameKind.None;
        }

        private void TickActivities(float dt)
        {
            for (int i = 0; i < MaxCrew; i++)
            {
                if (miniGames[i].Kind == MiniGameKind.None) continue;
                bool gone = !Crew[i].Active || Crew[i].Swimming || clock - miniGames[i].StartedAt > MiniGameRules.MaximumSeconds(miniGames[i].Kind) + 15f;
                if (gone) ClearMiniGame(i);
            }
            if (!HarborOpen) return;
            // Every port starts with untouched sand, chests and coins.
            int key = CurrentPortKey + 1;
            if (State.ActivityPortKey != key)
            {
                State.ActivityPortKey = key;
                State.DigMask = State.ChestMask = State.CoinMask = 0;
            }
            for (int i = 0; i < MaxCrew; i++)
            {
                if (!Crew[i].Active || !Crew[i].Swimming) continue;
                for (int c = 0; c < CoinSpots.Length; c++)
                {
                    if ((State.CoinMask & (1 << c)) != 0 || Vector2.Distance(Crew[i].Position, CoinSpots[c]) > CoinReach) continue;
                    State.CoinMask |= 1 << c;
                    State.Wallet += CoinValue;
                    string text = $"+{CoinValue} coins";
                    if (State.CoinMask == CoinMaskAll)
                    {
                        State.Wallet += AllCoinsBonus;
                        State.Reputation++;
                        text = $"That was the last sunken coin! +{CoinValue + AllCoinsBonus} coins and +1 reputation";
                    }
                    CrewNotice?.Invoke(i, text);
                }
            }
        }
    }
}
