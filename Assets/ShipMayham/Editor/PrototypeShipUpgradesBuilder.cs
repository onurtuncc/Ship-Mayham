using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        // Deck top is y = .5. Static FBX props authored with Blender +Y imported as Unity -Z, so the booster nozzle (+Y) faces astern.
        private static readonly Vector3[] FunnelPositions = { new Vector3(4.5f, .5f, -9.2f), new Vector3(-4.5f, .5f, -9.2f) };
        private static readonly Vector3[] BoosterPositions = { new Vector3(2.9f, -.6f, -10.9f), new Vector3(-2.9f, -.6f, -10.9f) };
        private static readonly Vector3[] PlanterPositions =
            { new Vector3(5.35f, .5f, 9f), new Vector3(-5.35f, .5f, 9f), new Vector3(5.35f, .5f, -5.2f), new Vector3(-5.35f, .5f, -5.2f) };
        private static readonly Vector3 CarpetPosition = new Vector3(0, .5f, -.2f);
        private static readonly Vector3[] BuntingPositions = { new Vector3(5.85f, .83f, -.1f), new Vector3(-5.85f, .83f, -.1f) };

        [MenuItem("Ship Mayham/Upgrade Prototype (Ship Upgrades)")]
        public static void UpgradeShipUpgrades()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreShipUpgrades.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreShipUpgrades.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureShipUpgrades(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_SHIP_UPGRADES_SCENE_READY");
        }

        // Visual-only and idempotent: module containers are rebuilt each run, gameplay roots are never touched.
        private static void EnsureShipUpgrades(PrototypeSession session, PrototypeView view)
        {
            if (session == null || view == null || session.ShipRoot == null) throw new InvalidOperationException("A complete prototype scene is required.");
            foreach (string model in new[] { "ShipFunnel", "ShipBoost", "ShipCarpet", "ShipPlanter", "ShipBunting" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedModels + model + ".fbx") == null)
                    throw new FileNotFoundException("Ship upgrade model is missing.", GeneratedModels + model + ".fbx");
            Transform visuals = session.ShipRoot.Find("Visuals");
            if (visuals == null) throw new InvalidOperationException("Ship_Player has no Visuals child.");

            Transform parent = visuals.Find("Upgrades");
            if (parent != null) Object.DestroyImmediate(parent.gameObject);
            parent = Root("Upgrades", visuals);

            var funnels = new List<Transform>(); var funnelSmoke = new List<Transform>();
            foreach (var position in FunnelPositions)
            {
                Transform module = Module(parent, "Funnel", "ShipFunnel", position);
                funnels.Add(module);
                funnelSmoke.Add(Anchor(module, "SmokeAnchor", new Vector3(0, 2.55f, 0)));
            }
            var boosters = new List<Transform>(); var nozzles = new List<Transform>();
            foreach (var position in BoosterPositions)
            {
                Transform module = Module(parent, "Booster", "ShipBoost", position);
                boosters.Add(module);
                nozzles.Add(Anchor(module, "NozzleAnchor", new Vector3(0, .5f, -1.05f)));
            }
            var comfort = new List<Transform> { Module(parent, "Carpet", "ShipCarpet", CarpetPosition) };
            foreach (var position in PlanterPositions) comfort.Add(Module(parent, "Planter", "ShipPlanter", position));
            foreach (var position in BuntingPositions) comfort.Add(Module(parent, "Bunting", "ShipBunting", position));

            var seats = new Transform[session.PassengerAnchors.Length];
            for (int i = 0; i < seats.Length; i++) seats[i] = session.PassengerAnchors[i].Find("SeatVisuals");

            var fx = GameObject.Find(FeedbackRootName) != null ? GameObject.Find(FeedbackRootName).GetComponent<FeedbackFx>() : null;
            var upgrades = view.GetComponent<PrototypeShipUpgradeView>();
            if (upgrades == null) upgrades = view.gameObject.AddComponent<PrototypeShipUpgradeView>();
            upgrades.View = view; upgrades.Fx = fx; upgrades.Seats = seats; upgrades.Funnels = funnels.ToArray();
            upgrades.Boosters = boosters.ToArray(); upgrades.ComfortFittings = comfort.ToArray();
            EditorUtility.SetDirty(upgrades);

            var effects = view.GetComponent<PrototypeShipEffects>();
            if (effects == null) effects = view.gameObject.AddComponent<PrototypeShipEffects>();
            effects.View = view; effects.Fx = fx; effects.FunnelSmoke = funnelSmoke.ToArray(); effects.BoosterNozzles = nozzles.ToArray();
            EditorUtility.SetDirty(effects);

            // Start hidden in the authored scene; the view reveals them from replicated levels at runtime.
            foreach (var module in funnels) module.gameObject.SetActive(false);
            foreach (var module in boosters) module.gameObject.SetActive(false);
            foreach (var module in comfort) module.gameObject.SetActive(false);
        }

        private static Transform Module(Transform parent, string name, string model, Vector3 position)
        {
            Transform container = Root(name + "_" + parent.childCount, parent);
            container.localPosition = position;
            AddProp(container, model, Vector3.zero, 1f);
            return container;
        }

        private static Transform Anchor(Transform parent, string name, Vector3 localPosition)
        {
            Transform anchor = Root(name, parent);
            anchor.localPosition = localPosition;
            return anchor;
        }
    }
}
