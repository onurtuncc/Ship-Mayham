using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipMayham.Tests
{
    public class GeneratedPropsSceneTests
    {
        private Scene scene, originalScene;
        private bool ownsScene;

        [SetUp]
        public void OpenReadOnlyScene()
        {
            Assert.IsFalse(Application.isPlaying);
            originalScene = SceneManager.GetActiveScene();
            scene = SceneManager.GetSceneByPath("Assets/ShipMayham/Scenes/ShipPrototype.unity");
            ownsScene = !scene.IsValid() || !scene.isLoaded;
            if (ownsScene) scene = EditorSceneManager.OpenScene("Assets/ShipMayham/Scenes/ShipPrototype.unity", OpenSceneMode.Additive);
        }

        [TearDown]
        public void CloseOnlyOwnedScene()
        {
            if (ownsScene && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
        }

        [Test]
        public void AuthoredPropsKeepGameplayRootsAndUseWebCompatibleMaterials()
        {
            PrototypeSession session = null;
            PrototypeView view = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (session == null) session = root.GetComponentInChildren<PrototypeSession>(true);
                if (view == null) view = root.GetComponentInChildren<PrototypeView>(true);
            }
            Assert.IsNotNull(session);
            Assert.IsNotNull(view);
            Assert.AreEqual(10, session.Stations.Length);
            Assert.AreEqual(8, session.PassengerAnchors.Length);
            Transform wheel = null;
            foreach (var station in session.Stations)
            {
                Assert.IsNotNull(station.InteractionAnchor);
                Assert.IsNotNull(station.Label);
                Assert.IsNotNull(station.Visuals);
                if (station.Kind != StationKind.Helm) continue;
                wheel = station.Visuals.Find("Model_ShipWheel");
                Assert.IsNotNull(wheel);
                Assert.IsFalse(station.Visuals.Find("Counter_Primitive").GetComponent<Renderer>().enabled);
            }
            Assert.IsNotNull(wheel);
            var wheelRenderers = wheel.GetComponentsInChildren<Renderer>(true);
            Assert.IsNotEmpty(wheelRenderers);
            Bounds bounds = wheelRenderers[0].bounds;
            foreach (var renderer in wheelRenderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.y, Is.InRange(.95f, 1.5f), "Wheel must be upright and meter-scaled.");
            Assert.That(bounds.size.x, Is.InRange(.5f, 1.1f));
            Assert.That(bounds.size.z, Is.InRange(.2f, .7f));

            foreach (var passenger in session.PassengerAnchors)
            {
                Assert.IsNotNull(passenger.Find("InteractionAnchor"));
                Assert.IsNotNull(passenger.Find("SeatVisuals/Model_DeckBench"));
                Assert.IsNotNull(passenger.Find("Visuals/RescueFloat_Visual"));
            }
            foreach (var crew in view.CrewRoots) Assert.IsNotNull(crew.Find("CarrySocket"));
            var combat = view.GetComponent<PrototypeCombatView>();
            Assert.IsNotNull(combat.CannonPivot);
            Assert.IsNotNull(combat.MuzzleAnchor);
            Assert.IsTrue(combat.PowerIndicator.enabled);
            Assert.IsNotNull(combat.PirateRoot.Find("Visuals/Model_PirateShipSmall"));
            foreach (var port in session.Harbor.PortRoots)
            {
                Assert.IsNotNull(port.Find("Visuals/Model_HarborHut"));
                Assert.IsTrue(port.Find("Visuals/Pier_Primitive").GetComponent<Renderer>().enabled);
            }
            int modelCount = 0;
            foreach (var root in session.gameObject.scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!transform.name.StartsWith("Model_")) continue;
                    modelCount++;
                    Assert.IsEmpty(transform.GetComponentsInChildren<NetworkObject>(true), "Props cannot introduce network identities.");
                    foreach (var collider in transform.GetComponentsInChildren<Collider>(true)) Assert.IsFalse(collider.enabled);
                    foreach (var renderer in transform.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                }
            Assert.GreaterOrEqual(modelCount, 20);
        }
    }
}
