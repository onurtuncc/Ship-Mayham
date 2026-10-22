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
        private const string FeedbackRootName = "Feedback_Fx";

        [MenuItem("Ship Mayham/Upgrade Prototype (Game Feel)")]
        public static void UpgradeGameFeel()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreGameFeel.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreGameFeel.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureGameFeel(Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_GAME_FEEL_SCENE_READY");
        }

        // Cosmetic components only; gameplay roots, anchors and state are never touched. Idempotent.
        private static void EnsureGameFeel(PrototypeView view)
        {
            if (view == null) throw new InvalidOperationException("A complete prototype scene is required.");
            var existingRoot = GameObject.Find(FeedbackRootName);
            Transform root = existingRoot != null ? existingRoot.transform : Root(FeedbackRootName);
            var fx = root.GetComponent<FeedbackFx>();
            if (fx == null) fx = root.gameObject.AddComponent<FeedbackFx>();
            fx.ParticleMaterial = EnsureParticleMaterial();
            fx.ViewCamera = view.Camera;
            Transform template = root.Find("PopupTemplate");
            if (template == null)
            {
                var label = Label("PopupTemplate", root, Vector3.zero, "+0", .05f);
                label.fontStyle = FontStyle.Bold;
                template = label.transform;
            }
            template.gameObject.SetActive(false);
            fx.PopupTemplate = template.GetComponent<TextMesh>();
            EditorUtility.SetDirty(fx);

            var feedback = view.GetComponent<PrototypeFeedback>();
            if (feedback == null) feedback = view.gameObject.AddComponent<PrototypeFeedback>();
            feedback.View = view; feedback.Fx = fx; feedback.PassengerAnimator = view.GetComponent<PrototypePassengerAnimator>();
            EditorUtility.SetDirty(feedback);

            var ambient = view.GetComponent<PrototypeAmbientLife>();
            if (ambient == null) ambient = view.gameObject.AddComponent<PrototypeAmbientLife>();
            ambient.View = view; ambient.Fx = fx;
            ambient.GullMaterial = Material("Gull_White", new Color(.96f, .97f, 1f));
            EditorUtility.SetDirty(ambient);
        }

        private static Material EnsureParticleMaterial()
        {
            string path = MaterialsPath + "/Fx_Particle.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) throw new InvalidOperationException("The URP particle shader was not found.");
                material = new Material(shader) { name = "Fx_Particle" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
