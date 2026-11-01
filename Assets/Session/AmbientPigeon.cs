using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pigeon de décor du menu, local et sans réseau : marche, roucoule, s'envole pour un tour et se repose,
/// autour de son point de départ. Pilote l'Animator du modèle du joueur.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AmbientPigeon : MonoBehaviour
{
    enum State { Idle, Walk, Turn, TakeOff, Fly, Land }

    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
    static readonly int FlapHash = Animator.StringToHash("Flap");
    static readonly int HoverHash = Animator.StringToHash("Hover");
    static readonly int DanceHash = Animator.StringToHash("Dance");

    [Tooltip("Rayon autour du point de départ où il marche et se pose.")]
    [SerializeField, Min(0.5f)] float _groundRadius = 3f;
    [Tooltip("Écart de hauteur accepté entre un point de marche et le départ. Petit pour un pigeon perché (bord du nid) : il reste sur son perchoir.")]
    [SerializeField, Min(0.01f)] float _groundTolerance = 0.6f;
    [SerializeField, Min(0.1f)] float _walkSpeed = 0.6f;
    [SerializeField, Min(0.5f)] float _flySpeed = 7f;
    [Tooltip("Rayon et hauteur (au-dessus du départ) du tour en vol.")]
    [SerializeField] Vector2 _flightRadius = new Vector2(6f, 14f);
    [SerializeField] Vector2 _flightHeight = new Vector2(3f, 9f);
    [Tooltip("Décalage du centre du tour en vol par rapport au départ, pour qu'il ne passe pas devant la caméra.")]
    [SerializeField] Vector3 _flightCenterOffset;
    [SerializeField] Vector2 _idleDuration = new Vector2(1.5f, 5f);
    [Tooltip("Chances, à chaque pause, de s'envoler plutôt que de marcher.")]
    [SerializeField, Range(0f, 1f)] float _flyChance = 0.25f;
    [SerializeField, Range(0f, 1f)] float _cooChance = 0.35f;
    [SerializeField] List<AudioClip> _cooClips = new List<AudioClip>();
    [SerializeField] AudioClip _takeOffClip;
    [Tooltip("Sons des événements d'animation du modèle (mêmes que PigeonAnimationSFX).")]
    [SerializeField] AudioClip _flapClip;
    [SerializeField] List<AudioClip> _footstepClips = new List<AudioClip>();
    [SerializeField] List<AudioClip> _landingClips = new List<AudioClip>();
    [SerializeField] LayerMask _groundLayers = ~0;
    [Tooltip("Groupe d'AmbientPigeonSpot où il se pose (un libre au hasard au lancement et après chaque vol). Vide : reste autour de sa place.")]
    [SerializeField] string _spotGroup = "";

    AmbientPigeonSpot _spot;

    /// <summary>Ce qu'il fait en ce moment (repos, marche, envol, vol, atterrissage), pour les tests.</summary>
    public string Activity => _state.ToString();

    Animator _animator;
    AudioSource _audio;
    Vector3 _home;
    State _state;
    float _timer;
    Vector3 _target;
    float _flightAngle;
    float _flightRadiusNow;
    float _flightAltitude;
    float _verticalSpeed;
    float _turnYaw;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _audio = GetComponent<AudioSource>();
        _home = transform.position;
        _timer = Random.Range(0f, _idleDuration.y);
        SetGrounded(true);
    }

    void Start()
    {
        // Après les OnEnable : tous les emplacements sont inscrits.
        if (!TakeSpot())
            return;
        transform.SetPositionAndRotation(_home, _spot.transform.rotation);
    }

    /// <summary>Prend un emplacement libre de son groupe, qui devient son point de départ.</summary>
    bool TakeSpot()
    {
        if (string.IsNullOrEmpty(_spotGroup))
            return false;
        _spot = AmbientPigeonSpot.Take(_spotGroup, this, _spot);
        if (_spot == null)
            return false;
        _home = _spot.transform.position;
        _groundRadius = _spot.GroundRadius;
        _groundTolerance = _spot.GroundTolerance;
        return true;
    }

    Vector3 FlightCenter => _home + _flightCenterOffset;

    void Update()
    {
        float dt = Time.deltaTime;
        _timer -= dt;
        switch (_state)
        {
            case State.Idle: UpdateIdle(); break;
            case State.Walk: UpdateWalk(dt); break;
            case State.Turn: UpdateTurn(dt); break;
            case State.TakeOff: UpdateTakeOff(dt); break;
            case State.Fly: UpdateFly(dt); break;
            case State.Land: UpdateLand(dt); break;
        }
        _animator.SetFloat(VerticalSpeedHash, _verticalSpeed);
    }

    void UpdateIdle()
    {
        _animator.SetFloat(SpeedHash, 0f);
        if (_timer > 0f)
            return;
        _animator.SetBool(DanceHash, false);

        float roll = Random.value;
        if (roll < _flyChance)
        {
            StartTakeOff();
            return;
        }
        if (roll < _flyChance + _cooChance)
        {
            Coo();
            _animator.SetBool(DanceHash, Random.value < 0.5f);
            _timer = Random.Range(_idleDuration.x, _idleDuration.y);
            return;
        }
        if (TryGroundPoint(out _target))
        {
            _state = State.Walk;
            return;
        }
        // Perché (bord du nid) : pas de place pour marcher, il regarde ailleurs ou repart plus souvent.
        if (Random.value < 0.35f)
        {
            StartTakeOff();
            return;
        }
        _turnYaw = transform.eulerAngles.y + Random.Range(60f, 150f) * (Random.value < 0.5f ? -1f : 1f);
        _state = State.Turn;
    }

    void UpdateTurn(float dt)
    {
        Face(Quaternion.Euler(0f, _turnYaw, 0f) * Vector3.forward, dt * 0.5f);
        _animator.SetFloat(SpeedHash, _walkSpeed * 0.5f);
        if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _turnYaw)) > 1f)
            return;
        _state = State.Idle;
        _timer = Random.Range(_idleDuration.x, _idleDuration.y);
    }

    void UpdateWalk(float dt)
    {
        Vector3 flat = _target - transform.position;
        flat.y = 0f;
        if (flat.magnitude < 0.05f)
        {
            _state = State.Idle;
            _timer = Random.Range(_idleDuration.x, _idleDuration.y);
            return;
        }
        Face(flat, dt);
        Vector3 next = transform.position + flat.normalized * Mathf.Min(_walkSpeed * dt, flat.magnitude);
        if (GroundAt(next, out Vector3 ground))
            next.y = ground.y;
        transform.position = next;
        _animator.SetFloat(SpeedHash, _walkSpeed);
    }

    void StartTakeOff()
    {
        _state = State.TakeOff;
        _timer = 0.8f;
        _flightRadiusNow = Random.Range(_flightRadius.x, _flightRadius.y);
        _flightAltitude = _home.y + Random.Range(_flightHeight.x, _flightHeight.y);
        Vector3 fromCenter = transform.position - FlightCenter;
        _flightAngle = Mathf.Atan2(fromCenter.z, fromCenter.x);
        SetGrounded(false);
        _animator.SetBool(FlapHash, true);
        if (_audio != null && _takeOffClip != null)
            _audio.PlayOneShot(_takeOffClip);
    }

    void UpdateTakeOff(float dt)
    {
        _verticalSpeed = 3f;
        transform.position += Vector3.up * _verticalSpeed * dt;
        _animator.SetFloat(SpeedHash, 2f);
        if (_timer > 0f)
            return;
        _state = State.Fly;
        _timer = Random.Range(5f, 10f);
    }

    void UpdateFly(float dt)
    {
        _flightAngle += _flySpeed / _flightRadiusNow * dt;
        Vector3 goal = FlightCenter + new Vector3(Mathf.Cos(_flightAngle), 0f, Mathf.Sin(_flightAngle)) * _flightRadiusNow;
        goal.y = _flightAltitude;
        MoveToward(goal, _flySpeed, dt);
        _animator.SetBool(FlapHash, Mathf.Repeat(_timer, 3f) < 1.2f);

        if (_timer > 0f)
            return;
        if (TakeSpot())
            _target = _home;
        else if (!TryGroundPoint(out _target))
            _target = _home;
        _state = State.Land;
    }

    void UpdateLand(float dt)
    {
        Vector3 toTarget = _target - transform.position;
        bool close = new Vector2(toTarget.x, toTarget.z).magnitude < 1.5f;
        _animator.SetBool(FlapHash, !close);
        _animator.SetBool(HoverHash, close);
        MoveToward(close ? _target : _target + Vector3.up * 1.5f, close ? 1.5f : _flySpeed * 0.7f, dt);
        if (toTarget.magnitude > 0.05f)
            return;
        transform.position = _target;
        _verticalSpeed = 0f;
        SetGrounded(true);
        _state = State.Idle;
        _timer = Random.Range(_idleDuration.x, _idleDuration.y);
    }

    void MoveToward(Vector3 goal, float speed, float dt)
    {
        Vector3 step = Vector3.MoveTowards(transform.position, goal, speed * dt) - transform.position;
        _verticalSpeed = dt > 0f ? step.y / dt : 0f;
        Face(new Vector3(step.x, 0f, step.z), dt);
        transform.position += step;
        _animator.SetFloat(SpeedHash, dt > 0f ? step.magnitude / dt : 0f);
    }

    void Face(Vector3 direction, float dt)
    {
        if (direction.sqrMagnitude < 0.000001f)
            return;
        Quaternion goal = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, 360f * dt);
    }

    void SetGrounded(bool grounded)
    {
        _animator.SetBool(GroundedHash, grounded);
        _animator.SetBool(HoverHash, false);
        if (grounded)
            _animator.SetBool(FlapHash, false);
    }

    void Coo() => PlayRandom(_cooClips, 1f);

    public void PlayFlapSound()
    {
        if (_audio != null && _flapClip != null)
            _audio.PlayOneShot(_flapClip, 0.4f);
    }

    public void PlayFootStepSound() => PlayRandom(_footstepClips, 0.25f);

    public void PlayLandingSound() => PlayRandom(_landingClips, 0.5f);

    void PlayRandom(List<AudioClip> clips, float volume)
    {
        if (_audio != null && clips.Count > 0)
            _audio.PlayOneShot(clips[Random.Range(0, clips.Count)], volume);
    }

    bool TryGroundPoint(out Vector3 point)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * _groundRadius;
            if (GroundAt(_home + new Vector3(offset.x, 0f, offset.y), out point) && Mathf.Abs(point.y - _home.y) < _groundTolerance)
                return true;
        }
        point = _home;
        return false;
    }

    bool GroundAt(Vector3 position, out Vector3 ground)
    {
        bool hit = Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit info, 6f, _groundLayers, QueryTriggerInteraction.Ignore);
        ground = hit ? info.point : position;
        return hit;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 home = Application.isPlaying ? _home : transform.position;
        Gizmos.DrawWireSphere(home, _groundRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(home + _flightCenterOffset, _flightRadius.x);
        Gizmos.DrawWireSphere(home + _flightCenterOffset, _flightRadius.y);
    }
}
