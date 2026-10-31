using System.Collections.Generic;
using PurrNet.Prediction;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public struct RaceState : IPredictedData<RaceState>
{
    public PigeonCharacterRace.RacePhase Phase;
    public float Elapsed;
    public float StoppedTime;
    public float ArrivalRemaining;
    public int StartCount;

    public void Dispose() { }
}

/// <summary>
/// Course prédite contre un personnage. Lancée par sa MissionStartZone ; seuls les coureurs de ce départ comptent.
/// Échec (hauteur, temps) ou victoire ramènent la course au départ ; à la victoire, les coureurs restent gelés
/// jusqu'à ce que chacun ait fini la cinématique d'arrivée. Le personnage suit Elapsed, identique pour tous.
/// </summary>
[DisallowMultipleComponent]
public class PigeonCharacterRace : PredictedIdentity<RaceState>
{
    private static readonly int GlideHash = Animator.StringToHash("Glide");

    public enum RacePhase : byte { Ready, DepartureCinematic, Racing, ArrivalCinematic }

    [field: SerializeField, BoxGroup("Départ"), Required]
    [field: Tooltip("Zone de départ multijoueur. Son StartPose remplace l'ancien PlayerDeparturePose : l'y régler à la même pose. Régler son Cinematic Timeout (> 0) pour attendre la cinématique de départ.")]
    public MissionStartZone DepartureZone { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Required]
    [field: Tooltip("Jouée localement à la fin du décompte, par chaque participant ; la course part quand tous l'ont finie. Ne plus la relier à l'ancien PigeonStopZone ni à StartRace.")]
    public DialogCinematic DepartureCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Tooltip("Visuel facultatif, séparé des cinématiques et du contrôleur de course.")]
    public GameObject DepartureMarker { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public Transform Character { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    [field: Tooltip("Trajet éditable comme les autres CityPath : ajout de points dans la Scene avec Ctrl gauche. Le personnage le parcourt une seule fois, sans boucle ni arrêts.")]
    public CharacterRacePath Path { get; private set; }

    // Conserver le nom et le type sérialisés pour les courses déjà configurées.
    // Ces références restent utilisables jusqu'à leur conversion en CharacterRacePath.
    [field: SerializeField, HideInInspector]
    public Transform[] Waypoints { get; private set; } = new Transform[0];

    [field: SerializeField, BoxGroup("Course"), ListDrawerSettings(ShowIndexLabels = true)]
    [field: Tooltip("Objets affichés uniquement pendant la course. Masqués à la victoire, à l'échec et à l'annulation. Ne pas inclure la zone de départ, les cinématiques, le personnage ou le message, ni leurs parents.")]
    public GameObject[] RaceObjects { get; private set; } = new GameObject[0];

    [field: SerializeField, BoxGroup("Course"), MinValue(0.01f), SuffixLabel("s", true)]
    public float DurationSeconds { get; private set; } = 30f;

    [field: SerializeField, BoxGroup("Course")]
    public bool FaceMovement { get; private set; } = true;

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    [field: Tooltip("Objet activé pendant la course. Doit contenir Destination Stop, mais pas les cinématiques ni ce contrôleur.")]
    public GameObject DestinationObject { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    [field: Tooltip("Sert uniquement de configuration (position, rayon, vitesse d'arrêt, durée d'arrêt). Son composant est désactivé : l'arrivée est évaluée dans la simulation. Retirer son événement OnPlayerStopped.")]
    public PigeonStopZone DestinationStop { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    [field: Tooltip("Position et rotation du personnage pour la cinématique finale.")]
    public Transform CharacterArrivalPose { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public DialogCinematic ArrivalCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), MinValue(0.01f), SuffixLabel("s", true)]
    [field: Tooltip("Les coureurs restent gelés jusqu'à ce que chacun ait fini la cinématique d'arrivée sur son écran. "
        + "Délai maximal, si un joueur ne la termine pas (ou pour un bot).")]
    public float ArrivalCinematicTimeout { get; private set; } = 90f;

    [field: SerializeField, BoxGroup("Arrivée")]
    [field: Tooltip("Achievement attribué à la victoire. L'ajouter aussi à la liste de l'AchievementManager.")]
    public AchievementData CompletionAchievement { get; private set; }

    [field: SerializeField, BoxGroup("Échec"), SuffixLabel("Y monde", true)]
    [field: Tooltip("Altitude absolue maximale des pigeons pendant la course. Si UN pigeon la dépasse, la course échoue pour tous.")]
    public float MaximumHeight { get; private set; } = 10f;

    [field: SerializeField, BoxGroup("Échec"), Required]
    public FadingMessage FailureMessage { get; private set; }

    [field: SerializeField, BoxGroup("Événements")]
    public UnityEvent OnCompleted { get; private set; } = new UnityEvent();

    [ShowInInspector, ReadOnly]
    public RacePhase Phase
    {
        get
        {
            RacePhase phase = _viewState.Phase;
            if (phase == RacePhase.Ready && DepartureZone != null
                && DepartureZone.Phase == MissionStartZone.StartPhase.Cinematic)
                return RacePhase.DepartureCinematic;
            return phase;
        }
    }

    private PredictedEvent<int> _cheaterEvent;
    private PredictedEvent<int> _timeoutEvent;
    private PredictedEvent<int> _arrivalEvent;
    private PredictedEvent<int> _completedEvent;

    private RaceState _viewState;
    private int _presentedPhase = -1;
    private bool _holdCharacter;
    private Vector3[] _path;
    private float[] _distances;
    private float _pathLength;
    private Vector3 _characterHomePosition;
    private Quaternion _characterHomeRotation;
    private Animator _characterAnimator;
    private bool _wasRootMotionEnabled;
    private bool _ownsRootMotion;
    private bool _playingDepartureCinematic;
    private int _playingArrivalStart;

    protected override void LateAwake()
    {
        if (Character != null)
        {
            _characterHomePosition = Character.position;
            _characterHomeRotation = Character.rotation;
            _characterAnimator = Character.GetComponentInChildren<Animator>();
        }

        if (DestinationStop != null)
            DestinationStop.enabled = false;

        _cheaterEvent = new PredictedEvent<int>(predictionManager, this);
        _timeoutEvent = new PredictedEvent<int>(predictionManager, this);
        _arrivalEvent = new PredictedEvent<int>(predictionManager, this);
        _completedEvent = new PredictedEvent<int>(predictionManager, this);

        _cheaterEvent.AddListener(startCount => HandleFailed(startCount, true));
        _timeoutEvent.AddListener(startCount => HandleFailed(startCount, false));
        _arrivalEvent.AddListener(HandleArrival);
        _completedEvent.AddListener(HandleCompleted);

        if (DepartureZone != null)
        {
            DepartureZone.OnCountdownStarted.AddListener(HandleDepartureCountdown);
            DepartureZone.OnCancelled.AddListener(HandleDepartureCancelled);
            DepartureZone.OnCinematicLocal.AddListener(HandleDepartureCinematic);
        }
        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicEnded.AddListener(HandleDepartureCinematicEnded);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.AddListener(HandleArrivalCinematicEnded);
    }

    protected override void Destroyed()
    {
        if (DepartureZone != null)
        {
            DepartureZone.OnCountdownStarted.RemoveListener(HandleDepartureCountdown);
            DepartureZone.OnCancelled.RemoveListener(HandleDepartureCancelled);
            DepartureZone.OnCinematicLocal.RemoveListener(HandleDepartureCinematic);
        }
        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicEnded.RemoveListener(HandleDepartureCinematicEnded);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.RemoveListener(HandleArrivalCinematicEnded);
        RestoreRootMotion();
    }

    protected override RaceState GetInitialState() => new RaceState { Phase = RacePhase.Ready };

    protected override void GetUnityState(ref RaceState s) { }
    protected override void SetUnityState(RaceState s) { }

    protected override RaceState Interpolate(RaceState from, RaceState to, float t)
    {
        RaceState result = to;
        result.Elapsed = Mathf.Lerp(from.Elapsed, to.Elapsed, t);
        return result;
    }

    protected override void Simulate(ref RaceState s, float dt)
    {
        switch (s.Phase)
        {
            case RacePhase.Ready:
                if (DepartureZone != null && DepartureZone.SimState.Phase == MissionStartZone.StartPhase.Started)
                {
                    s.Phase = RacePhase.Racing;
                    s.Elapsed = 0f;
                    s.StoppedTime = 0f;
                    s.StartCount = DepartureZone.SimState.StartCount;
                }
                break;

            case RacePhase.Racing:
                SimulateRacing(ref s, dt);
                break;

            case RacePhase.ArrivalCinematic:
                s.ArrivalRemaining -= dt;
                DepartureZone.CountCinematicProgress(id, s.StartCount, out int racers, out int finished);
                if (finished >= racers || s.ArrivalRemaining <= 0f)
                {
                    SetRacersFrozen(false);
                    ResetRace(ref s);
                    _completedEvent.Invoke(s.StartCount);
                }
                break;
        }
    }

    private void SimulateRacing(ref RaceState s, float dt)
    {
        s.Elapsed += dt;

        if (Racers().Count == 0)
        {
            ResetRace(ref s);
            return;
        }
        if (IsAnyRacerAboveMaxHeight())
        {
            ResetRace(ref s);
            _cheaterEvent.Invoke(s.StartCount);
            return;
        }
        if (s.Elapsed >= DurationSeconds)
        {
            ResetRace(ref s);
            _timeoutEvent.Invoke(s.StartCount);
            return;
        }

        bool stopped = IsAnyRacerStoppedAtDestination();
        s.StoppedTime = stopped ? s.StoppedTime + dt : 0f;
        if (stopped && s.StoppedTime >= DestinationStop.StopDuration)
        {
            s.Phase = RacePhase.ArrivalCinematic;
            s.ArrivalRemaining = ArrivalCinematicTimeout;
            SetRacersFrozen(true);
            _arrivalEvent.Invoke(s.StartCount);
        }
    }

    private void ResetRace(ref RaceState s)
    {
        s.Phase = RacePhase.Ready;
        s.Elapsed = 0f;
        s.StoppedTime = 0f;
        DepartureZone.ResetToWaiting();
    }

    private readonly List<PigeonController> _racers = new List<PigeonController>();

    /// <summary>Pigeons du départ en cours (état simulé). Liste réutilisée à chaque appel.</summary>
    private List<PigeonController> Racers()
    {
        _racers.Clear();
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
            if (DepartureZone.IsInCurrentStart(pigeons[i]))
                _racers.Add(pigeons[i]);
        return _racers;
    }

    private bool IsAnyRacerAboveMaxHeight()
    {
        foreach (PigeonController racer in Racers())
            if (racer.SimState.Position.y > MaximumHeight)
                return true;
        return false;
    }

    private bool IsAnyRacerStoppedAtDestination()
    {
        Vector3 center = DestinationStop.transform.position;
        float radius = DestinationStop.Radius;
        float stopSpeed = DestinationStop.StopSpeedThreshold;

        foreach (PigeonController racer in Racers())
        {
            ref PigeonState ps = ref racer.SimState;
            if ((ps.Position - center).sqrMagnitude <= radius * radius
                && ps.LastVelocity.magnitude <= stopSpeed)
                return true;
        }
        return false;
    }

    private void SetRacersFrozen(bool frozen)
    {
        foreach (PigeonController racer in Racers())
            racer.ForceFreeze(frozen);
    }

    protected override void UpdateView(RaceState view, RaceState? verified)
    {
        _viewState = view;

        if ((int)view.Phase != _presentedPhase)
            ApplyPhasePresentation(view.Phase);

        if (view.Phase == RacePhase.Racing && !_holdCharacter && Character != null)
        {
            if (_path == null)
                BuildPath();
            if (_path != null)
                MoveCharacter(view.Elapsed / Mathf.Max(0.01f, DurationSeconds));
        }
    }

    private void ApplyPhasePresentation(RacePhase phase)
    {
        _presentedPhase = (int)phase;

        switch (phase)
        {
            case RacePhase.Ready:
                ResetPresentation();
                break;

            case RacePhase.Racing:
                BeginRacePresentation();
                break;

            case RacePhase.ArrivalCinematic:
                SetCharacterGliding(false);
                SetDestinationActive(false);
                SetRaceObjectsActive(false);
                SetDepartureActive(false);
                RestoreRootMotion();
                break;
        }
    }

    private void ResetPresentation()
    {
        _holdCharacter = false;
        _path = null;
        SetCharacterGliding(false);
        RestoreRootMotion();
        if (Character != null)
            Character.SetPositionAndRotation(_characterHomePosition, _characterHomeRotation);
        SetDestinationActive(false);
        SetRaceObjectsActive(false);
        SetDepartureActive(true);
    }

    private void BeginRacePresentation()
    {
        _holdCharacter = false;
        if (_characterAnimator != null)
        {
            if (!_ownsRootMotion)
            {
                _wasRootMotionEnabled = _characterAnimator.applyRootMotion;
                _ownsRootMotion = true;
            }
            _characterAnimator.applyRootMotion = false;
        }

        BuildPath();
        SetCharacterGliding(true);
        SetDepartureActive(false);
        SetDestinationActive(true);
        SetRaceObjectsActive(true);
    }

    private void HandleDepartureCountdown()
    {
        if (FailureMessage != null)
            FailureMessage.Hide();
        SetDepartureActive(false);
    }

    private void HandleDepartureCancelled()
    {
        SetDepartureActive(true);
    }

    private void HandleDepartureCinematic()
    {
        if (!HasValidSetup())
        {
            Debug.LogWarning("PigeonCharacterRace : assigner la zone de départ, le personnage, un CharacterRacePath avec des points, une zone d'arrivée, deux cinématiques distinctes, l'objet d'arrivée, la pose finale et le FadingMessage actif avec CanvasGroup et texte TMP. Vérifier les durées positives, la hauteur finie et les hiérarchies des Race Objects.", this);
            if (DepartureZone != null)
                DepartureZone.ReportLocalCinematicFinished();
            return;
        }

        _playingDepartureCinematic = true;
        DepartureCinematic.PlayCinematic();
        if (!DepartureCinematic.IsPlaying)
            HandleDepartureCinematicEnded();
    }

    private void HandleDepartureCinematicEnded()
    {
        if (!_playingDepartureCinematic)
            return;
        _playingDepartureCinematic = false;
        DepartureZone.ReportLocalCinematicFinished();
    }

    private bool LocalRaced(int startCount) => DepartureZone != null && DepartureZone.LocalParticipated(startCount);

    private void HandleFailed(int startCount, bool cheater)
    {
        if (FailureMessage != null && LocalRaced(startCount))
            FailureMessage.Show(cheater ? "CHEATER" : "YOU LOOSE");
    }

    private void HandleArrival(int startCount)
    {
        _holdCharacter = true;
        SetCharacterGliding(false);
        RestoreRootMotion();
        if (Character != null && CharacterArrivalPose != null)
            Character.SetPositionAndRotation(CharacterArrivalPose.position, CharacterArrivalPose.rotation);
        if (!LocalRaced(startCount))
            return;

        _playingArrivalStart = startCount;
        if (ArrivalCinematic != null)
            ArrivalCinematic.PlayCinematic();
        if (ArrivalCinematic == null || !ArrivalCinematic.IsPlaying)
            HandleArrivalCinematicEnded();
    }

    private void HandleArrivalCinematicEnded()
    {
        if (_playingArrivalStart == 0)
            return;
        ReportArrivalCinematicFinished(PigeonController.Local, _playingArrivalStart);
        _playingArrivalStart = 0;
    }

    /// <summary>Fin de la cinématique d'arrivée pour un pigeon contrôlé ici (joueur local, ou bot sur l'hôte).</summary>
    public void ReportArrivalCinematicFinished(PigeonController pigeon, int startCount)
    {
        if (pigeon != null && startCount > 0)
            pigeon.AcknowledgeCinematic(id, startCount);
    }

    /// <summary>Scénarios : fin de la cinématique d'arrivée pour ce pigeon, sur son dernier départ de cette course.</summary>
    public void ReportArrivalCinematicFinished(PigeonController pigeon)
        => ReportArrivalCinematicFinished(pigeon, pigeon != null && DepartureZone != null ? pigeon.LastStartCountFor(DepartureZone.id) : 0);

    private void HandleCompleted(int startCount)
    {
        if (!LocalRaced(startCount))
            return;
        if (CompletionAchievement != null)
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.Unlock(CompletionAchievement);
            else
                Debug.LogWarning("PigeonCharacterRace : course gagnée, mais aucun AchievementManager actif dans la scène.", this);
        }
        OnCompleted.Invoke();
    }

    private bool BuildPath()
    {
        int pointCount = Path != null ? Path.PointCount : Waypoints.Length;
        _path = new Vector3[pointCount + 1];
        _distances = new float[_path.Length];
        _path[0] = _characterHomePosition;
        _pathLength = 0f;
        for (int i = 0; i < pointCount; i++)
        {
            _path[i + 1] = Path != null ? Path.GetPointPosition(i) : Waypoints[i].position;
            _pathLength += Vector3.Distance(_path[i], _path[i + 1]);
            _distances[i + 1] = _pathLength;
        }
        if (IsFinite(_pathLength) && _pathLength > 0.001f)
            return true;

        Debug.LogWarning("PigeonCharacterRace : le trajet doit avoir une longueur finie et non nulle.", this);
        _path = null;
        return false;
    }

    private void MoveCharacter(float progress)
    {
        float distance = Mathf.Clamp01(progress) * _pathLength;
        for (int i = 1; i < _path.Length; i++)
        {
            float length = _distances[i] - _distances[i - 1];
            if (length <= 0f || distance > _distances[i])
                continue;

            Character.position = Vector3.Lerp(_path[i - 1], _path[i], (distance - _distances[i - 1]) / length);
            Vector3 heading = _path[i] - _path[i - 1];
            heading.y = 0f;
            if (FaceMovement && heading.sqrMagnitude > 0.0001f)
                Character.rotation = Quaternion.LookRotation(heading, Vector3.up);
            return;
        }
    }

    private void RestoreRootMotion()
    {
        if (_ownsRootMotion && _characterAnimator != null)
            _characterAnimator.applyRootMotion = _wasRootMotionEnabled;
        _ownsRootMotion = false;
    }

    private void SetCharacterGliding(bool gliding)
    {
        if (_characterAnimator != null && _characterAnimator.runtimeAnimatorController != null)
            _characterAnimator.SetBool(GlideHash, gliding);
    }

    private void SetDepartureActive(bool active)
    {
        if (DepartureMarker != null)
            DepartureMarker.SetActive(active);
    }

    private void SetDestinationActive(bool active)
    {
        if (DestinationObject != null)
            DestinationObject.SetActive(active);
    }

    private void SetRaceObjectsActive(bool active)
    {
        if (RaceObjects == null)
            return;
        foreach (GameObject raceObject in RaceObjects)
            if (raceObject != null && CanToggleRaceObject(raceObject))
                raceObject.SetActive(active);
    }

    private bool CanToggleRaceObject(GameObject raceObject)
    {
        Transform root = raceObject.transform;
        return !transform.IsChildOf(root)
            && (DepartureZone == null || !DepartureZone.transform.IsChildOf(root))
            && (Character == null || !Character.IsChildOf(root))
            && (DepartureCinematic == null || !DepartureCinematic.transform.IsChildOf(root))
            && (ArrivalCinematic == null || !ArrivalCinematic.transform.IsChildOf(root))
            && (FailureMessage == null || !FailureMessage.transform.IsChildOf(root));
    }

    private bool HasValidSetup()
    {
        if (DepartureZone == null || DestinationStop == null
            || DepartureCinematic == null || ArrivalCinematic == null || DepartureCinematic == ArrivalCinematic
            || !DepartureCinematic.isActiveAndEnabled || !ArrivalCinematic.isActiveAndEnabled
            || Character == null || !Character.gameObject.activeInHierarchy
            || CharacterArrivalPose == null || DestinationObject == null
            || !DestinationStop.transform.IsChildOf(DestinationObject.transform)
            || FailureMessage == null || !FailureMessage.isActiveAndEnabled || FailureMessage.Group == null
            || FailureMessage.GetComponent<TMPro.TMP_Text>() == null
            || !IsFinite(DurationSeconds) || DurationSeconds <= 0f || !IsFinite(MaximumHeight)
            || !IsFinite(ArrivalCinematicTimeout) || ArrivalCinematicTimeout <= 0f)
            return false;

        if (Path != null)
        {
            if (Path.PointCount == 0 || Path.transform.IsChildOf(Character))
                return false;
            for (int i = 0; i < Path.PointCount; i++)
                if (Path.GetPoint(i) == null)
                    return false;
        }
        else
        {
            if (Waypoints == null || Waypoints.Length == 0)
                return false;
            foreach (Transform waypoint in Waypoints)
                if (waypoint == null || waypoint.IsChildOf(Character))
                    return false;
        }
        if (RaceObjects != null)
            foreach (GameObject raceObject in RaceObjects)
                if (raceObject != null && !CanToggleRaceObject(raceObject))
                    return false;
        return true;
    }

#if UNITY_EDITOR
    [Button("Créer le trajet (reprendre les anciens points)"), BoxGroup("Course")]
    [HideInPlayMode, ShowIf("@Path == null")]
    public void CreatePath()
    {
        if (Application.isPlaying || Path != null)
            return;

        UnityEditor.Undo.RecordObject(this, "Créer le trajet de course");
        GameObject pathObject = new GameObject(name + " Path");
        UnityEditor.Undo.RegisterCreatedObjectUndo(pathObject, "Créer le trajet de course");
        pathObject.transform.SetParent(transform.parent, false);
        pathObject.transform.position = transform.position;
        Path = UnityEditor.Undo.AddComponent<CharacterRacePath>(pathObject);
        if (Waypoints != null)
            foreach (Transform waypoint in Waypoints)
                if (waypoint != null)
                    Path.AddPoint(waypoint.position);
        UnityEditor.EditorUtility.SetDirty(Path);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.Selection.activeGameObject = pathObject;
    }
#endif

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnDrawGizmosSelected()
    {
        if (Character == null || (Path == null && Waypoints == null))
            return;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Vector3 previous = Character.position;
        int pointCount = Path != null ? Path.PointCount : Waypoints.Length;
        for (int i = 0; i < pointCount; i++)
        {
            if ((Path != null && Path.GetPoint(i) == null) || (Path == null && Waypoints[i] == null))
                continue;
            Vector3 position = Path != null ? Path.GetPointPosition(i) : Waypoints[i].position;
            Gizmos.DrawLine(previous, position);
            Gizmos.DrawWireSphere(position, 0.25f);
            previous = position;
        }
        Gizmos.color = previousColor;
    }
}