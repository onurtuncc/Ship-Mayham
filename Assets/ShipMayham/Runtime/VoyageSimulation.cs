using System;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>Host-owned game rules. No scene lookups, RPCs or visual dependencies.</summary>
    public sealed partial class VoyageSimulation
    {
        public const int MaxCrew = 4, MaxPassengers = 8;
        public const float InteractionRange = 2.1f, DockRadius = 12f;
        public const float JumpSpeed = CrewLocomotion.JumpSpeed, JumpGravity = CrewLocomotion.JumpGravity;
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

        public VoyageSimulation(StationDefinition[] stations, Vector2[] seats, bool harborEnabled = false, uint eventSeed = 1)
        {
            if (seats.Length != MaxPassengers) throw new ArgumentException("Exactly eight seat anchors required.");
            Stations = stations;
            Seats = seats;
            HarborEnabled = harborEnabled;
            State = new VoyageState { Health = 100, Voyage = 1, FoodStock = 16, DrinkStock = 16, RepairKitStock = VoyageRepairKits };
            eventRandomState = eventSeed == 0 ? 1u : eventSeed;
            hazardSessionSeed = eventRandomState;
            ResetHazards();
            ResetWorkload();
            ResetEncounter();
            ResetEvents();
            ResetDeck();
            ResetEmergencies();
            ResetOpportunities();
            ResetJourney();
        }

        public int FindCrew(ulong clientId)
        {
            if (clientId == ulong.MaxValue) return -1;
            for (int i = 0; i < MaxCrew; i++) if (Crew[i].Active && Crew[i].ClientId == clientId) return i;
            return -1;
        }

        public int FindPlayer(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return -1;
            for (int i = 0; i < MaxCrew; i++) if (crewPlayerIds[i] == playerId) return i;
            return -1;
        }

        public int Join(ulong clientId, string playerId = null)
        {
            if (clientId == ulong.MaxValue) return -1;
            if (!string.IsNullOrEmpty(playerId))
            {
                for (int i = 0; i < MaxCrew; i++)
                {
                    if (crewPlayerIds[i] != playerId) continue;
                    Crew[i].Active = true;
                    Crew[i].ClientId = clientId;
                    inputs[i] = Vector2.zero;
                    inputExpires[i] = 0;
                    ResetCommandStream(i);
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
                ResetCommandStream(i);
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
            ReleaseCrewResponsibilities(i);
            Crew[i] = default;
            crewPlayerIds[i] = null;
            ClearLobbySlot(i);
            ClearTally(i);
            ResetCommandStream(i);
        }

        public void SuspendConnection(ulong clientId)
        {
            int i = FindCrew(clientId);
            if (i < 0) return;
            // Nothing is worth keeping for a player who leaves before the voyage starts.
            if (InLobby) { LeaveSlot(i); return; }
            ReleaseCrewResponsibilities(i);
            Crew[i].ClientId = ulong.MaxValue;
            Crew[i].JumpHeight = Crew[i].VerticalSpeed = 0;
            if (Crew[i].OnShore)
            {
                Crew[i].OnShore = false;
                Crew[i].Position = ShipGangway;
            }
        }

        private void ReleaseCrewResponsibilities(int i)
        {
            if (Crew[i].AtHelm) ShipMotionState = default;
            Crew[i].AtHelm = false;
            Crew[i].AtCrane = false;
            if (Crew[i].AtCannon) ReleaseCannon(i);
            int carriedPassenger = CarriedPassenger(i);
            if (carriedPassenger >= 0) ReturnPassengerToSeat(carriedPassenger);
            ReturnItem(i);
            ClearMiniGame(i);
            inputs[i] = Vector2.zero;
            inputExpires[i] = 0;
            interactionReady[i] = 0;
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
            if (i < 0 || Crew[i].Swimming || Crew[i].AtHelm || Crew[i].AtCannon || Crew[i].AtCrane || Crew[i].JumpHeight > 0 || Crew[i].VerticalSpeed > 0) return;
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
            if (InLobby) return;
            GrantWorkshopKits();
            Vector2 helmInput = Vector2.zero;
            bool helmManned = false;
            for (int i = 0; i < MaxCrew; i++)
            {
                if (!Crew[i].Active) { Crew[i].Velocity = Vector2.zero; continue; }
                moveBudget[i] = Mathf.Min(MoveBudgetCap, moveBudget[i] + dt * MoveBudgetRate);
                Vector2 input = clock <= inputExpires[i] ? inputs[i] : Vector2.zero;
                AdvanceCrew(i, input, dt, ref helmInput, ref helmManned);
                if (Crew[i].AtHelm && State.Phase == VoyagePhase.Sailing) TickHelmTime(i, dt);
            }
            if (State.Phase == VoyagePhase.Sailing)
            {
                var motion = ShipMotionState;
                float headingDelta = ShipSteering.Step(ref motion, helmInput, helmManned, MaxShipSpeed, dt);
                ShipMotionState = motion;
                State.Heading = Mathf.Repeat(State.Heading + headingDelta, 360);
            }
            else ShipMotionState = default;
            TickPassengers(dt);
            TickSwimmers(dt);
            TickActivities(dt);
            if (State.DockJobActive && State.DockJobType == DockJobKind.CraneLoading && HarborOpen) State.CraneSwayTime += dt;
            if (State.Phase != VoyagePhase.Sailing) return;
            State.Elapsed += dt;
            Vector2 previousShip = State.Position, previousPirate = Encounter.PiratePosition;
            State.Position += ShipSteering.Velocity(ShipMotionState, State.Heading) * dt;
            // Prototype sailing area: visible map bounds, no possibility of sailing away forever.
            State.Position.x = Mathf.Clamp(State.Position.x, -90, 90);
            State.Position.y = Mathf.Clamp(State.Position.y, -20, 250);
            if (!workloadReady) BeginWorkload();
            TickHazards(dt);
            TickEncounter(dt);
            TickCannonballs(dt, previousShip, previousPirate);
            TickEvents(dt);
            TickDeck(dt);
            TickEmergencies(dt);
            TickOpportunities(dt);
            TickJourney();
            if (State.Elapsed >= nextLeak && State.Leak == 0)
            {
                State.Leak = 1;
                State.RepairSteps = 0;
            }
            if (State.Leak != 0 && Opportunity.PlugRemaining <= 0) DamageHull(dt * .65f);
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
                    Passengers[i].Satisfaction = Mathf.Max(0, Passengers[i].Satisfaction - dt * (State.ComfortLevel > 0 ? .22f : .45f) * PatienceFactor(Passengers[i].Personality));
            }
        }

        /// <summary>Top speed ahead with the current engine.</summary>
        public float MaxShipSpeed => 4 + State.EngineLevel * 1.5f;

        /// <summary>The helm and hull motion, stored in the replicated state so clients, migration and saves all carry it.</summary>
        public ShipMotion ShipMotionState
        {
            get => new ShipMotion { Throttle = State.Throttle, Rudder = State.Rudder, Speed = State.Speed, Drift = State.Drift, YawRate = State.YawRate };
            set { State.Throttle = value.Throttle; State.Rudder = value.Rudder; State.Speed = value.Speed; State.Drift = value.Drift; State.YawRate = value.YawRate; }
        }

        private bool Walkable(Vector2 p, int ignorePassenger = -1)
        {
            foreach (var station in Stations) if ((p - station.Position).sqrMagnitude < StationClearance * StationClearance) return false;
            for (int i = 0; i < Mathf.Min(Seats.Length, OpenSeats); i++) if ((p - Seats[i]).sqrMagnitude < SeatClearance * SeatClearance) return false;
            for (int i = 0; i < MaxPassengers; i++)
                if (i != ignorePassenger && Passengers[i].Active && Passengers[i].Motion == PassengerMotion.OnDeck &&
                    (p - new Vector2(Passengers[i].Position.x, Passengers[i].Position.z)).sqrMagnitude < PassengerClearance * PassengerClearance) return false;
            return true;
        }

        public string Interact(ulong clientId, int target)
        {
            int i = FindCrew(clientId);
            if (i < 0) return "Crew not registered.";
            if (Crew[i].Swimming) return "You are in the water. Swim to a ladder and climb out first.";
            if (Crew[i].OnShore) return "Board the ship before using ship stations.";
            if (clock < interactionReady[i]) return string.Empty;
            interactionReady[i] = clock + .35f;
            if (Crew[i].AtHelm) { Crew[i].AtHelm = false; return "Helm released. The engine eases to STOP and the ship carries its way."; }
            if (Crew[i].AtCannon) { ReleaseCannon(i); return "Cannon released."; }
            if (Crew[i].JumpHeight > 0 || Crew[i].VerticalSpeed > 0) return "Land before interacting.";
            if (CarriedPassenger(i) >= 0) return "Put the passenger down before using stations or serving.";
            if (target < 0 || target >= Stations.Length + MaxPassengers) return "No interaction target.";
            Vector3 passengerPoint = target >= Stations.Length ? Passengers[target - Stations.Length].Position : Vector3.zero;
            Vector2 point = target < Stations.Length ? Stations[target].Position : new Vector2(passengerPoint.x, passengerPoint.z);
            if (Vector2.Distance(Crew[i].Position, point) > InteractionRange) return "Move closer.";
            if (target >= Stations.Length) return Serve(i, target - Stations.Length);
            var kind = Stations[target].Kind;
            if (Crew[i].Carrying == ServiceItem.Ammo && kind != StationKind.Ammo && kind != StationKind.Cannon)
                return "Deliver the cannonball at CANNON or return it to AMMO first.";
            if (Crew[i].Carrying == ServiceItem.RepairKit && kind != StationKind.RepairSupply && kind != StationKind.Repair)
                return "Deliver the kit at REPAIR or return it to KITS first.";
            switch (kind)
            {
                case StationKind.Manifest:
                    if (State.Phase == VoyagePhase.Arrived || State.Phase == VoyagePhase.Failed)
                    {
                        if (CrewAshore > 0) return "Wait for all crew to board before preparing a new voyage.";
                        if (State.DockJobActive) return "Finish or cancel the cargo relay before preparing a new voyage.";
                        if (AnyPassengerLeaving) return "Wait until the passengers have left the ship.";
                        ResetVoyage(); return "Back at the departure port. Board new passengers.";
                    }
                    if (State.Phase != VoyagePhase.Boarding) return "Manifest is available at port.";
                    if (State.DockJobActive) return "Finish or cancel the cargo relay before boarding passengers.";
                    if (State.Boarded > 0) return "Passengers already boarded. Take the helm and press Space.";
                    State.Boarded = 4 + State.CapacityLevel * 2;
                    for (int p = 0; p < State.Boarded; p++) Passengers[p] = new PassengerState
                    { Active = true, Satisfaction = 100, Request = p % 2 == 0 ? ServiceItem.Food : ServiceItem.Drink,
                        FarePaid = p != (State.Voyage - 1) % State.Boarded, CarrierSlot = -1,
                        Personality = (PassengerPersonality)(p % 3), RoamIn = 15 + p * 4,
                        Position = BoardingStart(p), Motion = BoardingMotion, RecoveryRemaining = FlowTimeoutSeconds,
                        CanSwim = PassengerSwims(p, State.Voyage) };
                    return PassengersWalk ? "Passengers are coming aboard. Take the helm once everyone is seated." : "Passengers aboard. Take the helm; Space departs.";
                case StationKind.Helm:
                    foreach (var crew in Crew) if (crew.Active && crew.AtHelm) return "Helm occupied.";
                    Crew[i].AtHelm = true;
                    return "Helm: W/S engine lever (stays set), A/D wheel, Space depart/dock, E release.";
                case StationKind.Food: return TakeItem(i, ServiceItem.Food);
                case StationKind.Drink: return TakeItem(i, ServiceItem.Drink);
                case StationKind.Repair:
                    if (!RepairSuppliesOpen) return "Repair bench closed. Prepare a new voyage.";
                    if (Crew[i].Carrying == ServiceItem.TemporaryPlug)
                    {
                        if (State.Leak == 0 || Opportunity.PlugRemaining > 0) return "A temporary plug needs an unblocked active leak.";
                        Crew[i].Carrying = ServiceItem.None; Opportunity.PlugsUsed++; Opportunity.PlugRemaining = 20;
                        return "Leak held for 20 seconds. Use the time to diagnose valves or apply a permanent patch.";
                    }
                    if (Crew[i].Carrying == ServiceItem.RepairKit) return DeliverRepairKit(i);
                    if (State.Leak == 0)
                    {
                        string cargoResult = ClearLooseCargo(i);
                        if (!string.IsNullOrEmpty(cargoResult)) return cargoResult;
                    }
                    if (State.Leak == 0) return "Hull is watertight.";
                    if (Emergency.ValveAdjusted && Crew[i].Carrying == ServiceItem.None) return ApplyLeakValve();
                    if (State.ReadyRepairKits > 0)
                    {
                        if (Crew[i].Carrying != ServiceItem.None) return "Free your hands before applying a quick patch.";
                        State.ReadyRepairKits--;
                        State.RepairKitsUsed++;
                        Tally(i, TallyKind.Repairs);
                        FinishLeakRepair();
                        State.Health = Mathf.Min(100, State.Health + QuickPatchHullRecovery);
                        return "Quick patch applied! One kit used; leak sealed and up to 8 hull restored.";
                    }
                    State.RepairSteps++;
                    Tally(i, TallyKind.Repairs);
                    if (State.RepairSteps < 4) return $"Patching leak: {State.RepairSteps}/4";
                    FinishLeakRepair();
                    return "Leak patched.";
                case StationKind.RepairSupply: return TakeOrReturnRepairKit(i);
                case StationKind.Cannon: return Crew[i].Carrying == ServiceItem.Ammo ? DeliverAmmo(i) : TakeCannon(i);
                case StationKind.Ammo: return TakeOrReturnAmmo(i);
                case StationKind.Power:
                    if (Emergency.Fire == FirePhase.Burning) return "Fire circuit unsafe. R isolates it; get an extinguisher at KITS.";
                    if (!Encounter.PowerOut) return "Power is online.";
                    Encounter.PowerRepairSteps++;
                    Tally(i, TallyKind.Repairs);
                    PowerRepairedForEvent();
                    if (Encounter.PowerRepairSteps < 3) return $"Resetting breakers: {Encounter.PowerRepairSteps}/3";
                    Encounter.PowerOut = false;
                    return "Power restored. Service counters are online.";
                case StationKind.EngineUpgrade:
                case StationKind.CapacityUpgrade:
                case StationKind.ComfortUpgrade:
                case StationKind.GunneryUpgrade:
                case StationKind.BatteryUpgrade: return Buy(kind);
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
                if (AnyPassengerBoarding) return "Passengers are still boarding. Wait until everyone is seated.";
                if (CrewAshore > 0) return "Cannot depart: crew are still ashore. Ask them to board at the gangway.";
                if (CrewOnGangway) return "Cannot depart: someone is still on the gangway.";
                if (SwimmersBlockDeparture) return "Cannot depart: someone is still swimming. Wait for them to climb a ladder.";
                if (State.DockJobActive) return "Finish or cancel the cargo relay before departure.";
                State.Phase = VoyagePhase.Sailing;
                return "Under way! W sets the engine ahead; she gathers way slowly. Serve passengers and watch the hull.";
            }
            if (State.Phase != VoyagePhase.Sailing) return "Use the manifest to prepare another voyage.";
            if (Vector2.Distance(State.Position, Destination) > DockRadius) return "Get within 12 m of the destination marker.";
            if (Mathf.Abs(State.Speed) > 1) return "Too fast to dock. Pull the lever to ASTERN to take the way off below 1 m/s.";
            if (Journey.Course != PassageCourse.Direct && Journey.Checkpoints < 2) return "Visit both marked passage gates before docking.";
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
            int units = State.Equipment == ShipEquipment.ServiceTray ? Mathf.Min(2, item == ServiceItem.Food ? State.FoodStock : State.DrinkStock) : 1;
            if (item == ServiceItem.Food) State.FoodStock -= units; else State.DrinkStock -= units;
            Crew[crew].Carrying = item;
            Crew[crew].ServiceUnits = (byte)units;
            return $"Carrying {item}. E beside a passenger with the matching request.";
        }

        private void ReturnItem(int crew)
        {
            int units = Mathf.Max(1, Crew[crew].ServiceUnits);
            if (Crew[crew].Carrying == ServiceItem.Food) State.FoodStock += units;
            if (Crew[crew].Carrying == ServiceItem.Drink) State.DrinkStock += units;
            if (Crew[crew].Carrying == ServiceItem.Ammo) Encounter.MagazineAmmo++;
            if (Crew[crew].Carrying == ServiceItem.RepairKit) State.RepairKitStock++;
            if (Crew[crew].Carrying == ServiceItem.CraneCrate) State.CraneStock++;
            if (Crew[crew].Carrying == ServiceItem.TemporaryPlug) Opportunity.PlugsAvailable++;
            Crew[crew].Carrying = ServiceItem.None;
            Crew[crew].ServiceUnits = 0;
        }

        private string Serve(int crew, int passenger)
        {
            if (State.Phase != VoyagePhase.Boarding && State.Phase != VoyagePhase.Sailing) return "Service is closed.";
            ref PassengerState p = ref Passengers[passenger];
            if (!p.Active || p.Request == ServiceItem.None) return "No order here.";
            if (p.Motion != PassengerMotion.OnDeck) return "Wait until the passenger is back on deck.";
            if (Crew[crew].Carrying != p.Request) return $"Passenger wants {p.Request}.";
            PausePassengerRoutine(ref p);
            p.Request = ServiceItem.None;
            p.RequestTimer = 0;
            p.Satisfaction = Mathf.Min(100, p.Satisfaction + 15);
            if (Crew[crew].ServiceUnits > 1) Crew[crew].ServiceUnits--;
            else { Crew[crew].Carrying = ServiceItem.None; Crew[crew].ServiceUnits = 0; }
            State.Served++;
            Tally(crew, TallyKind.Orders);
            RecordServiceForEvent(passenger);
            React(passenger, PassengerReaction.Thanks);
            return "Order delivered! +8 service income, paid on arrival.";
        }

        public static int UpgradeCost(StationKind kind, VoyageState state) => kind == StationKind.EngineUpgrade
            ? 100 * (state.EngineLevel + 1) : kind == StationKind.CapacityUpgrade ? 150 * (state.CapacityLevel + 1)
            : kind == StationKind.GunneryUpgrade ? 120 * (state.GunneryLevel + 1) : kind == StationKind.BatteryUpgrade ? 180 * (state.BatteryLevel + 1) : 120;

        public const float UnstuckCooldown = 30f;
        private readonly float[] unstuckReady = new float[MaxCrew];

        /// <summary>
        /// "I'm stuck": puts the crew member back on a safe spot (the deck spawn, or the quay boarding spot when ashore),
        /// drops whatever they hold or man, and starts a cooldown.
        /// </summary>
        public string Unstuck(ulong clientId)
        {
            int i = FindCrew(clientId);
            if (i < 0) return "Crew not registered.";
            if (clock < unstuckReady[i]) return $"You can use this again in {Mathf.CeilToInt(unstuckReady[i] - clock)}s.";
            unstuckReady[i] = clock + UnstuckCooldown;
            ReleaseCrewResponsibilities(i);
            Crew[i].Swimming = false;
            overboardClock[i] = 0;
            Crew[i].Position = Crew[i].OnShore ? HarborPoint(HarborAction.BoardShip) : new Vector2(-1.5f + i, -6);
            Crew[i].Velocity = Vector2.zero;
            Crew[i].JumpHeight = Crew[i].VerticalSpeed = 0;
            ResetCommandStream(i);
            return "Back on your feet.";
        }

        /// <summary>Position of the purser's desk (the Manifest station), or null when the ship has none.</summary>
        public Vector2? PurserPoint
        {
            get
            {
                foreach (var station in Stations) if (station.Kind == StationKind.Manifest) return station.Position;
                return null;
            }
        }

        /// <summary>Buys a ship upgrade from the purser: aboard, next to the Manifest desk, and only at port.</summary>
        public string BuyUpgrade(ulong clientId, StationKind kind)
        {
            { int swimmer = FindCrew(clientId); if (swimmer >= 0 && Crew[swimmer].Swimming) return "Climb out of the water first."; }
            int i = FindCrew(clientId);
            if (i < 0) return "Crew not registered.";
            if (!IsUpgrade(kind)) return "Unknown upgrade.";
            if (Crew[i].OnShore) return "Return aboard to talk to the purser.";
            Vector2? purser = PurserPoint;
            if (purser == null || Vector2.Distance(Crew[i].Position, purser.Value) > InteractionRange) return "Move closer to the purser.";
            if (clock < interactionReady[i]) return string.Empty;
            interactionReady[i] = clock + .35f;
            return Buy(kind);
        }

        private string Buy(StationKind kind)
        {
            if (State.Phase == VoyagePhase.Sailing) return "Upgrades are available at port.";
            int level = UpgradeLevel(kind, State);
            int max = UpgradeMax(kind);
            if (level >= max) return "Upgrade maxed out.";
            int cost = UpgradeCost(kind, State);
            if (State.Wallet < cost) return $"Need {cost} coins.";
            State.Wallet -= cost;
            if (kind == StationKind.EngineUpgrade) State.EngineLevel++;
            else if (kind == StationKind.CapacityUpgrade) State.CapacityLevel++;
            else if (kind == StationKind.GunneryUpgrade) State.GunneryLevel++;
            else if (kind == StationKind.BatteryUpgrade) State.BatteryLevel++;
            else State.ComfortLevel++;
            if (kind == StationKind.GunneryUpgrade || kind == StationKind.BatteryUpgrade) return "Upgrade purchased. The gun crew has it from the next shot.";
            return "Upgrade purchased. Capacity changes apply to the next boarding.";
        }

        private void Settle(bool success)
        {
            if (State.Phase != VoyagePhase.Sailing) return;
            State.Phase = success ? VoyagePhase.Arrived : VoyagePhase.Failed;
            Encounter.CrewBall.Active = Encounter.CrewBallB.Active = Encounter.CrewBallC.Active = Encounter.PirateBall.Active = false;
            EndHazards();
            ShipMotionState = default;
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
                else if (success && !passenger.WorkedFare) State.LastTickets += 40;
            }
            State.LastServices = success ? State.Served * 8 : 0;
            float satisfaction = 0;
            foreach (var p in Passengers) if (p.Active && p.Motion != PassengerMotion.Rescuing) satisfaction += p.Satisfaction;
            State.LastTips = success ? Mathf.RoundToInt(satisfaction * .1f) : 0;
            State.LastRescueCosts = State.RescueCount * 15;
            State.LastCosts = State.Served * 2 + Mathf.CeilToInt((100 - State.Health) * .5f) + State.LastRescueCosts;
            State.LastBounty = success ? Encounter.Bounty : 0;
            State.LastEventBonus = success ? Event.Reward + Opportunity.ChestReward : 0;
            State.LastRouteBonus = success ? RouteBonus() + PassageBonus() : 0;
            State.LastReputation = success ? RouteReputation() : 0;
            State.Reputation += State.LastReputation;
            State.LastNet = State.LastTickets + State.LastServices + State.LastTips + State.LastBounty + State.LastEventBonus + State.LastRouteBonus - State.LastCosts;
            State.Wallet = Mathf.Max(0, State.Wallet + State.LastNet);
            CancelCannonReload();
            if (Encounter.Pirate == PiratePhase.Warning || Encounter.Pirate == PiratePhase.Attacking) Encounter.Pirate = PiratePhase.Escaped;
            Encounter.PowerOut = false;
            BeginDisembarking();
            for (int i = 0; i < MaxCrew; i++) { Crew[i].AtHelm = false; Crew[i].AtCannon = false; ReturnItem(i); }
        }

        private void ResetVoyage()
        {
            var old = State;
            State = new VoyageState { Health = 100, Voyage = old.Voyage + 1, Wallet = old.Wallet,
                EngineLevel = old.EngineLevel, CapacityLevel = old.CapacityLevel, ComfortLevel = old.ComfortLevel,
                GunneryLevel = old.GunneryLevel, BatteryLevel = old.BatteryLevel,
                Equipment = old.Equipment, OwnedEquipment = old.OwnedEquipment, OwnedPennant = old.OwnedPennant, PennantVisible = old.PennantVisible,
                FoodStock = Mathf.Max(16, old.FoodStock), DrinkStock = Mathf.Max(16, old.DrinkStock), HullShield = old.HullShield,
                RepairKitStock = VoyageRepairKits,
                Reputation = old.Reputation, Livery = old.Livery, DockJobPortKey = old.DockJobPortKey, DockJobsCompleted = old.DockJobsCompleted };
            Array.Clear(Passengers, 0, Passengers.Length);
            ResetTallies();
            nextLeak = 25;
            ResetHazards();
            ResetWorkload();
            ResetEncounter();
            ResetEvents();
            ResetDeck();
            ResetEmergencies();
            ResetOpportunities();
            ResetJourney();
            ResetActivities();
            for (int i = 0; i < MaxCrew; i++)
            {
                Crew[i].AtHelm = false;
                Crew[i].AtCannon = false;
                Crew[i].AtCrane = false;
                Crew[i].OnShore = false;
                Crew[i].Swimming = false;
                overboardClock[i] = 0;
                Crew[i].Carrying = ServiceItem.None;
                Crew[i].ServiceUnits = 0;
                Crew[i].Position = new Vector2(-1.5f + i, -6);
                Crew[i].JumpHeight = 0;
                Crew[i].VerticalSpeed = 0;
                inputs[i] = Vector2.zero;
            }
        }

        private int RouteBonus()
        {
            if (State.Route == RouteProfile.MarketRun) return State.Served >= 3 ? 35 : 5;
            if (State.Route == RouteProfile.StormPassage) return State.Health >= 70 ? 50 : 0;
            return 0;
        }

        private int RouteReputation()
        {
            if (State.Route == RouteProfile.MarketRun && State.Served >= 3) return 2;
            if (State.Route == RouteProfile.StormPassage && State.Health >= 70) return 3;
            return 1;
        }
    }
}
