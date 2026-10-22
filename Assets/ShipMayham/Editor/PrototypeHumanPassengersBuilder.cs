using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        private const string AnimationsPath = "Assets/ShipMayham/Art/Generated/Animations/";
        private const string PassengerControllerPath = AnimationsPath + "Passenger.controller";
        private const int HumanVariants = 3;
        private const float HumanScale = .8f;

        [MenuItem("Ship Mayham/Upgrade Prototype (Human Passengers)")]
        public static void UpgradeHumanPassengers()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreHumanPassengers.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreHumanPassengers.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureHumanPassengers(Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_HUMAN_PASSENGERS_SCENE_READY");
        }

        // Visual-only and idempotent: the capsule stays as a disabled renderer so authored references keep resolving.
        private static void EnsureHumanPassengers(PrototypeView view)
        {
            if (view == null || view.PassengerVisuals.Length != VoyageSimulation.MaxPassengers)
                throw new InvalidOperationException("Eight authored passenger Visuals are required.");
            var controller = EnsurePassengerController();
            var animator = view.GetComponent<PrototypePassengerAnimator>();
            if (animator == null) animator = view.gameObject.AddComponent<PrototypePassengerAnimator>();
            var cues = view.GetComponent<PrototypePassengerCueView>();
            animator.View = view;
            animator.Animators = new Animator[view.PassengerVisuals.Length];
            for (int i = 0; i < view.PassengerVisuals.Length; i++)
            {
                string model = $"Character_Human{i % HumanVariants + 1}_Rigged";
                string modelPath = GeneratedModels + model + ".fbx";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                    throw new FileNotFoundException("Rigged passenger model is missing.", modelPath);
                Transform container = AddProp(view.PassengerVisuals[i], model, Vector3.zero, HumanScale);
                var instance = container.GetChild(0);
                var humanAnimator = instance.GetComponent<Animator>();
                if (humanAnimator == null) humanAnimator = instance.gameObject.AddComponent<Animator>();
                if (humanAnimator.avatar == null)
                    humanAnimator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
                if (humanAnimator.avatar == null || !humanAnimator.avatar.isHuman)
                    throw new InvalidOperationException("Passenger model did not import as a Humanoid avatar: " + model);
                humanAnimator.runtimeAnimatorController = controller;
                humanAnimator.applyRootMotion = false;
                humanAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.Animators[i] = humanAnimator;
                HidePrimitive(view.PassengerVisuals[i], "Passenger_Primitive");
                if (cues != null && i < cues.Bodies.Length) cues.Bodies[i] = container;
            }
            if (cues != null) EditorUtility.SetDirty(cues);
            EditorUtility.SetDirty(animator);
        }

        private static AnimatorController EnsurePassengerController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PassengerControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(PassengerControllerPath);
            var machine = controller.layers[0].stateMachine;
            (PassengerPose pose, string clip)[] map =
            {
                (PassengerPose.Seated, "SittingIdle"), (PassengerPose.Pleading, "Waving"),
                (PassengerPose.Fidgeting, "Talking"), (PassengerPose.Panicked, "Terrified")
            };
            foreach (var (pose, clipName) in map)
            {
                string path = AnimationsPath + "Mixamo_" + clipName + ".fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip == null) throw new FileNotFoundException("Passenger animation clip is missing.", path);
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == pose.ToString()) ?? machine.AddState(pose.ToString());
                state.motion = clip;
                if (pose == PassengerPose.Seated) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
