using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

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

    [field: SerializeField, BoxGroup("Départ")]
    [field: FormerlySerializedAs("<CharacterDeparturePose>k__BackingField")]
    [field: Tooltip("Repère facultatif : le pigeon est placé ici, orienté selon son forward (axe bleu), avec sa caméra derrière lui. Incliner le repère vers l'eau pour préparer le plongeon. Appliqué après la cinématique de départ.")]
    public Transform PlayerDeparturePose { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public PigeonController Player { get; private set; }

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
    [field: Tooltip("Objets affichés uniquement pendant la course, après le dialogue de départ. Masqués à la victoire, à l'échec et à l'annulation. Ne pas inclure le contrôleur, le départ, les cinématiques, le joueur, le personnage ou le message, ni leurs parents.")]
    public GameObject[] RaceObjects { get; private set; } = new GameObject[0];

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

    [field: SerializeField, BoxGroup("Arrivée")]
    [field: Tooltip("Achievement attribué après la fin normale de la cinématique de victoire. L'ajouter aussi à la liste de l'AchievementManager.")]
    public AchievementData CompletionAchievement { get; private set; }

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
        SetRaceObjectsActive(false);
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
            Debug.LogWarning("PigeonCharacterRace : assigner le pigeon, le personnage, un CharacterRacePath avec des points, deux PigeonStop et deux cinématiques distincts, l'objet d'arrivée, la pose finale et le FadingMessage actif avec CanvasGroup et texte TMP. Vérifier la durée positive, la hauteur finie et les hiérarchies des Race Objects (voir le guide de montage).", this);
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
        if (!DepartureCinematic.CompletedNormally || !HasValidSetup())
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
        if (!BuildPath())
        {
            ResetAttempt();
            return;
        }
        if (PlayerDeparturePose != null)
            Player.Teleport(PlayerDeparturePose.position, PlayerDeparturePose.rotation);
        _duration = DurationSeconds;
        _startedAt = Time.timeAsDouble;
        Phase = RacePhase.Racing;
        SetDestinationActive(true);
        SetRaceObjectsActive(true);
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
        SetRaceObjectsActive(false);
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
        _path[0] = Character.position;
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
        SetRaceObjectsActive(false);
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
            && (DepartureStop == null || !DepartureStop.transform.IsChildOf(root))
            && (Player == null || !Player.transform.IsChildOf(root))
            && (Character == null || !Character.IsChildOf(root))
            && (DepartureCinematic == null || !DepartureCinematic.transform.IsChildOf(root))
            && (ArrivalCinematic == null || !ArrivalCinematic.transform.IsChildOf(root))
            && (FailureMessage == null || !FailureMessage.transform.IsChildOf(root));
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
            || !IsFinite(DurationSeconds) || DurationSeconds <= 0f || !IsFinite(MaximumHeight))
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

    private void OnDisable()
    {
        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicStarted.RemoveListener(HandleDepartureStarted);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.RemoveListener(HandleArrivalEnded);

        RacePhase previousPhase = Phase;
        if (Phase != RacePhase.Completed)
            ResetAttempt();
        else
            SetRaceObjectsActive(false);
        if (previousPhase == RacePhase.DepartureCinematic)
            DepartureCinematic.CancelCinematic();
        else if (previousPhase == RacePhase.ArrivalCinematic)
            ArrivalCinematic.CancelCinematic();
    }

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
