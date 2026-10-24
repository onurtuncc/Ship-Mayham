using UnityEngine;

namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        public const int CraneJobReward = 45, CranePadCapacity = 2;
        public const float CraneLandingRadius = 1.35f;
        public static readonly Vector2 CraneBarge = new Vector2(-36, 10);
        public static readonly Rect CraneTravel = Rect.MinMaxRect(-37, 6, -23, 14);
        public static Vector2 CraneLandingPoint(VoyageState state) => state.CraneHook + (state.CraneHolding
            ? new Vector2(Mathf.Sin(state.CraneSwayTime * 1.7f) * .65f, Mathf.Cos(state.CraneSwayTime * 1.3f) * .4f) : Vector2.zero);
        private bool CraneJobOpen => HarborOpen && State.DockJobActive && State.DockJobType == DockJobKind.CraneLoading;

        private void ClearHarborCrane()
        {
            State.CraneStock = State.CraneReady = 0; State.CraneHolding = false;
            State.CraneHook = HarborPoint(HarborAction.CraneLoadingPad); State.CraneSwayTime = 0;
            for (int i = 0; i < MaxCrew; i++)
                if (Crew[i].AtCrane) { Crew[i].AtCrane = false; inputs[i] = Vector2.zero; }
        }

        private string TakeCraneCrate(int slot)
        {
            if (!CraneJobOpen) return "Start CRANE LOADING at the PORT OFFICE. Fish crates are only available for that job.";
            if (State.CraneStock <= 0) return "All crates are on the pad, hook, barge or in a crewmate's hands.";
            State.CraneStock--; Crew[slot].Carrying = ServiceItem.CraneCrate;
            return "Crate picked up. Carry it to LOAD PAD beside the crane. FISH MARKET or FOOD accepts returns.";
        }

        private string HandleCraneCrate(int slot, HarborAction action)
        {
            if (action == HarborAction.FishMarket || action == HarborAction.BuyFood)
            { ReturnItem(slot); return "Crate returned to the fish market."; }
            if (!CraneJobOpen || action != HarborAction.CraneLoadingPad) return "Deliver at LOAD PAD beside the crane, or return at FISH MARKET / FOOD.";
            if (State.CraneReady >= CranePadCapacity) return "Loading pad full. Wait for the operator to lift a crate, or return yours.";
            State.CraneReady++; Crew[slot].Carrying = ServiceItem.None;
            return $"Crate on loading pad: {State.CraneReady}/{CranePadCapacity}. Operator: move hook over LOAD PAD, then Space / X.";
        }

        private string TakeHarborCrane(int slot)
        {
            if (!CraneJobOpen) return "Start CRANE LOADING at the PORT OFFICE first.";
            foreach (var member in Crew) if (member.Active && member.AtCrane) return "Crane occupied. Help carry fish crates to LOAD PAD.";
            Crew[slot].AtCrane = true;
            Crew[slot].Position = HarborPoint(HarborAction.CraneControls) + Vector2.down * .8f;
            inputs[slot] = Vector2.zero;
            return "CRANE: WASD / left stick moves hook. Space / X lifts or lands. Match the barge ring. E / A releases.";
        }

        private bool CanUseHarborCrane(int slot) => slot >= 0 && Crew[slot].Active && Crew[slot].AtCrane && Crew[slot].OnShore && CraneJobOpen &&
            Vector2.Distance(Crew[slot].Position, HarborPoint(HarborAction.CraneControls)) <= InteractionRange;

        private void MoveHarborCrane(int slot, Vector2 input, float dt)
        {
            if (!CanUseHarborCrane(slot)) return;
            Vector2 next = State.CraneHook + input * (3.5f * dt);
            State.CraneHook = new Vector2(Mathf.Clamp(next.x, CraneTravel.xMin, CraneTravel.xMax), Mathf.Clamp(next.y, CraneTravel.yMin, CraneTravel.yMax));
        }

        public string HarborCraneAction(ulong clientId, bool hoist)
        {
            int slot = FindCrew(clientId);
            if (slot < 0 || !Crew[slot].AtCrane) return "Use the CRANE controls first.";
            if (clock < interactionReady[slot]) return string.Empty;
            interactionReady[slot] = clock + .35f;
            if (!hoist)
            {
                Crew[slot].AtCrane = false; inputs[slot] = Vector2.zero;
                return "Crane released. The load stays on the hook; another operator can take over.";
            }
            if (!CanUseHarborCrane(slot)) return "Crane job is closed, or you moved away from its controls.";
            if (!State.CraneHolding)
            {
                if (Vector2.Distance(State.CraneHook, HarborPoint(HarborAction.CraneLoadingPad)) > 1.5f) return "Move the empty hook above LOAD PAD before lifting.";
                if (State.CraneReady <= 0) return "Loading pad empty. Ask the crew to bring a crate from FISH MARKET.";
                State.CraneReady--; State.CraneHolding = true;
                return "Crate lifted! Move the hook over the offshore barge ring, then Space / X to lower.";
            }
            State.CraneHolding = false;
            // Use the loaded sway, not the empty-hook point, for the landing check.
            VoyageState loaded = State; loaded.CraneHolding = true;
            if (Vector2.Distance(CraneLandingPoint(loaded), CraneBarge) > CraneLandingRadius)
            {
                if (State.CraneReady < CranePadCapacity) State.CraneReady++; else State.CraneStock++;
                return "Missed the barge! Safety crew recovered the crate to the pad/market. Aim the swinging hook inside the ring.";
            }
            string result = RecordDockDelivery(CraneJobReward, slot);
            if (!State.DockJobActive)
                for (int i = 0; i < MaxCrew; i++) if (Crew[i].AtCrane) { Crew[i].AtCrane = false; inputs[i] = Vector2.zero; }
            return result;
        }
    }
}
