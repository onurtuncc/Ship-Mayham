using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    /// <summary>Release-pass scene additions: the gangway gate, swimming ladders and the land activity props. Additive and idempotent.</summary>
    public static partial class PrototypeSceneBuilder
    {
        [MenuItem("Ship Mayham/Upgrade Prototype (Release Props)")]
        public static void UpgradeReleaseProps()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var session = Object.FindAnyObjectByType<PrototypeSession>();
            var view = Object.FindAnyObjectByType<PrototypeView>();
            if (session == null || view == null || session.Harbor == null) throw new InvalidOperationException("A complete prototype scene is required.");
            ApplyGangwayGate(session, view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_RELEASE_PROPS_SCENE_READY");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private static void ApplyGangwayGate(PrototypeSession session, PrototypeView view)
        {
            var harbor = session.Harbor;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + "GangwayGate.fbx");
            if (asset == null) throw new FileNotFoundException("The gangway gate model is missing.", GeneratedModels + "GangwayGate.fbx");
            Transform parent = harbor.ShipGate.Find("Visuals");
            Transform wrapper = parent.Find("Model_GangwayGate");
            if (wrapper == null)
            {
                wrapper = Root("Model_GangwayGate", parent);
                // The plank runs along the deck's x axis: the gate's opening faces west/east.
                wrapper.localPosition = new Vector3(0, -.45f, 0);
                wrapper.localRotation = Quaternion.Euler(0, 90, 0);
                wrapper.localScale = Vector3.one * .9f;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, wrapper);
                model.transform.localPosition = Vector3.zero;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
            var primitive = parent.Find("GangwayMarker_Primitive");
            if (primitive != null && primitive.TryGetComponent<Renderer>(out var marker)) marker.enabled = false;

            var gateView = view.GetComponent<PrototypeGangwayView>() ?? view.gameObject.AddComponent<PrototypeGangwayView>();
            gateView.Session = session;
            gateView.View = view;
            gateView.Gate = harbor.ShipGate;
            gateView.LeafLeft = FindDeep(wrapper, "LeafLeft");
            gateView.LeafRight = FindDeep(wrapper, "LeafRight");
            if (gateView.LeafLeft == null || gateView.LeafRight == null)
                throw new InvalidOperationException("GangwayGate must contain LeafLeft and LeafRight.");
            EditorUtility.SetDirty(gateView);

            // The gate opens by walking into it, so no label may promise a button.
            foreach (var label in harbor.Labels)
            {
                if (label == null) continue;
                if (label.text.StartsWith("ASHORE")) label.text = "ASHORE\nwalk through";
                else if (label.text.Contains("BOARD") && label.text.Contains("E / A")) label.text = label.text.Replace("E / A", "walk through");
            }
        }
    }
}
