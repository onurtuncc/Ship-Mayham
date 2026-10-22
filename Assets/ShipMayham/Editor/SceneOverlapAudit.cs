using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipMayham.Editor
{
    /// <summary>
    /// Lists pairs of visible renderers whose top or bottom faces sit within a few millimetres of each other while their footprints
    /// overlap. Those pairs z-fight (flicker, shimmer) at distance. Writes Logs/OverlapAudit.txt; never modifies the scene.
    /// </summary>
    public static class SceneOverlapAudit
    {
        private const float PlaneTolerance = .01f;
        private const float MinOverlapArea = .04f;
        private const float MaxSlabThickness = .6f;

        [MenuItem("Ship Mayham/Audit Coplanar Surfaces")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/ShipMayham/Scenes/ShipPrototype.unity", OpenSceneMode.Additive);
            try
            {
                var renderers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(false))
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>() != null && !(r.GetComponent<TextMesh>() != null)).ToList();
                var report = new StringBuilder();
                int found = 0;
                for (int i = 0; i < renderers.Count; i++)
                    for (int j = i + 1; j < renderers.Count; j++)
                    {
                        var a = renderers[i]; var b = renderers[j];
                        if (a.sharedMaterial == b.sharedMaterial && a.sharedMaterial != null && Same(a, b)) continue;
                        if (!TryCoplanar(a.bounds, b.bounds, out string where, out float overlap)) continue;
                        found++;
                        report.AppendLine($"{where} overlap={overlap:0.00} m2 :: {Path(a)} [{Name(a)}]  <->  {Path(b)} [{Name(b)}]");
                    }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/OverlapAudit.txt", $"{found} coplanar pairs{System.Environment.NewLine}{report}");
                Debug.Log($"SHIP_OVERLAP_AUDIT {found} pairs, see Logs/OverlapAudit.txt");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static bool Same(MeshRenderer a, MeshRenderer b) => a.transform.parent == b.transform.parent && a.name == b.name;

        private static string Name(Renderer r) => r.sharedMaterial != null ? r.sharedMaterial.name : "no-material";

        private static string Path(Component c)
        {
            var names = new List<string>();
            for (var t = c.transform; t != null && names.Count < 4; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static bool TryCoplanar(Bounds a, Bounds b, out string where, out float overlap)
        {
            where = null; overlap = 0;
            float ox = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            float oz = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            if (ox <= 0 || oz <= 0 || ox * oz < MinOverlapArea) return false;
            overlap = ox * oz;
            bool slabA = a.size.y < MaxSlabThickness, slabB = b.size.y < MaxSlabThickness;
            if ((slabA || slabB) && Mathf.Abs(a.max.y - b.max.y) < PlaneTolerance && (slabA && slabB)) { where = $"TOP y={a.max.y:0.000}"; return true; }
            if (slabA && slabB && Mathf.Abs(a.min.y - b.min.y) < PlaneTolerance) { where = $"BOTTOM y={a.min.y:0.000}"; return true; }
            if (slabA && Mathf.Abs(a.max.y - b.max.y) < PlaneTolerance && b.size.y >= MaxSlabThickness) { where = $"TOP-IN-SOLID y={a.max.y:0.000}"; return true; }
            if (slabB && Mathf.Abs(a.max.y - b.max.y) < PlaneTolerance && a.size.y >= MaxSlabThickness) { where = $"TOP-IN-SOLID y={b.max.y:0.000}"; return true; }
            return false;
        }
    }
}
