using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    /// <summary>Renders the authored prototype scene from a few fixed viewpoints to Logs/View_*.png for visual review. Never saves the scene.</summary>
    public static class ScenePreviewViews
    {
        private struct View { public string Name; public Vector3 Position, Target; public float Fov; public bool ShipLocal; }

        private static readonly View[] Views =
        {
            new View { Name = "DeckFront", Position = new Vector3(0, 2.4f, -7.5f), Target = new Vector3(0, 1.2f, 4), Fov = 70, ShipLocal = true },
            new View { Name = "DeckSeats", Position = new Vector3(-4.5f, 2.6f, -1f), Target = new Vector3(1.9f, 1f, 1.5f), Fov = 60, ShipLocal = true },
            new View { Name = "Stern", Position = new Vector3(0, 5f, -17f), Target = new Vector3(0, 1f, -4f), Fov = 55, ShipLocal = true },
            new View { Name = "Harbor", Position = new Vector3(-9f, 3.2f, -9f), Target = new Vector3(-14f, .4f, 4f), Fov = 70, ShipLocal = false },
            new View { Name = "Overview", Position = new Vector3(14f, 14f, -22f), Target = new Vector3(-4f, 0, 0), Fov = 50, ShipLocal = false },
        };

        public static void Capture()
        {
            var scene = EditorSceneManager.OpenScene("Assets/ShipMayham/Scenes/ShipPrototype.unity");
            var session = Object.FindAnyObjectByType<PrototypeSession>();
            var view = Object.FindAnyObjectByType<PrototypeView>();
            var upgrades = view.GetComponent<PrototypeShipUpgradeView>();
            if (upgrades != null) upgrades.Apply(2, 2, 1, false);   // show every upgrade module; never saved
            foreach (var crew in view.CrewRoots) crew.gameObject.SetActive(true);
            for (int i = 0; i < view.PassengerVisuals.Length; i++) view.PassengerVisuals[i].gameObject.SetActive(true);
            foreach (var label in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) label.gameObject.SetActive(false);

            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool batching = pipeline != null && pipeline.useSRPBatcher;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var camera = view.Camera;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            Directory.CreateDirectory("Logs");
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                if (pipeline != null) pipeline.useSRPBatcher = false;
                camera.targetTexture = target;
                foreach (var v in Views)
                {
                    Vector3 p = v.ShipLocal ? session.ShipRoot.TransformPoint(v.Position) : v.Position;
                    Vector3 t = v.ShipLocal ? session.ShipRoot.TransformPoint(v.Target) : v.Target;
                    camera.fieldOfView = v.Fov;
                    camera.transform.SetPositionAndRotation(p, Quaternion.LookRotation(t - p));
                    camera.Render(); camera.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                    File.WriteAllBytes($"Logs/View_{v.Name}.png", texture.EncodeToPNG());
                }
                Debug.Log("SHIP_VIEWS_WRITTEN");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                if (pipeline != null) pipeline.useSRPBatcher = batching;
                camera.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(texture);
                target.Release();
                EditorSceneManager.OpenScene("Assets/ShipMayham/Scenes/ShipPrototype.unity");   // discard staged state
            }
        }
    }
}
