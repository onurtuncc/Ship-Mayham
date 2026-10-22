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
                animator.Animators[i] = AddHumanAnimator(view.PassengerVisuals[i], model, HumanScale, controller, out Transform container);
                HidePrimitive(view.PassengerVisuals[i], "Passenger_Primitive");
                if (cues != null && i < cues.Bodies.Length) cues.Bodies[i] = container;
            }
            if (cues != null) EditorUtility.SetDirty(cues);
            EditorUtility.SetDirty(animator);
        }

        private static AnimatorController EnsurePassengerController() => EnsureAnimatorController(PassengerControllerPath,
            ("Seated", "SittingIdle"), ("Pleading", "Waving"), ("Fidgeting", "Talking"), ("Panicked", "Terrified"),
            ("Delighted", "CheeringWhileSitting"), ("Yelling", "SittingYell"), ("Disbelief", "SittingDisbelief"), ("Laughing", "SittingLaughing"),
            ("Clapping", "SittingClap"), ("Drinking", "SittingDrinking"));

        // State names double as the strings the runtime animators cross-fade to; the first state is the default.
        private static AnimatorController EnsureAnimatorController(string controllerPath, params (string state, string clip)[] map)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < map.Length; i++)
            {
                string path = AnimationsPath + "Mixamo_" + map[i].clip + ".fbx";
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
                if (clip == null) throw new FileNotFoundException("Animation clip is missing.", path);
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == map[i].state) ?? machine.AddState(map[i].state);
                state.motion = clip;
                if (i == 0) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // Adds a rigged human under a visuals parent and returns its Animator wired to the controller.
        private static Animator AddHumanAnimator(Transform parent, string model, float scale, RuntimeAnimatorController controller, out Transform container)
        {
            string modelPath = GeneratedModels + model + ".fbx";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
                throw new FileNotFoundException("Rigged human model is missing.", modelPath);
            // Re-created on every run so a re-exported model (new materials, new rig) always replaces the scene instance.
            var stale = parent.Find("Model_" + model);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);
            container = AddProp(parent, model, Vector3.zero, scale);
            var instance = container.GetChild(0);
            var humanAnimator = instance.GetComponent<Animator>();
            if (humanAnimator == null) humanAnimator = instance.gameObject.AddComponent<Animator>();
            if (humanAnimator.avatar == null)
                humanAnimator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (humanAnimator.avatar == null || !humanAnimator.avatar.isHuman)
                throw new InvalidOperationException("Model did not import as a Humanoid avatar: " + model);
            humanAnimator.runtimeAnimatorController = controller;
            humanAnimator.applyRootMotion = false;
            humanAnimator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            return humanAnimator;
        }
    }
}
