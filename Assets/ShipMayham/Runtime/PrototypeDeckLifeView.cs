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
        private GameObject seaChest, plugLocker;
        private Material wood;
        private GameObject pennant;
        private readonly GameObject[] extraOrders = new GameObject[VoyageSimulation.MaxCrew];
        private readonly Renderer[] extraRenderers = new Renderer[VoyageSimulation.MaxCrew];
        private MaterialPropertyBlock extraColor;
        private readonly GameObject[] extinguishers = new GameObject[VoyageSimulation.MaxCrew];
        private readonly GameObject[] plugs = new GameObject[VoyageSimulation.MaxCrew];
        private void Start()
        {
            extraColor = new MaterialPropertyBlock();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || View == null || View.Session == null) return;
            material = new Material(shader) { color = new Color(.15f, .65f, .85f) };
            safetyMaterial = new Material(shader) { color = new Color(.9f, .15f, .05f) };
            wood = new Material(shader) { color = new Color(.48f, .27f, .08f) };
            pennant = GameObject.CreatePrimitive(PrimitiveType.Cube); pennant.name = "ShipCosmetic_Pennant_Visuals";
            pennant.transform.SetParent(View.Session.ShipRoot, false);
            pennant.transform.localPosition = new Vector3(1, 6, 0); pennant.transform.localScale = new Vector3(1.4f, .7f, .05f);
            Destroy(pennant.GetComponent<Collider>()); pennant.GetComponent<Renderer>().sharedMaterial = safetyMaterial;
            seaChest = GameObject.CreatePrimitive(PrimitiveType.Cube); seaChest.name = "SeaOpportunity_Chest_Visuals";
            seaChest.transform.SetParent(transform, false); seaChest.transform.localScale = new Vector3(1.3f, .8f, 1);
            Destroy(seaChest.GetComponent<Collider>()); seaChest.GetComponent<Renderer>().sharedMaterial = wood;
            plugLocker = GameObject.CreatePrimitive(PrimitiveType.Cube); plugLocker.name = "TemporaryPlug_Locker_Visuals";
            plugLocker.transform.SetParent(View.Session.ShipRoot, false);
            plugLocker.transform.localPosition = new Vector3(VoyageSimulation.PlugLocker.x, .6f, VoyageSimulation.PlugLocker.y);
            plugLocker.transform.localScale = new Vector3(.65f, .2f, .65f);
            Destroy(plugLocker.GetComponent<Collider>()); plugLocker.GetComponent<Renderer>().sharedMaterial = wood;
            fire = GameObject.CreatePrimitive(PrimitiveType.Sphere); fire.name = "ElectricalFire_Visuals";
            fire.transform.SetParent(View.Session.ShipRoot, false); fire.transform.localScale = new Vector3(1.2f, 2.2f, 1.2f);
            fire.transform.localPosition = new Vector3(VoyageSimulation.FirePoint.x, 1, VoyageSimulation.FirePoint.y);
            Destroy(fire.GetComponent<Collider>()); fire.GetComponent<Renderer>().sharedMaterial = safetyMaterial;
            for (int i = 0; i < extinguishers.Length; i++)
            {
                var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); item.name = "Extinguisher_Visuals";
                item.transform.SetParent(View.CarriedItems[i], false); item.transform.localScale = new Vector3(.22f, .24f, .22f);
                Destroy(item.GetComponent<Collider>()); item.GetComponent<Renderer>().sharedMaterial = safetyMaterial; extinguishers[i] = item;
                var plug = GameObject.CreatePrimitive(PrimitiveType.Cylinder); plug.name = "TemporaryPlug_Visuals";
                plug.transform.SetParent(View.CarriedItems[i], false); plug.transform.localScale = new Vector3(.3f, .12f, .3f);
                Destroy(plug.GetComponent<Collider>()); plug.GetComponent<Renderer>().sharedMaterial = wood; plugs[i] = plug;
                var extra = GameObject.CreatePrimitive(PrimitiveType.Cube); extra.name = "Tray_SecondOrder_Visuals";
                extra.transform.SetParent(View.CarriedItems[i], false); extra.transform.localPosition = new Vector3(.24f, .05f, 0);
                extra.transform.localScale = new Vector3(.18f, .18f, .18f); Destroy(extra.GetComponent<Collider>());
                extraRenderers[i] = extra.GetComponent<Renderer>();
                extraRenderers[i].sharedMaterial = material; extraOrders[i] = extra;
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
            pennant.SetActive(session.IsSpawned && session.Voyage.Value.PennantVisible);
            seaChest.SetActive(session.IsSpawned && session.Voyage.Value.Phase == VoyagePhase.Sailing && session.Opportunity.Value.ChestAvailable);
            Vector2 chest = session.Opportunity.Value.ChestPosition;
            seaChest.transform.position = new Vector3(chest.x, -.5f + Mathf.Sin(Time.time) * .1f, chest.y);
            plugLocker.SetActive(session.IsSpawned && !session.InLobby);
            spill.SetActive(session.IsSpawned && session.Voyage.Value.Phase == VoyagePhase.Sailing && deck.Spill);
            spill.transform.localPosition = new Vector3(deck.SpillPosition.x, .52f, deck.SpillPosition.y);
            fire.SetActive(session.IsSpawned && session.Voyage.Value.Phase == VoyagePhase.Sailing &&
                (session.Emergency.Value.Fire == FirePhase.Warning || session.Emergency.Value.Fire == FirePhase.Burning));
            for (int i = 0; i < extinguishers.Length; i++)
            {
                extinguishers[i].SetActive(session.IsSpawned && i < session.Crew.Count && session.Crew[i].Carrying == ServiceItem.Extinguisher);
                plugs[i].SetActive(session.IsSpawned && i < session.Crew.Count && session.Crew[i].Carrying == ServiceItem.TemporaryPlug);
                bool extra = session.IsSpawned && i < session.Crew.Count && session.Crew[i].ServiceUnits > 1;
                extraOrders[i].SetActive(extra);
                if (extra)
                {
                    extraColor.SetColor("_BaseColor", session.Crew[i].Carrying == ServiceItem.Drink ? View.DrinkColor : View.FoodColor);
                    extraRenderers[i].SetPropertyBlock(extraColor);
                }
            }
        }
        private void OnDestroy()
        {
            if (spill != null) Destroy(spill);
            if (material != null) Destroy(material);
            if (fire != null) Destroy(fire);
            foreach (var item in extinguishers) if (item != null) Destroy(item);
            foreach (var plug in plugs) if (plug != null) Destroy(plug);
            if (safetyMaterial != null) Destroy(safetyMaterial);
            if (wood != null) Destroy(wood);
            if (seaChest != null) Destroy(seaChest);
            if (plugLocker != null) Destroy(plugLocker);
            if (pennant != null) Destroy(pennant);
            foreach (var extra in extraOrders) if (extra != null) Destroy(extra);
        }
    }
}
