using UnityEngine;

namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        public const int LuggageJobReward = 30, SalvageJobReward = 36;
        public static bool IsSalvage(ServiceItem item) => item >= ServiceItem.SalvageA && item <= ServiceItem.SalvageC;
        public static bool IsLuggage(ServiceItem item) => item >= ServiceItem.LuggageA && item <= ServiceItem.LuggageC;
        public static bool IsPortCargo(ServiceItem item) => item == ServiceItem.Cargo || IsLuggage(item) || IsSalvage(item) || item == ServiceItem.CraneCrate;
        public static string LuggageTag(ServiceItem item) => item == ServiceItem.LuggageA ? "A" : item == ServiceItem.LuggageB ? "B" : item == ServiceItem.LuggageC ? "C" : "?";
        public static string DockJobName(DockJobKind kind) => kind == DockJobKind.CraneLoading ? "CRANE LOADING" : kind == DockJobKind.SalvageRun ? "TOWN SALVAGE" : kind == DockJobKind.LuggageSort ? "BAGGAGE SORT" : "CARGO RELAY";
        public string DockItemCommand(ulong clientId, HarborAction action, DockJobKind expectedKind)
        {
            // A delayed job interaction must not turn into a shared-wallet purchase.
            if (!State.DockJobActive || State.DockJobType != expectedKind) return "Dock contract changed. No items or coins moved; check the board again.";
            int slot = FindCrew(clientId);
            if (slot < 0) return "Crew not registered.";
            if ((action == HarborAction.BuyDrink || action == HarborAction.MaintainHull) && !IsPortCargo(Crew[slot].Carrying))
                return "No job item in your hands. No purchase made; check the board again.";
            return HarborCommand(clientId, action);
        }
        public static HarborAction LuggageDestination(VoyageState state, ServiceItem item)
        {
            if (!IsLuggage(item)) return HarborAction.BuyFood;
            int tag = (int)item - (int)ServiceItem.LuggageA;
            return (tag + state.Voyage + (state.Phase == VoyagePhase.Arrived ? 1 : 0)) % 2 == 0 ? HarborAction.BuyDrink : HarborAction.MaintainHull;
        }
        public static string LuggageDesk(VoyageState state, ServiceItem item) => LuggageDestination(state, item) == HarborAction.BuyDrink ? "BLUE / DRINKS" : "GREEN / HULL";
        public static Color LuggageColor(ServiceItem item) => item == ServiceItem.LuggageA ? new Color(.1f, .8f, 1) : item == ServiceItem.LuggageB ? new Color(1, .55f, .1f) : new Color(.85f, .3f, 1);

        private string PickUpDockItem(int slot)
        {
            if (State.DockJobType == DockJobKind.CraneLoading) return "Crane crates are at FISH MARKET. This counter only accepts returns for that job.";
            if (State.DockJobType == DockJobKind.SalvageRun) return "Find the 3 marked salvage bundles around town. This counter only accepts returns for that job.";
            if (State.DockJobType == DockJobKind.CargoRelay)
            {
                int committed = State.DockJobProgress;
                foreach (var member in Crew) if (member.Active && member.Carrying == ServiceItem.Cargo) committed++;
                if (committed >= DockJobGoal) return "All remaining crates are already being carried. Help the crew deliver them.";
                Crew[slot].Carrying = ServiceItem.Cargo;
                return $"Cargo crate picked up. Deliver it to HULL ({State.DockJobProgress}/{DockJobGoal}).";
            }
            for (int tag = 0; tag < DockJobGoal; tag++)
            {
                if ((State.DockJobDeliveredMask & (1 << tag)) != 0) continue;
                ServiceItem item = (ServiceItem)((int)ServiceItem.LuggageA + tag);
                bool held = false;
                foreach (var member in Crew) if (member.Active && member.Carrying == item) { held = true; break; }
                if (held) continue;
                Crew[slot].Carrying = item;
                return $"Bag {LuggageTag(item)} picked up. Deliver to {LuggageDesk(State, item)}. Orange returns it.";
            }
            return "All remaining bags are already carried. Help the couriers deliver them.";
        }

        private string HandleDockItem(int slot, HarborAction action)
        {
            ServiceItem item = Crew[slot].Carrying;
            if (item == ServiceItem.CraneCrate) return HandleCraneCrate(slot, action);
            if (action == HarborAction.BuyFood)
            {
                Crew[slot].Carrying = ServiceItem.None;
                return "Item returned to the orange pickup.";
            }
            if (!State.DockJobActive) return "No dock job is active. Return the item at the pickup before starting another job.";
            if (IsSalvage(item))
            {
                if (State.DockJobType != DockJobKind.SalvageRun || action != HarborAction.HarborMaster)
                    return "Bring salvage to the PORT OFFICE, or return it at the FOOD market.";
                int bit = 1 << ((int)item - (int)ServiceItem.SalvageA);
                if ((State.DockJobDeliveredMask & bit) != 0) return "That salvage bundle is already delivered.";
                State.DockJobDeliveredMask |= bit;
            }
            else if (IsLuggage(item))
            {
                if (State.DockJobType != DockJobKind.LuggageSort || action != LuggageDestination(State, item))
                    return $"Bag {LuggageTag(item)} belongs at {LuggageDesk(State, item)}. Keep it, or return to orange.";
                int bit = 1 << ((int)item - (int)ServiceItem.LuggageA);
                if ((State.DockJobDeliveredMask & bit) != 0) return "That bag is already delivered.";
                State.DockJobDeliveredMask |= bit;
            }
            else if (State.DockJobType != DockJobKind.CargoRelay || action != HarborAction.MaintainHull)
                return "Deliver cargo at HULL, or return it to the FOOD pickup.";
            Crew[slot].Carrying = ServiceItem.None;
            return RecordDockDelivery(State.DockJobType == DockJobKind.SalvageRun ? SalvageJobReward : State.DockJobType == DockJobKind.LuggageSort ? LuggageJobReward : DockJobReward, slot);
        }

        private string RecordDockDelivery(int reward, int slot = -1)
        {
            State.DockJobProgress++;
            Tally(slot, TallyKind.Deliveries);
            if (State.DockJobProgress < DockJobGoal) return $"Delivered: {State.DockJobProgress}/{DockJobGoal}.";
            State.DockJobActive = false;
            State.DockJobPortKey = CurrentPortKey;
            State.DockJobsCompleted++;
            State.Wallet += reward;
            State.Reputation++;
            return $"{DockJobName(State.DockJobType)} complete! +{reward} shared coins and +1 reputation.";
        }

        public string PickUpSalvage(ulong clientId, int index)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !HarborOpen || !Crew[slot].OnShore || !State.DockJobActive || State.DockJobType != DockJobKind.SalvageRun)
                return "Start TOWN SALVAGE at the port office while ashore.";
            if (index < 0 || index >= HarborTownLayout.SalvageCount) return "Unknown salvage bundle.";
            if (Crew[slot].AtHelm || Crew[slot].AtCannon || Crew[slot].JumpHeight > 0 || Crew[slot].VerticalSpeed > 0)
                return "Land and release your station first.";
            if (Vector2.Distance(Crew[slot].Position, HarborTownLayout.SalvagePoint(index)) > InteractionRange) return "Move closer to the salvage bundle.";
            if (Crew[slot].Carrying != ServiceItem.None || CarriedPassenger(slot) >= 0) return "Free your hands first.";
            if (clock < interactionReady[slot]) return string.Empty;
            ServiceItem item = (ServiceItem)((int)ServiceItem.SalvageA + index);
            if ((State.DockJobDeliveredMask & (1 << index)) != 0) return "Already delivered.";
            foreach (var member in Crew) if (member.Active && member.Carrying == item) return "A crewmate is already carrying that bundle.";
            interactionReady[slot] = clock + .35f;
            Crew[slot].Carrying = item;
            return "Salvage picked up. Carry it to the PORT OFFICE; FOOD accepts returns.";
        }
    }
}
