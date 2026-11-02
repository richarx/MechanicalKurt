using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Contrôles Edit Mode : vrai CharacterController, puis règles et classement de la chute libre.</summary>
[InitializeOnLoad]
public static class FreeFallRegressionChecks
{
    private const string RequestPath = "Temp/FreeFallRegression.request";
    private const string ReportPath = "Logs/FreeFallRegressionReport.txt";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3 FloorPosition = new Vector3(-10000f, 1000f, 0f);
    private static readonly StringBuilder Report = new StringBuilder();
    private static int _passed;
    private static int _failed;

    static FreeFallRegressionChecks() => EditorApplication.delayCall += RunRequested;

    private static void RunRequested()
    {
        if (!File.Exists(RequestPath))
            return;
        File.Delete(RequestPath);
        Run();
    }

    [MenuItem("Tools/Scénarios/Vérifier la chute libre (collisions et classement)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Chute libre : quitter Play pour ces vérifications isolées.");
            return;
        }
        Report.Clear();
        _passed = _failed = 0;
        Scene original = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var registered = (List<PigeonController>)PigeonController.All;
        var pigeons = new List<PigeonController>();
        try
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Free fall test floor";
            floor.transform.position = FloorPosition - Vector3.up * 0.5f;
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            CharacterController prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pigeon/Player.prefab").GetComponent<CharacterController>();
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Free fall test pigeon " + i);
                go.layer = LayerMask.NameToLayer("Player");
                PigeonController pigeon = go.AddComponent<PigeonController>();
                CharacterController controller = go.GetComponent<CharacterController>();
                controller.height = prefab.height;
                controller.radius = prefab.radius;
                controller.center = prefab.center;
                controller.slopeLimit = prefab.slopeLimit;
                controller.stepOffset = prefab.stepOffset;
                controller.skinWidth = prefab.skinWidth;
                controller.minMoveDistance = prefab.minMoveDistance;
                Set(pigeon, "_controller", controller);
                pigeons.Add(pigeon);
                if (!registered.Contains(pigeon))
                    registered.Add(pigeon);
            }
            Physics.SyncTransforms();
            CheckNativeContact(pigeons[0]);
            CheckFlapBeforeContact(pigeons[0]);
            CheckRanking(pigeons);
        }
        catch (Exception exception)
        {
            _failed++;
            Report.AppendLine("ERROR " + exception);
        }
        finally
        {
            foreach (PigeonController pigeon in pigeons)
                registered.Remove(pigeon);
            EditorSceneManager.CloseScene(testScene, true);
            SceneManager.SetActiveScene(original);
            Report.Insert(0, $"Free fall regression: {_passed} passed, {_failed} failed\n");
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, Report.ToString());
            Debug.Log($"Chute libre : {_passed} vérifications réussies, {_failed} échecs. {ReportPath}");
        }
    }

    private static void CheckNativeContact(PigeonController pigeon)
    {
        PigeonState air = State(3f);
        air = Move(pigeon, air, Vector3.down * 34f, 0.05f);
        object[] contactArgs = { air };
        Call(pigeon, "CaptureFreeFallContact", contactArgs);
        air = (PigeonState)contactArgs[0];
        Check(!air.Grounded && air.FreeFall.Loss == FreeFallChallenge.LossReason.None,
            "Pas de faux contact à plus d'un mètre du sol", $"height={air.Position.y - FloorPosition.y:0.0000}");

        PigeonState before = State(0.3f);
        PigeonState ground = Move(pigeon, before, Vector3.down * 34f, 0.05f);
        contactArgs[0] = ground;
        Call(pigeon, "CaptureFreeFallContact", contactArgs);
        ground = (PigeonState)contactArgs[0];
        float gap = ground.Position.y - FloorPosition.y;
        Check(ground.Grounded && ground.FreeFall.Loss == FreeFallChallenge.LossReason.Ground && gap < 0.02f,
            "Contact natif confirmé seulement près de la surface", $"gap={gap:0.000000}m, skin={pigeon.GetComponent<CharacterController>().skinWidth}");

        ground.FreeFall.Finished = true;
        PigeonState midway = (PigeonState)Call(pigeon, "Interpolate", new object[] { before, ground, 0.25f });
        Check(midway.FreeFall.Loss == FreeFallChallenge.LossReason.Ground && midway.Grounded
            && Mathf.Abs(midway.Position.y - ground.Position.y) < 0.0001f,
            "Au contact final, le modèle affiché est aligné sur la collision réelle",
            $"displayedHeight={midway.Position.y - FloorPosition.y:0.0000}, grounded={midway.Grounded}, loss={midway.FreeFall.Loss}");
        PigeonState end = (PigeonState)Call(pigeon, "Interpolate", new object[] { before, ground, 1f });
        Check(end.Grounded && end.FreeFall.Loss == FreeFallChallenge.LossReason.Ground,
            "L'affichage confirme le contact à la fin de l'interpolation");
    }

    private static void CheckFlapBeforeContact(PigeonController pigeon)
    {
        PigeonState state = State(0.5f);
        state.LastVelocity = Vector3.down * 34f;
        pigeon.transform.position = state.Position;
        Physics.SyncTransforms();
        var input = new PigeonInput { Flap = true, FreeFallAction = FreeFallChallenge.LossReason.Flap, FreeFallActionDelay = 0.005f };
        object[] captureArgs = { input, state, 0.05f };
        Call(pigeon, "CaptureFreeFallInput", captureArgs);
        state = (PigeonState)captureArgs[1];
        Check(state.FreeFall.Loss == FreeFallChallenge.LossReason.Flap && !state.Grounded
            && state.FreeFall.InputPosition.y > FloorPosition.y,
            "Appui au-dessus du sol conservé comme tentative valide", $"height={state.FreeFall.InputPosition.y - FloorPosition.y:0.000000}");
        state = Move(pigeon, state, Vector3.down * 34f, 0.05f);
        object[] contactArgs = { state };
        Call(pigeon, "CaptureFreeFallContact", contactArgs);
        state = (PigeonState)contactArgs[0];
        Check(state.Grounded && state.FreeFall.Loss == FreeFallChallenge.LossReason.Flap,
            "Le contact après cet appui ne disqualifie pas rétroactivement le joueur");
    }

    private static void CheckRanking(List<PigeonController> pigeons)
    {
        FreeFallChallenge activity = new GameObject("Free fall test activity").AddComponent<FreeFallChallenge>();
        Set(activity, "_groundLayers", (LayerMask)1);
        for (int i = 0; i < pigeons.Count; i++)
            pigeons[i].SimState = State(10f + i);
        var round = new FreeFallState { Phase = FreeFallChallenge.ActivityPhase.Falling, StartCount = 77, Participants = 4 };
        Finish(pigeons[0], FreeFallChallenge.LossReason.Flap, 10f);
        Step(activity, ref round);
        Finish(pigeons[1], FreeFallChallenge.LossReason.Movement, 5f);
        Step(activity, ref round);
        Finish(pigeons[2], FreeFallChallenge.LossReason.Ground, 0f);
        Step(activity, ref round);
        Finish(pigeons[3], FreeFallChallenge.LossReason.Respawn, 0f);
        Step(activity, ref round);
        Check(round.Phase == FreeFallChallenge.ActivityPhase.Results, "Résultats après les quatre tentatives");
        Check(pigeons[1].SimState.FreeFall.Place == 1 && pigeons[0].SimState.FreeFall.Place == 2,
            "Le dernier appui valide gagne même si les disqualifiés terminent plus tard",
            $"places={pigeons[0].SimState.FreeFall.Place},{pigeons[1].SimState.FreeFall.Place},{pigeons[2].SimState.FreeFall.Place},{pigeons[3].SimState.FreeFall.Place}");
        Check(pigeons[2].SimState.FreeFall.Place > 2 && pigeons[3].SimState.FreeFall.Place > 2,
            "Les disqualifiés sont classés après tous les joueurs valides");
        var view = (List<PigeonController>)Get(activity, "_viewParticipants");
        view.Clear(); view.AddRange(pigeons);
        foreach (PigeonController pigeon in pigeons)
            Set(pigeon, "_viewState", pigeon.SimState);
        Set(activity, "_viewState", round);
        string results = (string)Call(activity, "BuildResultsText", new object[] { pigeons[1] });
        Check(results.Contains("Winner!") && results.Contains("DQ"), "Résultats : gagnant valide et disqualifications explicites", results.Replace('\n', '|'));

        for (int i = 0; i < pigeons.Count; i++)
        {
            pigeons[i].SimState = State(0f);
            Finish(pigeons[i], FreeFallChallenge.LossReason.Ground, 0f);
        }
        round = new FreeFallState { Phase = FreeFallChallenge.ActivityPhase.Falling, StartCount = 77, Participants = 4 };
        Step(activity, ref round);
        foreach (PigeonController pigeon in pigeons)
            Set(pigeon, "_viewState", pigeon.SimState);
        Set(activity, "_viewState", round);
        results = (string)Call(activity, "BuildResultsText", new object[] { pigeons[0] });
        Check(!results.Contains("Winner!") && results.Contains("No valid attempt"), "Tous disqualifiés : aucun faux gagnant");
    }

    private static void Finish(PigeonController pigeon, FreeFallChallenge.LossReason reason, float height)
    {
        pigeon.SimState.FreeFall.Loss = reason;
        pigeon.SimState.FreeFall.InputPosition = FloorPosition + Vector3.up * height;
        pigeon.SimState.Grounded = reason == FreeFallChallenge.LossReason.Ground;
    }

    private static PigeonState State(float height) => new PigeonState
    {
        Position = FloorPosition + Vector3.up * height, StunRotation = Quaternion.identity,
        Mode = (byte)PigeonController.MovementState.Falling,
        FreeFall = new FreeFallProgress { Active = true, StartCount = 77, CurrentHeight = height, MovementDeadZone = 0.15f }
    };

    private static PigeonState Move(PigeonController pigeon, PigeonState state, Vector3 velocity, float delta)
    {
        pigeon.transform.position = state.Position;
        Physics.SyncTransforms();
        object[] args = { state, velocity, delta, false };
        Call(pigeon, "MoveController", args);
        return (PigeonState)args[0];
    }

    private static void Step(FreeFallChallenge activity, ref FreeFallState state)
    {
        object[] args = { state, 0.05f };
        Call(activity, "SimulateFalling", args);
        state = (FreeFallState)args[0];
    }

    private static void Check(bool success, string label, string detail = null)
    {
        if (success) _passed++; else _failed++;
        Report.AppendLine($"{(success ? "PASS" : "FAIL")} {label}{(detail == null ? "" : " | " + detail)}");
    }

    private static object Call(object instance, string method, object[] args)
        => instance.GetType().GetMethod(method, PrivateInstance).Invoke(instance, args);
    private static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, PrivateInstance).SetValue(instance, value);
    private static object Get(object instance, string field)
        => instance.GetType().GetField(field, PrivateInstance).GetValue(instance);
}
