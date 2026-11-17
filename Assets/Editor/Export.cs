using UnityEditor;

namespace CableLabels.Editor
{
    // Builds the prefab and exports the mod as the editor's Build button would, for
    // tools/install.ps1 to run headless.
    // This assembly is editor-only and its path contains "Editor", so the exporter leaves it out
    // of the mod.
    public static class Export
    {
        // The exporter writes into a folder named after the product name, next to this path:
        // Build/CableLabels/. The .exe itself is never built.
        private const string ExportTarget = "Build/CableLabels.exe";

        public static void Run()
        {
            // The prefab is built from code, so every export starts from the builder's.
            BuildPrefabs.BuildAll();
            stationeers.modding.exporter.LaunchPadExport.Export(new BuildPlayerOptions
            {
                locationPathName = ExportTarget,
                target = BuildTarget.StandaloneWindows64,
                // Start from an empty folder, so nothing stale is installed.
                options = BuildOptions.CleanBuildCache,
            });
        }
    }
}
