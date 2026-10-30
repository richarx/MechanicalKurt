#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Banc de scénarios « en situation réelle ».
//
//  Lancé depuis Tools > Scénarios ou le DebugManager : ouvre MainCityScene, entre en Play et joue les scénarios
//  dans la vraie scène, en temps réel, avec la vraie simulation PurrNet. Chaque étape attend une condition avec un
//  délai maximal ; à la première étape ratée, le scénario s'arrête et ses nettoyages sont exécutés.
//
//  Deux familles :
//   - solo : l'hôte seul, les autres pigeons sont des bots (BotControls) ;
//   - multi : un vrai second joueur, l'éditeur clone « Player 2 » de Multiplayer Play Mode, client PurrNet.
//     Il est piloté par ScenarioClientAgent via ScenarioRemoteLink : ses actions passent par l'input de son
//     pigeon (vrai réseau), et il renvoie ce qu'il voit sur son écran.
//  Pendant les scénarios solo, le second joueur éventuel est déconnecté de la partie, puis reconnecté.
//
//  Rapport : panneau à l'écran, console, et Logs/ScenarioReport.txt.
//  Les achievements sont sauvegardés au début puis restaurés à la fin (PlayerPrefs).
// ─────────────────────────────────────────────────────────────────────────────
public partial class ScenarioRunner : MonoBehaviour
{
    public const string RunKey = "ScenarioRunner.Run";
    public const string ExitOnFinishKey = "ScenarioRunner.ExitOnFinish";
    public const string ActiveKey = "ScenarioRunner.Active";   // lu par ScenarioPauseGuard (éditeur)
    // Filtre : noms de scénarios séparés par FilterSeparator, ou l'un de ces jokers.
    public const string AllScenarios = "*";
    public const string AllSolo = "*solo";
    public const string AllMulti = "*multi";
    public const char FilterSeparator = '|';   // pas la virgule : certains noms en contiennent
    const string ReportPath = "Logs/ScenarioReport.txt";
    const float ScenarioTimeout = 90f;
    const float MultiScenarioTimeout = 150f;
    const float RemoteWaitInteractive = 45f;
    const float RemoteWaitCommandLine = 300f;

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
        public bool Multi;   // demande un vrai second joueur (Player 2)
    }

    string _filter = AllSolo;
    readonly List<StepResult> _results = new List<StepResult>();
    readonly List<Action> _cleanups = new List<Action>();
    readonly Dictionary<string, int> _achievementBackup = new Dictionary<string, int>();
    readonly Dictionary<string, bool?> _scenarioStatus = new Dictionary<string, bool?>();
    List<ScenarioDef> _scenarios;

    string _currentScenario;
    string _currentStep;
    float _scenarioStart;
    float _scenarioTimeout = ScenarioTimeout;
    bool _scenarioFailed;
    bool _finished;
    bool _commandLine;
    bool _achievementsRestored = true;
    Vector2 _scroll;

    // Second joueur (scénarios multi) : null tant qu'aucun n'est attendu.
    ScenarioRemoteLink _link;
    bool _lastRemoteOk;
    string _lastRemoteDetail;

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
        _scenarios = BuildScenarios();

        // Les solo d'abord, les multi ensuite : une seule déconnexion / reconnexion du second joueur.
        var selected = new List<ScenarioDef>();
        foreach (ScenarioDef def in _scenarios)
            if (Selected(def) && !def.Multi)
                selected.Add(def);
        foreach (ScenarioDef def in _scenarios)
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

        // Après la sauvegarde des achievements : le second joueur repart lui aussi d'une sauvegarde vide.
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
        // Laisser le pigeon apparaître et se poser.
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

        // Le second joueur est piloté par les tests pendant le scénario (son clavier n'agit plus).
        bool withRemote = def.Multi && RemoteReady;
        if (withRemote)
        {
            yield return RemoteCommand("override", "on");
            yield return RemoteCommand("release");
        }

        yield return Execute(def.Multi && !withRemote ? MissingRemote() : def.Body());

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
        if (def.Multi && _link != null && _link.Connected)
        {
            yield return RemoteCommand("play-dialogs", "off");
            yield return RemoteCommand("release");
            yield return RemoteCommand("cancel-dialogs");
        }

        // Laisser les nettoyages passer par quelques ticks de simulation (échec de course, suppression de bot),
        // puis remettre les pigeons à leur point d'apparition pour le scénario suivant.
        yield return new WaitForSecondsRealtime(0.5f);
        yield return SettleRace();
        Debug.Log("[Scénario]   · respawn du joueur");
        Pigeon.RequestRespawn();
        if (def.Multi && RemoteReady)
            yield return RemoteCommand("respawn");   // seul le joueur qui possède le pigeon peut le respawn
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

    // ── Second joueur (vrai client : éditeur clone « Player 2 ») ─────────────
    //  Ces étapes tournent entre les scénarios : pas d'Expect / Check (ils bloquent jusqu'au scénario suivant).

    /// <summary>Second joueur relié, en jeu, et son pigeon présent chez l'hôte.</summary>
    protected bool RemoteReady => _link != null && _link.Connected && _link.Get("net") == "connected"
        && _link.Get("local") == "1" && RemotePigeon != null;

    /// <summary>Le pigeon du second joueur, tel que l'hôte le simule (null s'il n'est pas en jeu).</summary>
    protected PigeonController RemotePigeon
    {
        get
        {
            if (_link == null || !_link.Connected)
                return null;
            string id = _link.Get("net/player");
            if (id == null)
                return null;
            foreach (PigeonController pigeon in PigeonController.All)
                if (pigeon != Pigeon && pigeon.owner.HasValue && !pigeon.owner.Value.isBot
                    && pigeon.owner.Value.id.value.ToString(CultureInfo.InvariantCulture) == id)
                    return pigeon;
            return null;
        }
    }

    /// <summary>Un pigeon de joueur autre que celui de l'hôte (les bots n'ont pas de propriétaire).</summary>
    static bool HasRemotePlayerPigeon()
    {
        foreach (PigeonController pigeon in PigeonController.All)
            if (pigeon != PigeonController.Local && pigeon.owner.HasValue && !pigeon.owner.Value.isBot)
                return true;
        return false;
    }

    // Multiplayer Play Mode : un joueur virtuel est activé (Window > Multiplayer > Multiplayer Play Mode).
    static bool MppmVirtualPlayerActive()
    {
        try
        {
            string path = Path.Combine("Library", "VP", "SystemData.json");
            if (!File.Exists(path))
                return false;
            string json = File.ReadAllText(path);
            return Regex.IsMatch(json, "\"IsMppmActive\"\\s*:\\s*true")
                && Regex.IsMatch(json, "\"Active\"\\s*:\\s*true\\s*,\\s*\"Index\"\\s*:\\s*\\d+\\s*,\\s*\"Type\"\\s*:\\s*1\\b");
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Ouvre le lien et attend le second joueur : toujours si un scénario multi est choisi ; sinon seulement s'il y en
    // a un (les scénarios solo le déconnectent le temps de leur passage).
    IEnumerator PrepareRemote(bool required)
    {
        bool expected = required || HasRemotePlayerPigeon() || (!_commandLine && MppmVirtualPlayerActive());
        if (!expected)
            yield break;

        _currentScenario = "Second joueur";
        _scenarioStart = Time.realtimeSinceStartup;
        _link = new ScenarioRemoteLink();
        if (!_link.Listen())
        {
            Record($"Lien de test avec le second joueur impossible (port {ScenarioLinkProtocol.Port})", false, () => _link.ListenError);
            _link = null;
            yield break;
        }

        float timeout = required ? (_commandLine ? RemoteWaitCommandLine : RemoteWaitInteractive) : RemoteWaitInteractive;
        Debug.Log($"[Scénario] En attente du second joueur (au plus {timeout:0} s)…");
        float deadline = Time.realtimeSinceStartup + timeout;
        float nextConnect = 0f;
        // Solo seul : il suffit d'être relié (il sera déconnecté). Multi : il faut qu'il soit en jeu.
        while (Time.realtimeSinceStartup < deadline && !(required ? RemoteReady : _link.Connected))
        {
            _lastProgress = Time.realtimeSinceStartup;   // attente normale : pas un blocage
            // Clone entré en Play avant le serveur : son client PurrNet a abandonné et ne réessaie pas seul.
            if (required && _link.Connected && _link.Get("net") == "offline" && Time.realtimeSinceStartup >= nextConnect)
            {
                _link.Send("connect");
                nextConnect = Time.realtimeSinceStartup + 5f;
            }
            yield return null;
        }

        if (_link.Connected)
            yield return ResetRemoteAchievementsOnce();
        if (!(required ? RemoteReady : _link.Connected))
        {
            Record(required ? "Aucun second joueur : les scénarios multi vont échouer" : "Second joueur annoncé mais jamais relié",
                false, RemoteMissingReason);
            yield break;
        }
        Record("Second joueur relié (éditeur clone, client PurrNet)", true, RemoteState);
    }

    // Le clone a chargé ses achievements au démarrage, depuis la vraie sauvegarde (partagée avec l'hôte) : il repart
    // d'une sauvegarde vide, une seule fois, la vraie étant restaurée par l'hôte à la fin (Finish).
    bool _remoteAchievementsReset;

    IEnumerator ResetRemoteAchievementsOnce()
    {
        if (_remoteAchievementsReset || _link == null || !_link.Connected)
            yield break;
        yield return RemoteCommand("reset-achievements");
        _remoteAchievementsReset = _lastRemoteOk;
        if (!_lastRemoteOk)
            Debug.LogWarning($"[Scénario] Achievements du second joueur non remis à zéro : {_lastRemoteDetail}");
    }

    string RemoteMissingReason()
    {
        if (_link == null)
            return "lien de test non ouvert";
        if (!_link.Connected)
            return "aucun éditeur clone relié : activer Player 2 (Window > Multiplayer > Multiplayer Play Mode), "
                + "ou lancer ScenarioMenu.JoinAsClientFromCommandLine dans le clone";
        return $"relié mais pas en jeu : réseau {_link.Get("net") ?? "?"}, pigeon local {_link.Get("local") ?? "?"}, "
            + $"pigeon vu par l'hôte {(RemotePigeon != null ? "oui" : "non")}";
    }

    IEnumerator MissingRemote()
    {
        yield return Check("Second joueur en jeu", false, RemoteMissingReason());
    }

    // Avant un scénario solo : le second joueur quitte la partie (sinon il compte dans les départs de course, etc.).
    IEnumerator EnsureRemoteOffline()
    {
        if (_link == null || !_link.Connected)
        {
            if (HasRemotePlayerPigeon())
                Debug.LogWarning("[Scénario] Un autre joueur est en jeu sans agent de test : les scénarios solo peuvent échouer.");
            yield break;
        }
        if (_link.Get("net") == "offline" && !HasRemotePlayerPigeon())
            yield break;

        yield return RemoteCommand("override", "off");
        yield return RemoteCommand("disconnect");
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < deadline && (HasRemotePlayerPigeon() || _link.Get("net") != "offline"))
        {
            _lastProgress = Time.realtimeSinceStartup;
            yield return null;
        }
        if (HasRemotePlayerPigeon())
        {
            string previous = _currentScenario;
            _currentScenario = "Second joueur";
            Record("Le second joueur n'a pas quitté la partie : les scénarios solo peuvent échouer", false, RemoteState);
            _currentScenario = previous;
        }
        yield return new WaitForSecondsRealtime(0.5f);
    }

    // Avant un scénario multi : le second joueur (re)joint la partie.
    IEnumerator EnsureRemoteOnline()
    {
        if (_link == null || !_link.Connected)
            yield break;
        yield return ResetRemoteAchievementsOnce();
        if (RemoteReady)
            yield break;

        yield return RemoteCommand("connect");
        float deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline && !RemoteReady)
        {
            _lastProgress = Time.realtimeSinceStartup;
            yield return null;
        }
        // Laisser le pigeon apparaître et se poser.
        if (RemoteReady)
            yield return new WaitForSecondsRealtime(1f);
    }

    // Fin : rendre au second joueur sa partie telle qu'avant les tests (en jeu, commandes au clavier).
    IEnumerator ReleaseRemote()
    {
        if (_link == null || !_link.Connected)
            yield break;
        yield return EnsureRemoteOnline();
        yield return RemoteCommand("override", "off");
        yield return RemoteCommand(_commandLine ? "quit" : "bye");
    }

    /// <summary>Commande au second joueur ; résultat dans _lastRemoteOk / _lastRemoteDetail (sans faire échouer).</summary>
    IEnumerator RemoteCommand(string verb, string argument = "", float timeout = 5f)
    {
        _lastRemoteOk = false;
        _lastRemoteDetail = "pas de second joueur relié";
        if (_link == null || !_link.Connected)
            yield break;

        int id = _link.Send(verb, argument);
        float deadline = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (_link.TryGetReply(id, out bool ok, out string detail))
            {
                _lastRemoteOk = ok;
                _lastRemoteDetail = detail;
                yield break;
            }
            if (!_link.Connected)
            {
                _lastRemoteDetail = "lien perdu";
                yield break;
            }
            yield return null;
        }
        _lastRemoteDetail = $"pas de réponse en {timeout:0} s";
    }

    /// <summary>Résumé de ce que voit le second joueur, pour le détail des étapes.</summary>
    protected string RemoteState()
    {
        if (_link == null || !_link.Connected)
            return "second joueur non relié";
        var sb = new StringBuilder();
        sb.Append($"chez le second joueur : réseau {_link.Get("net")}, pigeon {_link.Get("local/pos") ?? "absent"}");
        sb.Append($", gelé {_link.Get("local/frozen")}, prêt {_link.Get("local/ready")}, piloté {_link.Get("local/driven")}");
        string dialogs = _link.Get("dialogs");
        if (!string.IsNullOrEmpty(dialogs))
            sb.Append($", dialogues [{dialogs}]");
        foreach (KeyValuePair<string, string> entry in _link.Snapshot)
            if (entry.Key.StartsWith("zone/", StringComparison.Ordinal) || entry.Key.StartsWith("dialogs/", StringComparison.Ordinal)
                || (entry.Key.StartsWith("race/", StringComparison.Ordinal)
                && (entry.Key.EndsWith("/phase", StringComparison.Ordinal) || entry.Key.EndsWith("/departure", StringComparison.Ordinal)
                    || entry.Key.EndsWith("/arrival", StringComparison.Ordinal))))
                sb.Append($", {entry.Key} « {entry.Value} »");
        sb.Append($" (instantané il y a {Time.realtimeSinceStartup - _link.LastSnapshotTime:0.00} s)");
        return sb.ToString();
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
        _link?.Dispose();
        _link = null;

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
        _link?.Dispose();
        _link = null;
    }

    // Battement dans le log (utile en ligne de commande) et garde-fou si plus rien n'avance.
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
