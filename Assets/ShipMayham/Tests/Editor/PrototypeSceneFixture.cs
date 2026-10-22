using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipMayham.Tests
{
    /// <summary>Opens the authored prototype scene additively for read-only inspection and closes only what it opened.</summary>
    public abstract class PrototypeSceneFixture
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

        protected T FindInScene<T>() where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
