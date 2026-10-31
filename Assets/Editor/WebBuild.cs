using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Export web des scènes du build dans Builds/Web (menu ou -executeMethod WebBuild.BuildFromCommandLine).</summary>
public static class WebBuild
{
    const string OutputPath = "Builds/Web";

    [MenuItem("Tools/Build web")]
    public static void BuildFromMenu() => Build();

    public static void BuildFromCommandLine() => EditorApplication.Exit(Build() ? 0 : 1);

    static bool Build()
    {
        var options = new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(System.Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path),
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[WebBuild] {report.summary.result} en {report.summary.totalTime}, {report.summary.totalSize / (1024 * 1024)} Mo, {report.summary.totalErrors} erreur(s) → {OutputPath}");
        return report.summary.result == BuildResult.Succeeded;
    }
}
