using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShipMayham.Editor
{
    public static class WebGLBuild
    {
        private const string ScenePath = "Assets/ShipMayham/Scenes/ShipPrototype.unity";
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("Ship Mayham/Build Prototype (WebGL)")]
        public static void Build()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Prototype scene was not found.", ScenePath);

            // GitHub Pages cannot add the Content-Encoding headers Unity's pre-compressed
            // files require. Disabled compression is larger, but works without a custom server.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;

            Directory.CreateDirectory(OutputPath);
            string graphicsBackup = File.ReadAllText("ProjectSettings/GraphicsSettings.asset");
            string qualityBackup = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            var pipeline = CreateWebPipeline();
            var originalPipeline = GraphicsSettings.defaultRenderPipeline;
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            var originalQualityPipelines = new UnityEngine.Object[levels.arraySize];
            for (int i = 0; i < levels.arraySize; i++)
            {
                var reference = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                originalQualityPipelines[i] = reference.objectReferenceValue;
                reference.objectReferenceValue = pipeline;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });

                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"WebGL build failed: {report.summary.result}");
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = originalPipeline;
                quality.Update();
                for (int i = 0; i < originalQualityPipelines.Length; i++)
                    levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = originalQualityPipelines[i];
                quality.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                // Preserve even dangling GUID references that Unity's object getter resolves to null.
                File.WriteAllText("ProjectSettings/GraphicsSettings.asset", graphicsBackup);
                File.WriteAllText("ProjectSettings/QualitySettings.asset", qualityBackup);
                AssetDatabase.Refresh();
            }

            File.WriteAllText(Path.Combine(OutputPath, ".nojekyll"), string.Empty);
            VersionBuildUrls();
            UnityEngine.Debug.Log($"WebGL build completed: {Path.GetFullPath(OutputPath)}");
        }

        private static void VersionBuildUrls()
        {
            string indexPath = Path.Combine(OutputPath, "index.html");
            string html = File.ReadAllText(indexPath);
            foreach (string file in Directory.GetFiles(Path.Combine(OutputPath, "Build")))
            {
                string token = "\"/" + Path.GetFileName(file) + "\"";
                if (!html.Contains(token)) continue;
                string hash;
                using (var sha = SHA256.Create())
                using (var input = File.OpenRead(file))
                    hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").Substring(0, 16);
                html = html.Replace(token, "\"/" + Path.GetFileName(file) + "?v=" + hash + "\"");
            }
            File.WriteAllText(indexPath, html);
        }

        private static UniversalRenderPipelineAsset CreateWebPipeline()
        {
            const string folder = "Assets/ShipMayham/WebBuildSettings";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/ShipMayham", "WebBuildSettings");
            const string rendererPath = folder + "/WebRenderer.asset";
            const string pipelinePath = folder + "/WebPipeline.asset";
            if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath) == null &&
                !AssetDatabase.CopyAsset("Assets/Core/Settings/PC_Renderer.asset", rendererPath))
                throw new InvalidOperationException("Could not create the WebGL renderer.");
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath) == null &&
                !AssetDatabase.CopyAsset("Assets/Core/Settings/PC_RPAsset.asset", pipelinePath))
                throw new InvalidOperationException("Could not create the WebGL render pipeline.");

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            renderer.renderingMode = RenderingMode.Forward;
            renderer.rendererFeatures.Clear();
            var rendererSettings = new SerializedObject(renderer);
            rendererSettings.FindProperty("m_UseNativeRenderPass").boolValue = false;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            var settings = new SerializedObject(pipeline);
            var renderers = settings.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            settings.FindProperty("m_DefaultRendererIndex").intValue = 0;
            settings.FindProperty("m_SupportsHDR").boolValue = false;
            settings.FindProperty("m_MSAA").intValue = 1;
            settings.FindProperty("m_UpscalingFilter").intValue = 1; // Linear; FSR requires unavailable WebGL shader features.
            settings.FindProperty("m_LightProbeSystem").intValue = 0; // Classic probes; APV uses unsupported storage buffers on WebGL.
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            return pipeline;
        }
    }
}
