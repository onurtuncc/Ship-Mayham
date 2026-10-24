using UnityEngine;
using UnityEngine.UI;

namespace ShipMayham
{
    /// <summary>Presentation of host-owned navigation hazards. Created by the existing first-person view.</summary>
    public sealed class PrototypeNavigationView : MonoBehaviour
    {
        public PrototypeView View;
        private readonly Transform[] rocks = new Transform[VoyageSimulation.ShoalCount];
        private Transform wave;
        private Material material;
        private Text warning;
        private string lastWarning;

        private void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) { Debug.LogWarning("Navigation visuals need the URP Lit shader."); return; }
            material = new Material(shader) { color = new Color(.28f, .35f, .3f) };
            for (int i = 0; i < rocks.Length; i++) rocks[i] = Create("Navigation_Shoal_" + (i + 1), PrimitiveType.Sphere, new Vector3(18, 3, 18));
            wave = Create("Navigation_RogueWave", PrimitiveType.Cube, new Vector3(45, 2, 3));
            var canvasRoot = new GameObject("Navigation_Status", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            warning = HudKit.Label(canvasRoot.transform, "NavigationWarning", 23, Color.yellow, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudKit.Anchor(warning.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(740, 60));
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
            var n = session.Navigation.Value; var s = session.Voyage.Value;
            bool nearby = false;
            for (int i = 0; i < rocks.Length; i++)
            {
                rocks[i].gameObject.SetActive(active);
                Vector2 p = VoyageSimulation.ShoalPosition(n.Seed, i);
                rocks[i].position = new Vector3(p.x, -1, p.y);
                nearby |= Vector2.Distance(s.Position, p) < 30;
            }
            wave.gameObject.SetActive(active && n.WaveActive);
            if (n.WaveActive)
            {
                Quaternion rotation = Quaternion.Euler(0, n.WaveHeading, 0);
                wave.SetPositionAndRotation(new Vector3(s.Position.x, 0, s.Position.y) + rotation * Vector3.forward * (8 + n.Warning * 6), rotation);
            }
            string text = !active ? string.Empty : n.WaveActive
                ? $"WAVE IN {n.Warning:0.0}s — face {n.WaveHeading:0}° to ride it!"
                : nearby ? "SHOAL AHEAD — steer clear or slow below 1 m/s" : string.Empty;
            if (text != lastWarning) { warning.text = text; lastWarning = text; }
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
