namespace ShipMayham
{
    public sealed partial class VoyageSimulation
    {
        public const int VoyageRepairKits = 3, RepairKitBufferCapacity = 2;
        public const float QuickPatchHullRecovery = 8;
        private bool RepairSuppliesOpen => State.Phase == VoyagePhase.Boarding || State.Phase == VoyagePhase.Sailing;

        private string TakeOrReturnRepairKit(int slot)
        {
            if (!RepairSuppliesOpen) return "Kit store closed. Prepare a new voyage.";
            if (Crew[slot].Carrying == ServiceItem.RepairKit)
            {
                ReturnItem(slot);
                return "Kit returned to the store.";
            }
            if (Crew[slot].Carrying != ServiceItem.None) return "Return your item before taking a repair kit.";
            if (State.RepairKitStock <= 0) return "Kit store empty. Check the repair bench; manual four-step patches still work.";
            State.RepairKitStock--;
            Crew[slot].Carrying = ServiceItem.RepairKit;
            return "Carrying a quick patch kit. E / A at REPAIR delivers it; KITS returns it.";
        }

        private string DeliverRepairKit(int slot)
        {
            if (!RepairSuppliesOpen) return "Repair deliveries closed. Prepare a new voyage.";
            if (State.ReadyRepairKits >= RepairKitBufferCapacity) return "Repair bench full. Keep the kit or return it to KITS.";
            State.ReadyRepairKits++;
            Crew[slot].Carrying = ServiceItem.None;
            return $"Kit delivered: {State.ReadyRepairKits}/{RepairKitBufferCapacity}. E / A again at REPAIR applies one to a leak.";
        }

        private void FinishLeakRepair()
        {
            State.Leak = 0;
            State.RepairSteps = 4;
            nextLeak = State.Elapsed + 45;
        }
    }
}
