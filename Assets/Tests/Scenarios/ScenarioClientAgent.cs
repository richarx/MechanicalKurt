#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using PurrNet;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  Côté second joueur : l'éditeur clone de Multiplayer Play Mode (« Player 2 »), client PurrNet.
//
//  Se relie au ScenarioRunner de l'hôte quand celui-ci tourne (sinon ne fait rien), puis :
//   - joue les actions demandées comme le ferait le joueur : input de son pigeon (BotControls, envoyé au serveur
//     par le vrai réseau), fin de cinématique, respawn, déconnexion / reconnexion PurrNet ;
//   - envoie à l'hôte ce que ce joueur voit : son pigeon, la zone de départ, la course, le tuto, les dialogues,
//     les objets à ramasser, ses achievements, ses sons.
//  Éditeur uniquement : rien de tout ça n'existe dans un build.
// ─────────────────────────────────────────────────────────────────────────────
public class ScenarioClientAgent : MonoBehaviour
{
    /// <summary>Clone lancé en ligne de commande (ScenarioMenu.JoinAsClientFromCommandLine) : quitter à la fin.</summary>
    public const string ExitOnFinishKey = "ScenarioClientAgent.ExitOnFinish";

    const float RetryInterval = 1f;
    const float SnapshotInterval = 0.1f;
    const float ConfirmInterval = 0.3f;
    const float CommandLineIdleTimeout = 600f;

    TcpClient _tcp;
    Task _connecting;
    ScenarioLinkConnection _link;
    readonly List<string> _lines = new List<string>();
    readonly StringBuilder _snapshot = new StringBuilder();
    readonly BotControls _bot = new BotControls();

    bool _overrideWanted;
    bool _disconnectedByHost;
    PigeonController _overridden;
    bool _playDialogs;
    float _nextConfirm;
    float _nextAttempt;
    float _nextSnapshot;
    float _startTime;
    bool _everLinked;
    bool _commandLine;
    bool _quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // Seulement dans un clone (Assets en jonction, comme le détecte PurrNet) : l'éditeur principal est l'hôte.
        if (!PurrNet.Utils.ApplicationContext.isClone || FindAnyObjectByType<ScenarioClientAgent>() != null)
            return;
        var go = new GameObject("[ScenarioClientAgent]");
        DontDestroyOnLoad(go);
        go.AddComponent<ScenarioClientAgent>();
    }

    void Start()
    {
        _startTime = Time.realtimeSinceStartup;
        _commandLine = UnityEditor.SessionState.GetBool(ExitOnFinishKey, false);
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (_link != null && !_link.IsOpen)
            OnLinkLost();

        if (_link == null)
            TryConnect(now);
        else
        {
            _lines.Clear();
            _link.Receive(_lines);
            foreach (string line in _lines)
                Handle(line);
        }

        ApplyOverride();
        if (_playDialogs)
            ConfirmDialogs(now);

        if (_link != null && _link.IsOpen && now >= _nextSnapshot)
        {
            _nextSnapshot = now + SnapshotInterval;
            _link.Send("SNAP\t" + BuildSnapshot());
        }

        if (!_everLinked && now - _startTime > CommandLineIdleTimeout)
            Quit($"aucun hôte en {CommandLineIdleTimeout:0} s", 1);
    }

    void OnDestroy()
    {
        _link?.Close();
        _link = null;
        CloseTcp();
        UnityEditor.SessionState.EraseBool(ScenarioRunner.ActiveKey);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Connexion à l'hôte
    // ─────────────────────────────────────────────────────────────────────────
    void TryConnect(float now)
    {
        if (_connecting != null)
        {
            if (!_connecting.IsCompleted)
                return;
            bool connected = !_connecting.IsFaulted && !_connecting.IsCanceled && _tcp != null && _tcp.Connected;
            _ = _connecting.Exception;   // observée : pas d'« unobserved task exception » quand l'hôte n'écoute pas
            _connecting = null;
            if (!connected)
            {
                CloseTcp();
                _nextAttempt = now + RetryInterval;
                return;
            }

            _link = new ScenarioLinkConnection(_tcp);
            _tcp = null;
            _everLinked = true;
            _nextSnapshot = 0f;
            // « Error Pause » ignorée ici aussi pendant les tests (ScenarioPauseGuard) : un clone en pause n'enverrait
            // plus ni input ni instantané, et toute la suite multi échouerait.
            UnityEditor.SessionState.SetBool(ScenarioRunner.ActiveKey, true);
            _link.Send($"HELLO\t{ScenarioLinkProtocol.Version}");
            Debug.Log("[Agent] Relié aux scénarios de l'hôte : ce joueur est piloté par les tests.");
            return;
        }

        if (now < _nextAttempt)
            return;
        try
        {
            _tcp = new TcpClient(AddressFamily.InterNetwork);
            _connecting = _tcp.ConnectAsync(IPAddress.Loopback, ScenarioLinkProtocol.Port);
        }
        catch (Exception)
        {
            CloseTcp();
            _nextAttempt = now + RetryInterval;
        }
    }

    void CloseTcp()
    {
        try { _tcp?.Close(); }
        catch (Exception) { }
        _tcp = null;
    }

    void OnLinkLost()
    {
        _link = null;
        _nextAttempt = Time.realtimeSinceStartup + RetryInterval;
        UnityEditor.SessionState.EraseBool(ScenarioRunner.ActiveKey);
        StopDriving();
        Debug.Log("[Agent] Lien avec l'hôte terminé : le joueur reprend la main.");
        Quit("hôte parti", 0);
    }

    // Le joueur reprend la main : plus d'input scripté, plus de dialogue validé tout seul, et de retour dans la
    // partie si les tests l'en avaient sorti (même si l'hôte s'est arrêté sans le reconnecter).
    void StopDriving()
    {
        _overrideWanted = false;
        _playDialogs = false;
        _bot.ReleaseAll();
        ApplyOverride();
        NetworkManager network = NetworkManager.main;
        if (_disconnectedByHost && !_commandLine && network != null && network.clientState == PurrNet.Transports.ConnectionState.Disconnected)
            network.StartClient();
        _disconnectedByHost = false;
    }

    // Seulement pour un clone lancé en ligne de commande : dans Multiplayer Play Mode, le joueur reste en jeu.
    void Quit(string reason, int code)
    {
        if (!_commandLine || _quitting)
            return;
        _quitting = true;
        Debug.Log($"[Agent] Fin ({reason}) : fermeture de l'éditeur clone.");
        UnityEditor.SessionState.EraseBool(ExitOnFinishKey);
        UnityEditor.EditorApplication.Exit(code);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Commandes
    // ─────────────────────────────────────────────────────────────────────────
    void Handle(string line)
    {
        string[] parts = line.Split('\t');
        if (parts.Length < 3 || parts[0] != "CMD")
            return;
        string id = parts[1];
        string verb = parts[2];
        string argument = parts.Length > 3 ? parts[3] : "";

        string detail;
        bool ok;
        try
        {
            ApplyOverride();
            detail = Execute(verb, argument);
            ok = true;
        }
        catch (Exception e)
        {
            detail = e.Message;
            ok = false;
        }
        _link?.Send($"ACK\t{id}\t{(ok ? "ok" : "err")}\t{ScenarioLinkProtocol.Clean(detail)}");

        if (ok && verb == "quit")
            Quit("fin des scénarios", 0);
    }

    string Execute(string verb, string argument)
    {
        PigeonController local = PigeonController.Local;
        NetworkManager network = NetworkManager.main;
        switch (verb)
        {
            case "override":
                _overrideWanted = argument == "on";
                if (!_overrideWanted)
                    _bot.ReleaseAll();
                ApplyOverride();
                return _overrideWanted ? "input piloté par les tests" : "input rendu au joueur";

            case "release":
                _bot.ReleaseAll();
                return "";

            case "interact":
                RequireDriven(local);
                _bot.PressInteract();
                return "";

            case "flap":
                RequireDriven(local);
                _bot.Flap = argument == "1";
                return "";

            case "brake":
                RequireDriven(local);
                _bot.Brake = argument == "1";
                return "";

            case "respawn":
                if (local == null)
                    throw new InvalidOperationException("pas de pigeon local");
                local.RequestRespawn();
                return "";

            case "play-dialogs":
                _playDialogs = argument != "off";
                _nextConfirm = 0f;
                return "";

            case "cancel-dialogs":
            {
                int cancelled = 0;
                foreach (DialogCinematic cinematic in FindObjectsByType<DialogCinematic>(FindObjectsInactive.Exclude))
                    if (cinematic.IsPlaying)
                    {
                        cinematic.CancelCinematic();
                        cancelled++;
                    }
                return $"{cancelled} cinématique(s) interrompue(s)";
            }

            case "finish-cinematics":
            {
                // Filet de nettoyage : interrompt ses cinématiques et signale directement leur fin (input du pigeon).
                foreach (DialogCinematic cinematic in FindObjectsByType<DialogCinematic>(FindObjectsInactive.Exclude))
                    if (cinematic.IsPlaying)
                        cinematic.CancelCinematic();
                if (local == null)
                    return "pas de pigeon local";
                foreach (PigeonCharacterRace race in FindObjectsByType<PigeonCharacterRace>(FindObjectsInactive.Exclude))
                {
                    if (argument == "arrival")
                        race.ReportArrivalCinematicFinished(local);
                    else if (race.DepartureZone != null)
                        race.DepartureZone.ReportLocalCinematicFinished();
                }
                return "";
            }

            case "disconnect":
                if (network == null)
                    throw new InvalidOperationException("pas de NetworkManager");
                _disconnectedByHost = true;
                network.StopClient();
                return "";

            case "connect":
                if (network == null)
                    throw new InvalidOperationException("pas de NetworkManager");
                _disconnectedByHost = false;
                // Déjà en cours de connexion : StartClient loguerait une erreur (« already running »).
                if (network.clientState == PurrNet.Transports.ConnectionState.Disconnected)
                    network.StartClient();
                return network.clientState.ToString();

            case "reset-achievements":
                if (AchievementManager.Instance != null)
                    AchievementManager.Instance.ResetAll();
                return "";

            case "bye":
                StopDriving();
                return "";

            case "quit":
                StopDriving();
                return "";

            default:
                throw new InvalidOperationException($"commande inconnue « {verb} »");
        }
    }

    void RequireDriven(PigeonController local)
    {
        if (local == null)
            throw new InvalidOperationException("pas de pigeon local");
        if (!_overrideWanted)
            throw new InvalidOperationException("input non piloté (envoyer « override on » d'abord)");
    }

    // Le pigeon local change à chaque (re)connexion : y remettre l'input scripté tant qu'il est demandé.
    void ApplyOverride()
    {
        PigeonController local = PigeonController.Local;
        if (_overrideWanted)
        {
            if (local != null && !ReferenceEquals(local.Controls, _bot))
            {
                local.SetControlsOverride(_bot);
                _overridden = local;
            }
            return;
        }
        if (_overridden != null && ReferenceEquals(_overridden.Controls, _bot))
            _overridden.SetControlsOverride(null);
        _overridden = null;
    }

    // Valide les répliques comme un joueur (même rythme que PlayThroughDialogCinematic côté hôte).
    void ConfirmDialogs(float now)
    {
        DialogManager dialogs = DialogManager.instance;
        if (dialogs == null || !dialogs.IsPlaying || now < _nextConfirm)
            return;
        dialogs.RequestConfirm();
        _nextConfirm = now + ConfirmInterval;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ce que voit ce joueur
    // ─────────────────────────────────────────────────────────────────────────
    string BuildSnapshot()
    {
        _snapshot.Clear();
        NetworkManager network = NetworkManager.main;
        Add("net", network == null ? "none"
            : network.clientState == PurrNet.Transports.ConnectionState.Disconnected ? "offline"
            : network.clientState == PurrNet.Transports.ConnectionState.Connected && network.isLocalPlayerReady ? "connected" : "connecting");
        if (network != null && network.isClient && network.isLocalPlayerReady)
            Add("net/player", network.localPlayer.id.value.ToString(CultureInfo.InvariantCulture));
        Add("driven", Flag(_overrideWanted));
        Add("dialogs/auto", Flag(_playDialogs));

        PigeonController local = PigeonController.Local;
        Add("local", Flag(local != null));
        if (local != null)
        {
            Add("local/pos", ScenarioLinkProtocol.Format(local.transform.position));
            Add("local/frozen", Flag(local.IsMovementFrozen));
            Add("local/grounded", Flag(local.IsGrounded));
            Add("local/speed", ScenarioLinkProtocol.Format(local.Speed));
            Add("local/ready", Flag(local.WantsStart));
            Add("local/equipment", local.WornEquipment.ToString(CultureInfo.InvariantCulture));
            PlayerControls controls = local.GetComponent<PlayerControls>();
            Add("local/blocked", Flag(controls != null && controls.AreControlsBlocked));
            Add("local/driven", Flag(ReferenceEquals(local.Controls, _bot)));
        }
        Add("pigeons", PigeonController.All.Count.ToString(CultureInfo.InvariantCulture));

        foreach (MissionStartZone zone in FindObjectsByType<MissionStartZone>(FindObjectsInactive.Exclude))
        {
            string key = "zone/" + zone.name;
            Add(key + "/phase", zone.Phase.ToString(CultureInfo.InvariantCulture));
            Add(key + "/hud", zone.HudText ?? "");
            Add(key + "/prompt", Flag(ScenarioRunner.PromptVisible(zone.Prompt)));
        }

        foreach (PigeonCharacterRace race in FindObjectsByType<PigeonCharacterRace>(FindObjectsInactive.Exclude))
        {
            string key = "race/" + race.name;
            Add(key + "/phase", race.Phase.ToString());
            Add(key + "/departure", Flag(race.DepartureCinematic != null && race.DepartureCinematic.IsPlaying));
            Add(key + "/arrival", Flag(race.ArrivalCinematic != null && race.ArrivalCinematic.IsPlaying));
            if (race.Character != null)
                Add(key + "/character", ScenarioLinkProtocol.Format(race.Character.position));
            float failure = race.FailureMessage != null && race.FailureMessage.Group != null ? race.FailureMessage.Group.alpha : 0f;
            Add(key + "/failure", ScenarioLinkProtocol.Format(failure));
            Add(key + "/marker", Flag(race.DepartureMarker != null && race.DepartureMarker.activeInHierarchy));
        }

        foreach (PigeonStopDialogCinematic tuto in FindObjectsByType<PigeonStopDialogCinematic>(FindObjectsInactive.Exclude))
        {
            if (tuto.StopZone == null)
                continue;
            string key = "tuto/" + tuto.name;
            Add(key + "/consumed", Flag(!tuto.StopZone.enabled));
            Add(key + "/playing", Flag(tuto.Cinematic != null && tuto.Cinematic.IsPlaying));
        }

        var playing = new List<string>();
        foreach (DialogCinematic cinematic in FindObjectsByType<DialogCinematic>(FindObjectsInactive.Exclude))
        {
            if (cinematic.IsPlaying)
                playing.Add(cinematic.name);
            // Diagnostic : nombre de démarrages de chaque cinématique (une relance après interruption se voit ici).
            if (_watchedCinematics.Add(cinematic))
            {
                DialogCinematic watched = cinematic;
                watched.OnCinematicStarted.AddListener(() => _cinematicStarts[watched.name] = StartsOf(watched.name) + 1);
            }
        }
        Add("dialogs", string.Join(ScenarioLinkProtocol.ListSeparator, playing));
        foreach (KeyValuePair<string, int> entry in _cinematicStarts)
            Add("dialogs/starts/" + entry.Key, entry.Value.ToString(CultureInfo.InvariantCulture));
        DialogManager dialogManager = DialogManager.instance;
        Add("dialogs/open", Flag(dialogManager != null && dialogManager.IsPlaying));

        var loots = new List<string>();
        foreach (LootableEquipment loot in FindObjectsByType<LootableEquipment>(FindObjectsInactive.Exclude))
            loots.Add($"{loot.Equipment}@{ScenarioLinkProtocol.Format(loot.transform.position)}");
        Add("loots", string.Join(ScenarioLinkProtocol.ListSeparator, loots));

        var unlocked = new List<string>();
        AchievementManager achievements = AchievementManager.Instance;
        if (achievements != null && achievements.Achievements != null)
            foreach (AchievementData achievement in achievements.Achievements)
                if (achievement != null && achievements.IsUnlocked(achievement))
                    unlocked.Add(achievement.Id);
        Add("ach", string.Join(ScenarioLinkProtocol.ListSeparator, unlocked));

        Add("audio/local", DescribeSources(local));
        var others = new StringBuilder();
        foreach (PigeonController pigeon in PigeonController.All)
        {
            if (pigeon == local)
                continue;
            string sources = DescribeSources(pigeon);
            if (sources.Length == 0)
                continue;
            if (others.Length > 0)
                others.Append(ScenarioLinkProtocol.ListSeparator);
            others.Append(sources);
        }
        Add("audio/others", others.ToString());

        return _snapshot.ToString();
    }

    // « clip=spatialBlend » pour chaque son ponctuel sous ce pigeon.
    static string DescribeSources(PigeonController pigeon)
    {
        if (pigeon == null)
            return "";
        var parts = new List<string>();
        // Les boucles de vol existent dès l'apparition : seuls les sons ponctuels prouvent qu'un son vient d'être joué.
        foreach (AudioSource source in pigeon.GetComponentsInChildren<AudioSource>())
            if (!source.loop)
                parts.Add($"{(source.clip != null ? source.clip.name : "?").Replace("=", "-").Replace("|", "/")}={ScenarioLinkProtocol.Format(source.spatialBlend)}");
        return string.Join(ScenarioLinkProtocol.ListSeparator, parts);
    }

    void Add(string key, string value)
    {
        if (_snapshot.Length > 0)
            _snapshot.Append(ScenarioLinkProtocol.EntrySeparator);
        _snapshot.Append(ScenarioLinkProtocol.Clean(key)).Append(ScenarioLinkProtocol.KeyValueSeparator).Append(ScenarioLinkProtocol.Clean(value));
    }

    static string Flag(bool value) => value ? "1" : "0";

    readonly HashSet<DialogCinematic> _watchedCinematics = new HashSet<DialogCinematic>();
    readonly Dictionary<string, int> _cinematicStarts = new Dictionary<string, int>();

    int StartsOf(string name) => _cinematicStarts.TryGetValue(name, out int count) ? count : 0;
}
#endif
