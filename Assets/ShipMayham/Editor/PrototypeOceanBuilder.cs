using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        private const string OceanMaterialPath = "Assets/ShipMayham/Art/Materials/Ocean_Stylized.mat";
        private const string OceanObjectName = "Ocean_Stylized";
        private const float OceanSeaLevel = -1.05f;

        [MenuItem("Ship Mayham/Upgrade Prototype (Stylized Ocean)")]
        public static void UpgradeStylizedOcean()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            string backup = "Logs/ShipPrototype-PreStylizedOcean.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyStylizedOcean(Object.FindAnyObjectByType<PrototypeSession>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_STYLIZED_OCEAN_SCENE_READY");
        }

        /// <summary>
        /// Visual-only, additive and idempotent: the primitive sea box keeps its transform and collider but stops rendering,
        /// and a generated-grid ocean follows the ship. Nothing authored by gameplay is deleted.
        /// </summary>
        public static Transform ApplyStylizedOcean(PrototypeSession session)
        {
            if (Application.isPlaying) throw new InvalidOperationException("The ocean is authored in Edit Mode only.");
            if (session == null || session.ShipRoot == null) throw new InvalidOperationException("A complete prototype scene with a ship is required.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
            if (material == null) throw new FileNotFoundException("Stylized ocean material is missing.", OceanMaterialPath);

            Transform legacy = null;
            foreach (var candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == "Ocean_Visual") { legacy = candidate; break; }
            if (legacy == null) throw new InvalidOperationException("Ocean_Visual was not found; the prototype environment is incomplete.");

            Transform existing = legacy.parent != null ? legacy.parent.Find(OceanObjectName) : null;
            Transform ocean = existing != null ? existing : CreateOceanObject(legacy.parent);
            ocean.position = new Vector3(0, OceanSeaLevel, legacy.position.z);
            ocean.GetComponent<MeshRenderer>().sharedMaterial = material;
            ocean.GetComponent<OceanSurface>().Follow = session.ShipRoot;
            if (legacy.TryGetComponent<Renderer>(out var legacyRenderer)) legacyRenderer.enabled = false;
            EditorUtility.SetDirty(ocean.gameObject);
            return ocean;
        }

        private static Transform CreateOceanObject(Transform parent)
        {
            var go = new GameObject(OceanObjectName, typeof(MeshFilter), typeof(MeshRenderer), typeof(OceanSurface));
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Edit-mode mesh so the scene renders without entering Play Mode; Awake rebuilds it at runtime.
            go.GetComponent<OceanSurface>().Rebuild();
            return go.transform;
        }
    }
}
