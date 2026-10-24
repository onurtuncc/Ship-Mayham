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
            ApplyLadders(session);
            EnsureCrewController();
            EnsurePassengerController();
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

        // Rope ladders hang from the ship's rails and the quay edge down into the water; the same positions drive the gameplay (VoyageSimulation.Ladders).
        private static void ApplyLadders(PrototypeSession session)
        {
            var ship = session.ShipRoot;
            for (int i = 0; i < VoyageSimulation.Ladders.Length; i++)
            {
                var ladder = VoyageSimulation.Ladders[i];
                if (ladder.OnQuay)
                {
                    foreach (var port in session.Harbor.PortRoots)
                    {
                        Transform visuals = port.Find("Visuals");
                        if (visuals == null || visuals.Find("RopeLadder_" + i) != null) continue;
                        var holder = Root("RopeLadder_" + i, visuals);
                        // Harbor-local: the quay edge is the east side of the pier area.
                        holder.localPosition = new Vector3(ladder.Top.x + .85f, VoyageSimulation.ShoreHeight + .1f, ladder.Top.y);
                        holder.localRotation = Quaternion.Euler(0, 90, 0);
                        AddProp(holder, "RopeLadder", Vector3.zero, 1);
                    }
                    continue;
                }
                if (ship.Find("RopeLadder_" + i) != null) continue;
                var shipHolder = Root("RopeLadder_" + i, ship);
                // Hung just outside the rail on the side the ladder belongs to.
                Vector2 outward = ladder.Base - ladder.Top;
                outward.Normalize();
                shipHolder.localPosition = new Vector3(ladder.Top.x + outward.x * .75f, 1.1f, ladder.Top.y + outward.y * .75f);
                shipHolder.localRotation = Quaternion.Euler(0, Mathf.Abs(outward.x) > Mathf.Abs(outward.y) ? 90 : 0, 0);
                AddProp(shipHolder, "RopeLadder", Vector3.zero, 1);
            }
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
