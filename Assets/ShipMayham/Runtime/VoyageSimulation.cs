using System;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>Host-owned game rules. No scene lookups, RPCs or visual dependencies.</summary>
    public sealed partial class VoyageSimulation
    {
        public const int MaxCrew = 4, MaxPassengers = 8;
        public const float InteractionRange = 2.1f, DockRadius = 12f;
        public const float JumpSpeed = 6f, JumpGravity = 18f;
        public static readonly Vector2 Destination = new Vector2(0, 220);
        public static readonly Vector2 DeckBounds = new Vector2(5.2f, 8.2f);
        public VoyageState State;
        public readonly CrewState[] Crew = new CrewState[MaxCrew];
        public readonly PassengerState[] Passengers = new PassengerState[MaxPassengers];
        public readonly StationDefinition[] Stations;
        public readonly Vector2[] Seats;
        private readonly float[] interactionReady = new float[MaxCrew];
        private readonly float[] inputExpires = new float[MaxCrew];
        private readonly Vector2[] inputs = new Vector2[MaxCrew];
        private readonly string[] crewPlayerIds = new string[MaxCrew];
        private float clock;
        private float nextLeak = 25;

        public VoyageSimulation(StationDefinition[] stations, Vector2[] seats, bool harborEnabled = false)
        {
            if (seats.Length != MaxPassengers) throw new ArgumentException("Exactly eight seat anchors required.");
            Stations = stations;
            Seats = seats;
            HarborEnabled = harborEnabled;
            State = new VoyageState { Health = 100, Voyage = 1, FoodStock = 16, DrinkStock = 16 };
            ResetEncounter();
        }

        public int FindCrew(ulong clientId)
        {
            for (int i = 0; i < MaxCrew; i++) if (Crew[i].Active && Crew[i].ClientId == clientId) return i;
            return -1;
        }

        public int Join(ulong clientId, string playerId = null)
        {
            if (!string.IsNullOrEmpty(playerId))
            {
                for (int i = 0; i < MaxCrew; i++)
                {
                    if (crewPlayerIds[i] != playerId) continue;
                    Crew[i].Active = true;
                    Crew[i].ClientId = clientId;
                    inputs[i] = Vector2.zero;
                    inputExpires[i] = 0;
                    return i;
                }
            }
            int existing = FindCrew(clientId);
            if (existing >= 0) return existing;
            for (int i = 0; i < MaxCrew; i++)
            {
                if (Crew[i].Active) continue;
                Crew[i] = new CrewState { Active = true, ClientId = clientId, Position = new Vector2(-1.5f + i, -6) };
                crewPlayerIds[i] = playerId;
                inputs[i] = Vector2.zero;
                interactionReady[i] = 0;
                return i;
            }
            return -1;
        }

        public void Leave(ulong clientId)
        {
            int i = FindCrew(clientId);
            if (i < 0) return;
            LeaveSlot(i);
        }

        public void LeavePlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            for (int i = 0; i < MaxCrew; i++)
            {
                if (crewPlayerIds[i] != playerId) continue;
                LeaveSlot(i);
                return;
            }
        }

        private void LeaveSlot(int i)
        {
            if (Crew[i].AtHelm) State.Speed = 0;
            if (Crew[i].AtCannon) ReleaseCannon(i);
            int carriedPassenger = CarriedPassenger(i);
            if (carriedPassenger >= 0) ReturnPassengerToSeat(carriedPassenger);
            ReturnItem(i);
            Crew[i] = default;
            crewPlayerIds[i] = null;
            inputs[i] = Vector2.zero;
        }

        public void SetInput(ulong clientId, Vector2 input)
        {
            int i = FindCrew(clientId);
            if (i < 0 || !Finite(input.x) || !Finite(input.y)) return;
            inputs[i] = Vector2.ClampMagnitude(input, 1);
            inputExpires[i] = clock + .35f;
        }

        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

        public void Jump(ulong clientId)
        {
            int i = FindCrew(clientId);
            if (i < 0 || Crew[i].AtHelm || Crew[i].AtCannon || Crew[i].JumpHeight > 0 || Crew[i].VerticalSpeed > 0) return;
            Crew[i].VerticalSpeed = JumpSpeed;
        }

        private void TickJump(int i, float dt)
        {
            if (Crew[i].JumpHeight <= 0 && Crew[i].VerticalSpeed == 0) return;
            Crew[i].JumpHeight += Crew[i].VerticalSpeed * dt - .5f * JumpGravity * dt * dt;
            Crew[i].VerticalSpeed -= JumpGravity * dt;
            if (Crew[i].JumpHeight <= 0)
            {
                Crew[i].JumpHeight = 0;
                Crew[i].VerticalSpeed = 0;
            }
        }

        public void Tick(float dt)
        {
            if (!Finite(dt) || dt <= 0) return;
            dt = Mathf.Min(dt, .1f);
            clock += dt;
            for (int i = 0; i < MaxCrew; i++)
            {
                if (!Crew[i].Active) continue;
                TickJump(i, dt);
                Vector2 input = clock <= inputExpires[i] ? inputs[i] : Vector2.zero;
                if (Crew[i].AtHelm)
                {
                    if (State.Phase != VoyagePhase.Sailing) continue;
                    State.Speed = Mathf.Clamp(State.Speed + input.y * dt * 3, 0, 4 + State.EngineLevel * 1.5f);
                    State.Heading = Mathf.Repeat(State.Heading + input.x * dt * 40, 360);
                }
                else if (Crew[i].OnShore) MoveShoreCrew(i, input * (4.2f * dt));
                else if (!Crew[i].AtCannon) MoveCrew(i, input * (4.2f * dt * (CarriedPassenger(i) >= 0 ? .7f : 1)));
            }
            TickPassengers(dt);
            if (State.Phase != VoyagePhase.Sailing) return;
            State.Elapsed += dt;
            float radians = State.Heading * Mathf.Deg2Rad;
            State.Position += new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * (State.Speed * dt);
            // Prototype sailing area: visible map bounds, no possibility of sailing away forever.
            State.Position.x = Mathf.Clamp(State.Position.x, -90, 90);
            State.Position.y = Mathf.Clamp(State.Position.y, -20, 250);
            TickEncounter(dt);
            if (State.Elapsed >= nextLeak && State.Leak == 0)
            {
                State.Leak = 1;
                State.RepairSteps = 0;
            }
            if (State.Leak != 0) DamageHull(dt * .65f);
            if (State.Health <= 0) { Settle(false); return; }
            for (int i = 0; i < MaxPassengers; i++)
            {
                if (!Passengers[i].Active) continue;
                if (Passengers[i].Motion == PassengerMotion.Rescuing) continue;
                Passengers[i].RequestTimer += dt;
                if (Passengers[i].Request == ServiceItem.None && Passengers[i].RequestTimer >= 24)
                {
                    Passengers[i].Request = (i + State.Served) % 2 == 0 ? ServiceItem.Food : ServiceItem.Drink;
                    Passengers[i].RequestTimer = 0;
                }
                if (Passengers[i].Request != ServiceItem.None && Passengers[i].RequestTimer > 18)
                    Passengers[i].Satisfaction = Mathf.Max(0, Passengers[i].Satisfaction - dt * (State.ComfortLevel > 0 ? .22f : .45f));
            }
        }

        private void MoveCrew(int i, Vector2 movement)
        {
            Vector2 position = Crew[i].Position;
            // Separate axes allow sliding along counters. Gameplay footprint is independent of Visuals.
            Vector2 candidate = new Vector2(Mathf.Clamp(position.x + movement.x, -DeckBounds.x, DeckBounds.x), position.y);
            if (Walkable(candidate)) position = candidate;
            candidate = new Vector2(position.x, Mathf.Clamp(position.y + movement.y, -DeckBounds.y, DeckBounds.y));
            if (Walkable(candidate)) position = candidate;
            Crew[i].Position = position;
        }

        private bool Walkable(Vector2 p, int ignorePassenger = -1)
        {
            foreach (var station in Stations) if ((p - station.Position).sqrMagnitude < .75f * .75f) return false;
            for (int i = 0; i < Seats.Length; i++) if ((p - Seats[i]).sqrMagnitude < .7f * .7f) return false;
            for (int i = 0; i < MaxPassengers; i++)
                if (i != ignorePassenger && Passengers[i].Active && Passengers[i].Motion == PassengerMotion.OnDeck &&
                    (p - new Vector2(Passengers[i].Position.x, Passengers[i].Position.z)).sqrMagnitude < .6f * .6f) return false;
            return true;
        }

        public string Interact(ulong clientId, int target)
        {
            int i = FindCrew(clientId);
            if (i < 0) return "Crew not registered.";
            if (Crew[i].OnShore) return "Board the ship before using ship stations.";
            if (clock < interactionReady[i]) return string.Empty;
            interactionReady[i] = clock + .35f;
            if (Crew[i].AtHelm) { Crew[i].AtHelm = false; return "Helm released. The ship keeps its speed; S brakes."; }
            if (Crew[i].AtCannon) { ReleaseCannon(i); return "Cannon released."; }
            if (Crew[i].JumpHeight > 0 || Crew[i].VerticalSpeed > 0) return "Land before interacting.";
            if (CarriedPassenger(i) >= 0) return "Put the passenger down before using stations or serving.";
            if (target < 0 || target >= Stations.Length + MaxPassengers) return "No interaction target.";
            Vector3 passengerPoint = target >= Stations.Length ? Passengers[target - Stations.Length].Position : Vector3.zero;
            Vector2 point = target < Stations.Length ? Stations[target].Position : new Vector2(passengerPoint.x, passengerPoint.z);
            if (Vector2.Distance(Crew[i].Position, point) > InteractionRange) return "Move closer.";
            if (target >= Stations.Length) return Serve(i, target - Stations.Length);
            var kind = Stations[target].Kind;
            switch (kind)
            {
                case StationKind.Manifest:
                    if (State.Phase == VoyagePhase.Arrived || State.Phase == VoyagePhase.Failed)
                    {
                        if (CrewAshore > 0) return "Wait for all crew to board before preparing a new voyage.";
                        ResetVoyage(); return "Back at the departure port. Board new passengers.";
                    }
                    if (State.Phase != VoyagePhase.Boarding) return "Manifest is available at port.";
                    if (State.Boarded > 0) return "Passengers already boarded. Take the helm and press Space.";
                    State.Boarded = 4 + State.CapacityLevel * 2;
                    for (int p = 0; p < State.Boarded; p++) Passengers[p] = new PassengerState
                    { Active = true, Satisfaction = 100, Request = p % 2 == 0 ? ServiceItem.Food : ServiceItem.Drink,
                        FarePaid = p != (State.Voyage - 1) % State.Boarded, CarrierSlot = -1,
                        Position = new Vector3(Seats[p].x, .5f, Seats[p].y) };
                    return "Passengers aboard. Take the helm; Space departs.";
                case StationKind.Helm:
                    foreach (var crew in Crew) if (crew.Active && crew.AtHelm) return "Helm occupied.";
                    Crew[i].AtHelm = true;
                    return "Helm: W/S throttle & brake, A/D turn, Space depart/dock, E release.";
                case StationKind.Food: return TakeItem(i, ServiceItem.Food);
                case StationKind.Drink: return TakeItem(i, ServiceItem.Drink);
                case StationKind.Repair:
                    if (State.Leak == 0) return "Hull is watertight.";
                    State.RepairSteps++;
                    if (State.RepairSteps < 4) return $"Patching leak: {State.RepairSteps}/4";
                    State.Leak = 0;
                    nextLeak = State.Elapsed + 45;
                    return "Leak patched.";
                case StationKind.Cannon: return TakeCannon(i);
                case StationKind.Power:
                    if (!Encounter.PowerOut) return "Power is online.";
                    Encounter.PowerRepairSteps++;
                    if (Encounter.PowerRepairSteps < 3) return $"Resetting breakers: {Encounter.PowerRepairSteps}/3";
                    Encounter.PowerOut = false;
                    return "Power restored. Service counters are online.";
                case StationKind.EngineUpgrade:
                case StationKind.CapacityUpgrade:
                case StationKind.ComfortUpgrade: return Buy(kind);
                default: return "Unknown station.";
            }
        }

        public string HelmAction(ulong clientId)
        {
            int i = FindCrew(clientId);
            if (i < 0 || !Crew[i].AtHelm) return "Use the helm first.";
            if (State.Phase == VoyagePhase.Boarding)
            {
                if (State.Boarded == 0) return "Board passengers at the manifest first.";
                if (CrewAshore > 0) return "Cannot depart: crew are still ashore. Ask them to board at the gangway.";
                State.Phase = VoyagePhase.Sailing;
                return "Under way! W accelerates. Serve passengers and watch the hull.";
            }
            if (State.Phase != VoyagePhase.Sailing) return "Use the manifest to prepare another voyage.";
            if (Vector2.Distance(State.Position, Destination) > DockRadius) return "Get within 12 m of the destination marker.";
            if (State.Speed > 1) return "Too fast to dock. Hold S to brake below 1 m/s.";
            Settle(true);
            return "Docked! Earnings paid to the shared wallet.";
        }

        private string TakeItem(int crew, ServiceItem item)
        {
            if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing) return "Service is closed.";
            if (Crew[crew].Carrying != ServiceItem.None)
            {
                ReturnItem(crew);
                return "Carried item returned to stock.";
            }
            if (Encounter.PowerOut) return "No power! Reset 3 breakers at the POWER panel. Held orders can still be served.";
            if (item == ServiceItem.Food && State.FoodStock <= 0 || item == ServiceItem.Drink && State.DrinkStock <= 0) return "Out of stock this voyage.";
            if (item == ServiceItem.Food) State.FoodStock--; else State.DrinkStock--;
            Crew[crew].Carrying = item;
            return $"Carrying {item}. E beside a passenger with the matching request.";
        }

        private void ReturnItem(int crew)
        {
            if (Crew[crew].Carrying == ServiceItem.Food) State.FoodStock++;
            if (Crew[crew].Carrying == ServiceItem.Drink) State.DrinkStock++;
            Crew[crew].Carrying = ServiceItem.None;
        }

        private string Serve(int crew, int passenger)
        {
            if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing) return "Service is closed.";
            ref PassengerState p = ref Passengers[passenger];
            if (!p.Active || p.Request == ServiceItem.None) return "No order here.";
            if (p.Motion != PassengerMotion.OnDeck) return "Wait until the passenger is back on deck.";
            if (Crew[crew].Carrying != p.Request) return $"Passenger wants {p.Request}.";
            p.Request = ServiceItem.None;
            p.RequestTimer = 0;
            p.Satisfaction = Mathf.Min(100, p.Satisfaction + 15);
            Crew[crew].Carrying = ServiceItem.None;
            State.Served++;
            React(passenger, PassengerReaction.Thanks);
            return "Order delivered! +8 service income, paid on arrival.";
        }

        public static int UpgradeCost(StationKind kind, VoyageState state) => kind == StationKind.EngineUpgrade
            ? 100 * (state.EngineLevel + 1) : kind == StationKind.CapacityUpgrade ? 150 * (state.CapacityLevel + 1) : 120;

        private string Buy(StationKind kind)
        {
            if (State.Phase == VoyagePhase.Sailing) return "Upgrades are available at port.";
            int level = kind == StationKind.EngineUpgrade ? State.EngineLevel : kind == StationKind.CapacityUpgrade ? State.CapacityLevel : State.ComfortLevel;
            int max = kind == StationKind.ComfortUpgrade ? 1 : 2;
            if (level >= max) return "Upgrade maxed out.";
            int cost = UpgradeCost(kind, State);
            if (State.Wallet < cost) return $"Need {cost} coins.";
            State.Wallet -= cost;
            if (kind == StationKind.EngineUpgrade) State.EngineLevel++;
            else if (kind == StationKind.CapacityUpgrade) State.CapacityLevel++;
            else State.ComfortLevel++;
            return "Upgrade purchased. Capacity changes apply to the next boarding.";
        }

        private void Settle(bool success)
        {
            if (State.Phase != VoyagePhase.Sailing) return;
            State.Phase = success ? VoyagePhase.Arrived : VoyagePhase.Failed;
            State.Speed = 0;
            // A fixed berth aligns the gangway with the destination pier.
            if (success && HarborEnabled) { State.Position = Destination; State.Heading = 0; }
            State.LastTickets = 0;
            State.LastUnpaid = 0;
            State.LastMissing = 0;
            foreach (var passenger in Passengers)
            {
                if (!passenger.Active) continue;
                if (passenger.Motion == PassengerMotion.Rescuing) { State.LastMissing++; continue; }
                if (!passenger.FarePaid) State.LastUnpaid++;
                else if (success) State.LastTickets += 40;
            }
            State.LastServices = success ? State.Served * 8 : 0;
            float satisfaction = 0;
            foreach (var p in Passengers) if (p.Active && p.Motion != PassengerMotion.Rescuing) satisfaction += p.Satisfaction;
            State.LastTips = success ? Mathf.RoundToInt(satisfaction * .1f) : 0;
            State.LastRescueCosts = State.RescueCount * 15;
            State.LastCosts = State.Served * 2 + Mathf.CeilToInt((100 - State.Health) * .5f) + State.LastRescueCosts;
            State.LastBounty = success ? Encounter.Bounty : 0;
            State.LastNet = State.LastTickets + State.LastServices + State.LastTips + State.LastBounty - State.LastCosts;
            State.Wallet = Mathf.Max(0, State.Wallet + State.LastNet);
            Encounter.ReloadRemaining = 0;
            if (Encounter.Pirate == PiratePhase.Warning || Encounter.Pirate == PiratePhase.Attacking) Encounter.Pirate = PiratePhase.Escaped;
            Encounter.PowerOut = false;
            for (int p = 0; p < MaxPassengers; p++)
                if (Passengers[p].Active && Passengers[p].Motion != PassengerMotion.Rescuing) ReturnPassengerToSeat(p);
            for (int i = 0; i < MaxCrew; i++) { Crew[i].AtHelm = false; Crew[i].AtCannon = false; ReturnItem(i); }
        }

        private void ResetVoyage()
        {
            var old = State;
            State = new VoyageState { Health = 100, Voyage = old.Voyage + 1, Wallet = old.Wallet,
                EngineLevel = old.EngineLevel, CapacityLevel = old.CapacityLevel, ComfortLevel = old.ComfortLevel,
                FoodStock = Mathf.Max(16, old.FoodStock), DrinkStock = Mathf.Max(16, old.DrinkStock), HullShield = old.HullShield };
            Array.Clear(Passengers, 0, Passengers.Length);
            nextLeak = 25;
            ResetEncounter();
            for (int i = 0; i < MaxCrew; i++)
            {
                Crew[i].AtHelm = false;
                Crew[i].AtCannon = false;
                Crew[i].OnShore = false;
                Crew[i].Carrying = ServiceItem.None;
                Crew[i].Position = new Vector2(-1.5f + i, -6);
                Crew[i].JumpHeight = 0;
                Crew[i].VerticalSpeed = 0;
                inputs[i] = Vector2.zero;
            }
        }
    }
}
