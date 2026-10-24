using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        private const string GeneratedModels = "Assets/ShipMayham/Art/Generated/Models/";

        public static void CaptureGeneratedPropsPreview()
        {
            UpgradeGeneratedProps();
            CapturePreview();
        }

        [MenuItem("Ship Mayham/Upgrade Prototype (Generated Props)")]
        public static void UpgradeGeneratedProps()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            string backup = "Logs/ShipPrototype-PreGeneratedProps.unity";
            if (!File.Exists(backup)) File.Copy(ScenePath, backup);
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyGeneratedProps(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_GENERATED_PROPS_SCENE_READY");
        }

        // Visual-only, additive and idempotent. Authored gameplay objects are never deleted.
        public static void ApplyGeneratedProps(PrototypeSession session, PrototypeView view)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Props are authored in Edit Mode only.");
            if (session == null || view == null || session.Harbor == null)
                throw new InvalidOperationException("A complete Phase Four prototype scene is required.");
            string[] models = { "ShipWheel", "TicketCounter", "FoodPlatter", "RumBottle", "Toolbox", "FuseBox", "DeckBench", "PirateShipSmall", "HarborHut", "Bollard", "RopeCoil" };
            foreach (string name in models)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + name + ".fbx") == null)
                    throw new FileNotFoundException("Generated prop is missing.", GeneratedModels + name + ".fbx");

            foreach (var station in session.Stations)
            {
                switch (station.Kind)
                {
                    case StationKind.Helm:
                        ReplaceProp(station.Visuals, "Counter_Primitive", "ShipWheel", Vector3.zero, .8f);
                        break;
                    case StationKind.Manifest:
                        ReplaceProp(station.Visuals, "Counter_Primitive", "TicketCounter", Vector3.zero, .65f);
                        break;
                    case StationKind.Food:
                        AddProp(station.Visuals, "FoodPlatter", new Vector3(0, 1.02f, 0), 1.5f);
                        break;
                    case StationKind.Drink:
                        AddProp(station.Visuals, "RumBottle", new Vector3(0, 1.02f, 0), 1.5f);
                        break;
                    case StationKind.Repair:
                        AddProp(station.Visuals, "Toolbox", new Vector3(0, 1.02f, 0), 1.2f);
                        break;
                    case StationKind.Power:
                        ReplaceProp(station.Visuals, "Counter_Primitive", "FuseBox", Vector3.zero, 1.3f);
                        break;
                }
            }
            foreach (var passenger in session.PassengerAnchors)
                ReplaceProp(passenger.Find("SeatVisuals"), "Seat_Primitive", "DeckBench", Vector3.zero, .65f);

            var combat = view.GetComponent<PrototypeCombatView>();
            if (combat != null && combat.PirateRoot != null)
            {
                Transform visuals = combat.PirateRoot.Find("Visuals");
                foreach (string name in new[] { "Hull_Primitive", "Cabin_Primitive", "Mast_Primitive", "Flag_Primitive" })
                    HidePrimitive(visuals, name);
                AddProp(visuals, "PirateShipSmall", new Vector3(0, -.8f, 0), 1.3f);
            }
            foreach (var port in session.Harbor.PortRoots)
            {
                Transform visuals = port.Find("Visuals");
                ReplaceProp(visuals, "Terminal_Primitive", "HarborHut", new Vector3(-3, .3f, 0), 1.2f);
                AddProp(visuals, "Bollard", new Vector3(-5, .3f, -9), 1);
                AddProp(visuals, "RopeCoil", new Vector3(-4, .3f, -9), 1);
            }
        }

        private static void ReplaceProp(Transform parent, string primitive, string model, Vector3 position, float scale)
        {
            AddProp(parent, model, position, scale);
            HidePrimitive(parent, primitive);
        }

        private static void HidePrimitive(Transform parent, string name)
        {
            var existing = parent != null ? parent.Find(name) : null;
            if (existing != null && existing.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
        }

        private static Transform AddProp(Transform parent, string name, Vector3 position, float scale)
        {
            if (parent == null) throw new InvalidOperationException("Generated prop has no Visuals parent: " + name);
            string containerName = "Model_" + name;
            Transform container = parent.Find(containerName);
            if (container != null) return container;
            container = Root(containerName, parent);
            container.localPosition = position;
            container.localScale = Vector3.one * scale;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + name + ".fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, container);
            // The wrapper owns placement; the imported root retains FBX axis/unit conversion.
            model.transform.localPosition = Vector3.zero;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] palette = renderer.sharedMaterials;
                for (int i = 0; i < palette.Length; i++)
                {
                    Material source = palette[i];
                    if (source == null) throw new InvalidOperationException("Generated prop has a missing material: " + name);
                    if (source.mainTexture != null) continue;   // hand-painted model (docs/art.md): keep its baked texture instead of flattening to a colour
                    Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
                    string materialName = "Generated_" + Regex.Replace(source.name, "[^A-Za-z0-9_-]", "_");
                    palette[i] = Material(materialName, color);
                }
                renderer.sharedMaterials = palette;
            }
            return container;
        }
    }
}
