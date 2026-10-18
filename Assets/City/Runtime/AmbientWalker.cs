using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class AmbientWalker : MonoBehaviour
{
    private static readonly int AnimationOffsetHash = Animator.StringToHash("AnimationOffset");

    [Header("Parcours")]
    [Tooltip("La liste de points du WalkerPath definit la boucle dans l'ordre.")]
    [SerializeField] private WalkerPath _path;

    [Header("Deplacement")]
    [Tooltip("Vitesse minimale et maximale en unites/seconde. Une vitesse est tiree a chaque activation.")]
    [SerializeField] private Vector2 _speedRange = new Vector2(1f, 1.5f);
    [SerializeField, Min(0f)] private float _turnSpeed = 360f;
    [Tooltip("Pause minimale et maximale a chaque point, en secondes. Mettre les deux a zero pour marcher sans pause.")]
    [SerializeField] private Vector2 _pauseRange = Vector2.zero;

    [Header("Animation")]
    [Tooltip("Animator du personnage, cherche automatiquement dans les enfants si vide. Le script desactive le root motion.")]
    [SerializeField] private Animator _animator;
    [Tooltip("Parametre bool de l'Animator. Laisser vide pour ne pas piloter l'animation.")]
    [SerializeField] private string _walkingParameter = "IsWalking";

    [field: SerializeField, MinMaxSlider(0f, 1f, true)]
    [field: Tooltip("Decalage aleatoire du cycle de marche a chaque activation (0 a 1 = un cycle).")]
    public Vector2 AnimationOffsetRange { get; private set; } = new Vector2(0f, 1f);

    public bool IsWalking { get; private set; }
    public bool IsPaused { get; private set; }

    public void SetPaused(bool paused)
    {
        if (IsPaused && !paused && _path != null && _path.PointCount >= 2)
        {
            // The hit may have moved the character to the other side of its next waypoint.
            Vector3 heading = _path.GetPosition(_targetIndex % _path.PointCount) - transform.position;
            heading.y = 0f;
            if (heading.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
        }

        IsPaused = paused;
        if (paused)
            SetWalking(false);
    }

    private int _targetIndex;
    private float _speed;
    private float _pauseRemaining;
    private bool _arrivedAtTarget;
    private int _walkingParameterHash;
    private bool _canSetWalkingParameter;
    private bool? _lastAnimationValue;

    private void Reset()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    private void OnValidate()
    {
        _speedRange.x = Mathf.Max(0f, _speedRange.x);
        _speedRange.y = Mathf.Max(_speedRange.x, _speedRange.y);
        _pauseRange.x = Mathf.Max(0f, _pauseRange.x);
        _pauseRange.y = Mathf.Max(_pauseRange.x, _pauseRange.y);
        _turnSpeed = Mathf.Max(0f, _turnSpeed);
    }

    private void OnEnable()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        ConfigureAnimation();
        SetWalking(false);
        _pauseRemaining = 0f;
        _arrivedAtTarget = false;
        _speed = Random.Range(_speedRange.x, _speedRange.y);

        if (_path == null || _path.PointCount < 2)
        {
            Debug.LogWarning("AmbientWalker : assigner un WalkerPath avec au moins deux points.", this);
            return;
        }

        // Start from the closest point without teleporting the character.
        _targetIndex = _path.GetClosestPointIndex(transform.position);
    }

    private void Update()
    {
        if (IsPaused)
            return;

        float deltaTime = Time.deltaTime;
        if (_path == null || _path.PointCount < 2 || _speed <= 0f || deltaTime <= 0f)
        {
            SetWalking(false);
            return;
        }

        if (_pauseRemaining > 0f)
        {
            _pauseRemaining = Mathf.Max(0f, _pauseRemaining - deltaTime);
            SetWalking(false);
            return;
        }

        _targetIndex %= _path.PointCount;
        Vector3 position = transform.position;
        Vector3 target = _path.GetPosition(_targetIndex);

        // Bound the search so coincident points cannot cause an infinite loop.
        for (int i = 0; (target - position).sqrMagnitude <= 0.0001f; i++)
        {
            if (i >= _path.PointCount)
            {
                SetWalking(false);
                return;
            }

            CityPathPoint point = _path.GetPoint(_targetIndex);
            if (!_arrivedAtTarget)
            {
                _arrivedAtTarget = true;
                _pauseRemaining = Random.Range(_pauseRange.x, _pauseRange.y) + point.RollWaitDuration();
            }
            if (_pauseRemaining > 0f)
            {
                SetWalking(false);
                return;
            }

            if (point.GetWaitDuration() > 0f)
            {
                SetWalking(false);
                return;
            }

            _targetIndex = (_targetIndex + 1) % _path.PointCount;
            _arrivedAtTarget = false;

            target = _path.GetPosition(_targetIndex);
        }

        Vector3 heading = target - position;
        heading.y = 0f;
        if (heading.sqrMagnitude > 0.0001f)
        {
            Quaternion rotation = Quaternion.LookRotation(heading, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, _turnSpeed * deltaTime);
        }

        transform.position = Vector3.MoveTowards(position, target, _speed * deltaTime);
        SetWalking(true);
    }

    private void OnDisable()
    {
        SetWalking(false);
    }

    private void ConfigureAnimation()
    {
        _canSetWalkingParameter = false;
        _lastAnimationValue = null;
        if (_animator == null)
            return;

        _animator.applyRootMotion = false;
        if (_animator.runtimeAnimatorController == null)
            return;

        _walkingParameterHash = string.IsNullOrWhiteSpace(_walkingParameter) ? 0 : Animator.StringToHash(_walkingParameter);
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.nameHash == AnimationOffsetHash && parameter.type == AnimatorControllerParameterType.Float)
            {
                float minimum = Mathf.Clamp01(AnimationOffsetRange.x);
                float maximum = Mathf.Clamp(AnimationOffsetRange.y, minimum, 1f);
                _animator.SetFloat(AnimationOffsetHash, Random.Range(minimum, maximum));
            }

            if (parameter.nameHash == _walkingParameterHash && parameter.type == AnimatorControllerParameterType.Bool)
            {
                _canSetWalkingParameter = true;
            }
        }

        if (_canSetWalkingParameter || string.IsNullOrWhiteSpace(_walkingParameter))
            return;

        Debug.LogWarning($"AmbientWalker : ajouter un parametre bool '{_walkingParameter}' a l'Animator, ou vider Walking Parameter.", this);
    }

    private void SetWalking(bool walking)
    {
        IsWalking = walking;
        if (!_canSetWalkingParameter || _animator == null || _lastAnimationValue == walking)
            return;

        _animator.SetBool(_walkingParameterHash, walking);
        _lastAnimationValue = walking;
    }
}
