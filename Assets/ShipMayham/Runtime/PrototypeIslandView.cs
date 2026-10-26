using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    /// <summary>
    /// Dresses the two harbor towns as islands: the ground and the scenery around the town platform, and the mounds, holes, chests
    /// and fishing rods of the spots out on the island. Each town root shows the island the voyage chain puts there (the departure
    /// island or the destination); when the chain moves, the root is rebuilt for the new island from cached meshes.
    /// Presentation only: what can be walked on is decided by <see cref="IslandTerrain"/> alone.
    /// </summary>
    public sealed class PrototypeIslandView : MonoBehaviour
    {
        private sealed class Spot
        {
            public MiniGameKind Kind;
            public int Index;
            public GameObject Prop, Alternate;
        }

        private sealed class Slot
        {
            public Transform Root;
            public int Island = -1;
            public GameObject Ground, Decor;
            public readonly List<Spot> Spots = new List<Spot>();
        }

        private static readonly string[] SeaWallNames = { "SeaWall_0", "SeaWall_1", "WestSeaWall" };

        private static readonly Dictionary<int, Mesh> GroundMeshes = new Dictionary<int, Mesh>(), DecorMeshes = new Dictionary<int, Mesh>();
        private static readonly Dictionary<IslandTheme, Material> GroundMaterials = new Dictionary<IslandTheme, Material>();
        private static Material decorMaterial;

        private readonly Slot[] slots = { new Slot(), new Slot() };

        /// <summary>The island each town root currently shows (for tests).</summary>
        public int IslandOf(int slot) => slots[slot].Island;

        public int SpotProps(int slot) => slots[slot].Spots.Count;

        public bool GroundVisible(int slot) => slots[slot].Ground != null && slots[slot].Ground.activeInHierarchy;

        /// <summary>Called every frame by the harbor after it has placed the town roots.</summary>
        public void Present(Transform[] roots, in VoyageState voyage)
        {
            if (roots == null || roots.Length < 2) return;
            int dockedRoot = voyage.Phase == VoyagePhase.Arrived ? 1 : 0;
            for (int k = 0; k < 2; k++)
            {
                var slot = slots[k];
                slot.Root = roots[k];
                if (slot.Root == null) continue;
                int island = k == 0 ? voyage.Island : voyage.Target;
                if (slot.Island != island) Rebuild(slot, island);
                UpdateSpots(slot, k == dockedRoot, voyage);
            }
        }

        // ---------------------------------------------------------------- building

        private static Material GroundMaterial(IslandTheme theme)
        {
            if (GroundMaterials.TryGetValue(theme, out var cached) && cached != null) return cached;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "IslandGround_" + theme, hideFlags = HideFlags.HideAndDontSave };
            var palette = IslandMeshes.BuildGroundPalette(theme);
            palette.hideFlags = HideFlags.HideAndDontSave;
            material.SetTexture("_BaseMap", palette);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0f);
            return GroundMaterials[theme] = material;
        }

        private static Material DecorMaterial()
        {
            if (decorMaterial != null) return decorMaterial;
            decorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "IslandDecor", hideFlags = HideFlags.HideAndDontSave };
            var palette = IslandMeshes.BuildDecorPalette();
            palette.filterMode = FilterMode.Point;
            palette.hideFlags = HideFlags.HideAndDontSave;
            decorMaterial.SetTexture("_BaseMap", palette);
            decorMaterial.SetColor("_BaseColor", Color.white);
            decorMaterial.SetFloat("_Smoothness", 0f);
            return decorMaterial;
        }

        private static Mesh Cached(Dictionary<int, Mesh> cache, int island, System.Func<IslandTerrain, Mesh> build)
        {
            if (cache.TryGetValue(island, out var mesh) && mesh != null) return mesh;
            mesh = build(IslandTerrain.For(island));
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return cache[island] = mesh;
        }

        private static GameObject Layer(Transform root, string name)
        {
            var existing = root.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private void Rebuild(Slot slot, int island)
        {
            slot.Island = island;
            var terrain = IslandTerrain.For(island);
            slot.Ground = Layer(slot.Root, "IslandGround");
            slot.Ground.GetComponent<MeshFilter>().sharedMesh = Cached(GroundMeshes, terrain.Island, IslandMeshes.BuildTerrain);
            slot.Ground.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial(terrain.Theme);
            slot.Decor = Layer(slot.Root, "IslandDecor");
            slot.Decor.GetComponent<MeshFilter>().sharedMesh = Cached(DecorMeshes, terrain.Island, IslandMeshes.BuildDecor);
            slot.Decor.GetComponent<MeshRenderer>().sharedMaterial = DecorMaterial();
            // The sea walls fenced the platform from the sea; the island's beach continues past them.
            var town = slot.Root.Find("Town_Visuals");
            if (town != null) foreach (var wall in SeaWallNames) { var t = town.Find(wall); if (t != null) t.gameObject.SetActive(false); }
            BuildSpots(slot, terrain);
        }

        private static Transform Template(Transform root, string name) => root.Find("Visuals/" + name);

        private void BuildSpots(Slot slot, IslandTerrain terrain)
        {
            foreach (var spot in slot.Spots)
            {
                if (spot.Prop != null) Destroy(spot.Prop);
                if (spot.Alternate != null) Destroy(spot.Alternate);
            }
            slot.Spots.Clear();
            Transform mound = Template(slot.Root, "DigMound_0"), hole = Template(slot.Root, "DigHole_0"),
                chest = Template(slot.Root, "BuriedChest_0"), rod = Template(slot.Root, "FishingRod_0");
            AddSpots(slot, terrain, MiniGameKind.Dig, mound, hole);
            AddSpots(slot, terrain, MiniGameKind.Lockpick, chest, null);
            AddSpots(slot, terrain, MiniGameKind.Fishing, rod, null);
        }

        private static void AddSpots(Slot slot, IslandTerrain terrain, MiniGameKind kind, Transform prop, Transform alternate)
        {
            if (prop == null) return;
            var all = VoyageSimulation.SpotsOf(kind, terrain.Island);
            int town = VoyageSimulation.SpotsOf(kind).Length;
            for (int i = town; i < all.Length; i++)
            {
                var spot = new Spot { Kind = kind, Index = i };
                spot.Prop = Place(prop, all[i], terrain, kind == MiniGameKind.Dig ? "DigMound_" : kind == MiniGameKind.Lockpick ? "BuriedChest_" : "FishingRod_", i);
                if (alternate != null) spot.Alternate = Place(alternate, all[i], terrain, "DigHole_", i);
                slot.Spots.Add(spot);
            }
        }

        private static GameObject Place(Transform template, Vector2 spot, IslandTerrain terrain, string name, int index)
        {
            var clone = Instantiate(template.gameObject, template.parent);
            clone.name = name + index;
            clone.transform.localPosition = new Vector3(spot.x, terrain.Height(spot), spot.y);
            clone.SetActive(true);
            return clone;
        }

        // ---------------------------------------------------------------- state

        private static void UpdateSpots(Slot slot, bool docked, in VoyageState voyage)
        {
            foreach (var spot in slot.Spots)
            {
                bool used = docked && (spot.Kind == MiniGameKind.Dig ? (voyage.DigMask & (1 << spot.Index)) != 0 :
                    spot.Kind == MiniGameKind.Lockpick && (voyage.ChestMask & (1 << spot.Index)) != 0);
                if (spot.Prop != null) spot.Prop.SetActive(spot.Kind == MiniGameKind.Fishing || !used);
                if (spot.Alternate != null) spot.Alternate.SetActive(used);
            }
        }
    }
}
