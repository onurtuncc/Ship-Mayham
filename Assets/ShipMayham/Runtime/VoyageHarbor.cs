using UnityEngine;

namespace ShipMayham
{
    /// <summary>Bounded port-local movement and atomic, host-owned preparation purchases.</summary>
    public sealed partial class VoyageSimulation
    {
        public const int SupplyPrice = 12, SupplyAmount = 8, SupplyCap = 32, MaintenancePrice = 30;
        public const int DockJobGoal = 3, DockJobReward = 24;
        public const float MaxHullShield = 20, ShoreHeight = .3f;
        public readonly bool HarborEnabled;
        public static readonly Vector2 ShipGangway = new Vector2(-5, -6);
        public static Vector2 HarborPoint(HarborAction action)
        {
            switch (action)
            {
                case HarborAction.GoAshore: return ShipGangway;
                case HarborAction.BoardShip: return new Vector2(5, -6);
                case HarborAction.BuyFood: return new Vector2(-10, -10);
                case HarborAction.BuyDrink: return new Vector2(-25, -7);
                case HarborAction.MaintainHull: return new Vector2(-12, 11);
                case HarborAction.HarborMaster: return new Vector2(-7, 1);
                case HarborAction.PaintShop: return new Vector2(-26, -12);
                case HarborAction.FishMarket: return new Vector2(-20, 14);
                case HarborAction.CraneLoadingPad: return new Vector2(-25, 12);
                case HarborAction.CraneControls: return new Vector2(-23.5f, 8);
                default: return Vector2.zero;
            }
        }

        public static Vector3 HarborOrigin(VoyageState state) => new Vector3(-14, 0, state.Phase == VoyagePhase.Arrived ? 220 : 0);
        public bool HarborOpen => HarborEnabled && (State.Phase == VoyagePhase.Boarding || State.Phase == VoyagePhase.Arrived);
        public int CrewAshore
        {
            get { int count = 0; foreach (var crew in Crew) if (crew.Active && crew.OnShore) count++; return count; }
        }

        public static string RouteName(RouteProfile route) => route == RouteProfile.MarketRun
            ? "Market Run" : route == RouteProfile.StormPassage ? "Storm Passage" : "Coastal Ferry";

        public static int RouteReputation(RouteProfile route) => route == RouteProfile.MarketRun
            ? 2 : route == RouteProfile.StormPassage ? 5 : 0;

        private int CurrentPortKey => State.Voyage * 2 + (State.Phase == VoyagePhase.Arrived ? 1 : 0);

        public static int LiveryReputation(ShipLivery livery) => livery == ShipLivery.Seafoam ? 2 : livery == ShipLivery.Sunset ? 5 : 0;

        public string SelectLivery(ulong clientId, ShipLivery livery)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !HarborOpen || !Crew[slot].OnShore) return "Go ashore to repaint the ship.";
            if (livery > ShipLivery.Sunset) return "Unknown ship paint.";
            int required = LiveryReputation(livery);
            if (State.Reputation < required) return $"This paint unlocks at {required} reputation.";
            State.Livery = livery;
            return $"Ship paint changed to {livery}. Cosmetic only; no coins charged.";
        }

        public string SelectRoute(ulong clientId, RouteProfile route)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !HarborOpen || State.Phase != VoyagePhase.Boarding) return "Route contracts are available before departure.";
            if (!Crew[slot].OnShore) return "Go ashore to use the contract board.";
            if ((int)route < 0 || route > RouteProfile.StormPassage) return "Unknown route contract.";
            if (State.Boarded > 0) return "The passenger manifest is locked. Choose the route before boarding passengers.";
            int required = RouteReputation(route);
            if (State.Reputation < required) return $"{RouteName(route)} unlocks at {required} reputation. Complete ferry voyages or harbor cargo relays.";
            State.Route = route;
            nextLeak = route == RouteProfile.StormPassage ? 16 : 25;
            Event.NextIn = route == RouteProfile.StormPassage ? 12 : route == RouteProfile.MarketRun ? 16 : FirstEventDelay;
            return route == RouteProfile.MarketRun
                ? "Market Run selected: serve 3 orders for a 35-coin contract bonus."
                : route == RouteProfile.StormPassage
                    ? "Storm Passage selected: earlier crises and leaks; arrive above 70 hull for 50 coins."
                    : "Coastal Ferry selected: standard risk with no contract condition.";
        }

        public string ToggleDockJob(ulong clientId, DockJobKind kind = DockJobKind.CargoRelay)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !HarborOpen || !Crew[slot].OnShore) return "Go ashore to use the job board.";
            if (kind > DockJobKind.CraneLoading) return "Unknown dock job.";
            if (State.DockJobActive)
            {
                if (State.DockJobType != kind) return "Finish or cancel the current job before changing contracts.";
                if (State.CraneHolding) return "Lower the crane's load before cancelling the job.";
                foreach (var member in Crew)
                    if (member.Active && IsPortCargo(member.Carrying)) return "Return every held item to the orange pickup before cancelling.";
                State.DockJobActive = false;
                State.DockJobProgress = 0;
                State.DockJobDeliveredMask = 0;
                ClearHarborCrane();
                return "Port job cancelled. Departure is unlocked.";
            }
            if (State.DockJobPortKey == CurrentPortKey) return "This port's job is already complete.";
            State.DockJobActive = true;
            State.DockJobProgress = 0;
            State.DockJobType = kind;
            State.DockJobDeliveredMask = 0;
            if (kind == DockJobKind.CraneLoading)
            {
                ClearHarborCrane(); State.CraneStock = DockJobGoal;
                State.CraneHook = HarborPoint(HarborAction.CraneLoadingPad);
                return "Crane loading: carry 3 FISH MARKET crates to LOAD PAD. Operator: WASD moves hook, Space grabs/lands, E releases.";
            }
            if (kind == DockJobKind.SalvageRun) return "Salvage run: find 3 marked bundles around town. Carry them to the PORT OFFICE. Food counter accepts returns.";
            if (kind == DockJobKind.LuggageSort) return "Bag sort: ORANGE pickup/return. Read A/B/C desks on the board. Deliver to BLUE or GREEN; wrong desks keep the bag.";
            return "Cargo relay started: carry 3 crates from FOOD pickup to HULL delivery. Multiple crew can relay together.";
        }

        private static bool ShoreWalkable(Vector2 p)
        {
            if (!HarborTownLayout.Walkable(p)) return false;
            for (int a = (int)HarborAction.BuyFood; a <= (int)HarborAction.MaintainHull; a++)
                if (Vector2.Distance(p, HarborPoint((HarborAction)a)) < .9f) return false;
            return true;
        }

        public string HarborCommand(ulong clientId, HarborAction action)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !HarborOpen || (int)action > (int)HarborAction.CraneControls) return "Harbor is closed.";
            ref var crew = ref Crew[slot];
            if (crew.AtHelm || crew.AtCannon || crew.AtCrane) return "Release your station first.";
            if (crew.JumpHeight > 0 || crew.VerticalSpeed > 0) return "Land before interacting.";
            if (crew.OnShore == (action == HarborAction.GoAshore)) return "Use the gangway to change areas first.";
            if (Vector2.Distance(crew.Position, HarborPoint(action)) > InteractionRange) return "Move closer to the harbor station.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            if (IsPortCargo(crew.Carrying)) return HandleDockItem(slot, action);
            if (crew.Carrying != ServiceItem.None || CarriedPassenger(slot) >= 0) return "Return your item or put the passenger down first.";
            if (action == HarborAction.GoAshore || action == HarborAction.BoardShip)
            {
                crew.OnShore = action == HarborAction.GoAshore;
                crew.Position = crew.OnShore ? HarborPoint(HarborAction.BoardShip) : ShipGangway;
                inputs[slot] = Vector2.zero;
                return crew.OnShore ? "Welcome ashore! Supplies and hull protection use the shared wallet." : "Back aboard. All crew must board before departure.";
            }
            if (action == HarborAction.HarborMaster) return "Port office: choose a route or a paid town job on the harbor board. Salvage pays 36 coins +1 reputation.";
            if (action == HarborAction.PaintShop) return "Paint dock: Classic is free. Seafoam needs 2 reputation; Sunset needs 5. Choose your paint on the board.";
            if (action == HarborAction.FishMarket) return TakeCraneCrate(slot);
            if (action == HarborAction.CraneLoadingPad) return "Carry FISH MARKET crates here during CRANE LOADING. The operator collects from this pad.";
            if (action == HarborAction.CraneControls) return TakeHarborCrane(slot);
            if (action == HarborAction.MaintainHull)
            {
                if (State.HullShield > 0) return "Hull protection is already fitted. Use the remaining shield first.";
                if (State.Wallet < MaintenancePrice) return "Hull protection costs 30 coins.";
                State.Wallet -= MaintenancePrice;
                State.HullShield = MaxHullShield;
                return "Hull protected: absorbs the next 20 damage, including leaks and pirate fire.";
            }
            if (action == HarborAction.BuyFood && State.DockJobActive)
                return PickUpDockItem(slot);
            bool food = action == HarborAction.BuyFood;
            int stock = food ? State.FoodStock : State.DrinkStock;
            // Include items held aboard so returning them cannot exceed the storage cap.
            foreach (var member in Crew)
                if (member.Active && member.Carrying == (food ? ServiceItem.Food : ServiceItem.Drink)) stock += Mathf.Max(1, member.ServiceUnits);
            if (stock + SupplyAmount > SupplyCap) return "Not enough storage for 8 items (maximum 32, including held items).";
            if (State.Wallet < SupplyPrice) return "An 8-item supply pack costs 12 coins.";
            State.Wallet -= SupplyPrice;
            if (food) State.FoodStock += SupplyAmount; else State.DrinkStock += SupplyAmount;
            return food ? "+8 food stored aboard. Unused supplies carry over to the next voyage." : "+8 drinks stored aboard. Unused supplies carry over to the next voyage.";
        }

        private void DamageHull(float damage)
        {
            float absorbed = Mathf.Min(State.HullShield, damage);
            State.HullShield -= absorbed;
            State.Health = Mathf.Max(0, State.Health - (damage - absorbed));
        }
    }
}
