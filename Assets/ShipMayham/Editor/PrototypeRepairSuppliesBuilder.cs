using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        [MenuItem("Ship Mayham/Upgrade Prototype (Repair Supplies)")]
        public static void UpgradeRepairSupplies()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreRepairSupplies.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreRepairSupplies.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureRepairSupplies(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            PrepareLabels(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_REPAIR_SUPPLIES_SCENE_READY");
        }

        private static void EnsureRepairSupplies(PrototypeSession session, PrototypeView view)
        {
            if (session == null || view == null || session.ShipRoot == null)
                throw new InvalidOperationException("Prototype session, view and ship root are required.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + "Toolbox.fbx") == null)
                throw new FileNotFoundException("Toolbox model required.", GeneratedModels + "Toolbox.fbx");
            var stations = new List<PrototypeStation>(session.Stations);
            var store = stations.Find(s => s.Kind == StationKind.RepairSupply);
            var bench = stations.Find(s => s.Kind == StationKind.Repair);
            if (bench == null) throw new InvalidOperationException("Repair station required.");
            if (store == null)
            {
                store = Station(session.ShipRoot.Find("Stations"), StationKind.RepairSupply, "Take / return quick patch kit",
                    new Vector2(4, -5.5f), Material("Repair_Supply", new Color(.16f, .55f, .45f)));
                stations.Add(store);
                session.Stations = stations.ToArray();
            }
            var presentation = view.GetComponent<PrototypeRepairSupplyView>();
            if (presentation == null) presentation = view.gameObject.AddComponent<PrototypeRepairSupplyView>();
            presentation.Session = session; presentation.Store = store; presentation.Bench = bench;
            presentation.StoredKits = new Transform[VoyageSimulation.VoyageRepairKits];
            presentation.ReadyKits = new Transform[VoyageSimulation.RepairKitBufferCapacity];
            for (int i = 0; i < presentation.StoredKits.Length; i++)
                presentation.StoredKits[i] = RepairKitProp(store.Visuals, "StoredKit_" + i, new Vector3(-.35f + i * .35f, 1.02f, 0), .65f);
            for (int i = 0; i < presentation.ReadyKits.Length; i++)
            {
                presentation.ReadyKits[i] = RepairKitProp(bench.Visuals, "ReadyKit_" + i, new Vector3(-.35f + i * .7f, 1.02f, -.35f), .65f);
                presentation.ReadyKits[i].gameObject.SetActive(false);
            }
            foreach (var socket in view.CarriedItems)
            {
                Transform kit = AddProp(socket, "Toolbox", Vector3.zero, .7f);
                kit.localScale = Vector3.one * .7f;
                kit.gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(session); EditorUtility.SetDirty(presentation);
        }

        private static Transform RepairKitProp(Transform parent, string name, Vector3 position, float scale)
        {
            Transform root = parent.Find(name);
            if (root == null) { root = Root(name, parent); root.localPosition = position; }
            AddProp(root, "Toolbox", Vector3.zero, scale);
            return root;
        }
    }
}
