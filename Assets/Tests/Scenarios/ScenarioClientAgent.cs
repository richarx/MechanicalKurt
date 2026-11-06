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

/// <summary>
/// Dans le second joueur (clone Multiplayer Play Mode) : se relie au ScenarioRunner de l'hôte quand il tourne, joue ses
/// commandes comme un joueur (input du pigeon, cinématiques, connexion) et lui renvoie ce que ce joueur voit.
/// </summary>
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

    void TryConnect(float now)
    {
        if (_connecting != null)
        {
            if (!_connecting.IsCompleted)
                return;
            bool connected = !_connecting.IsFaulted && !_connecting.IsCanceled && _tcp != null && _tcp.Connected;
            _ = _connecting.Exception;
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

    void Quit(string reason, int code)
    {
        if (!_commandLine || _quitting)
            return;
        _quitting = true;
        Debug.Log($"[Agent] Fin ({reason}) : fermeture de l'éditeur clone.");
        UnityEditor.SessionState.EraseBool(ExitOnFinishKey);
        UnityEditor.EditorApplication.Exit(code);
    }

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

            case "coo":
                RequireDriven(local);
                _bot.PressCoo();
                return "";

            case "flap":
                RequireDriven(local);
                _bot.Flap = argument == "1";
                return "";

            case "brake":
                RequireDriven(local);
                _bot.Brake = argument == "1";
                return "";

            case "racesetup":
            {
                string[] parts = argument.Split(';');
                TimedRace race = FindAnyObjectByType<TimedRace>();
                race.SetLapsForTests(int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture));
                race.SetGraceForTests(float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                return "";
            }

            case "walk":
                RequireDriven(local);
                _bot.Movement = argument == "1" ? new Vector2(0f, 0.4f) : Vector2.zero;
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
                if (network.clientState == PurrNet.Transports.ConnectionState.Disconnected)
                    network.StartClient();
                return network.clientState.ToString();

            case "pause":
            {
                PauseMenu menu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
                if (menu == null)
                    throw new InvalidOperationException("pas de PauseMenu");
                menu.SetPaused(argument == "on");
                return menu.IsPaused ? "en pause" : "reprise";
            }

            case "start-rule":
            {
                var rule = (MissionStartZone.StartRule)Enum.Parse(typeof(MissionStartZone.StartRule), argument);
                foreach (MissionStartZone zone in FindObjectsByType<MissionStartZone>(FindObjectsInactive.Include))
                    zone.SetRuleForTests(rule);
                return rule.ToString();
            }

            case "reset-statue":
            {
                RoarCinematic roar = FindAnyObjectByType<RoarCinematic>(FindObjectsInactive.Include);
                if (roar != null)
                    roar.ResetForTests();
                return "";
            }

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

    void ConfirmDialogs(float now)
    {
        DialogManager dialogs = DialogManager.instance;
        if (dialogs == null || !dialogs.IsPlaying || now < _nextConfirm)
            return;
        dialogs.RequestConfirm();
        _nextConfirm = now + ConfirmInterval;
    }

    string BuildSnapshot()
    {
        _snapshot.Clear();
        NetworkManager network = NetworkManager.main;
        Add(RemoteKeys.Network, network == null ? RemoteKeys.NetworkNone
            : network.clientState == PurrNet.Transports.ConnectionState.Disconnected ? RemoteKeys.NetworkOffline
            : network.clientState == PurrNet.Transports.ConnectionState.Connected && network.isLocalPlayerReady ? RemoteKeys.NetworkConnected
            : RemoteKeys.NetworkConnecting);
        if (network != null && network.isClient && network.isLocalPlayerReady)
            Add(RemoteKeys.PlayerId, network.localPlayer.id.value.ToString(CultureInfo.InvariantCulture));
        Add(RemoteKeys.AgentDriving, Flag(_overrideWanted));
        Add(RemoteKeys.AutoDialogs, Flag(_playDialogs));

        PigeonController local = PigeonController.Local;
        Add(RemoteKeys.HasPigeon, Flag(local != null));
        if (local != null)
        {
            Add(RemoteKeys.Position, ScenarioLinkProtocol.Format(local.transform.position));
            Add(RemoteKeys.Frozen, Flag(local.IsMovementFrozen));
            Add(RemoteKeys.Grounded, Flag(local.IsGrounded));
            Add(RemoteKeys.Speed, ScenarioLinkProtocol.Format(local.Speed));
            Add(RemoteKeys.Ready, Flag(local.WantsStart));
            Add(RemoteKeys.Equipment, local.WornEquipment.ToString(CultureInfo.InvariantCulture));
            PlayerControls controls = local.GetComponent<PlayerControls>();
            Add(RemoteKeys.ControlsBlocked, Flag(controls != null && controls.AreControlsBlocked));
            Add(RemoteKeys.PigeonDriven, Flag(ReferenceEquals(local.Controls, _bot)));
        }
        Add(RemoteKeys.PigeonCount, PigeonController.All.Count.ToString(CultureInfo.InvariantCulture));

        foreach (MissionStartZone zone in FindObjectsByType<MissionStartZone>(FindObjectsInactive.Exclude))
        {
            Add(RemoteKeys.ZonePhase(zone), zone.Phase.ToString());
            Add(RemoteKeys.ZoneHud(zone), zone.HudText ?? "");
            Add(RemoteKeys.ZonePrompt(zone), Flag(ScenarioRunner.PromptVisible(zone.Prompt)));
        }

        foreach (PigeonCharacterRace race in FindObjectsByType<PigeonCharacterRace>(FindObjectsInactive.Exclude))
        {
            Add(RemoteKeys.RacePhase(race), race.Phase.ToString());
            Add(RemoteKeys.RaceDeparture(race), Flag(race.DepartureCinematic != null && race.DepartureCinematic.IsPlaying));
            Add(RemoteKeys.RaceArrival(race), Flag(race.ArrivalCinematic != null && race.ArrivalCinematic.IsPlaying));
            if (race.Character != null)
                Add(RemoteKeys.RaceCharacter(race), ScenarioLinkProtocol.Format(race.Character.position));
            float failure = race.FailureMessage != null && race.FailureMessage.Group != null ? race.FailureMessage.Group.alpha : 0f;
            Add(RemoteKeys.RaceFailure(race), ScenarioLinkProtocol.Format(failure));
            Add(RemoteKeys.RaceMarker(race), Flag(race.DepartureMarker != null && race.DepartureMarker.activeInHierarchy));
        }

        foreach (PigeonStopDialogCinematic tuto in FindObjectsByType<PigeonStopDialogCinematic>(FindObjectsInactive.Exclude))
        {
            if (tuto.StopZone == null)
                continue;
            Add(RemoteKeys.TutoConsumed(tuto), Flag(!tuto.StopZone.enabled));
            Add(RemoteKeys.TutoPlaying(tuto), Flag(tuto.Cinematic != null && tuto.Cinematic.IsPlaying));
        }

        var playing = new List<string>();
        foreach (DialogCinematic cinematic in FindObjectsByType<DialogCinematic>(FindObjectsInactive.Exclude))
        {
            if (cinematic.IsPlaying)
                playing.Add(cinematic.name);
            if (_watchedCinematics.Add(cinematic))
            {
                DialogCinematic watched = cinematic;
                watched.OnCinematicStarted.AddListener(() => _cinematicStarts[watched.name] = StartsOf(watched.name) + 1);
            }
        }
        Add(RemoteKeys.PlayingDialogs, string.Join(ScenarioLinkProtocol.ListSeparator, playing));
        foreach (KeyValuePair<string, int> entry in _cinematicStarts)
            Add(RemoteKeys.DialogStarts(entry.Key), entry.Value.ToString(CultureInfo.InvariantCulture));
        DialogManager dialogManager = DialogManager.instance;
        Add(RemoteKeys.DialogOpen, Flag(dialogManager != null && dialogManager.IsPlaying));

        var loots = new List<string>();
        foreach (LootableEquipment loot in FindObjectsByType<LootableEquipment>(FindObjectsInactive.Exclude))
            loots.Add($"{loot.Equipment}@{ScenarioLinkProtocol.Format(loot.transform.position)}");
        Add(RemoteKeys.Loots, string.Join(ScenarioLinkProtocol.ListSeparator, loots));

        var unlocked = new List<string>();
        AchievementManager achievements = AchievementManager.Instance;
        if (achievements != null && achievements.Achievements != null)
            foreach (AchievementData achievement in achievements.Achievements)
                if (achievement != null && achievements.IsUnlocked(achievement))
                    unlocked.Add(achievement.Id);
        Add(RemoteKeys.Achievements, string.Join(ScenarioLinkProtocol.ListSeparator, unlocked));

        Add(RemoteKeys.OwnSounds, DescribeSources(local));
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
        Add(RemoteKeys.OtherSounds, others.ToString());

        var ringSounds = new List<string>();
        foreach (BoostRing ring in FindObjectsByType<BoostRing>(FindObjectsInactive.Exclude))
            foreach (AudioSource source in ring.GetComponentsInChildren<AudioSource>())
                if (!source.loop)
                    ringSounds.Add(ScenarioLinkProtocol.Format(source.spatialBlend));
        Add(RemoteKeys.RingSounds, string.Join(ScenarioLinkProtocol.ListSeparator, ringSounds));
        PigeonBoostFeedback feedback = local != null ? local.GetComponent<PigeonBoostFeedback>() : null;
        Add(RemoteKeys.ScreenSpeedLines, ScenarioLinkProtocol.Format(feedback != null ? feedback.ScreenEffectAlpha : 0f));
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        if (cycle != null)
            Add(RemoteKeys.DayNightClock, ScenarioLinkProtocol.Format(cycle.Clock));
        PlayerControls playerControls = local != null ? local.GetComponent<PlayerControls>() : null;
        Add(RemoteKeys.Paused, Flag(playerControls != null && playerControls.IsPaused));

        var bubbles = new List<string>();
        foreach (PigeonController pigeon in PigeonController.All)
            if (pigeon.TryGetComponent(out PigeonCooBeacon beacon) && beacon.Indicator != null && beacon.Indicator.IsShowing)
                bubbles.Add((pigeon == local ? "moi:" : "") + ColorUtility.ToHtmlStringRGB(beacon.Indicator.Color));
        Add(RemoteKeys.CooBubbles, string.Join(ScenarioLinkProtocol.ListSeparator, bubbles));

        TrafficNetwork traffic = FindAnyObjectByType<TrafficNetwork>();
        if (traffic != null && TrafficNetwork.IsRunning)
            Add(RemoteKeys.TrafficCars, ScenarioRunner.DescribeTraffic(traffic));
        Add(RemoteKeys.CarsGhosts, Flag(TrafficCollisionGate.CarsAreGhosts));
        Add(RemoteKeys.SupportId, local != null ? local.SupportId.ToString(CultureInfo.InvariantCulture) : "0");
        BallRolling ball = FindAnyObjectByType<BallRolling>();
        if (ball != null)
            Add(RemoteKeys.BallPosition, ScenarioRunner.DescribeVector(ball.transform.position));
        MoveZeppelin zeppelin = FindAnyObjectByType<MoveZeppelin>();
        if (zeppelin != null)
            Add(RemoteKeys.ZeppelinPosition, CityClock.Now.ToString("0.00", CultureInfo.InvariantCulture)
                + ";" + ScenarioRunner.DescribeVector(zeppelin.transform.position));
        TimedRace timedRace = FindAnyObjectByType<TimedRace>();
        Add(RemoteKeys.TimedRaceHud, (timedRace != null ? timedRace.HudText : null) ?? "");
        Add(RemoteKeys.TimedRaceResults, (timedRace != null ? timedRace.ResultsText : null) ?? "");

        return _snapshot.ToString();
    }

    static string DescribeSources(PigeonController pigeon)
    {
        if (pigeon == null)
            return "";
        var parts = new List<string>();
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
