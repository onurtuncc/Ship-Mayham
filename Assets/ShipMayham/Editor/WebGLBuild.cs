using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

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

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"WebGL build failed: {report.summary.result}");

            File.WriteAllText(Path.Combine(OutputPath, ".nojekyll"), string.Empty);
            UnityEngine.Debug.Log($"WebGL build completed: {Path.GetFullPath(OutputPath)}");
        }
    }
}
