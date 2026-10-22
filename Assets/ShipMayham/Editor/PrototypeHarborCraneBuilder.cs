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
        [MenuItem("Ship Mayham/Upgrade Prototype (Harbor Crane)")]
        public static void UpgradeHarborCrane()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before upgrading the crane.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            if (!File.Exists("Logs/ShipPrototype-PreHarborCrane.unity")) File.Copy(ScenePath, "Logs/ShipPrototype-PreHarborCrane.unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var session = Object.FindAnyObjectByType<PrototypeSession>(); var view = Object.FindAnyObjectByType<PrototypeView>();
            EnsureHarborTown(session, view); EnsureHarborCrane(session, view); PrepareLabels(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Debug.Log("SHIP_HARBOR_CRANE_READY");
        }

        public static void PolishHarborCrane()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before polishing the crane.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var view = Object.FindAnyObjectByType<PrototypeHarborCraneView>();
            if (view == null) throw new InvalidOperationException("Upgrade harbor crane first.");
            ApplyCranePolish(view);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        private static void ApplyCranePolish(PrototypeHarborCraneView view)
        {
            for (int port = 0; port < 2; port++)
            {
                view.HangingCrates[port].localScale = Vector3.one * .5f;
                for (int i = 0; i < 3; i++)
                {
                    var stock = view.StockCrates[port * 3 + i]; stock.localPosition = new Vector3(-21.3f + i * 1.15f, .3f, 15.1f);
                    stock.Find("Model_CrateStack").localScale = Vector3.one * .5f;
                    var landed = view.DeliveredCrates[port * 3 + i];
                    landed.localPosition = new Vector3(VoyageSimulation.CraneBarge.x - 1 + i, .55f, VoyageSimulation.CraneBarge.y);
                    landed.Find("Model_CrateStack").localScale = Vector3.one * .5f;
                }
                for (int i = 0; i < 2; i++)
                {
                    var pad = view.PadCrates[port * 2 + i]; Vector2 point = VoyageSimulation.HarborPoint(HarborAction.CraneLoadingPad);
                    pad.localPosition = new Vector3(point.x - .6f + i * 1.2f, .4f, point.y);
                    pad.Find("Model_CrateStack").localScale = Vector3.one * .5f;
                }
            }
        }

        private static void EnsureHarborCrane(PrototypeSession session, PrototypeView view)
        {
            if (session == null || session.Harbor == null) throw new InvalidOperationException("Town harbor required.");
            var harbor = session.Harbor;
            if (harbor.GetComponent<PrototypeHarborCraneView>() != null) return;
            var iron = Material("Crane_Iron", new Color(.15f, .19f, .21f));
            var gold = Material("Crane_Gold", new Color(.95f, .65f, .1f));
            var green = Material("Crane_Target", new Color(.15f, .72f, .45f));
            var wood = Material("Crane_Deck", new Color(.4f, .27f, .17f));
            var presentation = harbor.gameObject.AddComponent<PrototypeHarborCraneView>(); presentation.Harbor = harbor;
            presentation.Hooks = new Transform[2]; presentation.Booms = new Transform[2]; presentation.Cables = new Transform[2];
            presentation.AimMarkers = new Transform[2]; presentation.HangingCrates = new Transform[2];
            presentation.StockCrates = new Transform[6]; presentation.PadCrates = new Transform[4]; presentation.DeliveredCrates = new Transform[6];
            var markers = new Transform[HarborTownLayout.StationsPerPort * 2]; var labels = new List<TextMesh>(harbor.Labels);
            for (int port = 0; port < 2; port++)
            {
                Transform root = harbor.PortRoots[port], town = root.Find("Town_Visuals");
                for (int a = 1; a <= HarborTownLayout.TownStationsPerPort; a++) markers[port * HarborTownLayout.StationsPerPort + a - 1] = harbor.ShoreMarkers[port * HarborTownLayout.TownStationsPerPort + a - 1];
                town.Find("ShadeTree_3").localPosition = new Vector3(-30, .3f, 13);
                foreach (HarborAction action in new[] { HarborAction.FishMarket, HarborAction.CraneLoadingPad, HarborAction.CraneControls })
                {
                    Transform marker = Root("Harbor_" + action, root.Find("HarborStations"));
                    Vector2 point = VoyageSimulation.HarborPoint(action); marker.localPosition = new Vector3(point.x, .3f, point.y);
                    markers[port * HarborTownLayout.StationsPerPort + (int)action - 1] = marker;
                    string title = action == HarborAction.FishMarket ? "FISH MARKET\nCrane crate pickup" : action == HarborAction.CraneLoadingPad ? "LOAD PAD\nDeliver crates here" : "CRANE CONTROLS\nE / A: operate";
                    labels.Add(Label("HarborLabel", marker, Vector3.up * 1.7f, title, .07f));
                    if (action == HarborAction.CraneControls) Box("ControlConsole", marker, new Vector3(0, .4f, .3f), new Vector3(.8f, .8f, .5f), iron);
                }
                Transform rig = Root("CraneRig_Visuals", town);
                presentation.Hooks[port] = Root("Hook", rig);
                Primitive("HookHead", presentation.Hooks[port], PrimitiveType.Sphere, Vector3.zero, Vector3.one * .25f, iron);
                presentation.HangingCrates[port] = AddProp(presentation.Hooks[port], "CrateStack", new Vector3(0, -.65f, 0), .28f);
                presentation.Booms[port] = Box("MovingBoom", rig, Vector3.zero, Vector3.one, gold);
                presentation.Cables[port] = Box("Cable", rig, Vector3.zero, Vector3.one, iron);
                presentation.AimMarkers[port] = Primitive("HookAim", rig, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.5f, .015f, .5f), gold);
                Vector2 barge = VoyageSimulation.CraneBarge;
                TownProp(town, "WorkBarge", "Rowboat", new Vector3(barge.x, -.15f, barge.y), 1.6f, 90);
                Box("BargeDeck", rig, new Vector3(barge.x, .4f, barge.y), new Vector3(3.2f, .15f, 2.5f), wood);
                Primitive("LandingTarget", rig, PrimitiveType.Cylinder, new Vector3(barge.x, .49f, barge.y), new Vector3(2.7f, .02f, 2.7f), green);
                Vector2 pad = VoyageSimulation.HarborPoint(HarborAction.CraneLoadingPad);
                Box("LoadPad", rig, new Vector3(pad.x, .34f, pad.y), new Vector3(2, .06f, 1.6f), gold);
                for (int i = 0; i < 3; i++)
                {
                    presentation.StockCrates[port * 3 + i] = CraneCrateProp(rig, "StockCrate_" + i, new Vector3(-21 + i * .65f, .3f, 15.1f));
                    presentation.DeliveredCrates[port * 3 + i] = CraneCrateProp(rig, "LandedCrate_" + i, new Vector3(barge.x - .7f + i * .7f, .55f, barge.y));
                }
                for (int i = 0; i < 2; i++) presentation.PadCrates[port * 2 + i] = CraneCrateProp(rig, "PadCrate_" + i, new Vector3(pad.x - .4f + i * .8f, .4f, pad.y));
            }
            harbor.ShoreMarkers = markers; harbor.Labels = labels.ToArray();
            foreach (var socket in view.CarriedItems) AddProp(socket, "CrateStack", Vector3.zero, .28f).gameObject.SetActive(false);
            ApplyCranePolish(presentation);
            presentation.Present(new VoyageState(), 0, false);
            EditorUtility.SetDirty(harbor); EditorUtility.SetDirty(presentation);
        }

        private static Transform CraneCrateProp(Transform parent, string name, Vector3 position)
        {
            Transform root = Root(name, parent); root.localPosition = position;
            AddProp(root, "CrateStack", Vector3.zero, .28f); return root;
        }
    }
}
