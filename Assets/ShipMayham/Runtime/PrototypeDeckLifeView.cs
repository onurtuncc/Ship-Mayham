using UnityEngine;

namespace ShipMayham
{
    public sealed class PrototypeDeckLifeView : MonoBehaviour
    {
        public PrototypeView View;
        private GameObject spill;
        private Material material;
        private Material safetyMaterial;
        private GameObject fire;
        private readonly GameObject[] extinguishers = new GameObject[VoyageSimulation.MaxCrew];
        private void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || View == null || View.Session == null) return;
            material = new Material(shader) { color = new Color(.15f, .65f, .85f) };
            safetyMaterial = new Material(shader) { color = new Color(.9f, .15f, .05f) };
            fire = GameObject.CreatePrimitive(PrimitiveType.Sphere); fire.name = "ElectricalFire_Visuals";
            fire.transform.SetParent(View.Session.ShipRoot, false); fire.transform.localScale = new Vector3(1.2f, 2.2f, 1.2f);
            fire.transform.localPosition = new Vector3(VoyageSimulation.FirePoint.x, 1, VoyageSimulation.FirePoint.y);
            Destroy(fire.GetComponent<Collider>()); fire.GetComponent<Renderer>().sharedMaterial = safetyMaterial;
            for (int i = 0; i < extinguishers.Length; i++)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); item.name = "Extinguisher_Visuals";
                item.transform.SetParent(View.CarriedItems[i], false); item.transform.localScale = new Vector3(.22f, .24f, .22f);
                Destroy(item.GetComponent<Collider>()); item.GetComponent<Renderer>().sharedMaterial = safetyMaterial; extinguishers[i] = item;
            }
            spill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spill.name = "Deck_Spill_Visuals";
            spill.transform.SetParent(View.Session.ShipRoot, false);
            spill.transform.localScale = new Vector3(1.4f, .015f, 1.4f);
            Destroy(spill.GetComponent<Collider>()); spill.GetComponent<Renderer>().sharedMaterial = material;
        }
        private void LateUpdate()
        {
            if (spill == null) return;
            var session = View.Session;
            var deck = session.Deck.Value;
            spill.SetActive(session.IsSpawned && session.Voyage.Value.Phase == VoyagePhase.Sailing && deck.Spill);
            spill.transform.localPosition = new Vector3(deck.SpillPosition.x, .52f, deck.SpillPosition.y);
            fire.SetActive(session.IsSpawned && session.Voyage.Value.Phase == VoyagePhase.Sailing &&
                (session.Emergency.Value.Fire == FirePhase.Warning || session.Emergency.Value.Fire == FirePhase.Burning));
            for (int i = 0; i < extinguishers.Length; i++)
                extinguishers[i].SetActive(session.IsSpawned && i < session.Crew.Count && session.Crew[i].Carrying == ServiceItem.Extinguisher);
        }
        private void OnDestroy()
        {
            if (spill != null) Destroy(spill);
            if (material != null) Destroy(material);
            if (fire != null) Destroy(fire);
            foreach (var item in extinguishers) if (item != null) Destroy(item);
            if (safetyMaterial != null) Destroy(safetyMaterial);
        }
    }
}
