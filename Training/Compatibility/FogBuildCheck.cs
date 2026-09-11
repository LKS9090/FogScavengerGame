using UnityEditor;
using UnityEditor.Build.Reporting;
public static class FogBuildCheck
{
    public static void Build()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new [] { "Assets/ML-Agents/Examples/3DBall/Scenes/3DBall.unity" },
            locationPathName = "Builds/3DBall/3DBall.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (result.summary.result != BuildResult.Succeeded) throw new System.Exception("3DBall build failed");
    }
}
