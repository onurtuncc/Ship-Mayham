using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        [MenuItem("Ship Mayham/Upgrade Prototype (Phase Two)")]
        public static void UpgradePhaseTwo()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsurePhaseTwo(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_PHASE_TWO_SCENE_READY");
        }

        // Additive and idempotent: never recreate the ship, existing stations, camera or crew.
        private static void EnsurePhaseTwo(PrototypeSession session, PrototypeView view)
        {
            var hull = Material("Hull_Navy", new Color(.06f, .16f, .23f));
            var trim = Material("Trim_Cream", new Color(.85f, .8f, .65f));
            var danger = Material("Leak_Red", new Color(1, .16f, .1f));
            var green = Material("Repair_Green", new Color(.15f, .8f, .4f));
            var stations = new List<PrototypeStation>(session.Stations);
            var cannon = stations.Find(s => s.Kind == StationKind.Cannon);
            if (cannon == null)
            {
                cannon = Station(session.ShipRoot.Find("Stations"), StationKind.Cannon, "Use cannon", new Vector2(4, 6), hull);
                cannon.Visuals.Find("Counter_Primitive").localScale = new Vector3(.9f, .65f, .9f);
                stations.Add(cannon);
            }
            var power = stations.Find(s => s.Kind == StationKind.Power);
            if (power == null)
            {
                power = Station(session.ShipRoot.Find("Stations"), StationKind.Power, "Reset power (3 breakers)", new Vector2(4, 0), green);
                stations.Add(power);
            }
            session.Stations = stations.ToArray();
            var combat = view.GetComponent<PrototypeCombatView>();
            if (combat == null) combat = view.gameObject.AddComponent<PrototypeCombatView>();
            combat.Session = session;
            combat.View = view;
            if (combat.CannonPivot == null)
            {
                combat.CannonPivot = Root("AimPivot", cannon.transform);
                combat.CannonPivot.localPosition = new Vector3(0, 1.3f, 0);
                Transform model = Root("Visuals", combat.CannonPivot);
                Box("Barrel_Primitive", model, new Vector3(0, 0, .6f), new Vector3(.4f, .4f, 1.8f), hull);
                Box("Breech_Primitive", model, Vector3.zero, new Vector3(.65f, .65f, .65f), trim);
                combat.MuzzleAnchor = Root("MuzzleAnchor", combat.CannonPivot);
                combat.MuzzleAnchor.localPosition = new Vector3(0, 0, 1.5f);
            }
            if (combat.PowerIndicator == null)
                combat.PowerIndicator = Primitive("PowerStatus_Visual", power.Visuals, PrimitiveType.Sphere, new Vector3(0, 1.25f, 0), Vector3.one * .4f, green).GetComponent<Renderer>();
            if (combat.PirateRoot == null)
            {
                combat.PirateRoot = Root("Ship_Pirate_01");
                var model = Root("Visuals", combat.PirateRoot);
                Box("Hull_Primitive", model, new Vector3(0, .1f, 0), new Vector3(5, 1.7f, 9), danger);
                Box("Cabin_Primitive", model, new Vector3(0, 1.5f, -1), new Vector3(3, 2, 3), hull);
                Box("Mast_Primitive", model, new Vector3(0, 3, 0), new Vector3(.2f, 6, .2f), trim);
                Box("Flag_Primitive", model, new Vector3(1, 5.4f, 0), new Vector3(2, 1, .12f), hull);
                combat.PirateLabel = Label("ThreatLabel", combat.PirateRoot, new Vector3(0, 7, 0), "PIRATES", .15f);
                combat.PirateRoot.gameObject.SetActive(false);
            }
            if (combat.CannonTrace == null) combat.CannonTrace = Trace("CannonShot_Visual", view.transform, trim);
            if (combat.PirateTrace == null) combat.PirateTrace = Trace("PirateShot_Visual", view.transform, danger);
            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(combat);
        }

        private static LineRenderer Trace(string name, Transform parent, Material material)
        {
            var line = Root(name, parent).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = .15f;
            line.endWidth = .08f;
            line.enabled = false;
            return line;
        }
    }
}
