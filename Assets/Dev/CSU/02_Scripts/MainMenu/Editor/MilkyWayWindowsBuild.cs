using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dev.CSU._02_Scripts.MainMenu.Editor
{
    public static class MilkyWayWindowsBuild
    {
        private const string MenuPath =
            "Tools/Build/Montagem Windows QA";
        private const string IconAssetPath =
            "Assets/Dev/CSU/00_Assets/Branding/MilkyWayAppIcon.png";
        private const string BuildOutputEnvironmentVariable =
            "MONTAGEM_BUILD_PATH";
        private const string IsolatedBuildOutputEnvironmentVariable =
            "MONTAGEM_ISOLATED_BUILD_PATH";
        private const string EditingCaptureBuildOutputEnvironmentVariable =
            "MONTAGEM_EDIT_CAPTURE_BUILD_PATH";
        private const string UiCaptureBuildOutputEnvironmentVariable =
            "MONTAGEM_UI_CAPTURE_BUILD_PATH";
        private const string IsolatedQaProductName =
            "Space_QA_Isolated";
        private const string EditingCaptureProductName =
            "Space_Edit_Capture";
        private const string UiCaptureProductName =
            "Space_UI_Capture";
        private const string BuildRequestRelativePath =
            "Library/MilkyWayBuild.request";
        private const string AutoStartEnabledYaml =
            "\n  autoStart: 1";

        private static readonly string[] ScenePaths =
        {
            "Assets/Dev/CSU/01_Scenes/MainMenu.unity",
            "Assets/Dev/CSU/01_Scenes/Rocket Shooting.unity",
            "Assets/Dev/CSU/01_Scenes/InGame.unity",
            "Assets/Dev/CSU/01_Scenes/Ending.unity"
        };

        internal static bool IsReleaseBuildInProgress { get; private set; }

        [InitializeOnLoadMethod]
        private static void BuildWhenRequested()
        {
            string requestPath = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    BuildRequestRelativePath));
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);
            EditorApplication.delayCall += BuildWindowsPlayer;
        }

        [MenuItem(MenuPath)]
        public static void BuildWindowsPlayer()
        {
            BuildWindowsPlayer(false, false, false);
        }

        [MenuItem(
            "Tools/Build/Montagem Windows QA (Isolated Profile)")]
        public static void BuildWindowsPlayerIsolatedProfile()
        {
            BuildWindowsPlayer(true, false, false);
        }

        [MenuItem(
            "Tools/Build/Montagem Editing Capture (No UI, No Death)")]
        public static void BuildEditingCaptureWindowsPlayer()
        {
            BuildWindowsPlayer(true, true, false);
        }

        [MenuItem(
            "Tools/Build/Montagem UI Capture (Full UI, 1080p)")]
        public static void BuildUiCaptureWindowsPlayer()
        {
            BuildWindowsPlayer(true, false, true);
        }

        private static void BuildWindowsPlayer(
            bool isolatedProfile,
            bool editingCapture,
            bool uiCapture)
        {
            Texture2D icon = ValidateBuildInputs();

            string outputPath = ResolveOutputPath(
                isolatedProfile,
                editingCapture,
                uiCapture);
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath) ??
                throw new InvalidOperationException(
                    "Build output directory could not be resolved."));

            NamedBuildTarget standalone = NamedBuildTarget.Standalone;
            Texture2D[] previousIcons =
                PlayerSettings.GetIcons(standalone, IconKind.Any);
            string previousProductName = PlayerSettings.productName;
            BuildReport report;
            try
            {
                int iconCount = Math.Max(
                    1,
                    PlayerSettings
                        .GetIconSizes(standalone, IconKind.Any)
                        .Length);
                PlayerSettings.SetIcons(
                    standalone,
                    Enumerable.Repeat(icon, iconCount).ToArray(),
                    IconKind.Any);
                if (editingCapture)
                {
                    PlayerSettings.productName =
                        EditingCaptureProductName;
                }
                else if (uiCapture)
                {
                    PlayerSettings.productName =
                        UiCaptureProductName;
                }
                else if (isolatedProfile)
                {
                    PlayerSettings.productName = IsolatedQaProductName;
                }

                IsReleaseBuildInProgress =
                    !isolatedProfile || editingCapture || uiCapture;

                report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = ScenePaths,
                        locationPathName = outputPath,
                        target = BuildTarget.StandaloneWindows64,
                        options = isolatedProfile
                            && !editingCapture
                            && !uiCapture
                            ? BuildOptions.Development
                            : BuildOptions.None
                    });
            }
            finally
            {
                IsReleaseBuildInProgress = false;
                PlayerSettings.productName = previousProductName;
                PlayerSettings.SetIcons(
                    standalone,
                    previousIcons,
                    IconKind.Any);
                AssetDatabase.SaveAssets();
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Montagem Windows QA build failed: {summary.result}, " +
                    $"{summary.totalErrors} error(s), " +
                    $"{summary.totalWarnings} warning(s).");
            }

            string profileLabel = editingCapture
                ? "editing-capture-release"
                : uiCapture
                    ? "ui-capture-release"
                : isolatedProfile
                    ? "isolated-development"
                    : "legacy-compatible-release";
            Debug.Log(
                $"[MontagemBuild] SUCCESS: {outputPath} " +
                $"({summary.totalSize:N0} bytes, " +
                $"{summary.totalTime.TotalSeconds:F1}s, " +
                $"{summary.totalWarnings} warning(s), profile=" +
                $"{profileLabel}).");
            if (!Application.isBatchMode)
            {
                EditorUtility.RevealInFinder(outputPath);
            }
        }

        [MenuItem("Tools/Build/Validate Montagem Windows QA")]
        public static void ValidatePlayerAndScenes()
        {
            ValidateBuildInputs();
            Debug.Log(
                "[MontagemBuild] Validation passed: app icon and all " +
                "required scenes are present.");
        }

        private static Texture2D ValidateBuildInputs()
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    throw new FileNotFoundException(
                        $"Required build scene is missing: {scenePath}");
                }
            }

            string launchPreparationScene = ScenePaths[1];
            string launchPreparationYaml = File.ReadAllText(
                launchPreparationScene);
            if (launchPreparationYaml.Contains(
                    AutoStartEnabledYaml,
                    StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "Rocket Shooting autoStart must stay disabled for "
                    + "submission builds so players can use the upgrade "
                    + "and assembly preparation screen.");
            }

            Texture2D icon =
                AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            if (icon == null)
            {
                throw new FileNotFoundException(
                    $"Application icon is missing or not imported: " +
                    IconAssetPath);
            }

            return icon;
        }

        private static string ResolveOutputPath(
            bool isolatedProfile,
            bool editingCapture,
            bool uiCapture)
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string environmentVariable = editingCapture
                ? EditingCaptureBuildOutputEnvironmentVariable
                : uiCapture
                    ? UiCaptureBuildOutputEnvironmentVariable
                : isolatedProfile
                    ? IsolatedBuildOutputEnvironmentVariable
                    : BuildOutputEnvironmentVariable;
            string configured = Environment.GetEnvironmentVariable(
                environmentVariable);
            string candidate;
            if (string.IsNullOrWhiteSpace(configured))
            {
                candidate = Path.Combine(
                    projectRoot,
                    editingCapture || uiCapture ? "편집용" : "Builds",
                    editingCapture || uiCapture ? "Build" :
                        $"QA_Fixed_{DateTime.Now:yyyyMMdd_HHmmss}",
                    editingCapture
                        ? $"EditCapture_{DateTime.Now:yyyyMMdd_HHmmss}"
                        : uiCapture
                            ? $"UiCapture_{DateTime.Now:yyyyMMdd_HHmmss}"
                        : string.Empty,
                    editingCapture
                        ? "Montagem_Edit_Capture.exe"
                        : uiCapture
                            ? "Montagem_UI_Capture.exe"
                        : isolatedProfile
                            ? "Montagem_QA_Isolated.exe"
                            : "Montagem.exe");
            }
            else
            {
                candidate = Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(projectRoot, configured);
            }

            string outputPath = Path.GetFullPath(candidate);
            string projectPrefix =
                projectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(
                    projectPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{environmentVariable} must resolve inside " +
                    $"the project workspace: {projectRoot}");
            }

            if (!string.Equals(
                    Path.GetExtension(outputPath),
                    ".exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{environmentVariable} must name a Windows " +
                    $"executable (.exe): {outputPath}");
            }

            return outputPath;
        }
    }

    /// <summary>
    /// The transitive Unity Performance Testing package injects its run metadata
    /// into every player build. Submission builds do not run performance tests,
    /// so remove those generated Resources immediately after the package's own
    /// pre-build callback has created them.
    /// </summary>
    internal sealed class MontagemReleaseBuildSanitizer :
        IPreprocessBuildWithReport
    {
        private static readonly string[] GeneratedPerformanceTestAssets =
        {
            "Assets/Resources/PerformanceTestRunInfo.json",
            "Assets/Resources/PerformanceTestRunSettings.json"
        };

        public int callbackOrder => 10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!MilkyWayWindowsBuild.IsReleaseBuildInProgress)
            {
                return;
            }

            bool removedAny = false;
            foreach (string assetPath in GeneratedPerformanceTestAssets)
            {
                removedAny |= DeleteIfPresent(assetPath);
                removedAny |= DeleteIfPresent(assetPath + ".meta");
            }

            if (!removedAny)
            {
                return;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "[MontagemBuild] Removed generated performance-test " +
                "metadata from the release player build.");
        }

        private static bool DeleteIfPresent(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }
}
