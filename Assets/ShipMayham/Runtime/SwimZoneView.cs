using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Everything that makes the harbor bay a place to swim in: a sandy seabed with a few rocks and weeds, the ring of buoys that
    /// marks the edge of the swim zone, and the green-blue look of being under water. Presentation only; installs itself like the
    /// other directors and reads the replicated crew state.
    /// </summary>
    public sealed class SwimZoneView : MonoBehaviour
    {
        private const int BuoyCount = 40, SeabedProps = 90;
        private static readonly Color SandColor = new Color(.82f, .74f, .5f), BuoyRed = new Color(.9f, .2f, .15f), BuoyWhite = new Color(.95f, .95f, .9f);
        private static readonly Color UnderwaterColor = new Color(.04f, .32f, .42f);
        private const float UnderwaterFogDensity = .05f;

        private static SwimZoneView instance;
        private PrototypeSession session;
        private PrototypeView view;
        private float nextLookup;
        private GameObject seabed, buoyRing;
        private Transform[] buoys;
        private bool underwaterApplied;
        private bool savedFog;
        private FogMode savedFogMode;
        private Color savedFogColor;
        private float savedFogDensity;
        private CameraClearFlags savedClear;
        private Color savedBackground;
        private Camera tintedCamera;

        public static SwimZoneView Instance => instance;
        /// <summary>Whether the under-water look is on right now (for tests).</summary>
        public bool UnderwaterLook => underwaterApplied;
        public bool SeabedVisible => seabed != null && seabed.activeSelf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null || Application.isBatchMode && !Application.isEditor) return;
            var go = new GameObject("SwimZoneView") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SwimZoneView>();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            RestoreLook();
            if (seabed != null) Destroy(seabed);
            if (buoyRing != null) Destroy(buoyRing);
        }

        private void LateUpdate()
        {
            if (session == null || view == null)
            {
                if (Time.unscaledTime < nextLookup) return;
                nextLookup = Time.unscaledTime + .25f;
                session = FindAnyObjectByType<PrototypeSession>();
                view = FindAnyObjectByType<PrototypeView>();
                if (session == null || view == null) return;
            }
            bool harbor = session.IsSpawned && !session.InLobby && session.Harbor != null && session.Harbor.IsOpen && session.ShipRoot != null;
            if (harbor && seabed == null) Build();
            if (seabed != null)
            {
                seabed.SetActive(harbor);
                buoyRing.SetActive(harbor);
                if (harbor)
                {
                    Vector3 ship = session.ShipRoot.position;
                    seabed.transform.position = new Vector3(ship.x, VoyageSimulation.SeaLevel - VoyageSimulation.BaySeabedDepth, ship.z);
                    buoyRing.transform.position = new Vector3(ship.x, VoyageSimulation.SeaLevel, ship.z);
                    BobBuoys();
                }
            }
            ApplyUnderwater(session.IsSpawned && view.LocalEyeDepth > .02f ? view.LocalEyeDepth : 0);
        }

        private void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            seabed = new GameObject("Seabed");
            seabed.hideFlags = HideFlags.HideAndDontSave;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "SeabedFloor";
            Destroy(floor.GetComponent<Collider>());
            floor.transform.SetParent(seabed.transform, false);
            floor.transform.localRotation = Quaternion.Euler(90, 0, 0);
            floor.transform.localScale = Vector3.one * (VoyageSimulation.SwimZoneRadius * 2 + 80);
            floor.GetComponent<Renderer>().sharedMaterial = Lit(shader, SandColor);
            var rockMaterial = Lit(shader, new Color(.5f, .48f, .44f));
            var weedMaterial = Lit(shader, new Color(.15f, .5f, .25f));
            for (int i = 0; i < SeabedProps; i++)
            {
                uint h = (uint)(i * 2654435761u + 977u);
                float angle = (h & 1023) / 1023f * Mathf.PI * 2, radius = 8 + ((h >> 10) & 1023) / 1023f * (VoyageSimulation.SwimZoneRadius + 20);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                if (VoyageSimulation.QuaySlab.Contains(new Vector2(position.x, position.z))) continue;
                bool rock = (h >> 20 & 3) != 0;
                var prop = GameObject.CreatePrimitive(rock ? PrimitiveType.Sphere : PrimitiveType.Capsule);
                Destroy(prop.GetComponent<Collider>());
                prop.transform.SetParent(seabed.transform, false);
                float size = .6f + (h >> 24 & 7) * .25f;
                prop.transform.localPosition = position + Vector3.up * (rock ? size * .2f : size);
                prop.transform.localScale = rock ? new Vector3(size * 1.6f, size * .8f, size * 1.3f) : new Vector3(.25f, size * 1.2f, .25f);
                prop.GetComponent<Renderer>().sharedMaterial = rock ? rockMaterial : weedMaterial;
            }
            buoyRing = new GameObject("SwimZoneBuoys") { hideFlags = HideFlags.HideAndDontSave };
            buoys = new Transform[BuoyCount];
            var red = Lit(shader, BuoyRed);
            var white = Lit(shader, BuoyWhite);
            for (int i = 0; i < BuoyCount; i++)
            {
                float angle = i * Mathf.PI * 2 / BuoyCount;
                var buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(buoy.GetComponent<Collider>());
                buoy.name = "Buoy" + i;
                buoy.transform.SetParent(buoyRing.transform, false);
                buoy.transform.localPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * VoyageSimulation.SwimZoneRadius;
                buoy.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);
                buoy.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? red : white;
                buoys[i] = buoy.transform;
            }
        }

        private static Material Lit(Shader shader, Color color)
        {
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            return material;
        }

        private void BobBuoys()
        {
            float time = Time.time;
            for (int i = 0; i < buoys.Length; i++)
            {
                var p = buoys[i].localPosition;
                p.y = Mathf.Sin(time * 1.3f + i * .7f) * .18f + .1f;
                buoys[i].localPosition = p;
            }
        }

        private void ApplyUnderwater(float eyeDepth)
        {
            if (eyeDepth <= 0)
            {
                if (underwaterApplied) RestoreLook();
                return;
            }
            var camera = view.Camera;
            if (camera == null) return;
            if (!underwaterApplied)
            {
                savedFog = RenderSettings.fog; savedFogMode = RenderSettings.fogMode; savedFogColor = RenderSettings.fogColor; savedFogDensity = RenderSettings.fogDensity;
                tintedCamera = camera; savedClear = camera.clearFlags; savedBackground = camera.backgroundColor;
                underwaterApplied = true;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = UnderwaterColor;
            RenderSettings.fogDensity = UnderwaterFogDensity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UnderwaterColor;
        }

        private void RestoreLook()
        {
            if (!underwaterApplied) return;
            underwaterApplied = false;
            RenderSettings.fog = savedFog; RenderSettings.fogMode = savedFogMode; RenderSettings.fogColor = savedFogColor; RenderSettings.fogDensity = savedFogDensity;
            if (tintedCamera != null) { tintedCamera.clearFlags = savedClear; tintedCamera.backgroundColor = savedBackground; }
            tintedCamera = null;
        }
    }
}
