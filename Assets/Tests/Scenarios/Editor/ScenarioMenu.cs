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

    [MenuItem(Root + "5. " + ScenarioRunner.DeliveryScenario, priority = 24)]
    static void RunDelivery() => Run(ScenarioRunner.DeliveryScenario);

    [MenuItem(Root + "6. " + ScenarioRunner.CollectionScenario, priority = 25)]
    static void RunCollection() => Run(ScenarioRunner.CollectionScenario);

    [MenuItem(Root + "7. " + ScenarioRunner.BillboardScenario, priority = 26)]
    static void RunBillboard() => Run(ScenarioRunner.BillboardScenario);

    [MenuItem(Root + "8. " + ScenarioRunner.PrayerScenario, priority = 27)]
    static void RunPrayer() => Run(ScenarioRunner.PrayerScenario);

    [MenuItem(Root + "9. " + ScenarioRunner.EquipmentScenario, priority = 28)]
    static void RunEquipment() => Run(ScenarioRunner.EquipmentScenario);

    [MenuItem(Root + "10. " + ScenarioRunner.WaterSpeedScenario, priority = 29)]
    static void RunWaterSpeed() => Run(ScenarioRunner.WaterSpeedScenario);

    [MenuItem(Root + "11. " + ScenarioRunner.BeachBallScenario, priority = 30)]
    static void RunBeachBall() => Run(ScenarioRunner.BeachBallScenario);

    [MenuItem(Root + "12. " + ScenarioRunner.MaxDepthScenario, priority = 31)]
    static void RunMaxDepth() => Run(ScenarioRunner.MaxDepthScenario);

    [MenuItem(Root + "13. " + ScenarioRunner.CamerasScenario, priority = 32)]
    static void RunCameras() => Run(ScenarioRunner.CamerasScenario);

    [MenuItem(Root + "14. " + ScenarioRunner.SunglassesScenario, priority = 33)]
    static void RunSunglasses() => Run(ScenarioRunner.SunglassesScenario);

    [MenuItem(Root + "15. " + ScenarioRunner.SewerRingsScenario, priority = 34)]
    static void RunSewerRings() => Run(ScenarioRunner.SewerRingsScenario);

    [MenuItem(Root + "16. " + ScenarioRunner.CarRideScenario, priority = 35)]
    static void RunCarRide() => Run(ScenarioRunner.CarRideScenario);

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
