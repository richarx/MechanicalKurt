using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Scénarios : ouvre la vraie scène, entre en Play et lance les scénarios choisis (ScenarioRunner).
public static class ScenarioMenu
{
    const string ScenePath = "Assets/Scenes/MainCityScene.unity";
    const string Root = "Tools/Scénarios/";

    [MenuItem(Root + "Tout lancer", priority = 0)]
    static void RunAll() => Run(ScenarioRunner.AllScenarios);

    [MenuItem(Root + "1. " + ScenarioRunner.RingScenario, priority = 20)]
    static void RunRing() => Run(ScenarioRunner.RingScenario);

    [MenuItem(Root + "2. " + ScenarioRunner.TutoScenario, priority = 21)]
    static void RunTuto() => Run(ScenarioRunner.TutoScenario);

    [MenuItem(Root + "3. " + ScenarioRunner.RaceStartScenario, priority = 22)]
    static void RunRaceStart() => Run(ScenarioRunner.RaceStartScenario);

    [MenuItem(Root + "4. " + ScenarioRunner.RaceHeightScenario, priority = 23)]
    static void RunRaceHeight() => Run(ScenarioRunner.RaceHeightScenario);

    [MenuItem(Root + "Ouvrir le dernier rapport", priority = 40)]
    static void OpenReport()
    {
        string path = System.IO.Path.GetFullPath("Logs/ScenarioReport.txt");
        if (System.IO.File.Exists(path))
            EditorUtility.OpenWithDefaultApp(path);
        else
            Debug.LogWarning("Aucun rapport de scénarios pour l'instant.");
    }

    // Ligne de commande : Unity.exe -projectPath … -executeMethod ScenarioMenu.RunAllFromCommandLine
    // Joue tous les scénarios puis quitte Unity (code 0 si tout passe, 1 sinon). Rapport : Logs/ScenarioReport.txt.
    public static void RunAllFromCommandLine()
    {
        SessionState.SetBool(ScenarioRunner.ExitOnFinishKey, true);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        SessionState.SetString(ScenarioRunner.RunKey, ScenarioRunner.AllScenarios);
        EditorApplication.EnterPlaymode();
    }

    static void Run(string filter)
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
