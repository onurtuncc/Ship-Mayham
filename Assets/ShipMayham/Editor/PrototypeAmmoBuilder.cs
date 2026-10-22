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
        [MenuItem("Ship Mayham/Upgrade Prototype (Ammo Flow)")]
        public static void UpgradeAmmoFlow()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreAmmoFlow.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreAmmoFlow.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureAmmoFlow(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            PrepareLabels(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_AMMO_FLOW_SCENE_READY");
        }

        private static void EnsureAmmoFlow(PrototypeSession session, PrototypeView view)
        {
            if (session == null || view == null || session.ShipRoot == null)
                throw new InvalidOperationException("A prototype session and view are required.");
            var stations = new List<PrototypeStation>(session.Stations);
            var ammo = stations.Find(s => s.Kind == StationKind.Ammo);
            Material iron = Material("Ammo_Iron", new Color(.12f, .14f, .18f));
            if (ammo == null)
            {
                ammo = Station(session.ShipRoot.Find("Stations"), StationKind.Ammo, "Take / return cannonball", new Vector2(4, 3), iron);
                stations.Add(ammo);
                session.Stations = stations.ToArray();
            }
            AddProp(ammo.Visuals, "CannonballPile", new Vector3(0, 1.02f, 0), 1.3f);
            foreach (var socket in view.CarriedItems)
            {
                if (socket.Find("Ammo_Visual") != null) continue;
                var ball = Primitive("Ammo_Visual", socket, PrimitiveType.Sphere, Vector3.zero, Vector3.one * .32f, iron);
                ball.gameObject.SetActive(false);
            }
            var cannon = stations.Find(s => s.Kind == StationKind.Cannon);
            var combat = view.GetComponent<PrototypeCombatView>();
            if (cannon == null || combat == null) throw new InvalidOperationException("The cannon station and combat view are required.");
            Transform rack = cannon.Visuals.Find("AmmoRack_Visuals");
            if (rack == null)
            {
                rack = Root("AmmoRack_Visuals", cannon.Visuals);
                rack.localPosition = new Vector3(-.65f, 0, 0);
            }
            combat.ReadyAmmoVisuals = new Transform[VoyageSimulation.CannonBufferCapacity];
            for (int i = 0; i < combat.ReadyAmmoVisuals.Length; i++)
            {
                string name = $"Round_{i + 1:00}";
                Transform round = rack.Find(name);
                if (round == null) round = Primitive(name, rack, PrimitiveType.Sphere,
                    new Vector3(0, .2f, -.3f + i * .3f), Vector3.one * .24f, iron);
                round.gameObject.SetActive(false);
                combat.ReadyAmmoVisuals[i] = round;
            }
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(combat);
            EnsureBallisticVisuals(combat);
        }
    }
}
