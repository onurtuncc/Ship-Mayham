using UnityEngine;

namespace ShipMayham
{
    /// <summary>Presentation of the replicated repair inventory; never changes game rules.</summary>
    public sealed class PrototypeRepairSupplyView : MonoBehaviour
    {
        public PrototypeSession Session;
        public PrototypeStation Store, Bench;
        public Transform[] StoredKits, ReadyKits;
        private int shownStock = -1, shownReady = -1;

        private void LateUpdate()
        {
            if (Session == null) return;
            int stock = Session.IsSpawned ? Session.Voyage.Value.RepairKitStock : VoyageSimulation.VoyageRepairKits;
            int ready = Session.IsSpawned ? Session.Voyage.Value.ReadyRepairKits : 0;
            if (stock != shownStock)
            {
                shownStock = stock;
                SetVisibleCount(StoredKits, stock);
                if (Store != null && Store.Label != null) Store.Label.text = $"KITS {stock}/{VoyageSimulation.VoyageRepairKits}\nCarry to REPAIR";
            }
            if (ready != shownReady)
            {
                shownReady = ready;
                SetVisibleCount(ReadyKits, ready);
                if (Bench != null && Bench.Label != null) Bench.Label.text = $"REPAIR\n{ready} quick kits ready";
            }
        }

        private static void SetVisibleCount(Transform[] visuals, int count)
        {
            if (visuals == null) return;
            for (int i = 0; i < visuals.Length; i++) if (visuals[i] != null) visuals[i].gameObject.SetActive(i < count);
        }
    }
}
