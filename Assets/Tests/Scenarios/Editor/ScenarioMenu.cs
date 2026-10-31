using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ScenarioMenu
{
    const string ScenePath = "Assets/Scenes/MainCityScene.unity";
    const string Root = "Tools/Scénarios/";

    [MenuItem(Root + "Choisir les scénarios…", priority = 0)]
    static void OpenWindow() => ScenarioWindow.Open();

    [MenuItem(Root + "Tout lancer (solo)", priority = 20)]
    static void RunAllSolo() => Run(ScenarioRunner.AllSolo);

    [MenuItem(Root + "Tout lancer (multi)", priority = 21)]
    static void RunAllMulti() => Run(ScenarioRunner.AllMulti);

    [MenuItem(Root + "Tout lancer (solo + multi)", priority = 22)]
    static void RunEverything() => Run(ScenarioRunner.AllScenarios);

    [MenuItem(Root + "Ouvrir le dernier rapport", priority = 100)]
    public static void OpenReport()
    {
        string path = System.IO.Path.GetFullPath(ScenarioRunner.ReportPath);
        if (System.IO.File.Exists(path))
            EditorUtility.OpenWithDefaultApp(path);
        else
            Debug.LogWarning("Aucun rapport de scénarios pour l'instant.");
    }

    public static void RunAllFromCommandLine() => RunFromCommandLine(ScenarioRunner.AllSolo);

    public static void RunMultiFromCommandLine() => RunFromCommandLine(ScenarioRunner.AllMulti);

    public static void RunEverythingFromCommandLine() => RunFromCommandLine(ScenarioRunner.AllScenarios);

    public static void RunSelectedFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, "-scenarios");
        RunFromCommandLine(index >= 0 && index + 1 < args.Length ? args[index + 1] : ScenarioRunner.AllSolo);
    }

    public static void RunMenuProbeFromCommandLine()
    {
        SessionState.SetBool(MenuProbe.RunKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    public static void JoinAsClientFromCommandLine()
    {
        SessionState.SetBool(ScenarioClientAgent.ExitOnFinishKey, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void RunFromCommandLine(string filter)
    {
        SessionState.SetBool(ScenarioRunner.ExitOnFinishKey, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetString(ScenarioRunner.RunKey, filter);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Ouvre la scène de test et lance les scénarios du filtre (noms séparés par ScenarioRunner.FilterSeparator).</summary>
    public static void Run(string filter)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Scénarios : quitter le mode Play avant de les lancer.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        SessionState.SetString(ScenarioRunner.RunKey, filter);
        EditorApplication.EnterPlaymode();
    }
}
