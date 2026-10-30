using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Scénarios : ouvre la vraie scène, entre en Play et lance les scénarios choisis (ScenarioRunner).
// Les scénarios multi demandent Player 2 actif (Window > Multiplayer > Multiplayer Play Mode).
public static class ScenarioMenu
{
    const string ScenePath = "Assets/Scenes/MainCityScene.unity";
    const string Root = "Tools/Scénarios/";
    const string Solo = Root + "Solo/";
    const string Multi = Root + "Multi (Player 2)/";

    [MenuItem(Root + "Tout lancer (solo)", priority = 0)]
    static void RunAllSolo() => Run(ScenarioRunner.AllSolo);

    [MenuItem(Root + "Tout lancer (multi)", priority = 1)]
    static void RunAllMulti() => Run(ScenarioRunner.AllMulti);

    [MenuItem(Root + "Tout lancer (solo + multi)", priority = 2)]
    static void RunEverything() => Run(ScenarioRunner.AllScenarios);

    [MenuItem(Solo + "1. " + ScenarioRunner.RingScenario, priority = 20)]
    static void RunRing() => Run(ScenarioRunner.RingScenario);

    [MenuItem(Solo + "2. " + ScenarioRunner.TutoScenario, priority = 21)]
    static void RunTuto() => Run(ScenarioRunner.TutoScenario);

    [MenuItem(Solo + "3. " + ScenarioRunner.RaceStartScenario, priority = 22)]
    static void RunRaceStart() => Run(ScenarioRunner.RaceStartScenario);

    [MenuItem(Solo + "4. " + ScenarioRunner.RaceHeightScenario, priority = 23)]
    static void RunRaceHeight() => Run(ScenarioRunner.RaceHeightScenario);

    [MenuItem(Solo + "5. " + ScenarioRunner.RaceLockedScenario, priority = 24)]
    static void RunRaceLocked() => Run(ScenarioRunner.RaceLockedScenario);

    [MenuItem(Solo + "6. " + ScenarioRunner.RaceWinScenario, priority = 25)]
    static void RunRaceWin() => Run(ScenarioRunner.RaceWinScenario);

    [MenuItem(Solo + "7. " + ScenarioRunner.DeliveryScenario, priority = 26)]
    static void RunDelivery() => Run(ScenarioRunner.DeliveryScenario);

    [MenuItem(Solo + "8. " + ScenarioRunner.CollectionScenario, priority = 27)]
    static void RunCollection() => Run(ScenarioRunner.CollectionScenario);

    [MenuItem(Solo + "9. " + ScenarioRunner.BillboardScenario, priority = 28)]
    static void RunBillboard() => Run(ScenarioRunner.BillboardScenario);

    [MenuItem(Solo + "10. " + ScenarioRunner.PrayerScenario, priority = 29)]
    static void RunPrayer() => Run(ScenarioRunner.PrayerScenario);

    [MenuItem(Solo + "11. " + ScenarioRunner.EquipmentScenario, priority = 30)]
    static void RunEquipment() => Run(ScenarioRunner.EquipmentScenario);

    [MenuItem(Solo + "12. " + ScenarioRunner.WaterSpeedScenario, priority = 31)]
    static void RunWaterSpeed() => Run(ScenarioRunner.WaterSpeedScenario);

    [MenuItem(Solo + "13. " + ScenarioRunner.BeachBallScenario, priority = 32)]
    static void RunBeachBall() => Run(ScenarioRunner.BeachBallScenario);

    [MenuItem(Solo + "14. " + ScenarioRunner.MaxDepthScenario, priority = 33)]
    static void RunMaxDepth() => Run(ScenarioRunner.MaxDepthScenario);

    [MenuItem(Solo + "15. " + ScenarioRunner.CamerasScenario, priority = 34)]
    static void RunCameras() => Run(ScenarioRunner.CamerasScenario);

    [MenuItem(Solo + "16. " + ScenarioRunner.SunglassesScenario, priority = 35)]
    static void RunSunglasses() => Run(ScenarioRunner.SunglassesScenario);

    [MenuItem(Solo + "17. " + ScenarioRunner.SewerRingsScenario, priority = 36)]
    static void RunSewerRings() => Run(ScenarioRunner.SewerRingsScenario);

    [MenuItem(Solo + "18. " + ScenarioRunner.CarRideScenario, priority = 37)]
    static void RunCarRide() => Run(ScenarioRunner.CarRideScenario);

    [MenuItem(Solo + "19. " + ScenarioRunner.PigeonAudioScenario, priority = 38)]
    static void RunPigeonAudio() => Run(ScenarioRunner.PigeonAudioScenario);

    [MenuItem(Multi + "1. " + ScenarioRunner.MultiRaceStartScenario, priority = 60)]
    static void RunMultiRaceStart() => Run(ScenarioRunner.MultiRaceStartScenario);

    [MenuItem(Multi + "2. " + ScenarioRunner.MultiRaceWinScenario, priority = 61)]
    static void RunMultiRaceWin() => Run(ScenarioRunner.MultiRaceWinScenario);

    [MenuItem(Multi + "3. " + ScenarioRunner.MultiRaceHeightScenario, priority = 62)]
    static void RunMultiRaceHeight() => Run(ScenarioRunner.MultiRaceHeightScenario);

    [MenuItem(Multi + "4. " + ScenarioRunner.MultiLateJoinScenario, priority = 63)]
    static void RunMultiLateJoin() => Run(ScenarioRunner.MultiLateJoinScenario);

    [MenuItem(Multi + "5. " + ScenarioRunner.MultiTutoScenario, priority = 64)]
    static void RunMultiTuto() => Run(ScenarioRunner.MultiTutoScenario);

    [MenuItem(Multi + "6. " + ScenarioRunner.MultiEquipmentScenario, priority = 65)]
    static void RunMultiEquipment() => Run(ScenarioRunner.MultiEquipmentScenario);

    [MenuItem(Multi + "7. " + ScenarioRunner.MultiAudioScenario, priority = 66)]
    static void RunMultiAudio() => Run(ScenarioRunner.MultiAudioScenario);

    [MenuItem(Root + "Ouvrir le dernier rapport", priority = 100)]
    static void OpenReport()
    {
        string path = System.IO.Path.GetFullPath("Logs/ScenarioReport.txt");
        if (System.IO.File.Exists(path))
            EditorUtility.OpenWithDefaultApp(path);
        else
            Debug.LogWarning("Aucun rapport de scénarios pour l'instant.");
    }

    // Ligne de commande : Unity.exe -projectPath … -executeMethod ScenarioMenu.RunAllFromCommandLine
    // Joue tous les scénarios solo puis quitte Unity (code 0 si tout passe, 1 sinon). Rapport : Logs/ScenarioReport.txt.
    public static void RunAllFromCommandLine() => RunFromCommandLine(ScenarioRunner.AllSolo);

    // Ligne de commande, scénarios multi : lancer d'abord l'hôte (cette méthode, dans le projet), puis le second
    // joueur dans le clone Multiplayer Play Mode (JoinAsClientFromCommandLine, -projectPath Library/VP/<clone>).
    // L'hôte attend le second joueur au plus 5 minutes, joue les scénarios multi, puis ferme les deux éditeurs.
    public static void RunMultiFromCommandLine() => RunFromCommandLine(ScenarioRunner.AllMulti);

    public static void RunEverythingFromCommandLine() => RunFromCommandLine(ScenarioRunner.AllScenarios);

    // Ligne de commande, scénarios choisis : -scenarios "Nom 1|Nom 2" (ou *solo, *multi, *).
    public static void RunSelectedFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, "-scenarios");
        RunFromCommandLine(index >= 0 && index + 1 < args.Length ? args[index + 1] : ScenarioRunner.AllSolo);
    }

    // Ligne de commande, dans le clone : entre en Play comme second joueur (client PurrNet), se relie à l'hôte,
    // obéit à ses scénarios puis quitte quand l'hôte a fini (ou au bout de 10 minutes sans hôte).
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
