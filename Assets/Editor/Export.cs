using UnityEditor;

namespace CableLabels.Editor
{
    /// <summary>
    /// Builds the prefab and exports the mod as the editor's Build button would.
    /// </summary>
    /// <remarks>
    /// tools/install.ps1 runs <see cref="Run"/> headless. This assembly is editor-only, so the
    /// exporter leaves it out of the mod.
    /// </remarks>
    public static class Export
    {
        // The exporter writes Build/CableLabels/ beside this path; the .exe is never built.
        private const string ExportTarget = "Build/CableLabels.exe";

        public static void Run()
        {
            BuildPrefabs.BuildAll();
            stationeers.modding.exporter.LaunchPadExport.Export(new BuildPlayerOptions
            {
                locationPathName = ExportTarget,
                target = BuildTarget.StandaloneWindows64,
                // Nothing stale from an earlier export gets installed.
                options = BuildOptions.CleanBuildCache,
            });
        }
    }
}
