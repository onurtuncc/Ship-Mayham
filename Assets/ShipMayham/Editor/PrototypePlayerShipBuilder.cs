using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        // The FBX pivot is bbox-centred on x/y with the keel at 0; this maps its stern/bow onto the authored 20 m deck
        // and its planked deck surface onto the DeckCollider top (y = .5).
        private static readonly Vector3 PlayerShipOffset = new Vector3(0, -1.2f, 2.256f);
        private static readonly string[] PlayerShipParts = { "PlayerShipHull", "PlayerShipDeco" };
        private static readonly string[] BoxShipPrimitives =
            { "Hull_Primitive", "Deck_Primitive", "Rail_Port_Primitive", "Rail_Starboard_Primitive", "Rail_Bow_Primitive", "Rail_Stern_Primitive" };

        public static void CapturePlayerShipPreview()
        {
            UpgradePlayerShip();
            CapturePreview();
        }

        [MenuItem("Ship Mayham/Upgrade Prototype (Player Ship)")]
        public static void UpgradePlayerShip()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PrePlayerShip.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PrePlayerShip.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsurePlayerShip(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_PLAYER_SHIP_SCENE_READY");
        }

        // Visual-only and idempotent: the box primitives stay as disabled renderers so authored references keep resolving.
        private static void EnsurePlayerShip(PrototypeSession session, PrototypeView view)
        {
            if (session == null || view == null || session.ShipRoot == null)
                throw new InvalidOperationException("A complete prototype scene with Ship_Player is required.");
            foreach (string part in PlayerShipParts)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + part + ".fbx") == null)
                    throw new FileNotFoundException("Player ship model is missing.", GeneratedModels + part + ".fbx");
            Transform visuals = session.ShipRoot.Find("Visuals");
            if (visuals == null) throw new InvalidOperationException("Ship_Player has no Visuals child.");
            foreach (string primitive in BoxShipPrimitives) HidePrimitive(visuals, primitive);

            Transform hull = AddProp(visuals, "PlayerShipHull", PlayerShipOffset, 1f);
            Transform deco = AddProp(visuals, "PlayerShipDeco", PlayerShipOffset, 1f);
            // Blender +Y (bow) imports as Unity -Z for static props; turn the ship so the prow leads along +Z.
            hull.localRotation = deco.localRotation = Quaternion.Euler(0, 180, 0);
            var hullRenderers = hull.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in hullRenderers) renderer.name = "Hull_Primitive";   // PrototypeView paints renderers by this name
            var paintable = (view.LiveryRenderers ?? Array.Empty<Renderer>()).ToList();
            foreach (var renderer in hullRenderers) if (!paintable.Contains(renderer)) paintable.Add(renderer);
            view.LiveryRenderers = paintable.ToArray();
            EditorUtility.SetDirty(view);
        }
    }
}
