using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Draws the shoals of the running voyage: chunky rounded rock lumps ringed by pale turquoise foam. Purely presentational:
    /// it derives the layout from the replicated seed and never touches the voyage. Installs itself like the other directors.
    /// </summary>
    public sealed class HazardView : MonoBehaviour
    {
        private const float LookupSeconds = .25f;
        private static readonly Color[] RockColors =
        {
            new Color(.62f, .55f, .47f), new Color(.52f, .5f, .46f), new Color(.7f, .6f, .46f)
        };
        private static readonly Color FoamColor = new Color(.7f, .95f, .93f);

        private static HazardView instance;
        private readonly List<GameObject> built = new List<GameObject>();
        private PrototypeSession session;
        private float nextLookup;
        private GameObject root;
        private uint builtSeed;
        private RouteProfile builtRoute;
        private bool hasBuilt;
        private MaterialPropertyBlock block;

        /// <summary>The installed view, or null before the first scene has loaded (for tests and tools).</summary>
        public static HazardView Instance => instance;

        /// <summary>Number of shoals currently drawn (for tests and tools).</summary>
        public int DrawnShoals { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null || Application.isBatchMode && !Application.isEditor) return;
            var go = new GameObject("HazardView") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HazardView>();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            Clear();
        }

        private void LateUpdate()
        {
            if (session == null && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + LookupSeconds;
                session = FindAnyObjectByType<PrototypeSession>();
            }
            bool show = session != null && session.IsSpawned && !session.InLobby && session.Voyage.Value.Phase == VoyagePhase.Sailing;
            if (!show) { Clear(); return; }
            var hazards = session.Hazards.Value;
            var route = session.Voyage.Value.Route;
            if (hasBuilt && builtSeed == hazards.Seed && builtRoute == route) return;
            Build(hazards.Seed, route);
        }

        private void Build(uint seed, RouteProfile route)
        {
            Clear();
            block = block ?? new MaterialPropertyBlock();
            root = new GameObject("Shoals") { hideFlags = HideFlags.HideAndDontSave };
            int count = ShoalSet.For(seed, route);
            for (int i = 0; i < count; i++) BuildShoal(ShoalSet.Shoals[i], i);
            builtSeed = seed;
            builtRoute = route;
            hasBuilt = true;
            DrawnShoals = count;
        }

        private void BuildShoal(Shoal shoal, int index)
        {
            var holder = new GameObject("Shoal" + index) { hideFlags = HideFlags.HideAndDontSave };
            holder.transform.SetParent(root.transform, false);
            holder.transform.position = new Vector3(shoal.Center.x, 0, shoal.Center.y);

            float r = shoal.Radius;
            AddPart(holder, "Foam", new Vector3(0, .12f, 0), new Vector3(r * 2.7f, .06f, r * 2.7f), FoamColor);
            AddPart(holder, "Dome", new Vector3(0, -.2f, 0), new Vector3(r * 1.5f, r * .5f, r * 1.5f), RockColors[index % RockColors.Length]);
            for (int lump = 0; lump < 3; lump++)
            {
                float angle = (index * 1.7f + lump * 2.1f) % (Mathf.PI * 2);
                var offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * r * .55f;
                float size = r * (.55f + .12f * lump);
                AddPart(holder, "Lump" + lump, offset + new Vector3(0, -.1f, 0), new Vector3(size, size * .75f, size), RockColors[(index + lump + 1) % RockColors.Length]);
            }
        }

        private void AddPart(GameObject holder, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.hideFlags = HideFlags.HideAndDontSave;
            DestroyUnity(part.GetComponent<Collider>());
            part.transform.SetParent(holder.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            built.Add(part);
        }

        private void Clear()
        {
            if (root != null) DestroyUnity(root);
            root = null;
            built.Clear();
            hasBuilt = false;
            DrawnShoals = 0;
        }

        private static void DestroyUnity(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
