using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>Presentation of passage gates and onboard safety status; HazardView owns captain hazards. Created by the existing first-person view.</summary>
    public sealed class PrototypeNavigationView : MonoBehaviour
    {
        public PrototypeView View;
        private Material material;
        private Text warning;
        private readonly Transform[] gates = new Transform[2];
        private string lastWarning;
        private int warningSignature = int.MinValue;
        private CanvasScaler scaler;

        private void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogWarning("Navigation visuals need the URP Lit shader."); return; }
            material = new Material(shader) { color = new Color(.28f, .35f, .3f) };
            for (int i = 0; i < gates.Length; i++) gates[i] = Create("Passage_Gate_" + (i + 1), PrimitiveType.Cylinder, new Vector3(30, .08f, 30));
            var canvasRoot = new GameObject("Navigation_Status", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            warning = HudKit.Label(canvasRoot.transform, "NavigationWarning", 23, Color.yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudKit.Anchor(warning.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(740, 180));
            warning.gameObject.AddComponent<Outline>().effectColor = Color.black;
        }
        private Transform Create(string objectName, PrimitiveType type, Vector3 scale)
        {
            var root = new GameObject(objectName); root.transform.SetParent(transform, false);
            var visual = GameObject.CreatePrimitive(type); visual.name = "Visuals"; visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = scale; Destroy(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;
            return root.transform;
        }
        private void LateUpdate()
        {
            if (View == null || View.Session == null || material == null) return;
            var session = View.Session;
            bool active = session.IsSpawned && !session.InLobby && session.Voyage.Value.Phase == VoyagePhase.Sailing;
            scaler.referenceResolution = new Vector2(1920, 1080) / GameSettings.UiScale;
            var journey = session.Journey.Value;
            for (int i = 0; i < gates.Length; i++)
            {
                gates[i].gameObject.SetActive(active && journey.Course != PassageCourse.Direct && i >= journey.Checkpoints);
                Vector2 point = VoyageSimulation.CourseGate(journey.Course, i);
                gates[i].position = new Vector3(point.x, -.65f, point.y);
            }
            var emergency = session.Emergency.Value;
            int signature = System.HashCode.Combine(active, emergency.Fire, (int)(emergency.Warning * 10), emergency.Isolated);
            signature = signature * 31 + Mathf.CeilToInt(session.Opportunity.Value.PlugRemaining) * 7 + (int)journey.Course * 3 + journey.Checkpoints;
            if (signature == warningSignature) return;
            warningSignature = signature;
            string text = string.Empty;
            if (active && emergency.Fire == FirePhase.Warning) text += $"\nSMOKE at POWER — fire in {emergency.Warning:0.0}s";
            if (active && emergency.Fire == FirePhase.Burning) text += emergency.Isolated
                ? "\nFIRE ISOLATED — get extinguisher: R at KITS; spray: E at fire"
                : "\nFIRE — R at POWER isolates damage; R at KITS equips extinguisher";
            if (active && session.Opportunity.Value.PlugRemaining > 0) text += $"\nTEMPORARY PLUG: {session.Opportunity.Value.PlugRemaining:0}s — finish a permanent repair";
            if (active && journey.Course != PassageCourse.Direct) text += journey.Checkpoints < 2
                ? $"\n{journey.Course} passage — visit GATE {journey.Checkpoints + 1}/2 on the map before docking"
                : "\nPassage complete — proceed to port and dock";
            if (text != lastWarning) { warning.text = text; lastWarning = text; }
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
