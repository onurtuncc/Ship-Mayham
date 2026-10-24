using UnityEngine;

namespace ShipMayham
{
    /// <summary>Presentation of the replicated repair inventory; never changes game rules.</summary>
    public sealed class PrototypeRepairSupplyView : MonoBehaviour
    {
        public PrototypeSession Session;
        public PrototypeStation Store, Bench;
        public Transform[] StoredKits, ReadyKits;
        private int shownStock = -1, shownReady = -1, shownLimit = -1;
        private readonly System.Collections.Generic.List<GameObject> workshopVisuals = new System.Collections.Generic.List<GameObject>(3);

        private void Awake()
        {
            ExtendKits(ref StoredKits, 5, true);
            ExtendKits(ref ReadyKits, 3, false);
        }

        private void ExtendKits(ref Transform[] visuals, int capacity, bool stored)
        {
            if (visuals == null || visuals.Length == 0 || visuals[0] == null || visuals.Length >= capacity) return;
            int originalCount = visuals.Length;
            Transform template = visuals[0];
            System.Array.Resize(ref visuals, capacity);
            for (int i = originalCount; i < capacity; i++)
            {
                var copy = Instantiate(template.gameObject, template.parent, false);
                copy.name = (stored ? "Workshop_StoredKit_" : "Workshop_ReadyKit_") + i;
                copy.transform.localPosition = stored ? new Vector3(-.175f + (i - originalCount) * .35f, 1.35f, 0) : new Vector3(0, 1.35f, -.35f);
                copy.SetActive(false);
                visuals[i] = copy.transform;
                workshopVisuals.Add(copy);
            }
        }

        private void OnDestroy()
        {
            foreach (var visual in workshopVisuals) if (visual != null) Destroy(visual);
        }

        private void LateUpdate()
        {
            if (Session == null) return;
            int stock = Session.IsSpawned ? Session.Voyage.Value.RepairKitStock : VoyageSimulation.VoyageRepairKits;
            int ready = Session.IsSpawned ? Session.Voyage.Value.ReadyRepairKits : 0;
            int limit = VoyageSimulation.RepairKitLimit(Session.Voyage.Value);
            if (stock != shownStock || limit != shownLimit)
            {
                shownStock = stock;
                shownLimit = limit;
                SetVisibleCount(StoredKits, stock);
                if (Store != null && Store.Label != null) Store.Label.text = $"KITS {stock}/{limit}\nCarry to REPAIR";
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
