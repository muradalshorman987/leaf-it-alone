using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FallenYard.EditorTools
{
    /// <summary>
    /// Entry point for headless builds (GameCI's unity-builder calls it via buildMethod).
    /// If no scene is enabled in Build Settings yet, it generates the test yard first,
    /// so CI can build before a real scene has been committed.
    /// </summary>
    public static class CIBuild
    {
        const string DefaultOutput = "Builds/StandaloneWindows64/FallenYard.exe";

        public static void BuildWindows()
        {
            string output = GetArg("-customBuildPath") ?? DefaultOutput;
            if (Directory.Exists(output) || !output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(output, "FallenYard.exe");

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.Log("[FallenYard CI] No scenes in Build Settings; generating the test yard.");
                TestYardBuilder.Build();
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"[FallenYard CI] Build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, " +
                      $"{summary.totalErrors} errors, {summary.totalWarnings} warnings -> {output}");

            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
