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
        [MenuItem("Ship Mayham/Upgrade Prototype (Harbor Town)")]
        public static void UpgradeHarborTown()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the scene.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreHarborTown.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreHarborTown.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            EnsureHarborTown(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            EnsureHarborCrane(Object.FindAnyObjectByType<PrototypeSession>(), Object.FindAnyObjectByType<PrototypeView>());
            PrepareLabels(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("SHIP_HARBOR_TOWN_READY");
        }

        public static void PolishHarborTown()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before polishing the town.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var harbor = Object.FindAnyObjectByType<PrototypeHarbor>();
            ApplyTownPolish(harbor);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("SHIP_HARBOR_TOWN_POLISHED");
        }

        private static void ApplyTownPolish(PrototypeHarbor harbor)
        {
            var leaf = Material("Town_Leaves", new Color(.22f, .48f, .3f));
            var bark = Material("Town_Bark", new Color(.32f, .21f, .12f));
            foreach (var port in harbor.PortRoots)
            {
                Transform town = port.Find("Town_Visuals");
                town.Find("TownWelcome").localPosition = new Vector3(.5f, .3f, -2);
                foreach (string name in new[] { "TownWelcome", "WestDirections" })
                    town.Find(name).GetComponentInChildren<TextMesh>().color = new Color(.1f, .16f, .17f);
                Vector3[] positions = { new Vector3(-18, .3f, -16), new Vector3(-16, .3f, -3), new Vector3(-8, .3f, 7), new Vector3(-30, .3f, 13) };
                for (int i = 0; i < positions.Length; i++)
                {
                    if (town.Find("ShadeTree_" + i) != null) continue;
                    Transform tree = Root("ShadeTree_" + i, town); tree.localPosition = positions[i];
                    Primitive("Trunk", tree, PrimitiveType.Cylinder, new Vector3(0, 1.5f, 0), new Vector3(.3f, 1.5f, .3f), bark);
                    Primitive("Crown", tree, PrimitiveType.Sphere, new Vector3(0, 3.5f, 0), new Vector3(2.5f, 2.4f, 2.5f), leaf);
                    Primitive("CrownUpper", tree, PrimitiveType.Sphere, new Vector3(.3f, 4.5f, 0), new Vector3(1.8f, 1.8f, 1.8f), leaf);
                    Box("Planter", tree, new Vector3(0, .12f, 0), new Vector3(1.2f, .24f, 1.2f), bark);
                }
            }
        }

        private static void EnsureHarborTown(PrototypeSession session, PrototypeView view)
        {
            if (session == null || session.Harbor == null) throw new InvalidOperationException("Harbor required.");
            var harbor = session.Harbor;
            // Once the town is authored, preserve subsequent manual art edits.
            if (harbor.GetComponent<PrototypeHarborTownView>() != null) return;
            var stone = Material("Town_Stone", new Color(.49f, .52f, .5f));
            var paving = Material("Town_Paving", new Color(.66f, .63f, .53f));
            var wood = Material("Town_Wood", new Color(.38f, .23f, .13f));
            var cream = Material("Town_Sign", new Color(.9f, .84f, .66f));
            var orange = Material("Town_Orange", new Color(.95f, .48f, .15f));
            var blue = Material("Town_Blue", new Color(.1f, .55f, .8f));
            var green = Material("Town_Green", new Color(.17f, .63f, .36f));
            var violet = Material("Town_Violet", new Color(.6f, .28f, .65f));
            var labels = new List<TextMesh>(harbor.Labels);
            var markers = new Transform[HarborTownLayout.TownStationsPerPort * 2];
            harbor.SalvageMarkers = new Transform[HarborTownLayout.SalvageCount * 2];
            string[] titles = { "", "BOARD SHIP", "FOOD MARKET\n8 food / 12 coins", "THE SALTY GULL\n8 drinks / 12 coins", "DRYDOCK\n20 shield / 30 coins", "PORT OFFICE\nRoutes / Jobs / Salvage", "PAINT DOCK\nShip colors" };
            for (int port = 0; port < 2; port++)
            {
                Transform root = harbor.PortRoots[port];
                Transform oldVisuals = root.Find("Visuals");
                foreach (string obsolete in new[] { "Model_HarborHut", "Terminal_Primitive", "WalkwayStripe_Primitive" })
                { var old = oldVisuals.Find(obsolete); if (old != null) old.gameObject.SetActive(false); }
                Transform town = Root("Town_Visuals", root);
                Box("QuayFoundation", town, new Vector3(-14, -.08f, 0), new Vector3(40, .76f, 48), stone);
                Box("MarketSquare", town, new Vector3(-14, .31f, 0), new Vector3(37, .04f, 44), paving);
                // Long boardwalk and east-west promenades leave wide, direct travel lanes.
                Box("MainPromenade", town, new Vector3(-3, .345f, 0), new Vector3(4, .03f, 44), wood);
                for (int lane = -1; lane <= 1; lane++)
                    Box("Crosswalk_" + lane, town, new Vector3(-15, .361f, lane * 9), new Vector3(35, .03f, 2.5f), wood);   // 16 mm above the promenade: no shared top face
                for (int edge = 0; edge < 2; edge++)
                    Box("SeaWall_" + edge, town, new Vector3(-14, .55f, edge == 0 ? -24 : 24), new Vector3(40, .5f, .45f), stone);
                Box("WestSeaWall", town, new Vector3(-34.5f, .55f, 0), new Vector3(.45f, .5f, 48), stone);
                for (int z = -20; z <= 20; z += 10)
                {
                    TownProp(town, "Lamppost_" + z, "FlagPole", new Vector3(2, .3f, z), 1.2f);
                    TownProp(town, "Bollard_" + z, "Bollard", new Vector3(4.7f, .3f, z), 1);
                    TownProp(town, "Bench_" + z, "DeckBench", new Vector3(-32, .3f, z), 1);
                }
                TownProp(town, "FoodStall", "MarketStall", new Vector3(-10, .3f, -12.5f), 1.5f, 180);
                TownProp(town, "Tavern", "HarborHut", new Vector3(-25, .3f, -3), 1.5f);
                TownProp(town, "Workshop", "HarborHut", new Vector3(-12, .3f, 15), 1.5f);
                TownProp(town, "PaintShop", "MarketStall", new Vector3(-26, .3f, -16), 1.7f, 180);
                TownProp(town, "FishMarket", "MarketStall", new Vector3(-20, .3f, 17.5f), 1.5f);
                TownProp(town, "HarborCrane", "HarborCrane", new Vector3(-28, .3f, 8), 1.6f);
                TownProp(town, "OfficeCounter", "TicketCounter", new Vector3(-7, .3f, 3), 1.2f);
                TownProp(town, "CargoYard", "CrateStack", new Vector3(-18, .3f, 8), 1.1f);
                TownProp(town, "FishCrates", "FishCrate", new Vector3(-23, .3f, 20), 1.5f);
                TownProp(town, "ShipyardAnchor", "Anchor", new Vector3(-8, .3f, 17), 1.7f);
                TownProp(town, "PaintedBoat", "Rowboat", new Vector3(-30, .3f, -15), 1.1f);
                TownProp(town, "Lighthouse", "Lighthouse", new Vector3(-31, .3f, 4), 1.1f);
                // Named, visible residents make each functional area recognizable.
                TownResident(town, "Mara / Market", "Character_Passenger1", new Vector3(-10, .3f, -12), 180);
                TownResident(town, "Finn / Tavern", "Character_Passenger2", new Vector3(-25, .3f, -5.4f), 0);
                TownResident(town, "Pip / Dockwright", "Character_Crew2", new Vector3(-12, .3f, 12.9f), 0);
                TownResident(town, "Nell / Paint", "Character_Passenger3", new Vector3(-26, .3f, -14), 180);
                TownResident(town, "Captain Ada / Harbor Master", "Character_VIP", new Vector3(-7, .3f, 3.9f), 0);
                TownResident(town, "Ollie / Fisher", "Character_Passenger2", new Vector3(-20, .3f, 16), 0);
                TownSign(town, labels, "TownWelcome", new Vector3(-3, .3f, -4), port == 0 ? "BRASS QUAY\nMarket <-  Office ^  Ship ->" : "NORTH HAVEN\nWelcome, passengers!", cream, wood);
                TownSign(town, labels, "WestDirections", new Vector3(-18, .3f, 0), "TAVERN <-  OFFICE ->\nMARKET v  DRYDOCK ^", cream, wood);
                Material[] colors = { green, orange, blue, green, cream, violet };
                for (int a = 1; a <= HarborTownLayout.TownStationsPerPort; a++)
                {
                    Transform marker = a <= 4 ? harbor.ShoreMarkers[port * 4 + a - 1] : Root("Harbor_" + (HarborAction)a, root.Find("HarborStations"));
                    Vector2 p = VoyageSimulation.HarborPoint((HarborAction)a);
                    marker.localPosition = new Vector3(p.x, VoyageSimulation.ShoreHeight, p.y);
                    markers[port * HarborTownLayout.TownStationsPerPort + a - 1] = marker;
                    if (a <= 4)
                    {
                        var cube = marker.Find("Visuals/ShopCounter_Primitive"); if (cube != null) cube.gameObject.SetActive(false);
                        marker.GetComponentInChildren<TextMesh>(true).text = titles[a];
                    }
                    else labels.Add(Label("HarborLabel", marker, Vector3.up * 2.8f, titles[a], .07f));
                    if (a > 1) Box("ShopWelcomeMat", marker, new Vector3(0, .072f, 0), new Vector3(1.7f, .04f, 1.3f), colors[a - 1]);   // clear of the crosswalk top
                }
                for (int i = 0; i < HarborTownLayout.SalvageCount; i++)
                {
                    Vector2 p = HarborTownLayout.SalvagePoint(i);
                    Transform pile = Root("Salvage_" + i, town); pile.localPosition = new Vector3(p.x, .3f, p.y);
                    AddProp(pile, "RopeCoil", Vector3.zero, 1.8f);
                    labels.Add(Label("HarborLabel", pile, Vector3.up * 1.6f, "SALVAGE " + (i + 1) + "\nCarry to PORT OFFICE", .07f));
                    harbor.SalvageMarkers[port * HarborTownLayout.SalvageCount + i] = pile;
                    pile.gameObject.SetActive(false);
                }
            }
            harbor.ShoreMarkers = markers; harbor.Labels = labels.ToArray();
            foreach (var socket in view.CarriedItems) AddProp(socket, "RopeCoil", Vector3.zero, .85f).gameObject.SetActive(false);
            var presentation = harbor.gameObject.AddComponent<PrototypeHarborTownView>(); presentation.Harbor = harbor;
            ApplyTownPolish(harbor);
            EditorUtility.SetDirty(harbor); EditorUtility.SetDirty(presentation);
        }

        private static Transform TownProp(Transform parent, string name, string model, Vector3 position, float scale, float yaw = 0)
        {
            Transform root = Root(name, parent); root.localPosition = position; root.localRotation = Quaternion.Euler(0, yaw, 0);
            AddProp(root, model, Vector3.zero, scale); return root;
        }
        private static void TownResident(Transform parent, string name, string model, Vector3 position, float yaw)
            => TownProp(parent, name, model, position, 1, yaw);
        private static void TownSign(Transform parent, List<TextMesh> labels, string name, Vector3 position, string text, Material face, Material post)
        {
            Transform sign = Root(name, parent); sign.localPosition = position;
            Box("Post", sign, new Vector3(0, 1, 0), new Vector3(.12f, 2, .12f), post);
            Box("Signboard", sign, new Vector3(0, 1.85f, 0), new Vector3(3.6f, 1, .12f), face);
            var label = Label("HarborLabel", sign, new Vector3(0, 1.85f, -.09f), text, .07f);
            label.color = new Color(.1f, .16f, .17f); labels.Add(label);
        }
    }
}
