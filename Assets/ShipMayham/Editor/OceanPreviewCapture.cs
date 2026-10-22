using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShipMayham.Editor
{
    /// <summary>Renders the stylized ocean plus generated models in an empty scene for visual review. Never touches project scenes.</summary>
    public static class OceanPreviewCapture
    {
        private const string Models = "Assets/ShipMayham/Art/Generated/Models/";

        public static void Capture()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = ArgValue(args, "--ocean-preview-out") ?? "Logs/OceanPreview.png";
            int width = int.TryParse(ArgValue(args, "--ocean-preview-width"), out int w) ? w : 1280, height = width * 9 / 16;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;   // otherwise the first frame is drawn with the loading placeholder shader
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ShipMayham/Art/Materials/Ocean_Stylized.mat");
            if (material == null) throw new FileNotFoundException("Ocean material missing.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(.66f, .87f, .95f);
            RenderSettings.fogStartDistance = 60; RenderSettings.fogEndDistance = 320;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.62f, .7f, .78f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.color = new Color(1f, .95f, .86f);
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);

            var seaGo = new GameObject("Sea", typeof(MeshFilter), typeof(MeshRenderer), typeof(OceanSurface));
            seaGo.GetComponent<MeshRenderer>().sharedMaterial = material;
            seaGo.GetComponent<OceanSurface>().Rebuild();

            Place("PirateShipSmall", new Vector3(14, 0.15f, 34), 205, 1.9f);
            Place("Lighthouse", new Vector3(-46, -0.5f, 120), 0, 1.4f);
            Place("Rowboat", new Vector3(-3.5f, 0.0f, 16), 20, 1.4f);
            Place("Buoy", new Vector3(5, 0.25f, 11), 0, 1.6f);
            Place("LifeRing", new Vector3(-1.2f, 0.12f, 9), 0, 1.6f);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 4.2f, -6); cam.transform.rotation = Quaternion.Euler(13, 6, 0);
            cam.fieldOfView = 58; cam.farClipPlane = 600; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.62f, .84f, .96f);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllBytes(output, tex.EncodeToPNG());
            Debug.Log("SHIP_OCEAN_PREVIEW_WRITTEN " + Path.GetFullPath(output));
            Object.DestroyImmediate(tex); Object.DestroyImmediate(rt);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
        }

        /// <summary>
        /// Renders the real prototype scene from outside the ship. The scene is opened read-only (never saved)
        /// so this is safe to run after <see cref="PrototypeSceneBuilder.UpgradeStylizedOcean"/> for review.
        /// </summary>
        public static void CaptureScene()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = ArgValue(args, "--ocean-preview-out") ?? "Logs/OceanScenePreview.png";
            int width = int.TryParse(ArgValue(args, "--ocean-preview-width"), out int w) ? w : 1280, height = width * 9 / 16;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            RenderTexture rt = null; Texture2D tex = null;
            try
            {
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
                var session = Object.FindAnyObjectByType<PrototypeSession>();
                if (session == null || session.ShipRoot == null) throw new InvalidOperationException("Prototype session/ship not found in the scene.");
                var cam = new GameObject("OceanPreviewCamera").AddComponent<Camera>();
                Transform ship = session.ShipRoot;
                cam.transform.position = ship.TransformPoint(new Vector3(7.5f, 4.2f, -11f));
                cam.transform.LookAt(ship.TransformPoint(new Vector3(0f, 1.2f, 4f)));
                cam.fieldOfView = 55; cam.farClipPlane = 800;
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render(); cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                RenderTexture.active = null;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllBytes(output, tex.EncodeToPNG());
                Debug.Log("SHIP_OCEAN_SCENE_PREVIEW_WRITTEN " + Path.GetFullPath(output));
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = null;
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) Object.DestroyImmediate(rt);
            }
        }

        private static void Place(string model, Vector3 position, float yaw, float scale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (asset == null) { Debug.LogWarning("Preview model missing: " + model); return; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            instance.transform.localScale = Vector3.one * scale;
        }

        private static string ArgValue(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
