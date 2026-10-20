using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        [MenuItem("Ship Mayham/Upgrade Prototype (Phase Three)")]
        public static void UpgradePhaseThree()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var view = Object.FindAnyObjectByType<PrototypeView>();
            EnsurePhaseTwo(Object.FindAnyObjectByType<PrototypeSession>(), view);
            EnsurePhaseThree(view);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_PHASE_THREE_SCENE_READY");
        }

        private static void EnsurePhaseThree(PrototypeView view)
        {
            var orange = Material("Service_Food_Orange", new Color(1, .48f, .12f));
            foreach (var visual in view.PassengerVisuals)
            {
                if (visual.Find("RescueFloat_Visual") != null) continue;
                var buoy = Primitive("RescueFloat_Visual", visual, PrimitiveType.Cylinder,
                    new Vector3(0, .35f, 0), new Vector3(1.3f, .08f, 1.3f), orange);
                buoy.gameObject.SetActive(false);
            }
        }
    }
}
