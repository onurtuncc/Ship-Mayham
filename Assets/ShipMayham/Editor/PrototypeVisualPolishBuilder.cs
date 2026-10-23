using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        private const string SkyMaterialPath = MaterialsPath + "/Sky_Cartoon.mat";

        [MenuItem("Ship Mayham/Upgrade Prototype (Visual Polish)")]
        public static void UpgradeVisualPolish()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreVisualPolish.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreVisualPolish.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyVisualPolish(Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_VISUAL_POLISH_SCENE_READY");
        }

        // Absolute, idempotent values: running it twice leaves the scene unchanged.
        private static void ApplyVisualPolish(PrototypeView view)
        {
            if (view == null) throw new InvalidOperationException("A complete prototype scene is required.");
            SeparateTownSurfaces();
            ApplyCartoonSky(view);
        }

        // Crosswalks, welcome mats and the promenade are all thin slabs; sharing a top face makes them z-fight.
        // Each layer sits a clear 16 mm above the one under it (well above depth precision at the camera ranges used).
        // The pier (top .30, east edge at the quay) and the stone foundation under the town used to share both the top and the
        // quay-edge face, so the boarding spot at the gangway flickered between planks and stone.
        private static readonly Vector3 QuayFoundationPosition = new Vector3(-14.025f, -.1f, 0), QuayFoundationScale = new Vector3(39.95f, .76f, 48);

        private static void SeparateTownSurfaces()
        {
            const float promenadeTop = .36f, crosswalkTop = promenadeTop + .016f, matTop = crosswalkTop + .016f;
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == "QuayFoundation")
                {
                    transform.localPosition = QuayFoundationPosition;
                    transform.localScale = QuayFoundationScale;
                    EditorUtility.SetDirty(transform);
                }
                else if (transform.name.StartsWith("Crosswalk_"))
                {
                    var p = transform.localPosition;
                    transform.localPosition = new Vector3(p.x, crosswalkTop - transform.localScale.y / 2, p.z);
                    EditorUtility.SetDirty(transform);
                }
                else if (transform.name == "ShopWelcomeMat" && transform.parent != null)
                {
                    float worldBottom = matTop - transform.lossyScale.y / 2;
                    var world = transform.position;
                    transform.position = new Vector3(world.x, worldBottom, world.z);
                    EditorUtility.SetDirty(transform);
                }
            }
        }

        private static void ApplyCartoonSky(PrototypeView view)
        {
            var shader = Shader.Find("ShipMayham/CartoonSky");
            if (shader == null) throw new InvalidOperationException("The CartoonSky shader is missing.");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(shader) { name = "Sky_Cartoon" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            sky.shader = shader;
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.74f, .9f, .98f);
            RenderSettings.fogStartDistance = 90;
            RenderSettings.fogEndDistance = 420;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .7f, .78f);
            var sun = GameObject.Find("Lighting_Sun");
            if (sun != null)
            {
                var light = sun.GetComponent<Light>();
                light.color = new Color(1f, .96f, .86f);
                light.intensity = 1.7f;
                RenderSettings.sun = light;
                sky.SetVector("_SunDirection", -light.transform.forward);
            }
            view.Camera.clearFlags = CameraClearFlags.Skybox;
            EditorUtility.SetDirty(sky);
        }
    }
}
