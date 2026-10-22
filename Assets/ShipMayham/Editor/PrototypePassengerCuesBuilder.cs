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
        [MenuItem("Ship Mayham/Upgrade Prototype (Passenger Cues)")]
        public static void UpgradePassengerCues()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PrePassengerCues.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PrePassengerCues.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsurePassengerCues(Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_PASSENGER_CUES_SCENE_READY");
        }

        private static void EnsurePassengerCues(PrototypeView view)
        {
            if (view == null || view.PassengerVisuals.Length != VoyageSimulation.MaxPassengers)
                throw new InvalidOperationException("Eight authored passenger Visuals are required.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + "Suitcase.fbx") == null)
                throw new FileNotFoundException("Passenger suitcase model is missing.", GeneratedModels + "Suitcase.fbx");
            var cues = view.GetComponent<PrototypePassengerCueView>();
            if (cues == null) cues = view.gameObject.AddComponent<PrototypePassengerCueView>();
            cues.View = view;
            cues.Bags = new Transform[view.PassengerVisuals.Length];
            cues.Bodies = new Transform[view.PassengerVisuals.Length];
            for (int i = 0; i < view.PassengerVisuals.Length; i++)
            {
                cues.Bags[i] = AddProp(view.PassengerVisuals[i], "Suitcase", new Vector3(-.48f, 0, .05f), .85f);
                cues.Bodies[i] = view.PassengerVisuals[i].Find("Passenger_Primitive");
                if (cues.Bodies[i] == null) throw new InvalidOperationException("Passenger body visual is missing: " + i);
                cues.Bags[i].gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(cues);
        }
    }
}
