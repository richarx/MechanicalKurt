#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Banc de scénarios en situation réelle (vraie scène, vraie simulation PurrNet), lancé depuis Tools > Scénarios ou
/// le DebugManager. Solo : l'hôte et des bots. Multi : un vrai second joueur (Player 2 de Multiplayer Play Mode),
/// déconnecté pendant les scénarios solo. Rapport : Logs/ScenarioReport.txt.
/// </summary>
public partial class ScenarioRunner : MonoBehaviour
{
    public const string RunKey = "ScenarioRunner.Run";
    public const string ExitOnFinishKey = "ScenarioRunner.ExitOnFinish";
    public const string ActiveKey = "ScenarioRunner.Active";
    public const string AllScenarios = "*";
    public const string AllSolo = "*solo";
    public const string AllMulti = "*multi";
    public const char FilterSeparator = '|';
    const float ScenarioTimeout = 90f;
    const float MultiScenarioTimeout = 150f;

    class StepResult
    {
        public string Scenario;
        public string Step;
        public bool Ok;
        public float Time;
        public string Detail;
    }

    string _filter = AllSolo;
    readonly List<StepResult> _results = new List<StepResult>();
    readonly List<Action> _cleanups = new List<Action>();
    readonly Dictionary<string, bool?> _scenarioStatus = new Dictionary<string, bool?>();

    string _currentScenario;
    string _currentStep;
    float _scenarioStart;
    float _scenarioTimeout = ScenarioTimeout;
    bool _scenarioFailed;
    bool _finished;
    bool _commandLine;

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

    /// <summary>
    /// Lance les scénarios dont le nom figure dans <paramref name="filter"/> (séparés par FilterSeparator),
    /// ou AllSolo / AllMulti / AllScenarios.
    /// </summary>
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
        _commandLine = UnityEditor.SessionState.GetBool(ExitOnFinishKey, false);
        MissionStartZone.TraceTransitions = true;

        var selected = new List<ScenarioDef>();
        foreach (ScenarioDef def in Catalog)
            if (Selected(def) && !def.Multi)
                selected.Add(def);
        foreach (ScenarioDef def in Catalog)
            if (Selected(def) && def.Multi)
                selected.Add(def);
        foreach (ScenarioDef def in selected)
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

        yield return PrepareRemote(selected.Exists(def => def.Multi));

        foreach (ScenarioDef def in selected)
        {
            if (def.Multi)
                yield return EnsureRemoteOnline();
            else
                yield return EnsureRemoteOffline();
            yield return RunScenario(def);
            yield return new WaitForSecondsRealtime(1f);
        }

        yield return ReleaseRemote();
        Finish();
    }

    bool Selected(ScenarioDef def)
    {
        foreach (string part in _filter.Split(FilterSeparator))
        {
            string token = part.Trim();
            if (token == AllScenarios || token == def.Name
                || (token == AllSolo && !def.Multi) || (token == AllMulti && def.Multi))
                return true;
        }
        return false;
    }

    IEnumerator WaitForLocalPigeon()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < deadline && PigeonController.Local == null)
            yield return null;
        Pigeon = PigeonController.Local;
        yield return new WaitForSecondsRealtime(1f);
    }

    IEnumerator RunScenario(ScenarioDef def)
    {
        _currentScenario = def.Name;
        _currentStep = "";
        _scenarioStart = Time.realtimeSinceStartup;
        _scenarioTimeout = def.Multi ? MultiScenarioTimeout : ScenarioTimeout;
        _scenarioFailed = false;
        Debug.Log($"[Scénario] ▶ {def.Name}");

        Bot.ReleaseAll();
        Pigeon.SetControlsOverride(Bot);
        Defer(() => Pigeon.SetControlsOverride(null));

        bool withRemote = def.Multi && RemoteReady;
        if (withRemote)
        {
            yield return RemoteCommand("override", "on");
            yield return RemoteCommand("release");
        }

        yield return Execute(def.Multi && !withRemote ? MissingRemote() : def.Body(this));

        _scenarioStatus[def.Name] = !_scenarioFailed;
        Debug.Log($"[Scénario] {(_scenarioFailed ? "✘ ÉCHEC" : "✔ OK")} {def.Name} ({Time.realtimeSinceStartup - _scenarioStart:0.0} s)");

        _currentStep = "Nettoyage";
        for (int i = _cleanups.Count - 1; i >= 0; i--)
        {
            Debug.Log($"[Scénario]   · nettoyage {i + 1}/{_cleanups.Count}");
            try { _cleanups[i](); }
            catch (Exception e) { Debug.LogException(e); }
        }
        _cleanups.Clear();
        Bot.ReleaseAll();
        if (def.Multi && _link != null && _link.Connected)
        {
            yield return RemoteCommand("play-dialogs", "off");
            yield return RemoteCommand("release");
            yield return RemoteCommand("cancel-dialogs");
        }

        yield return new WaitForSecondsRealtime(0.5f);
        yield return SettleRace();
        Debug.Log("[Scénario]   · respawn du joueur");
        Pigeon.RequestRespawn();
        if (def.Multi && RemoteReady)
            yield return RemoteCommand("respawn");
        yield return new WaitForSecondsRealtime(1f);
        Debug.Log("[Scénario]   · prêt pour la suite");
    }

    IEnumerator Execute(IEnumerator root)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0 && !_scenarioFailed)
        {
            if (Time.realtimeSinceStartup - _scenarioStart > _scenarioTimeout)
            {
                Fail($"Scénario trop long (> {_scenarioTimeout:0} s), étape « {_currentStep} »", null);
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
            if (current is IEnumerator nested && !(current is CustomYieldInstruction))
                stack.Push(nested);
            else
                yield return current;
        }
    }

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
        var trace = new StringBuilder();
        float start = Time.realtimeSinceStartup, nextSample = 0f;
        bool Landed()
        {
            float t = Time.realtimeSinceStartup - start;
            if (t >= nextSample && trace.Length < 900)
            {
                nextSample = t + 0.25f;
                Vector3 p = target.transform.position;
                trace.Append($" [{t:0.00}s {(target.IsGrounded ? "sol" : "air")} v{target.Speed:0.00} ({p.x:0.0},{p.y:0.00},{p.z:0.0})]");
            }
            return target.IsGrounded && target.Speed <= stopSpeed;
        }
        yield return ExpectHolds($"{pigeon.name} posé et immobile", Landed, 0.4f, timeout,
            () => $"au sol {target.IsGrounded}, vitesse {target.Speed:0.00} — trace :{trace}");
    }

    /// <summary>Attend qu'un pigeon soit immobile (au sol ou en vol stationnaire) de façon stable.</summary>
    protected IEnumerator ExpectStill(PigeonController pigeon, float timeout, float stopSpeed = 0.15f)
    {
        PigeonController target = pigeon;
        yield return ExpectHolds($"{pigeon.name} immobile", () => target.Speed <= stopSpeed, 0.4f, timeout,
            () => $"au sol {target.IsGrounded}, vitesse {target.Speed:0.00}, position {target.transform.position}");
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

    void OnDestroy()
    {
        if (!_finished)
            Debug.LogWarning($"[Scénario] Runner détruit avant la fin (scénario « {_currentScenario} », étape « {_currentStep} »).");
        UnityEditor.SessionState.EraseBool(ActiveKey);
        RestoreAchievements();
        _link?.Dispose();
        _link = null;
    }

    float _nextHeartbeat;
    float _lastProgress;
    const float StallTimeout = 120f;

    void Update()
    {
        _link?.Poll();
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
}
#endif
