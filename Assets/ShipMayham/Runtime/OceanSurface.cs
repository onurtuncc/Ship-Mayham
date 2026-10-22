using UnityEngine;
using UnityEngine.Rendering;

namespace ShipMayham
{
    /// <summary>Builds the flat grid mesh used by the stylized ocean shader. Pure and allocation-bounded so it can be tested.</summary>
    public static class OceanGridMesh
    {
        public const int MinCells = 4;
        public const int MaxCells = 400;

        /// <summary>Square grid on the XZ plane centred on the origin: (cells + 1)^2 vertices, 2 * cells^2 triangles.</summary>
        public static Mesh Build(int cells, float extent, float boundsHeight)
        {
            if (cells < MinCells || cells > MaxCells) throw new System.ArgumentOutOfRangeException(nameof(cells), cells, $"Ocean grid needs {MinCells}-{MaxCells} cells.");
            if (extent <= 0f || float.IsNaN(extent) || float.IsInfinity(extent)) throw new System.ArgumentOutOfRangeException(nameof(extent), extent, "Ocean extent must be a positive finite size.");
            int side = cells + 1;
            var vertices = new Vector3[side * side];
            var normals = new Vector3[vertices.Length];
            float step = extent / cells, half = extent * .5f;
            for (int z = 0; z < side; z++)
                for (int x = 0; x < side; x++)
                {
                    int index = z * side + x;
                    vertices[index] = new Vector3(x * step - half, 0, z * step - half);
                    normals[index] = Vector3.up;
                }
            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int z = 0; z < cells; z++)
                for (int x = 0; x < cells; x++)
                {
                    int a = z * side + x, b = a + 1, c = a + side, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            var mesh = new Mesh { name = $"OceanGrid_{cells}x{cells}", indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            // Vertex displacement happens in the shader, so the CPU bounds must be padded or the sea pops out of view.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(extent, boundsHeight * 2f, extent));
            return mesh;
        }

        /// <summary>Snap to the grid so world-space shader patterns do not swim while the sea follows the ship.</summary>
        public static float Snap(float value, float step) => step <= 0f ? value : Mathf.Round(value / step) * step;
    }

    /// <summary>
    /// Stylized sea surface. Generates its grid mesh at runtime (nothing to author), follows a target on XZ,
    /// and leaves wave motion to the StylizedOcean shader. Visual only: gameplay never reads this height.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class OceanSurface : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional: the sea re-centres under this transform (ship). Leave empty for a fixed sea.")] private Transform follow;
        [SerializeField, Range(OceanGridMesh.MinCells, OceanGridMesh.MaxCells)] private int cells = 160;
        [SerializeField, Min(10f), Tooltip("Side length of the square sea in metres.")] private float extent = 640f;
        [SerializeField, Min(0f), Tooltip("Largest swell height the shader may add; only pads culling bounds.")] private float boundsPadding = 1.5f;

        private Mesh mesh;
        private MeshFilter meshFilter;

        public Transform Follow { get => follow; set => follow = value; }
        public Mesh Mesh => mesh;

        // The generated mesh is never serialized (HideFlags.DontSave), so rebuild whenever the component (re)enables:
        // scene load, domain reload and entering Play Mode all pass through here.
        private void OnEnable() => EnsureMesh();

        private void OnValidate()
        {
            cells = Mathf.Clamp(cells, OceanGridMesh.MinCells, OceanGridMesh.MaxCells);
            if (extent < 10f) extent = 10f;
        }

        private void EnsureMesh()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (mesh == null || meshFilter.sharedMesh != mesh) Rebuild();
        }

        [ContextMenu("Rebuild Ocean Mesh")]
        public void Rebuild()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            DestroyMesh();
            mesh = OceanGridMesh.Build(cells, extent, boundsPadding);
            mesh.hideFlags = HideFlags.DontSave;
            meshFilter.sharedMesh = mesh;
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || follow == null) return;   // never move the sea while editing: no scene dirtying
            float step = extent / cells * 4f;
            Vector3 p = transform.position;
            p.x = OceanGridMesh.Snap(follow.position.x, step);
            p.z = OceanGridMesh.Snap(follow.position.z, step);
            transform.position = p;
        }

        private void OnDestroy() => DestroyMesh();

        private void DestroyMesh()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
