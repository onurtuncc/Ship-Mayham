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
        private const string CrewControllerPath = AnimationsPath + "Crew.controller";
        private const float CrewScale = .9f;

        [MenuItem("Ship Mayham/Upgrade Prototype (Human Crew)")]
        public static void UpgradeHumanCrew()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreHumanCrew.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreHumanCrew.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureHumanCrew(Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_HUMAN_CREW_SCENE_READY");
        }

        // Visual-only and idempotent: the capsule body stays as a disabled renderer so authored references keep resolving.
        private static void EnsureHumanCrew(PrototypeView view)
        {
            if (view == null || view.CrewRoots == null || view.CrewRoots.Length == 0)
                throw new InvalidOperationException("Authored crew slots are required.");
            var controller = EnsureAnimatorController(CrewControllerPath, ("Idle", "Idle"), ("Walking", "Walking"), ("Carrying", "Carrying"), ("Jump", "Jump"), ("PickUp", "PickingUp"),
                ("Throw", "Throwing"), ("Interact", "ButtonPushing"), ("Dance", "HipHopDancing"), ("Dance2", "SambaDancing"),
                ("Victory", "VictoryIdle"), ("Defeated", "Defeated"), ("Salute", "Salute"), ("Clap", "StandingClap"), ("LookAround", "LookingAround"));
            var animator = view.GetComponent<PrototypeCrewAnimator>();
            if (animator == null) animator = view.gameObject.AddComponent<PrototypeCrewAnimator>();
            animator.View = view;
            animator.Animators = new Animator[view.CrewRoots.Length];
            for (int i = 0; i < view.CrewRoots.Length; i++)
            {
                Transform visuals = view.CrewRoots[i].Find("Visuals");
                if (visuals == null) throw new InvalidOperationException("Crew slot has no Visuals child: " + i);
                string model = $"Character_Human{i % HumanVariants + 1}_Rigged";
                animator.Animators[i] = AddHumanAnimator(visuals, model, CrewScale, controller, out _);
                HidePrimitive(visuals, "Body_Primitive");
            }
            EditorUtility.SetDirty(animator);
        }
    }
}
