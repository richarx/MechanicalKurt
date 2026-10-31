using System;
using System.Collections.Generic;
using PurrNet.Prediction;
using UnityEngine;
using UnityEngine.Events;

public struct PigeonInput : IPredictedData
{
    public Vector2 Movement;
    public Vector2 RelativeMovement;
    public bool Flap;
    public bool Dive;
    public bool Brake;
    public bool BarrelLeft;
    public bool BarrelRight;
    public bool Interact;
    public bool Respawn;
    public byte EquipmentWear;
    public byte EquipmentToggle;
    /// <summary>Dernière cinématique partagée finie sur cet écran (zone ou course, numéro de départ), répétée à chaque tick. 0 = aucune.</summary>
    public PredictedComponentID CinematicAckZone;
    public int CinematicAckCount;

    public void Dispose() { }
}

public struct PigeonState : IPredictedData<PigeonState>
{
    public Vector3 Position;
    public float Yaw;
    public float Pitch;
    public Vector3 LastVelocity;
    public Vector3 PendingImpulse;
    public bool Grounded;
    public bool PreserveTeleportPitch;
    /// <summary>Temps passé en vol stationnaire : phase de l'oscillation verticale, identique sur chaque machine.</summary>
    public float HoverTime;

    public bool Stunned;
    public float StunRemaining;
    public float StunImmuneRemaining;
    public Vector3 LastHitNormal;
    public Vector3 PreStunVelocity;
    public Quaternion StunRotation;

    public bool InsideBoostRing;

    /// <summary>MovingSupport.Id du support sous le pigeon, 0 = aucun.</summary>
    public int SupportId;
    /// <summary>Position du pigeon dans le repère du support, en fin de tick.</summary>
    public Vector3 SupportLocalAnchor;
    public Vector3 SupportVelocity;

    /// <summary>Gel posé par un orchestrateur (zone de départ, course).</summary>
    public bool Frozen;
    public PigeonMissionState Mission;

    /// <summary>Équipements portés, un bit par PigeonEquipment.Equipment.</summary>
    public byte Equipment;

    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation;

    public void Dispose() { }
}

[RequireComponent(typeof(CharacterController))]
public partial class PigeonController : PredictedIdentity<PigeonInput, PigeonState>
{
    [Header("Interactions / Physics")]
    [SerializeField] float _pushPower = 5f;
    [SerializeField] float _weight = 1.5f;
    [SerializeField] float _stunMinImpact = 10f;
    [SerializeField] float _stunMinHorizontalSpeed = 3;
    [SerializeField] float _stunMinDuration = 0.5f;
    [SerializeField] float _stunImmuneDuration = 0.5f;
    [Tooltip("Layers des triggers BoostRing. Vide (Nothing) = tous les layers.")]
    [SerializeField] LayerMask _boostLayer;

    [Header("Stun (remplace le Rigidbody)")]
    [SerializeField] float _stunKnockback = 1f;
    [SerializeField] float _stunFriction = 8f;
    [SerializeField, Range(0f, 1f)] float _stunBounce = 0.35f;
    [SerializeField] float _stunBounceMinSpeed = 1.5f;
    [SerializeField] float _stunRollRadius = 0.3f;

    [Header("Ground Movement")]
    [SerializeField] float _groundSpeed = 1f;

    [Header("Rotation")]
    [SerializeField] float _turnSpeed = 90f;
    [SerializeField] float _pitchSpeed = 60f;
    [SerializeField] float _visualRollAmount = 90f;
    [SerializeField] float _rollSpeed = 3f;
    [SerializeField] Transform _visualRoot;

    [Header("Flight")]
    [SerializeField] float _maxSpeed = 25f;
    [SerializeField] float _dragAtMaxSpeed = 3f;
    [SerializeField] float _liftFactor = 0.85f;
    [SerializeField] float _stallSpeed = 1f;

    [Header("Gliding")]
    [SerializeField] float _maxPitch = 45f;

    [Header("Brake")]
    [SerializeField] float _brakeMaxSpeed = 1f;
    [SerializeField] float _brakeDragAtMaxSpeed = 0.1f;
    [SerializeField] float _hoverWobbleFrequency = 1f;
    [SerializeField] float _hoverWobbleAmplitude = 1f;
    [SerializeField] float _hoverMoveSpeed = 1f;

    [Header("Flapping")]
    [SerializeField] float _takeoffSpeed = 3f;
    [SerializeField] float _flapAccel = 12f;
    [SerializeField] float _flapMinSpeed = 1.5f;
    [SerializeField] float _flapSpeedPenalty = 0.5f;
    [SerializeField] float _flapMaxSpeed = 15f;
    [SerializeField] float _flapDragAtMaxSpeed = 3f;

    [Header("Diving")]
    [SerializeField] float _divePitchSpeed = 120f;
    [SerializeField] float _diveMaxPitch = 80f;
    [SerializeField] float _diveMaxSpeed = 40f;
    [SerializeField] float _diveDragAtMaxSpeed = 1.5f;

    [Header("Barrel Roll")]
    [Tooltip("Minimum speed to barrel roll. Keep above ~4 or the pigeon can roll while walking or taking off.")]
    [SerializeField] float _minBarrelRollSpeed = 20f;
    [SerializeField] float _rollDuration = 1.5f;

    [Header("Buoyancy")]
    [SerializeField] float _waterLevelModifier = 0.1f;
    [SerializeField] float _buoyancyForce = 10f;
    [Tooltip("Profondeur maximale (m) sous le niveau de l'eau : en dessous, le pigeon ne peut plus descendre.")]
    [SerializeField, Min(0f)] float _maxUnderwaterDepth = 2f;

    [Header("Sous l'eau : speedway")]
    [Tooltip("Accélération horizontale (m/s²) gagnée sous l'eau en piquant du nez, au piqué maximal du plané. 0 = effet désactivé.")]
    [SerializeField, Min(0f)] float _underwaterThrust = 7.5f;
    [Tooltip("Vitesse horizontale (m/s) à laquelle cette poussée s'annule ; elle diminue progressivement jusque-là. Avec les réglages par défaut, la vitesse plafonne vers 34 m/s.")]
    [SerializeField, Min(1f)] float _underwaterThrustFadeSpeed = 50f;
    [Tooltip("Part de la traînée de vol gardée pendant l'effet (0 = aucune, 1 = comme en l'air). Dosée selon le piqué.")]
    [SerializeField, Range(0f, 1f)] float _underwaterDragFactor = 0f;

    [Header("Debug")]
    [SerializeField] bool _activeDebug = false;

    [Header("Events")]
    [field: SerializeField]
    public UnityEvent OnTakeoff { get; private set; } = new UnityEvent();
    [field: SerializeField]
    public UnityEvent OnRoll { get; private set; } = new UnityEvent();
    [field: SerializeField]
    public UnityEvent OnStunned { get; private set; } = new UnityEvent();
    [field: SerializeField]
    public UnityEvent OnBoosted { get; private set; } = new UnityEvent();

    CharacterController _controller;
    IPigeonControls _controls;
    PigeonBoostFeedback _boostFeedback;
    PigeonEquipment _equipment;
    PigeonRagdollHandler _ragdoll;
    Water _water;

    PredictedEvent _takeoffEvent;
    PredictedEvent _stunnedEvent;
    PredictedEvent<bool> _rollEvent;
    PredictedEvent<BoostRing> _boostEvent;
    PredictedEvent<Vector3> _teleportEvent;

    PigeonState _viewState;
    bool _viewStunned;
    bool _cursorInitialized;

    static readonly List<PigeonController> _all = new List<PigeonController>();
    public static IReadOnlyList<PigeonController> All => _all;
    public static PigeonController Local { get; private set; }

    /// <summary>Le pigeon du joueur local a changé : apparition, reconnexion, ou null quand il disparaît.</summary>
    public static event Action<PigeonController> LocalChanged;

    static void SetLocal(PigeonController pigeon)
    {
        if (Local == pigeon)
            return;
        Local = pigeon;
        LocalChanged?.Invoke(pigeon);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _all.Clear();
        Local = null;
        LocalChanged = null;
#if UNITY_EDITOR
        _debugCommands.Clear();
#endif
    }

#if UNITY_EDITOR
    static readonly Queue<Action<PredictionManager>> _debugCommands = new Queue<Action<PredictionManager>>();

    /// <summary>Tests : exécute la commande au début de la prochaine simulation d'un pigeon, sur l'hôte uniquement.</summary>
    public static void RunInSimulation(Action<PredictionManager> command) => _debugCommands.Enqueue(command);
#endif

    public float Speed => _viewState.LastVelocity.magnitude;
    public float HorizontalSpeed => new Vector3(_viewState.LastVelocity.x, 0f, _viewState.LastVelocity.z).magnitude;
    /// <summary>Vitesse propre mesurée (relative au support s'il y en a un), lue depuis la vue.</summary>
    public Vector3 Velocity => _viewState.LastVelocity;
    /// <summary>Même valeur que Velocity (nom utilisé par les scripts de main).</summary>
    public Vector3 LastVelocity => _viewState.LastVelocity;
    public bool IsUndeWater => UnderWater(transform.position);
    public bool IsStun => _viewState.Stunned;
    /// <summary>Au sol, ou posé sur un support mobile.</summary>
    public bool IsGrounded => _viewState.Grounded;
    public bool HasSupport => _viewState.SupportId != 0;
    /// <summary>MovingSupport.Id du support sous le pigeon (0 = aucun), lu depuis la vue.</summary>
    public int SupportId => _viewState.SupportId;
    public bool IsMovementFrozen => _viewState.Frozen;
    /// <summary>Input simulé de ce pigeon (le sien ou celui reçu du réseau) : à utiliser pour l'animation des autres joueurs.</summary>
    public bool IsFlapping => currentInput.Flap;
    public bool IsDiving => currentInput.Dive;
    /// <summary>Frein tenu : vol stationnaire en l'air.</summary>
    public bool IsHovering => currentInput.Brake;
    /// <summary>Équipements portés, lus depuis la vue (bit = PigeonEquipment.Equipment).</summary>
    public byte WornEquipment => _viewState.Equipment;

    /// <summary>État simulé de ce pigeon (lecture/écriture), réservé à la simulation d'une autre identité.</summary>
    public ref PigeonState SimState => ref currentState;

    [SimulationOnly]
    public void ForceFreeze(bool frozen)
    {
        ref PigeonState s = ref currentState;
        if (s.Frozen == frozen)
            return;
        s.Frozen = frozen;
        if (frozen)
        {
            s.LastVelocity = Vector3.zero;
            s.PendingImpulse = Vector3.zero;
            ClearSupport(ref s);
        }
    }

    [SimulationOnly]
    public void ForceTeleport(Vector3 position, Quaternion rotation)
    {
        TeleportTo(ref currentState, position, rotation);
    }

    protected override void LateAwake()
    {
        _water = FindAnyObjectByType<Water>();
        _controller = GetComponent<CharacterController>();
        _controls = GetComponent<IPigeonControls>();
        _boostFeedback = GetComponent<PigeonBoostFeedback>();
        _equipment = GetComponent<PigeonEquipment>();
        _ragdoll = GetComponent<PigeonRagdollHandler>();

        _takeoffEvent = new PredictedEvent(predictionManager, this);
        _stunnedEvent = new PredictedEvent(predictionManager, this);
        _rollEvent = new PredictedEvent<bool>(predictionManager, this);
        _boostEvent = new PredictedEvent<BoostRing>(predictionManager, this);
        _teleportEvent = new PredictedEvent<Vector3>(predictionManager, this);

        _takeoffEvent.AddListener(() => OnTakeoff.Invoke());
        _stunnedEvent.AddListener(() => OnStunned.Invoke());
        _rollEvent.AddListener(PlayRoll);
        _boostEvent.AddListener(PlayBoost);
        _teleportEvent.AddListener(OnTeleported);

        if (!_all.Contains(this))
            _all.Add(this);
    }

    protected override void Destroyed()
    {
        _all.Remove(this);
        if (_cursorInitialized)
            SetCursorCaptured(false);
        if (Local == this)
            SetLocal(null);
    }

    protected override void Update()
    {
        base.Update();

        if (!isOwner)
            return;

        SetLocal(this);
        if (!_cursorInitialized)
        {
            _cursorInitialized = true;
            SetCursorCaptured(true);
        }
        UpdateCursorCapture();
    }

    protected override PigeonState GetInitialState()
    {
        return new PigeonState
        {
            Position = transform.position,
            Yaw = transform.eulerAngles.y,
            StunRotation = Quaternion.identity,
            Equipment = TryGetComponent(out PigeonEquipment equipment) ? equipment.CurrentWornMask() : (byte)0,
            SpawnPosition = transform.position,
            SpawnRotation = transform.rotation
        };
    }

    protected override void GetUnityState(ref PigeonState s)
    {
        s.Position = transform.position;
    }

    protected override void SetUnityState(PigeonState s)
    {
        WritePosition(s.Position);
    }

    protected override PigeonState Interpolate(PigeonState from, PigeonState to, float t)
    {
        PigeonState result = to;
        result.Position = Vector3.Lerp(from.Position, to.Position, t);
        result.Yaw = Mathf.LerpAngle(from.Yaw, to.Yaw, t);
        result.Pitch = Mathf.LerpAngle(from.Pitch, to.Pitch, t);
        result.LastVelocity = Vector3.Lerp(from.LastVelocity, to.LastVelocity, t);
        result.StunRotation = Quaternion.Slerp(from.StunRotation, to.StunRotation, t);
        if (from.SupportId == to.SupportId)
            result.SupportLocalAnchor = Vector3.Lerp(from.SupportLocalAnchor, to.SupportLocalAnchor, t);
        else if (to.SupportId != 0 && MovingSupport.TryGet(to.SupportId, out MovingSupport toSupport))
            result.SupportLocalAnchor = Vector3.Lerp(
                toSupport.ViewTransform.InverseTransformPoint(from.Position), to.SupportLocalAnchor, t);
        else if (from.SupportId != 0 && MovingSupport.TryGet(from.SupportId, out MovingSupport fromSupport))
            result.Position = Vector3.Lerp(
                fromSupport.ViewTransform.TransformPoint(from.SupportLocalAnchor), to.Position, t);
        return result;
    }

    protected override void Simulate(PigeonInput input, ref PigeonState s, float dt)
    {
#if UNITY_EDITOR
        while (_debugCommands.Count > 0)
            _debugCommands.Dequeue()(predictionManager);
#endif
        WritePosition(s.Position);

        if (input.Respawn)
            TeleportTo(ref s, s.SpawnPosition, s.SpawnRotation);

        ApplyMissionInput(input, ref s.Mission);
        s.Equipment = (byte)((s.Equipment | input.EquipmentWear) ^ input.EquipmentToggle);

        if (s.Frozen)
        {
            s.LastVelocity = Vector3.zero;
            s.PendingImpulse = Vector3.zero;
            ClearSupport(ref s);
            return;
        }

        s.StunImmuneRemaining -= dt;

        if (s.Stunned)
        {
            s.StunRemaining -= dt;
            if (s.StunRemaining > 0f || (!input.Flap && input.RelativeMovement.magnitude < 0.1f))
            {
                StunStep(ref s, dt);
                return;
            }

            bool wasGrounded = s.Grounded;
            WakeUp(ref s);
            if (!wasGrounded || input.Flap)
            {
                Vector3 launch = ForwardOf(s.Pitch, s.Yaw) * _flapMinSpeed;
                launch.y = _takeoffSpeed;
                s.LastVelocity = launch;
            }
        }

        if (input.BarrelLeft)
            TryBarrelRoll(input, s, true);
        if (input.BarrelRight)
            TryBarrelRoll(input, s, false);

        Vector3 velocity;
        if (s.Grounded && (s.LastVelocity.y <= 0.001f || s.SupportId != 0))
            velocity = GroundMovement(input, ref s);
        else
            velocity = CalculateFlightPhysics(input, ref s, dt);

        velocity = PigeonFan.ApplyUpdrafts(s.Position, velocity, dt, s.Grounded);
        if (UnderWater(s.Position))
        {
            float deep = _water.transform.position.y - _waterLevelModifier - s.Position.y;
            velocity += Vector3.up * _buoyancyForce * deep * deep * dt;
            velocity += UnderwaterThrust(s, velocity) * dt;
        }

        if (_water != null && velocity.y < 0f && dt > 0f)
        {
            float minimumY = _water.transform.position.y - _waterLevelModifier - _maxUnderwaterDepth;
            float allowedDrop = Mathf.Max(0f, s.Position.y - minimumY);
            velocity.y = Mathf.Max(velocity.y, -allowedDrop / dt);
        }

        _stunPossible = !UnderWater(s.Position)
            && s.StunImmuneRemaining < 0f
            && new Vector2(s.LastVelocity.x, s.LastVelocity.z).magnitude > _stunMinHorizontalSpeed;

        Vector3 velocityBeforeStun = s.LastVelocity;
        Vector3 positionBeforeMove = s.Position;
        MoveController(ref s, velocity, dt);

        if (_hasStunHit)
            Stun(ref s, _stunHitNormal, velocityBeforeStun);

        DetectBoostRings(ref s, positionBeforeMove);
    }

    void TeleportTo(ref PigeonState s, Vector3 position, Quaternion rotation)
    {
        Vector3 previous = s.Position;
        s.Position = position;
        WritePosition(position);

        Vector3 forward = rotation * Vector3.forward;
        s.Yaw = new Vector2(forward.x, forward.z).sqrMagnitude > 0.000001f
            ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg
            : rotation.eulerAngles.y;
        s.Pitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        s.PreserveTeleportPitch = true;
        s.Grounded = false;
        s.Stunned = false;
        s.StunRemaining = 0f;
        s.LastVelocity = Vector3.zero;
        s.PendingImpulse = Vector3.zero;
        s.StunRotation = Quaternion.identity;
        ClearSupport(ref s);

        transform.rotation = rotation;
        _teleportEvent.Invoke(position - previous);
    }

    static Vector3 ForwardOf(float pitch, float yaw) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

    bool UnderWater(Vector3 position) => _water != null && position.y < _water.transform.position.y - _waterLevelModifier;

    void WritePosition(Vector3 position)
    {
        if ((transform.position - position).sqrMagnitude < 1e-10f)
            return;
        transform.position = position;
        Physics.SyncTransforms();
    }
}