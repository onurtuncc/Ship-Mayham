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
        [MenuItem("Ship Mayham/Upgrade Prototype (Ballistic Cannonballs)")]
        public static void UpgradeBallistics()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreBallistics.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreBallistics.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureBallisticVisuals(Object.FindAnyObjectByType<PrototypeCombatView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_BALLISTICS_SCENE_READY");
        }

        private static void EnsureBallisticVisuals(PrototypeCombatView combat)
        {
            if (combat == null) throw new InvalidOperationException("A combat view is required.");
            Material iron = Material("Ammo_Iron", new Color(.12f, .14f, .18f));
            combat.CrewProjectileVisual = ProjectileVisual(combat.transform, "Crew_Cannonball_Visual", iron);
            combat.PirateProjectileVisual = ProjectileVisual(combat.transform, "Pirate_Cannonball_Visual", iron);
            EditorUtility.SetDirty(combat);
        }

        private static Transform ProjectileVisual(Transform parent, string name, Material material)
        {
            Transform visual = parent.Find(name);
            if (visual == null) visual = Primitive(name, parent, PrimitiveType.Sphere, Vector3.zero, Vector3.one * .4f, material);
            visual.gameObject.SetActive(false);
            return visual;
        }
    }
}
