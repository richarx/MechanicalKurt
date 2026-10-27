using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.Events;

// Orchestrateur prédit : remplace MissionZone / PigeonStopZone pour un départ de mission multijoueur.
//
// Règle : TOUS les pigeons doivent être dans la zone, à l'arrêt, et avoir validé (Interact) pour lancer
// le compte à rebours. Pendant le compte à rebours ils sont gelés ; si l'un d'eux annule (Interact) ou
// quitte la zone, tout est annulé. Au bout, tout le monde est téléporté sur la ligne de départ et dégelé.
//
// Tout se passe dans Simulate à partir des états des pigeons et du dt : tous les pairs prennent la même
// décision au même tick, et un rollback rejoue la même séquence.
public struct MissionStartState : IPredictedData<MissionStartState>
{
    public byte Phase;       // MissionStartZone.Waiting / CountingDown / Started
    public float Countdown;  // secondes restantes (décrémenté par dt, jamais par Time.time)
    public int StartCount;   // nombre de départs lancés (identifie les participants de chaque départ)

    public void Dispose() { }
}

public class MissionStartZone : PredictedIdentity<MissionStartState>
{
    public enum StartRule
    {
        AllPigeons, // tous les pigeons de la partie doivent être dans la zone, à l'arrêt, et prêts
        AnyPigeon   // dès qu'un pigeon est prêt ; seuls les pigeons prêts sont gelés et téléportés
    }

    public const byte Waiting = 0;
    public const byte CountingDown = 1;
    public const byte Started = 2;

    [Header("Zone")]
    [SerializeField] float _radius = 3f;
    [SerializeField] float _stopSpeedThreshold = 0.15f;
    [Tooltip("AllPigeons : tout le monde doit être prêt. AnyPigeon : un seul suffit (les autres ne sont pas affectés).")]
    [SerializeField] StartRule _startRule = StartRule.AllPigeons;
    [Tooltip("Revient en attente juste après un départ : chaque joueur peut lancer la zone à son tour (ex. tuto). Laisser décoché pour une course.")]
    [SerializeField] bool _rearm;

    [Header("Départ")]
    [SerializeField] float _countdownSeconds = 3f;
    [Tooltip("Position et orientation de la ligne de départ. Les pigeons gardent leur décalage relatif à la zone.")]
    [SerializeField] Transform _startPose;

    [Header("UI locale")]
    [Tooltip("Texte d'instruction partagé (« Appuyer sur Y / F »), affiché au pigeon local uniquement.")]
    [SerializeField] GameObject _prompt;

    // Déclenchés une fois l'occurrence vérifiée (pas à chaque replay), avec un léger retard selon le client :
    // adaptés à la présentation (cinématique locale, SFX, UI) et au pont vers du réseau classique côté serveur.
    // Tout ce qui influence la simulation prédite doit lire Phase / SimState depuis un Simulate.
    [field: SerializeField] public UnityEvent OnCountdownStarted { get; private set; } = new UnityEvent();
    [field: SerializeField] public UnityEvent OnCancelled { get; private set; } = new UnityEvent();
    [field: SerializeField] public UnityEvent OnStarted { get; private set; } = new UnityEvent();
    [Tooltip("Comme OnStarted, mais seulement sur le client dont le pigeon a participé au départ (cinématique, achievement…).")]
    [field: SerializeField] public UnityEvent OnStartedLocal { get; private set; } = new UnityEvent();

    PredictedEvent _countdownStartedEvent;
    PredictedEvent _cancelledEvent;
    PredictedEvent<int> _startedEvent;

    MissionStartState _viewState;
    bool _promptShown;

    // Lues depuis la vue : pour l'UI et la présentation, pas pour la simulation.
    public byte Phase => _viewState.Phase;
    public float CountdownRemaining => _viewState.Countdown;
    public float Radius => _radius;
    public float StopSpeedThreshold => _stopSpeedThreshold;
    public float CountdownSeconds => _countdownSeconds;
    public Transform StartPose => _startPose;
    public GameObject Prompt => _prompt;

    /// <summary>État simulé de la zone, réservé au Simulate d'une autre identité (ex. PigeonCharacterRace).</summary>
    public ref MissionStartState SimState => ref currentState;

    /// <summary>Permet de relancer un départ après un échec de course. À appeler depuis une simulation.</summary>
    [SimulationOnly]
    public void ResetToWaiting()
    {
        ref MissionStartState s = ref currentState;
        s.Phase = Waiting;
        s.Countdown = 0f;
    }

    protected override void LateAwake()
    {
        _countdownStartedEvent = new PredictedEvent(predictionManager, this);
        _cancelledEvent = new PredictedEvent(predictionManager, this);
        _startedEvent = new PredictedEvent<int>(predictionManager, this);

        _countdownStartedEvent.AddListener(() => OnCountdownStarted.Invoke());
        _cancelledEvent.AddListener(() => OnCancelled.Invoke());
        _startedEvent.AddListener(HandleStarted);
    }

    void HandleStarted(int startCount)
    {
        OnStarted.Invoke();
        if (LocalParticipated(startCount))
            OnStartedLocal.Invoke();
    }

    // La vue du pigeon local est en avance sur l'état vérifié : sa marque de départ y est déjà.
    bool LocalParticipated(int startCount)
    {
        PigeonController local = PigeonController.Local;
        return local != null && local.WasStartedBy(id, startCount);
    }

    protected override MissionStartState GetInitialState() => new MissionStartState { Phase = Waiting };

    // Cette identité ne dépend d'aucun état Unity.
    protected override void GetUnityState(ref MissionStartState s) { }
    protected override void SetUnityState(MissionStartState s) { }

    protected override void Simulate(ref MissionStartState s, float dt)
    {
        if (s.Phase == Started)
        {
            if (!_rearm)
                return;
            // Les participants ont perdu leur validation au départ : pas de relance immédiate.
            s.Phase = Waiting;
        }

        var pigeons = PigeonController.All;
        int total = pigeons.Count;
        int ready = 0;

        for (int i = 0; i < total; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            bool inside = IsInside(ps);
            bool mine = IsReadyHere(ps);

            // Interact dans la zone bascule la validation POUR CETTE ZONE ; quitter la zone l'annule.
            // Les autres zones ne touchent pas à cette validation (plusieurs zones par scène).
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

        // CountingDown
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
        Launch(s.StartCount);
        s.Phase = Started;
        s.Countdown = 0f;
        _startedEvent.Invoke(s.StartCount);
    }

    bool IsInArea(in PigeonState ps) => (ps.Position - transform.position).sqrMagnitude <= _radius * _radius;

    bool IsInside(in PigeonState ps) => IsInArea(ps) && ps.LastVelocity.magnitude <= _stopSpeedThreshold;

    bool IsReadyHere(in PigeonState ps) => ps.HasReadyZone && ps.ReadyZone.Equals(id);

    // Un participant est un pigeon dans la zone, à l'arrêt, qui a validé cette zone.
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

    // Dégèle les pigeons que CETTE zone a gelés, où qu'ils soient (un respawn a pu les sortir de la zone).
    // Un gel posé ailleurs (course) n'est pas touché.
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

    void Launch(int startCount)
    {
        var pigeons = PigeonController.All;
        Vector3 center = transform.position;

        for (int i = 0; i < pigeons.Count; i++)
        {
            PigeonController pigeon = pigeons[i];
            bool participant = IsParticipant(pigeon.SimState);
            if (!participant)
                continue;
            Vector3 offset = pigeon.SimState.Position - center;
            offset.y = 0f;

            pigeon.SimState.HasReadyZone = false;
            ReleaseIfMine(pigeon);

            pigeon.SimState.LastStartZone = id;
            pigeon.SimState.LastStartCount = startCount;

            if (_startPose != null)
                pigeon.ForceTeleport(_startPose.position + offset, _startPose.rotation);
        }
    }

    // Vue : aucune décision de gameplay ici.
    protected override void UpdateView(MissionStartState view, MissionStartState? verified)
    {
        _viewState = view;
        UpdatePrompt(view);
    }

    void UpdatePrompt(in MissionStartState view)
    {
        if (_prompt == null)
            return;

        PigeonController local = PigeonController.Local;
        bool show = view.Phase == Waiting
            && local != null
            && (local.transform.position - transform.position).sqrMagnitude <= _radius * _radius
            && local.Speed <= _stopSpeedThreshold;

        if (show == _promptShown)
            return;
        _promptShown = show;
        _prompt.SetActive(show);
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