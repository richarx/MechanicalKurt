using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PigeonCharacterRace : MonoBehaviour
{
    public enum RacePhase { Ready, DepartureCinematic, Racing, ArrivalCinematic, Completed }

    [field: SerializeField, BoxGroup("Départ"), Required]
    public PigeonStopZone DepartureStop { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Required]
    public DialogCinematic DepartureCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Tooltip("Visuel facultatif, séparé des cinématiques et du contrôleur de course.")]
    public GameObject DepartureMarker { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public PigeonController Player { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public Transform Character { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required, ListDrawerSettings(ShowIndexLabels = true)]
    [field: Tooltip("Points à rejoindre dans l'ordre, depuis la position du personnage à la fin du dialogue. Le dernier est la fin du trajet. Garder ces points indépendants du personnage.")]
    public Transform[] Waypoints { get; private set; } = new Transform[0];

    [field: SerializeField, BoxGroup("Course"), MinValue(0.01f), SuffixLabel("s", true)]
    public float DurationSeconds { get; private set; } = 30f;

    [field: SerializeField, BoxGroup("Course")]
    public bool FaceMovement { get; private set; } = true;

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    [field: Tooltip("Objet activé pendant la course. Doit contenir Destination Stop, mais pas les cinématiques ni ce contrôleur.")]
    public GameObject DestinationObject { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public PigeonStopZone DestinationStop { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    [field: Tooltip("Position et rotation du personnage pour la cinématique finale. Le pigeon joueur reste sur place.")]
    public Transform CharacterArrivalPose { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public DialogCinematic ArrivalCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Échec"), SuffixLabel("Y monde", true)]
    [field: Tooltip("Altitude absolue maximale du pigeon pendant la course.")]
    public float MaximumHeight { get; private set; } = 10f;

    [field: SerializeField, BoxGroup("Échec"), Required]
    public FadingMessage FailureMessage { get; private set; }

    [field: SerializeField, BoxGroup("Événements")]
    public UnityEvent OnCompleted { get; private set; } = new UnityEvent();

    [ShowInInspector, ReadOnly]
    public RacePhase Phase { get; private set; }

    private Vector3[] _path;
    private float[] _distances;
    private float _pathLength;
    private double _startedAt;
    private float _duration;
    private Vector3 _characterStartPosition;
    private Quaternion _characterStartRotation;
    private bool _hasCharacterPose;
    private Animator _characterAnimator;
    private bool _wasRootMotionEnabled;
    private bool _ownsRootMotion;

    private void OnEnable()
    {
        SetDepartureActive(Phase == RacePhase.Ready);
        SetDestinationActive(false);
        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicStarted.AddListener(HandleDepartureStarted);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.AddListener(HandleArrivalEnded);
    }

    private void HandleDepartureStarted()
    {
        if (!isActiveAndEnabled || Phase != RacePhase.Ready)
            return;
        if (!HasValidSetup())
        {
            Debug.LogWarning("PigeonCharacterRace : assigner le pigeon, le personnage, les points du trajet, deux PigeonStop et deux cinématiques distincts, l'objet d'arrivée, la pose finale et le FadingMessage actif avec CanvasGroup et texte TMP. Vérifier la durée positive, la hauteur finie et les hiérarchies (voir le guide de montage).", this);
            DepartureCinematic.CancelCinematic();
            return;
        }

        _characterStartPosition = Character.position;
        _characterStartRotation = Character.rotation;
        _hasCharacterPose = true;
        Phase = RacePhase.DepartureCinematic;
        FailureMessage.Hide();
        SetDepartureActive(false);
    }

    // Brancher sur OnCinematicEnded de la cinématique de départ.
    public void StartRace()
    {
        if (!isActiveAndEnabled || Phase != RacePhase.DepartureCinematic || DepartureCinematic.IsPlaying)
            return;
        if (!DepartureCinematic.CompletedNormally || !HasValidSetup() || !BuildPath())
        {
            ResetAttempt();
            return;
        }

        _characterAnimator = Character.GetComponentInChildren<Animator>();
        if (_characterAnimator != null)
        {
            _wasRootMotionEnabled = _characterAnimator.applyRootMotion;
            _ownsRootMotion = true;
            _characterAnimator.applyRootMotion = false;
        }
        _duration = DurationSeconds;
        _startedAt = Time.timeAsDouble;
        Phase = RacePhase.Racing;
        SetDestinationActive(true);
    }

    private void LateUpdate()
    {
        if (Phase != RacePhase.Racing || Time.deltaTime <= 0f)
            return;
        if (Player == null || Character == null)
        {
            ResetAttempt();
            return;
        }
        if (CheckFailure())
            return;

        MoveCharacter((float)((Time.timeAsDouble - _startedAt) / _duration));
    }

    // Brancher sur OnPlayerStopped du PigeonStop d'arrivée.
    public void CompleteRace()
    {
        if (!isActiveAndEnabled || Phase != RacePhase.Racing || Time.timeScale <= 0f
            || DestinationStop == null || !DestinationStop.isActiveAndEnabled)
            return;
        // Vérifier aussi ici : l'ordre des LateUpdate ne doit pas permettre une victoire hors délai.
        if (CheckFailure())
            return;

        Phase = RacePhase.ArrivalCinematic;
        SetDestinationActive(false);
        RestoreRootMotion();
        Character.SetPositionAndRotation(CharacterArrivalPose.position, CharacterArrivalPose.rotation);
        ArrivalCinematic.PlayCinematic();
        if (!ArrivalCinematic.IsPlaying && Phase == RacePhase.ArrivalCinematic)
            ResetAttempt();
    }

    private bool CheckFailure()
    {
        if (Player.transform.position.y > MaximumHeight)
        {
            ResetAttempt();
            FailureMessage.Show("CHEATER");
            return true;
        }
        if (Time.timeAsDouble - _startedAt >= _duration)
        {
            ResetAttempt();
            FailureMessage.Show("YOU LOOSE");
            return true;
        }
        return false;
    }

    private void HandleArrivalEnded()
    {
        if (Phase != RacePhase.ArrivalCinematic)
            return;
        if (!ArrivalCinematic.CompletedNormally)
        {
            ResetAttempt();
            return;
        }

        Phase = RacePhase.Completed;
        _hasCharacterPose = false;
        OnCompleted.Invoke();
    }

    private bool BuildPath()
    {
        _path = new Vector3[Waypoints.Length + 1];
        _distances = new float[_path.Length];
        _path[0] = Character.position;
        _pathLength = 0f;
        for (int i = 0; i < Waypoints.Length; i++)
        {
            _path[i + 1] = Waypoints[i].position;
            _pathLength += Vector3.Distance(_path[i], _path[i + 1]);
            _distances[i + 1] = _pathLength;
        }
        if (IsFinite(_pathLength) && _pathLength > 0.001f)
            return true;
        Debug.LogWarning("PigeonCharacterRace : le trajet doit avoir une longueur finie et non nulle.", this);
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

    private void ResetAttempt()
    {
        Phase = RacePhase.Ready;
        RestoreRootMotion();
        if (_hasCharacterPose && Character != null)
            Character.SetPositionAndRotation(_characterStartPosition, _characterStartRotation);
        _hasCharacterPose = false;
        SetDestinationActive(false);
        SetDepartureActive(true);
    }

    private void RestoreRootMotion()
    {
        if (_ownsRootMotion && _characterAnimator != null)
            _characterAnimator.applyRootMotion = _wasRootMotionEnabled;
        _ownsRootMotion = false;
    }

    private void SetDepartureActive(bool active)
    {
        if (DepartureStop != null)
            DepartureStop.enabled = active;
        if (DepartureMarker != null)
            DepartureMarker.SetActive(active);
    }

    private void SetDestinationActive(bool active)
    {
        if (DestinationStop != null)
            DestinationStop.enabled = active;
        if (DestinationObject != null)
            DestinationObject.SetActive(active);
    }

    private bool HasValidSetup()
    {
        if (DepartureStop == null || DestinationStop == null || DepartureStop == DestinationStop
            || DepartureCinematic == null || ArrivalCinematic == null || DepartureCinematic == ArrivalCinematic
            || !DepartureCinematic.isActiveAndEnabled || !ArrivalCinematic.isActiveAndEnabled
            || !DepartureStop.gameObject.activeInHierarchy || Player == null || !Player.isActiveAndEnabled
            || Character == null || !Character.gameObject.activeInHierarchy || Character == Player.transform
            || CharacterArrivalPose == null || DestinationObject == null
            || !DestinationStop.transform.IsChildOf(DestinationObject.transform)
            || FailureMessage == null || !FailureMessage.isActiveAndEnabled || FailureMessage.Group == null
            || FailureMessage.GetComponent<TMPro.TMP_Text>() == null
            || !IsFinite(DurationSeconds) || DurationSeconds <= 0f || !IsFinite(MaximumHeight)
            || Waypoints == null || Waypoints.Length == 0)
            return false;
        foreach (Transform waypoint in Waypoints)
            if (waypoint == null || waypoint.IsChildOf(Character))
                return false;
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private void OnDisable()
    {
        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicStarted.RemoveListener(HandleDepartureStarted);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.RemoveListener(HandleArrivalEnded);

        RacePhase previousPhase = Phase;
        if (Phase != RacePhase.Completed)
            ResetAttempt();
        if (previousPhase == RacePhase.DepartureCinematic)
            DepartureCinematic.CancelCinematic();
        else if (previousPhase == RacePhase.ArrivalCinematic)
            ArrivalCinematic.CancelCinematic();
    }

    private void OnDrawGizmosSelected()
    {
        if (Character == null || Waypoints == null)
            return;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Vector3 previous = Character.position;
        foreach (Transform point in Waypoints)
        {
            if (point == null)
                continue;
            Gizmos.DrawLine(previous, point.position);
            Gizmos.DrawWireSphere(point.position, 0.25f);
            previous = point.position;
        }
        Gizmos.color = previousColor;
    }
}
