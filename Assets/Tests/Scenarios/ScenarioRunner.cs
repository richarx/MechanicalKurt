#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Banc de scénarios « en situation réelle ».
//
//  Lancé depuis Tools > Scénarios : ouvre MainCityScene, entre en Play et joue les scénarios dans la
//  vraie scène, en temps réel, avec la vraie simulation PurrNet. Seuls les joueurs sont remplacés
//  par des bots (BotControls). Chaque étape attend une condition avec un délai maximal ;
//  à la première étape ratée, le scénario s'arrête et ses nettoyages sont exécutés.
//
//  Rapport : panneau à l'écran, console, et Logs/ScenarioReport.txt.
//  Les achievements sont sauvegardés au début puis restaurés à la fin (PlayerPrefs).
//  Limite : hôte solo uniquement (les commandes de test ne passent pas par l'input réseau).
// ─────────────────────────────────────────────────────────────────────────────
public partial class ScenarioRunner : MonoBehaviour
{
    public const string RunKey = "ScenarioRunner.Run";
    public const string ExitOnFinishKey = "ScenarioRunner.ExitOnFinish";
    public const string ActiveKey = "ScenarioRunner.Active";   // lu par ScenarioPauseGuard (éditeur)
    public const string AllScenarios = "*";
    const string ReportPath = "Logs/ScenarioReport.txt";
    const float ScenarioTimeout = 90f;

    class StepResult
    {
        public string Scenario;
        public string Step;
        public bool Ok;
        public float Time;
        public string Detail;
    }

    class ScenarioDef
    {
        public string Name;
        public Func<IEnumerator> Body;
    }

    string _filter = AllScenarios;
    readonly List<StepResult> _results = new List<StepResult>();
    readonly List<Action> _cleanups = new List<Action>();
    readonly Dictionary<string, int> _achievementBackup = new Dictionary<string, int>();
    readonly Dictionary<string, bool?> _scenarioStatus = new Dictionary<string, bool?>();
    List<ScenarioDef> _scenarios;

    string _currentScenario;
    string _currentStep;
    float _scenarioStart;
    bool _scenarioFailed;
    bool _finished;
    bool _achievementsRestored = true;
    Vector2 _scroll;

    // Pigeon du joueur, piloté par un bot pendant les scénarios.
    protected PigeonController Pigeon { get; private set; }
    protected BotControls Bot { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string filter = UnityEditor.SessionState.GetString(RunKey, "");
        if (string.IsNullOrEmpty(filter))
            return;
        UnityEditor.SessionState.EraseString(RunKey);
        Launch(filter);
    }

    public static bool IsRunning => FindAnyObjectByType<ScenarioRunner>() != null;

    /// <summary>Lance les scénarios dont le nom figure dans <paramref name="filter"/> (séparés par des virgules, ou « * »).</summary>
    public static void Launch(string filter)
    {
        if (IsRunning)
            return;
        var go = new GameObject("[ScenarioRunner]");
        go.AddComponent<ScenarioRunner>()._filter = filter;
    }

    IEnumerator Start()
    {
        UnityEditor.SessionState.SetBool(ActiveKey, true);
        _scenarios = BuildScenarios();
        foreach (ScenarioDef def in _scenarios)
            if (Selected(def))
                _scenarioStatus[def.Name] = null;

        _currentScenario = "Préparation";
        _scenarioStart = Time.realtimeSinceStartup;
        yield return WaitForLocalPigeon();
        if (Pigeon == null)
        {
            Record("Pigeon local introuvable après 20 s (réseau démarré ? port UDP libre ?)", false, null);
            Finish();
            yield break;
        }
        if (!BotSpawner.IsHost)
        {
            Record("Les scénarios se lancent depuis l'hôte, pas depuis un client", false, null);
            Finish();
            yield break;
        }

        BackupAndResetAchievements();
        Bot = new BotControls();

        foreach (ScenarioDef def in _scenarios)
        {
            if (!Selected(def))
                continue;
            yield return RunScenario(def);
            yield return new WaitForSecondsRealtime(1f);
        }

        Finish();
    }

    bool Selected(ScenarioDef def) => _filter == AllScenarios || Array.IndexOf(_filter.Split(','), def.Name) >= 0;

    IEnumerator WaitForLocalPigeon()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < deadline && PigeonController.Local == null)
            yield return null;
        Pigeon = PigeonController.Local;
        // Laisser le pigeon apparaître et se poser.
        yield return new WaitForSecondsRealtime(1f);
    }

    IEnumerator RunScenario(ScenarioDef def)
    {
        _currentScenario = def.Name;
        _currentStep = "";
        _scenarioStart = Time.realtimeSinceStartup;
        _scenarioFailed = false;
        Debug.Log($"[Scénario] ▶ {def.Name}");

        Bot.ReleaseAll();
        Pigeon.SetControlsOverride(Bot);
        Defer(() => Pigeon.SetControlsOverride(null));

        yield return Execute(def.Body());

        _scenarioStatus[def.Name] = !_scenarioFailed;
        Debug.Log($"[Scénario] {(_scenarioFailed ? "✘ ÉCHEC" : "✔ OK")} {def.Name} ({Time.realtimeSinceStartup - _scenarioStart:0.0} s)");

        // Nettoyage : en ordre inverse, chaque action isolée des erreurs des autres.
        _currentStep = "Nettoyage";
        for (int i = _cleanups.Count - 1; i >= 0; i--)
        {
            Debug.Log($"[Scénario]   · nettoyage {i + 1}/{_cleanups.Count}");
            try { _cleanups[i](); }
            catch (Exception e) { Debug.LogException(e); }
        }
        _cleanups.Clear();
        Bot.ReleaseAll();

        // Laisser les nettoyages passer par quelques ticks de simulation (échec de course, suppression de bot),
        // puis remettre le pigeon à son point d'apparition pour le scénario suivant.
        yield return new WaitForSecondsRealtime(0.5f);
        Debug.Log("[Scénario]   · respawn du joueur");
        Pigeon.RequestRespawn();
        yield return new WaitForSecondsRealtime(1f);
        Debug.Log("[Scénario]   · prêt pour la suite");
    }

    // Déroule le scénario et ses étapes imbriquées soi-même (pas de StopCoroutine) : s'arrête net au premier
    // échec, au délai maximal, ou sur une exception, qui devient un échec lisible au lieu de figer le runner.
    IEnumerator Execute(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0 && !_scenarioFailed)
        {
            if (Time.realtimeSinceStartup - _scenarioStart > ScenarioTimeout)
            {
                Fail($"Scénario trop long (> {ScenarioTimeout:0} s), étape « {_currentStep} »", null);
                yield break;
            }

            IEnumerator top = stack.Peek();
            bool moved;
            try { moved = top.MoveNext(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail($"Exception pendant « {_currentStep} »", () => $"{e.GetType().Name}: {e.Message}");
                yield break;
            }

            if (!moved)
            {
                stack.Pop();
                continue;
            }

            object current = top.Current;
            // Les attentes Unity (WaitForSecondsRealtime…) sont aussi des IEnumerator : les laisser à Unity.
            if (current is IEnumerator nested && !(current is CustomYieldInstruction))
                stack.Push(nested);
            else
                yield return current;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Primitives des scénarios
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Action exécutée à la fin du scénario, qu'il réussisse ou non.</summary>
    protected void Defer(Action cleanup) => _cleanups.Add(cleanup);

    /// <summary>Attend que la condition devienne vraie, au plus <paramref name="timeout"/> secondes.</summary>
    protected IEnumerator Expect(string step, Func<bool> condition, float timeout, Func<string> detail = null)
    {
        _currentStep = step;
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!SafeEval(condition))
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Fail(step, detail);
                yield return Halt();
            }
            yield return null;
        }
        Record(step, true, detail);
    }

    /// <summary>La condition doit rester vraie pendant toute la durée.</summary>
    protected IEnumerator ExpectStays(string step, Func<bool> condition, float duration, Func<string> detail = null)
    {
        _currentStep = step;
        float end = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < end)
        {
            if (!SafeEval(condition))
            {
                Fail(step, detail);
                yield return Halt();
            }
            yield return null;
        }
        Record(step, true, detail);
    }

    /// <summary>Vérification immédiate.</summary>
    protected IEnumerator Check(string step, bool ok, string detail = null)
    {
        _currentStep = step;
        Func<string> text = detail != null ? () => detail : (Func<string>)null;
        if (ok)
        {
            Record(step, true, text);
            yield break;
        }
        Fail(step, text);
        yield return Halt();
    }

    // Après un échec, le scénario ne doit plus avancer : le runner l'arrête à la frame suivante.
    static IEnumerator Halt()
    {
        while (true)
            yield return null;
    }

    protected IEnumerator Wait(string step, float seconds)
    {
        _currentStep = step;
        yield return new WaitForSecondsRealtime(seconds);
    }

    /// <summary>Téléporte un pigeon par la simulation (comme une zone de départ), puis attend que la vue suive.</summary>
    protected IEnumerator Teleport(PigeonController pigeon, Vector3 position, Quaternion rotation, string label = null)
    {
        PigeonController target = pigeon;
        PigeonController.RunInSimulation(_ => target.ForceTeleport(position, rotation));
        yield return Expect(label ?? $"Téléportation de {pigeon.name}",
            () => (target.transform.position - position).sqrMagnitude < 1f, 2f,
            () => $"position {target.transform.position}, attendue {position}");
    }

    /// <summary>Attend qu'un pigeon soit posé et immobile de façon stable (pas juste une frame).</summary>
    protected IEnumerator ExpectLanded(PigeonController pigeon, float timeout, float stopSpeed = 0.15f)
    {
        PigeonController target = pigeon;
        yield return ExpectHolds($"{pigeon.name} posé et immobile",
            () => target.IsGrounded && target.Speed <= stopSpeed, 0.4f, timeout,
            () => $"au sol {target.IsGrounded}, vitesse {target.Speed:0.00}");
    }

    /// <summary>Attend que la condition reste vraie <paramref name="hold"/> secondes d'affilée, au plus <paramref name="timeout"/>.</summary>
    protected IEnumerator ExpectHolds(string step, Func<bool> condition, float hold, float timeout, Func<string> detail = null)
    {
        _currentStep = step;
        float deadline = Time.realtimeSinceStartup + timeout;
        float since = -1f;
        while (true)
        {
            float now = Time.realtimeSinceStartup;
            if (SafeEval(condition))
            {
                if (since < 0f)
                    since = now;
                if (now - since >= hold)
                    break;
            }
            else
                since = -1f;

            if (now >= deadline)
            {
                Fail(step, detail);
                yield return Halt();
            }
            yield return null;
        }
        Record(step, true, detail);
    }

    // ── Bots supplémentaires (pigeons contrôlés par le serveur) ───────────────
    protected class SpawnedBot
    {
        public PigeonController Pigeon;
        public BotControls Controls;
    }

    protected IEnumerator SpawnBot(Vector3 position, Quaternion rotation, List<SpawnedBot> into)
    {
        PigeonController spawned = null;
        BotControls controls = null;
        yield return BotSpawner.Spawn(position, rotation, $"Bot {into.Count + 1}", (p, c) => { spawned = p; controls = c; });
        yield return Check("Apparition d'un pigeon bot", spawned != null);

        into.Add(new SpawnedBot { Pigeon = spawned, Controls = controls });
        Defer(() => BotSpawner.Delete(spawned));
        yield return new WaitForSecondsRealtime(0.5f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Résultats
    // ─────────────────────────────────────────────────────────────────────────
    bool SafeEval(Func<bool> condition)
    {
        try { return condition(); }
        catch (Exception e)
        {
            Debug.LogException(e);
            return false;
        }
    }

    void Fail(string step, Func<string> detail)
    {
        Record(step, false, detail);
        _scenarioFailed = true;
    }

    void Record(string step, bool ok, Func<string> detail)
    {
        string text = null;
        if (detail != null)
        {
            try { text = detail(); }
            catch (Exception e) { text = e.Message; }
        }

        var result = new StepResult
        {
            Scenario = _currentScenario,
            Step = step,
            Ok = ok,
            Time = Time.realtimeSinceStartup - _scenarioStart,
            Detail = text
        };
        _results.Add(result);
        _lastProgress = Time.realtimeSinceStartup;

        string line = $"[Scénario] {(ok ? "  ✔" : "  ✘")} {step} (t={result.Time:0.00} s){(text != null ? " — " + text : "")}";
        if (ok)
            Debug.Log(line);
        else
            Debug.LogError(line);
    }

    void Finish()
    {
        _finished = true;
        UnityEditor.SessionState.EraseBool(ActiveKey);
        _currentScenario = null;
        RestoreAchievements();

        int ok = 0, failed = 0;
        foreach (bool? status in _scenarioStatus.Values)
        {
            if (status == true) ok++;
            else failed++;
        }

        var report = new StringBuilder();
        report.AppendLine($"Scénarios — {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {ok} OK, {failed} en échec");
        string scenario = null;
        foreach (StepResult r in _results)
        {
            if (r.Scenario != scenario)
            {
                scenario = r.Scenario;
                report.AppendLine();
                report.AppendLine($"== {scenario}");
            }
            report.AppendLine($"  {(r.Ok ? "OK  " : "FAIL")} t={r.Time,6:0.00}s  {r.Step}{(r.Detail != null ? "  — " + r.Detail : "")}");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
        }
        catch (Exception e) { Debug.LogException(e); }

        string summary = $"[Scénario] Terminé : {ok} OK, {failed} en échec. Rapport : {ReportPath}";
        if (failed > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);

        if (UnityEditor.SessionState.GetBool(ExitOnFinishKey, false))
        {
            UnityEditor.SessionState.EraseBool(ExitOnFinishKey);
            UnityEditor.EditorApplication.Exit(failed > 0 ? 1 : 0);
        }
    }

    // ── Achievements : un joueur « neuf » pendant les tests, la vraie sauvegarde ensuite ──
    const string AchievementKeyPrefix = "achievement_";

    void BackupAndResetAchievements()
    {
        AchievementManager manager = AchievementManager.Instance;
        if (manager == null || manager.Achievements == null)
            return;
        foreach (AchievementData a in manager.Achievements)
        {
            if (a == null) continue;
            string key = AchievementKeyPrefix + a.Id;
            _achievementBackup[key] = PlayerPrefs.GetInt(key, 0);
        }
        _achievementsRestored = false;
        manager.ResetAll();
    }

    void RestoreAchievements()
    {
        if (_achievementsRestored)
            return;
        _achievementsRestored = true;
        foreach (KeyValuePair<string, int> entry in _achievementBackup)
        {
            if (entry.Value != 0)
                PlayerPrefs.SetInt(entry.Key, entry.Value);
            else
                PlayerPrefs.DeleteKey(entry.Key);
        }
        PlayerPrefs.Save();
    }

    // Sortie de Play en plein scénario : ne pas laisser la sauvegarde du joueur modifiée.
    void OnDestroy()
    {
        if (!_finished)
            Debug.LogWarning($"[Scénario] Runner détruit avant la fin (scénario « {_currentScenario} », étape « {_currentStep} »).");
        UnityEditor.SessionState.EraseBool(ActiveKey);
        RestoreAchievements();
    }

    // Battement dans le log (utile en ligne de commande) et garde-fou si plus rien n'avance.
    float _nextHeartbeat;
    float _lastProgress;
    const float StallTimeout = 120f;

    void Update()
    {
        if (_finished)
            return;
        float now = Time.realtimeSinceStartup;
        if (_lastProgress <= 0f)
            _lastProgress = now;

        if (now >= _nextHeartbeat)
        {
            _nextHeartbeat = now + 5f;
            Debug.Log($"[Scénario] … {_currentScenario} / {_currentStep} (timeScale {Time.timeScale}, frame {Time.frameCount})");
        }

        if (now - _lastProgress > StallTimeout)
        {
            Record($"Plus aucune progression depuis {StallTimeout:0} s (étape « {_currentStep} »)", false, null);
            StopAllCoroutines();
            Finish();
        }
    }

    void OnGUI()
    {
        const float width = 460f;
        GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, Screen.height * 0.6f), GUI.skin.box);
        GUILayout.Label(_finished ? "<b>Scénarios terminés</b>" : "<b>Scénarios en cours…</b>", RichLabel);
        if (!_finished && _currentScenario != null)
            GUILayout.Label($"▶ {_currentScenario} — {_currentStep} ({Time.realtimeSinceStartup - _scenarioStart:0.0} s)", RichLabel);

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (KeyValuePair<string, bool?> entry in _scenarioStatus)
        {
            string color = entry.Value == null ? "#BBBBBB" : entry.Value == true ? "#66FF66" : "#FF5555";
            string mark = entry.Value == null ? "…" : entry.Value == true ? "✔" : "✘";
            GUILayout.Label($"<color={color}>{mark} {entry.Key}</color>", RichLabel);
            foreach (StepResult r in _results)
            {
                if (r.Scenario != entry.Key || r.Ok)
                    continue;
                GUILayout.Label($"<color=#FF9999>    ✘ {r.Step} (t={r.Time:0.0} s){(r.Detail != null ? "\n       " + r.Detail : "")}</color>", RichLabel);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    static GUIStyle _richLabel;
    static GUIStyle RichLabel => _richLabel ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
}
#endif
