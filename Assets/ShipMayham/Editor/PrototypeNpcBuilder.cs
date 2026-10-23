using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    /// <summary>
    /// Replaces the static town residents with rigged humans that idle and have living eyes, and puts the purser behind the
    /// Manifest desk. Visual only and idempotent: the residents keep their roots, positions and yaw, the Manifest station keeps
    /// its interaction anchor, and the NPC animator component is created once.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        private const string NpcControllerPath = AnimationsPath + "Npc.controller";
        private const float NpcScale = .9f;
        private const string PurserName = "Purser_Npc";

        private static readonly (string resident, string model)[] Residents =
        {
            ("Mara / Market", "Character_Human1_Rigged"),
            ("Finn / Tavern", "Character_Human2_Rigged"),
            ("Pip / Dockwright", "Character_Human3_Rigged"),
            ("Nell / Paint", "Character_Human1_Rigged"),
            ("Captain Ada / Harbor Master", "Character_Human2_Rigged"),
            ("Ollie / Fisher", "Character_Human3_Rigged"),
        };

        [MenuItem("Ship Mayham/Upgrade Prototype (Living NPCs)")]
        public static void UpgradeNpcs()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreNpcs.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreNpcs.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureNpcs(Object.FindAnyObjectByType<PrototypeView>(), Object.FindAnyObjectByType<PrototypeSession>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_NPCS_SCENE_READY");
        }

        private static void EnsureNpcs(PrototypeView view, PrototypeSession session)
        {
            if (view == null || session == null || session.Harbor == null) throw new InvalidOperationException("The prototype scene needs a view, a session and a harbor.");
            var controller = EnsureAnimatorController(NpcControllerPath, ("Idle", "Idle"), ("Talking", "Talking"), ("LookingAround", "LookingAround"), ("Waving", "Waving"));
            var animators = new List<Animator>();

            foreach (var port in session.Harbor.PortRoots)
            {
                foreach (var (resident, model) in Residents)
                {
                    Transform root = FindDescendant(port, resident);
                    if (root == null) throw new InvalidOperationException($"Resident '{resident}' is missing under {port.name}.");
                    RemoveStaticCharacter(root);
                    animators.Add(AddHumanAnimator(root, model, NpcScale, controller, out _));
                }
            }

            PrototypeStation manifest = null;
            foreach (var station in session.Stations) if (station.Kind == StationKind.Manifest) manifest = station;
            if (manifest == null) throw new InvalidOperationException("The ship has no Manifest station for the purser.");
            Transform purser = manifest.transform.Find(PurserName);
            if (purser == null) purser = Root(PurserName, manifest.transform);
            // Behind the counter, facing the deck.
            purser.localPosition = new Vector3(-.9f, 0, 0);
            purser.localRotation = Quaternion.Euler(0, 90, 0);
            animators.Add(AddHumanAnimator(purser, "Character_Human3_Rigged", NpcScale, controller, out _));
            if (manifest.Label != null) manifest.Label.text = "PURSER";

            var npcAnimator = view.GetComponent<PrototypeNpcAnimator>();
            if (npcAnimator == null) npcAnimator = view.gameObject.AddComponent<PrototypeNpcAnimator>();
            npcAnimator.Camera = view.Camera;
            npcAnimator.Animators = animators.ToArray();
            EditorUtility.SetDirty(npcAnimator);
            EditorUtility.SetDirty(manifest);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // The static resident models are children named Model_Character_*; the rigged ones are added alongside by AddHumanAnimator.
        private static void RemoveStaticCharacter(Transform root)
        {
            var stale = new List<GameObject>();
            foreach (Transform child in root)
                if (child.name.StartsWith("Model_Character_") && !child.name.Contains("_Rigged")) stale.Add(child.gameObject);
            foreach (var go in stale) Object.DestroyImmediate(go);
        }
    }
}
