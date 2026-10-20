using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipMayham.Editor
{
    public static partial class PrototypeSceneBuilder
    {
        [MenuItem("Ship Mayham/Upgrade Prototype (Phase Four)")]
        public static void UpgradePhaseFour()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var session = Object.FindAnyObjectByType<PrototypeSession>();
            var view = Object.FindAnyObjectByType<PrototypeView>();
            EnsurePhaseTwo(session, view);
            EnsurePhaseThree(view);
            EnsurePhaseFour(session, view);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("SHIP_PHASE_FOUR_SCENE_READY");
        }

        private static void EnsurePhaseFour(PrototypeSession session, PrototypeView view)
        {
            if (session.Harbor != null) return;
            var harbor = view.gameObject.AddComponent<PrototypeHarbor>();
            session.Harbor = harbor;
            harbor.Session = session;
            harbor.View = view;
            var wood = Material("Deck_Wood", new Color(.45f, .28f, .14f));
            var cream = Material("Trim_Cream", new Color(.85f, .8f, .65f));
            var food = Material("Service_Food_Orange", new Color(1, .48f, .12f));
            var drink = Material("Service_Drink_Blue", new Color(.15f, .6f, 1));
            var green = Material("Repair_Green", new Color(.15f, .8f, .4f));
            var labels = new List<TextMesh>();
            harbor.ShipGate = Root("Harbor_Gangway", session.ShipRoot);
            harbor.ShipGate.localPosition = new Vector3(VoyageSimulation.ShipGangway.x, .5f, VoyageSimulation.ShipGangway.y);
            var gateVisual = Root("Visuals", harbor.ShipGate);
            Box("GangwayMarker_Primitive", gateVisual, new Vector3(0, .04f, 0), new Vector3(1.1f, .08f, 1.5f), green);
            Root("InteractionAnchor", harbor.ShipGate);
            labels.Add(Label("HarborLabel", harbor.ShipGate, Vector3.up * 1.6f, "ASHORE\nE / A", .07f));
            harbor.PortRoots = new Transform[2];
            harbor.ShoreMarkers = new Transform[8];
            harbor.Walkways = new Transform[2];
            for (int port = 0; port < 2; port++)
            {
                Transform root = GameObject.Find("Environment/" + (port == 0 ? "Port_Departure" : "Port_Destination")).transform;
                harbor.PortRoots[port] = root;
                Transform stations = Root("HarborStations", root);
                for (int a = 1; a <= 4; a++)
                {
                    var action = (HarborAction)a;
                    Vector2 p = VoyageSimulation.HarborPoint(action);
                    var marker = Root("Harbor_" + action, stations);
                    marker.localPosition = new Vector3(p.x, VoyageSimulation.ShoreHeight, p.y);
                    harbor.ShoreMarkers[port * 4 + a - 1] = marker;
                    Root("InteractionAnchor", marker);
                    var visual = Root("Visuals", marker);
                    if (a == 1) Box("BoardingMarker_Primitive", visual, new Vector3(0, .04f, 0), new Vector3(1, .08f, 1.5f), green);
                    else Box("ShopCounter_Primitive", visual, new Vector3(0, .5f, 0), new Vector3(1, 1, 1.3f), a == 2 ? food : a == 3 ? drink : green);
                    string title = a == 1 ? "BOARD SHIP\nE / A" : a == 2 ? "FOOD +8\n12 coins" : a == 3 ? "DRINK +8\n12 coins" : "HULL SHIELD +20\n30 coins";
                    labels.Add(Label("HarborLabel", marker, Vector3.up * 1.8f, title, .07f));
                }
                harbor.Walkways[port] = Root("Gangway_Visuals", root);
                Box("Gangway_Plank_Primitive", harbor.Walkways[port], new Vector3(7, .3f, -6), new Vector3(4.5f, .2f, 1.6f), wood);
                Box("WalkwayStripe_Primitive", root.Find("Visuals"), new Vector3(2.5f, .31f, 0), new Vector3(.15f, .02f, 19), cream);
            }
            harbor.Labels = labels.ToArray();
            EditorUtility.SetDirty(session);
        }
    }
}
