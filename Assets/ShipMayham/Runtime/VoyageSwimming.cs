using UnityEngine;

namespace ShipMayham
{
    /// <summary>A rope ladder: where it starts on the deck or quay and where it meets the water.</summary>
    public readonly struct Ladder
    {
        public readonly string Name;
        /// <summary>The top of the ladder in the coordinates of the place it hangs from (the deck, or the quay when <see cref="OnQuay"/>).</summary>
        public readonly Vector2 Top;
        public readonly bool OnQuay;
        /// <summary>The surface point at the bottom, in ship-local coordinates: every swimmer lives in the water around the ship.</summary>
        public readonly Vector2 Base;

        public Ladder(string name, Vector2 top, bool onQuay, Vector2 water) { Name = name; Top = top; OnQuay = onQuay; Base = water; }
    }

    /// <summary>
    /// Swimming. The water around the ship is simulated in ship-local coordinates, so a swimmer stays beside a moving ship and a
    /// ship at port has the quay a few metres to port. Crew dive in from a ladder top (at port only), swim, and climb back out of
    /// any ladder; the same water is where a wave can wash someone overboard.
    /// </summary>
    public sealed partial class VoyageSimulation
    {
        /// <summary>Surface of the sea in world height (the deck is 1.55 m above it).</summary>
        public const float SeaLevel = -1.05f;
        public const float SwimSpeedFactor = .6f, LadderReach = 1.7f, LadderCooldownSeconds = .7f, OverboardLimitSeconds = 30f;
        /// <summary>The quay's x offset: harbor coordinates plus this give ship-local ones (the harbor sits 14 m to port).</summary>
        public const float QuayOffsetX = -14f;
        public static readonly Vector2 WaterMin = new Vector2(-8f, -14f), WaterMax = new Vector2(12f, 14f);
        /// <summary>Half extents of the hull that swimmers cannot enter.</summary>
        public static readonly Vector2 HullHalfExtents = new Vector2(5.8f, 8.8f);

        public static readonly Ladder[] Ladders =
        {
            new Ladder("Port ladder", new Vector2(-4.6f, 3f), false, new Vector2(-6.6f, 3f)),
            new Ladder("Starboard ladder", new Vector2(4.6f, 3f), false, new Vector2(6.6f, 3f)),
            new Ladder("Stern ladder", new Vector2(0f, -7.6f), false, new Vector2(0f, -9.6f)),
            new Ladder("Quay ladder", new Vector2(5f, 5f), true, new Vector2(-7.2f, 5f)),
        };

        /// <summary>Three in ten passengers never learned to swim; who they are changes from voyage to voyage.</summary>
        public static bool PassengerSwims(int passenger, int voyage)
        {
            unchecked
            {
                uint h = (uint)passenger * 2654435761u ^ (uint)voyage * 40503u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return h % 10 >= 3;
            }
        }

        public const float PassengerSwimSpeed = 1.4f, PassengerNonSwimmerWaitSeconds = 8f, PassengerSwimDepth = -.75f;

        /// <summary>The ship ladder nearest to a point in the water, as an index into <see cref="Ladders"/>.</summary>
        public static int NearestShipLadder(Vector2 point)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Ladders.Length; i++)
            {
                if (Ladders[i].OnQuay) continue;
                float distance = Vector2.Distance(point, Ladders[i].Base);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        /// <summary>How long a swimmer needs to reach the nearest ladder, with a moment to climb.</summary>
        public static float SwimBackSeconds(Vector2 from) =>
            Mathf.Clamp(Vector2.Distance(from, Ladders[NearestShipLadder(from)].Base) / PassengerSwimSpeed + 1f, 3f, 8f);

        private readonly float[] ladderReady = new float[MaxCrew];
        private readonly float[] overboardClock = new float[MaxCrew];

        public int CrewInWater
        {
            get { int count = 0; foreach (var crew in Crew) if (crew.Active && crew.Swimming) count++; return count; }
        }

        public static ILocomotionWorld WaterWorld { get; } = new WaterLocomotionWorld();

        /// <summary>A swimmer may be anywhere in the bay except inside the hull.</summary>
        public static bool WaterWalkable(Vector2 p) => Mathf.Abs(p.x) >= HullHalfExtents.x || Mathf.Abs(p.y) >= HullHalfExtents.y;

        private sealed class WaterLocomotionWorld : ILocomotionWorld
        {
            public Vector2 Min => WaterMin;
            public Vector2 Max => WaterMax;
            public bool Walkable(Vector2 position) => WaterWalkable(position);
        }

        /// <summary>Where the top of a ladder stands, in ship-local coordinates (the quay is offset by the harbor's position).</summary>
        public static Vector2 LadderTopShipLocal(int ladder) =>
            Ladders[ladder].OnQuay ? Ladders[ladder].Top + new Vector2(QuayOffsetX, 0) : Ladders[ladder].Top;

        /// <summary>The ladder the crew member is next to, from above (to dive) or from the water (to climb), or -1.</summary>
        public int LadderNear(int slot)
        {
            var crew = Crew[slot];
            int best = -1;
            float bestDistance = LadderReach;
            for (int i = 0; i < Ladders.Length; i++)
            {
                float distance;
                if (crew.Swimming) distance = Vector2.Distance(crew.Position, Ladders[i].Base);
                else if (Ladders[i].OnQuay != crew.OnShore) continue;
                else distance = Vector2.Distance(crew.Position, Ladders[i].Top);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        /// <summary>The ladder command: dive in from the top, or climb out from the bottom, whichever applies.</summary>
        public string LadderCommand(ulong clientId, int ladder)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || InLobby) return "Join the crew first.";
            if (ladder < 0 || ladder >= Ladders.Length) return "Unknown ladder.";
            ref var crew = ref Crew[slot];
            if (clock < ladderReady[slot]) return string.Empty;
            if (crew.AtHelm || crew.AtCannon || crew.AtCrane) return "Release your station first.";
            if (crew.JumpHeight > 0 || crew.VerticalSpeed > 0) return "Land before using the ladder.";
            if (crew.Swimming)
            {
                if (Vector2.Distance(crew.Position, Ladders[ladder].Base) > LadderReach) return "Swim closer to the ladder.";
                ClimbOut(slot, ladder);
                return "Up you go!";
            }
            if (Ladders[ladder].OnQuay != crew.OnShore) return "That ladder is on the other side.";
            if (Vector2.Distance(crew.Position, Ladders[ladder].Top) > LadderReach) return "Move closer to the ladder.";
            if (!HarborOpen) return "The ship is under way. Nobody swims now.";
            if (crew.Carrying != ServiceItem.None || CarriedPassenger(slot) >= 0) return "Put your item or the passenger down before climbing down.";
            DiveIn(slot, ladder);
            return "Splash! WASD to swim, E at a ladder to climb out.";
        }

        private void DiveIn(int slot, int ladder)
        {
            ref var crew = ref Crew[slot];
            crew.Swimming = true;
            crew.OnShore = false;
            crew.Position = Ladders[ladder].Base;
            crew.Velocity = Vector2.zero;
            inputs[slot] = Vector2.zero;
            ladderReady[slot] = clock + LadderCooldownSeconds;
            overboardClock[slot] = 0;
        }

        private void ClimbOut(int slot, int ladder)
        {
            ref var crew = ref Crew[slot];
            crew.Swimming = false;
            crew.OnShore = Ladders[ladder].OnQuay;
            crew.Position = Ladders[ladder].Top;
            crew.Velocity = Vector2.zero;
            inputs[slot] = Vector2.zero;
            ladderReady[slot] = clock + LadderCooldownSeconds;
            overboardClock[slot] = 0;
        }

        /// <summary>The ship cannot leave while someone is still in the sea: they are not aboard.</summary>
        private bool SwimmersBlockDeparture => CrewInWater > 0;

        /// <summary>A crew member knocked into the water ends up outside the hull on the side they were pushed toward.</summary>
        private void WashOverboard(int slot, Vector2 direction)
        {
            ref var crew = ref Crew[slot];
            ReleaseCrewResponsibilities(slot);
            Vector2 outward = direction.sqrMagnitude > .01f ? direction.normalized : Vector2.left;
            Vector2 landing = crew.Position + outward * 4f;
            for (int attempt = 0; attempt < 8 && !WaterWalkable(landing); attempt++) landing += outward * 1f;
            crew.Swimming = true;
            crew.OnShore = false;
            crew.Position = new Vector2(Mathf.Clamp(landing.x, WaterMin.x, WaterMax.x), Mathf.Clamp(landing.y, WaterMin.y, WaterMax.y));
            if (!WaterWalkable(crew.Position)) crew.Position = Ladders[0].Base;
            crew.Velocity = Vector2.zero;
            crew.JumpHeight = crew.VerticalSpeed = 0;
            overboardClock[slot] = OverboardLimitSeconds;
        }

        /// <summary>Rail margin (metres) inside which a wave that is not faced can sweep a crew member off the lee side.</summary>
        public const float WaveSweepMargin = 2.2f;

        /// <summary>A wave that hits the ship broadside throws whoever stands unsecured at the lee rail into the sea.</summary>
        private void WashCrewOverboard()
        {
            if (State.Phase != VoyagePhase.Sailing) return;
            float radians = Hazards.WaveBearing * Mathf.Deg2Rad;
            // The wave comes FROM its bearing, so it travels the other way; compute that direction in ship-local axes.
            Vector3 travelWorld = new Vector3(-Mathf.Sin(radians), 0, -Mathf.Cos(radians));
            Vector3 travel3 = Quaternion.Euler(0, -State.Heading, 0) * travelWorld;
            var travel = new Vector2(travel3.x, travel3.z);
            for (int i = 0; i < MaxCrew; i++)
            {
                ref var crew = ref Crew[i];
                if (!crew.Active || crew.OnShore || crew.Swimming || crew.AtHelm || crew.AtCannon || crew.AtCrane) continue;
                bool atLeeRail = travel.x > .5f && crew.Position.x > DeckBounds.x - WaveSweepMargin ||
                                 travel.x < -.5f && crew.Position.x < -DeckBounds.x + WaveSweepMargin ||
                                 travel.y > .5f && crew.Position.y > DeckBounds.y - WaveSweepMargin ||
                                 travel.y < -.5f && crew.Position.y < -DeckBounds.y + WaveSweepMargin;
                if (atLeeRail) WashOverboard(i, travel);
            }
        }

        /// <summary>Someone who cannot find a ladder in time is hauled aboard on a line, a little embarrassed and a little poorer.</summary>
        private void TickSwimmers(float dt)
        {
            for (int i = 0; i < MaxCrew; i++)
            {
                if (!Crew[i].Active || !Crew[i].Swimming || overboardClock[i] <= 0) continue;
                overboardClock[i] -= dt;
                if (overboardClock[i] > 0) continue;
                overboardClock[i] = 0;
                Crew[i].Swimming = false;
                Crew[i].OnShore = false;
                Crew[i].Position = new Vector2(-1.5f + i, -6);
                Crew[i].Velocity = Vector2.zero;
                State.Wallet = Mathf.Max(0, State.Wallet - HaulAboardFine);
            }
        }

        public const int HaulAboardFine = 5;

        /// <summary>Seconds a washed-overboard crew member has left before being hauled aboard; 0 for someone swimming for fun.</summary>
        public float OverboardRemaining(int slot) => slot >= 0 && slot < MaxCrew ? overboardClock[slot] : 0;
    }
}
