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
        [MenuItem("Ship Mayham/Upgrade Prototype (Dock Baggage Sorting)")]
        public static void UpgradeDockSorting()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreDockSorting.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreDockSorting.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureDockSorting(Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_DOCK_SORTING_SCENE_READY");
        }

        private static void EnsureDockSorting(PrototypeView view)
        {
            if (view == null || view.CarriedItems.Length != VoyageSimulation.MaxCrew) throw new InvalidOperationException("Four carried-item sockets are required.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + "Suitcase.fbx") == null)
                throw new FileNotFoundException("Suitcase model is required.", GeneratedModels + "Suitcase.fbx");
            var tagMaterial = Material("Luggage_Tag", Color.white);
            foreach (var socket in view.CarriedItems)
            {
                Transform bag = AddProp(socket, "Suitcase", Vector3.zero, .6f);
                Transform tag = bag.Find("Tag_Visual");
                if (tag == null)
                    tag = Primitive("Tag_Visual", bag, PrimitiveType.Cube, Vector3.zero, Vector3.one, tagMaterial);
                tag.localPosition = new Vector3(.22f, .3f, -.14f);
                tag.localScale = new Vector3(.1f, .14f, .025f);
                bag.gameObject.SetActive(false);
            }
        }
    }
}
