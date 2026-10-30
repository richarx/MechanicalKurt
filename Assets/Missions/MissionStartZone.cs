using PurrNet.Prediction;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public struct MissionStartState : IPredictedData<MissionStartState>
{
    public byte Phase;
    /// <summary>Secondes restantes du décompte, ou du délai maximal de la cinématique.</summary>
    public float Countdown;
    /// <summary>Numéro du dernier départ : identifie ses participants (PigeonState.LastStartCount).</summary>
    public int StartCount;
    /// <summary>Attente et décompte : pigeons prêts. Cinématique : participants qui l'ont finie.</summary>
    public byte Ready;
    /// <summary>Attente et décompte : pigeons de la partie. Cinématique : participants.</summary>
    public byte Total;

    public void Dispose() { }
}

/// <summary>
/// Départ prédit d'une mission commune à plusieurs joueurs : attente des joueurs prêts (Interact), décompte annulable,
/// cinématique locale à chaque participant, puis départ quand tous l'ont finie. Sans réarmement, la zone reste
/// verrouillée jusqu'à ce que la mission appelle ResetToWaiting.
/// </summary>
public class MissionStartZone : PredictedIdentity<MissionStartState>
{
    public enum StartRule
    {
        AllPigeons,
        AnyPigeon
    }

    public const byte Waiting = 0;
    public const byte CountingDown = 1;
    public const byte Started = 2;
    public const byte Cinematic = 3;

    [Header("Zone")]
    [SerializeField] float _radius = 3f;
    [SerializeField] float _stopSpeedThreshold = 0.15f;
    [Tooltip("AllPigeons : tout le monde doit être prêt. AnyPigeon : un seul suffit (les autres ne sont pas affectés).")]
    [SerializeField] StartRule _startRule = StartRule.AllPigeons;
    [Tooltip("Revient en attente juste après un départ : chaque joueur peut lancer la zone à son tour. Laisser décoché pour une course.")]
    [SerializeField] bool _rearm;

    [Header("Départ")]
    [Tooltip("Décompte affiché avant la cinématique. Les participants sont gelés mais peuvent encore annuler (Interact).")]
    [SerializeField] float _countdownSeconds = 3f;
    [Tooltip("0 : pas de cinématique, départ à la fin du décompte. Sinon : délai maximal d'attente des cinématiques locales "
        + "(OnCinematicLocal → ReportLocalCinematicFinished) ; le départ part dès que tous les participants l'ont finie.")]
    [SerializeField] float _cinematicTimeout;
    [Tooltip("Position et orientation de la ligne de départ. Les pigeons gardent leur décalage relatif à la zone.")]
    [SerializeField] Transform _startPose;

    [Header("UI locale")]
    [Tooltip("Texte d'instruction partagé (« Appuyer sur Y / F »), affiché au pigeon local uniquement.")]
    [SerializeField] GameObject _prompt;
    [Tooltip("Facultatif : texte du décompte. Vide = un texte centré est créé à l'exécution (police du prompt).")]
    [SerializeField] TMP_Text _countdownText;
    [Tooltip("Affiché au joueur qui se pose dans la zone pendant un départ ou une course auxquels il ne participe pas.")]
    [SerializeField] string _inProgressText = "Race in progress\n<size=60%>Wait for it to finish</size>";

    [field: SerializeField, Tooltip("Présentation uniquement : déclenché une fois l'état vérifié, avec un léger retard chez les clients.")]
    public UnityEvent OnCountdownStarted { get; private set; } = new UnityEvent();
    [field: SerializeField, Tooltip("Présentation uniquement : déclenché une fois l'état vérifié, avec un léger retard chez les clients.")]
    public UnityEvent OnCancelled { get; private set; } = new UnityEvent();
    [field: SerializeField, Tooltip("Fin du décompte, sur le client dont le pigeon participe : jouer la cinématique, puis appeler ReportLocalCinematicFinished.")]
    public UnityEvent OnCinematicLocal { get; private set; } = new UnityEvent();
    [field: SerializeField, Tooltip("Présentation uniquement : déclenché une fois l'état vérifié, avec un léger retard chez les clients.")]
    public UnityEvent OnStarted { get; private set; } = new UnityEvent();
    [field: SerializeField, Tooltip("Comme OnStarted, mais seulement sur le client dont le pigeon a participé au départ (achievement…).")]
    public UnityEvent OnStartedLocal { get; private set; } = new UnityEvent();

    PredictedEvent _countdownStartedEvent;
    PredictedEvent _cancelledEvent;
    PredictedEvent<int> _cinematicEvent;
    PredictedEvent<int> _startedEvent;

    MissionStartState _viewState;
    bool _promptShown;
    TMP_Text _hudText;
    GameObject _hudRoot;
    string _hudContent;

    public byte Phase => _viewState.Phase;
    public float CountdownRemaining => _viewState.Countdown;
    public int ReadyCount => _viewState.Ready;
    public int TotalCount => _viewState.Total;
    public float Radius => _radius;
    public float StopSpeedThreshold => _stopSpeedThreshold;
    public float CountdownSeconds => _countdownSeconds;
    public float CinematicTimeout => _cinematicTimeout;
    public Transform StartPose => _startPose;
    public GameObject Prompt => _prompt;
    /// <summary>Texte du décompte actuellement affiché (null si masqué).</summary>
    public string HudText => _hudRoot != null && _hudRoot.activeInHierarchy ? _hudContent : null;

    /// <summary>État simulé de la zone, réservé au Simulate d'une autre identité (ex. PigeonCharacterRace).</summary>
    public ref MissionStartState SimState => ref currentState;

    /// <summary>Rouvre la zone à la fin de la mission (échec ou victoire). À appeler depuis une simulation.</summary>
    [SimulationOnly]
    public void ResetToWaiting()
    {
        ref MissionStartState s = ref currentState;
        s.Phase = Waiting;
        s.Countdown = 0f;
    }

    /// <summary>Ce pigeon fait partie du dernier départ lancé par cette zone (simulation : réservé à un Simulate).</summary>
    public bool IsInCurrentStart(PigeonController pigeon)
        => pigeon != null && currentState.StartCount > 0 && IsStartedHere(pigeon.SimState, currentState.StartCount);

    /// <summary>Le joueur local a fini la cinématique de départ. Le départ partira quand tous les participants l'auront finie.</summary>
    public void ReportLocalCinematicFinished() => ReportCinematicFinished(PigeonController.Local);

    /// <summary>Fin de cinématique pour un pigeon contrôlé ici (joueur local, ou bot sur l'hôte). Passe par son input.</summary>
    public void ReportCinematicFinished(PigeonController pigeon)
    {
        int startCount = pigeon != null ? pigeon.LastStartCountFor(id) : 0;
        if (startCount > 0)
            pigeon.AcknowledgeStartCinematic(id, startCount);
    }

    protected override void LateAwake()
    {
        _countdownStartedEvent = new PredictedEvent(predictionManager, this);
        _cancelledEvent = new PredictedEvent(predictionManager, this);
        _cinematicEvent = new PredictedEvent<int>(predictionManager, this);
        _startedEvent = new PredictedEvent<int>(predictionManager, this);

        _countdownStartedEvent.AddListener(() => OnCountdownStarted.Invoke());
        _cancelledEvent.AddListener(() => OnCancelled.Invoke());
        _cinematicEvent.AddListener(HandleCinematic);
        _startedEvent.AddListener(HandleStarted);
    }

    void HandleCinematic(int startCount)
    {
        if (LocalParticipated(startCount))
            OnCinematicLocal.Invoke();
    }

    void HandleStarted(int startCount)
    {
        OnStarted.Invoke();
        if (LocalParticipated(startCount))
            OnStartedLocal.Invoke();
    }

    /// <summary>Présentation : le pigeon local a participé au départ numéro <paramref name="startCount"/>.</summary>
    public bool LocalParticipated(int startCount)
    {
        PigeonController local = PigeonController.Local;
        return local != null && local.WasStartedBy(id, startCount);
    }

    protected override MissionStartState GetInitialState() => new MissionStartState { Phase = Waiting };

    protected override void GetUnityState(ref MissionStartState s) { }
    protected override void SetUnityState(MissionStartState s) { }

    /// <summary>Scénarios / débogage : journalise chaque changement de phase (hors replays).</summary>
    public static bool TraceTransitions;

    protected override void Simulate(ref MissionStartState s, float dt)
    {
        byte before = s.Phase;
        SimulatePhases(ref s, dt);
        if (TraceTransitions && s.Phase != before && !predictionManager.isReplaying)
            Debug.Log($"[MissionStartZone] {name} : {before} → {s.Phase} (départ n°{s.StartCount}, {s.Ready}/{s.Total}, reste {s.Countdown:0.00}) — {Describe()}");
    }

    /// <summary>État de la zone et des pigeons (simulation), pour le débogage.</summary>
    public string Describe()
    {
        ref MissionStartState s = ref currentState;
        var sb = new System.Text.StringBuilder($"phase {s.Phase}, départ n°{s.StartCount}, {s.Ready}/{s.Total}, reste {s.Countdown:0.00}");
        foreach (PigeonController pigeon in PigeonController.All)
        {
            ref PigeonState ps = ref pigeon.SimState;
            sb.Append($" | {pigeon.name} : prêt {IsReadyHere(ps)}, dedans {IsInside(ps)}, vitesse {ps.LastVelocity.magnitude:0.00}, "
                + $"gelé {ps.Frozen}, départ {(ps.LastStartZone.Equals(id) ? ps.LastStartCount : -1)}, "
                + $"fini {(ps.StartAckZone.Equals(id) ? ps.StartAckCount : -1)}");
        }
        return sb.ToString();
    }

    void SimulatePhases(ref MissionStartState s, float dt)
    {
        if (s.Phase == Started)
        {
            if (!_rearm)
                return;
            s.Phase = Waiting;
        }

        if (s.Phase == Cinematic)
        {
            SimulateCinematic(ref s, dt);
            return;
        }

        var pigeons = PigeonController.All;
        int total = pigeons.Count;
        int ready = 0;

        for (int i = 0; i < total; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            bool inside = IsInside(ps);
            bool mine = IsReadyHere(ps);

            if (inside && ps.InteractPressed)
            {
                mine = !mine;
                ps.HasReadyZone = mine;
                if (mine)
                    ps.ReadyZone = id;
            }
            else if (mine && !inside)
            {
                mine = false;
                ps.HasReadyZone = false;
            }

            if (mine)
                ready++;
        }
        s.Ready = (byte)Mathf.Min(ready, byte.MaxValue);
        s.Total = (byte)Mathf.Min(total, byte.MaxValue);

        bool canStart = _startRule == StartRule.AnyPigeon
            ? ready >= 1
            : total >= 1 && ready == total;

        if (s.Phase == Waiting)
        {
            if (canStart)
            {
                s.Phase = CountingDown;
                s.Countdown = _countdownSeconds;
                FreezeParticipants();
                _countdownStartedEvent.Invoke();
            }
            return;
        }

        if (!canStart)
        {
            s.Phase = Waiting;
            ReleaseFrozen();
            _cancelledEvent.Invoke();
            return;
        }

        s.Countdown -= dt;
        if (s.Countdown > 0f)
            return;

        s.StartCount++;
        MarkParticipants(s.StartCount);
        if (_cinematicTimeout > 0f)
        {
            s.Phase = Cinematic;
            s.Countdown = _cinematicTimeout;
            s.Ready = 0;
            s.Total = (byte)Mathf.Min(ready, byte.MaxValue);
            _cinematicEvent.Invoke(s.StartCount);
            return;
        }
        Launch(ref s);
    }

    void SimulateCinematic(ref MissionStartState s, float dt)
    {
        var pigeons = PigeonController.All;
        int participants = 0, finished = 0;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            if (!IsStartedHere(ps, s.StartCount))
                continue;
            participants++;
            if (ps.StartAckCount == s.StartCount && ps.StartAckZone.Equals(id))
                finished++;
        }
        s.Ready = (byte)Mathf.Min(finished, byte.MaxValue);
        s.Total = (byte)Mathf.Min(participants, byte.MaxValue);

        if (participants == 0)
        {
            s.Phase = Waiting;
            s.Countdown = 0f;
            return;
        }

        s.Countdown -= dt;
        if (finished < participants && s.Countdown > 0f)
            return;
        Launch(ref s);
    }

    bool IsInside(in PigeonState ps) =>
        (ps.Position - transform.position).sqrMagnitude <= _radius * _radius && ps.LastVelocity.magnitude <= _stopSpeedThreshold;

    bool IsReadyHere(in PigeonState ps) => ps.HasReadyZone && ps.ReadyZone.Equals(id);

    bool IsStartedHere(in PigeonState ps, int startCount) => ps.LastStartCount == startCount && ps.LastStartZone.Equals(id);

    bool IsParticipant(in PigeonState ps) => IsReadyHere(ps) && IsInside(ps);

    void FreezeParticipants()
    {
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            if (!IsParticipant(pigeons[i].SimState))
                continue;
            pigeons[i].ForceFreeze(true);
            pigeons[i].SimState.FrozenByZone = true;
            pigeons[i].SimState.FreezingZone = id;
        }
    }

    void ReleaseFrozen()
    {
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
            ReleaseIfMine(pigeons[i]);
    }

    void ReleaseIfMine(PigeonController pigeon)
    {
        ref PigeonState ps = ref pigeon.SimState;
        if (!ps.FrozenByZone || !ps.FreezingZone.Equals(id))
            return;
        ps.FrozenByZone = false;
        if (ps.Frozen)
            pigeon.ForceFreeze(false);
    }

    void MarkParticipants(int startCount)
    {
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            if (!IsParticipant(ps))
                continue;
            ps.HasReadyZone = false;
            ps.LastStartZone = id;
            ps.LastStartCount = startCount;
        }
    }

    void Launch(ref MissionStartState s)
    {
        var pigeons = PigeonController.All;
        Vector3 center = transform.position;

        for (int i = 0; i < pigeons.Count; i++)
        {
            PigeonController pigeon = pigeons[i];
            if (!IsStartedHere(pigeon.SimState, s.StartCount))
                continue;
            Vector3 offset = pigeon.SimState.Position - center;
            offset.y = 0f;

            ReleaseIfMine(pigeon);
            if (_startPose != null)
                pigeon.ForceTeleport(_startPose.position + offset, _startPose.rotation);
        }

        s.Phase = Started;
        s.Countdown = 0f;
        _startedEvent.Invoke(s.StartCount);
    }

    protected override void UpdateView(MissionStartState view, MissionStartState? verified)
    {
        _viewState = view;
        UpdatePrompt(view);
        UpdateHud(view);
    }

    void UpdatePrompt(in MissionStartState view)
    {
        if (_prompt == null)
            return;

        PigeonController local = PigeonController.Local;
        bool show = view.Phase == Waiting
            && local != null
            && !local.IsReadyFor(id)
            && (local.transform.position - transform.position).sqrMagnitude <= _radius * _radius
            && local.Speed <= _stopSpeedThreshold;

        if (show == _promptShown)
            return;
        _promptShown = show;
        _prompt.SetActive(show);
    }

    void UpdateHud(in MissionStartState view)
    {
        PigeonController local = PigeonController.Local;
        string content = null;
        if (local != null)
        {
            switch (view.Phase)
            {
                case Waiting:
                    if (local.IsReadyFor(id))
                        content = $"Waiting for players: {view.Ready}/{view.Total} ready\n<size=60%>F / Y: cancel</size>";
                    break;
                case CountingDown:
                    if (local.IsReadyFor(id))
                        content = $"<size=250%>{Mathf.Max(1, Mathf.CeilToInt(view.Countdown))}</size>\n"
                            + $"Players: {view.Ready}/{view.Total}\n<size=60%>F / Y: cancel</size>";
                    break;
                case Cinematic:
                    if (local.WasStartedBy(id, view.StartCount))
                    {
                        if (local.HasFinishedStartCinematic(id, view.StartCount))
                            content = $"Waiting for players: {view.Ready}/{view.Total}";
                    }
                    else if (IsLocalInZone(local))
                        content = _inProgressText;
                    break;
                case Started:
                    if (!_rearm && !local.WasStartedBy(id, view.StartCount) && IsLocalInZone(local))
                        content = _inProgressText;
                    break;
            }
        }

        if (content == _hudContent)
            return;
        _hudContent = content;
        if (content != null)
            EnsureHud();
        if (_hudText == null)
            return;
        if (content != null)
            _hudText.text = content;
        _hudRoot.SetActive(content != null);
    }

    bool IsLocalInZone(PigeonController local)
        => (local.transform.position - transform.position).sqrMagnitude <= _radius * _radius;

    void EnsureHud()
    {
        if (_hudText != null)
            return;
        if (_countdownText != null)
        {
            _hudText = _countdownText;
            _hudRoot = _countdownText.gameObject;
            return;
        }

        var root = new GameObject($"{name} — décompte", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);
        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 220f);
        rect.sizeDelta = new Vector2(1400f, 400f);

        var text = textObject.AddComponent<TextMeshProUGUI>();
        TMP_Text promptText = _prompt != null ? _prompt.GetComponentInChildren<TMP_Text>(true) : null;
        if (promptText != null)
        {
            text.font = promptText.font;
            text.fontSharedMaterial = promptText.fontSharedMaterial;
            text.color = promptText.color;
        }
        text.fontSize = 56f;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        _hudText = text;
        _hudRoot = root;
        root.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _radius);
        if (_startPose != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _startPose.position);
            Gizmos.DrawWireSphere(_startPose.position, 0.5f);
        }
    }
}
