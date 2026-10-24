using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShipMayham.Editor
{
    public static class AdventureArtUpgrade
    {
        [MenuItem("Ship Mayham/Upgrade Adventure Animations")]
        public static void Upgrade()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/ShipMayham/Art/Generated/Animations/Passenger.controller");
            if (controller == null) throw new InvalidOperationException("Passenger controller is missing.");
            Add(controller, "Walking", "Mixamo_Walking");
            Add(controller, "Standing", "Mixamo_Idle");
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_ADVENTURE_ANIMATIONS_READY");
        }
        private static void Add(AnimatorController controller, string stateName, string file)
        {
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) if (child.state.name == stateName) return;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/ShipMayham/Art/Generated/Animations/" + file + ".fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    machine.AddState(stateName).motion = clip; EditorUtility.SetDirty(controller); return;
                }
            throw new InvalidOperationException("Missing humanoid animation: " + file);
        }
    }
}
