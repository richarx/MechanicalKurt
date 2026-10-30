using System;
using System.Collections.Generic;
using PurrNet.Prediction;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
    public PredictedComponentID StartAckZone;
    public int StartAckCount;

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
    /// <summary>Gel posé par FreezingZone pendant son décompte : c'est elle qui le lève.</summary>
    public bool FrozenByZone;
    public PredictedComponentID FreezingZone;
    public bool InteractPressed;
    public bool HasReadyZone;
    public PredictedComponentID ReadyZone;
    /// <summary>Dernier départ de MissionStartZone auquel ce pigeon a participé (zone, numéro).</summary>
    public PredictedComponentID LastStartZone;
    public int LastStartCount;
    public PredictedComponentID StartAckZone;
    public int StartAckCount;

    /// <summary>Équipements portés, un bit par PigeonEquipment.Equipment.</summary>
    public byte Equipment;

    public Vector3 SpawnPosition;
    public Quaternion SpawnRotation;

    public void Dispose() { }
}

[RequireComponent(typeof(CharacterController))]
public class PigeonController : PredictedIdentity<PigeonInput, PigeonState>
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
    [SerializeField] float _minHoverPitch = 30f;

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

    bool _simulating;
    bool _stunPossible;
    Vector3 _velocityBeforeMove;
    bool _hasStunHit;
    float _bestStunImpact;
    Vector3 _stunHitNormal;
    bool _hasStaticHit;
    Vector3 _staticHitNormal;
    bool _carrying;
    bool _collectSupport;
    int _foundSupportId;
    float _foundSupportNormalY;
    readonly Collider[] _overlapBuffer = new Collider[32];
    readonly RaycastHit[] _castBuffer = new RaycastHit[32];

    bool _respawnRequested;
    byte _equipmentWearRequested;
    byte _equipmentToggleRequested;
    PredictedComponentID _startAckZone;
    int _startAckCount;

    PigeonState _viewState;
    bool _viewStunned;
    bool _cursorInitialized;

    static readonly List<PigeonController> _all = new List<PigeonController>();
    public static IReadOnlyList<PigeonController> All => _all;
    public static PigeonController Local { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _all.Clear();
        Local = null;
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
    /// <summary>Équipements portés, lus depuis la vue (bit = PigeonEquipment.Equipment).</summary>
    public byte WornEquipment => _viewState.Equipment;
    /// <summary>Le joueur a validé une zone de départ (lu depuis la vue).</summary>
    public bool WantsStart => _viewState.HasReadyZone;

    /// <summary>Vrai si ce pigeon a participé au départ numéro <paramref name="count"/> de cette zone.</summary>
    public bool WasStartedBy(PredictedComponentID zone, int count)
        => (_viewState.LastStartCount == count && _viewState.LastStartZone.Equals(zone))
        || (currentState.LastStartCount == count && currentState.LastStartZone.Equals(zone));

    /// <summary>Numéro du dernier départ de cette zone auquel ce pigeon a participé (0 = aucun). État simulé d'abord : la vue peut retarder d'un tick.</summary>
    public int LastStartCountFor(PredictedComponentID zone)
        => currentState.LastStartZone.Equals(zone) ? currentState.LastStartCount
        : _viewState.LastStartZone.Equals(zone) ? _viewState.LastStartCount : 0;

    /// <summary>Le joueur a validé cette zone de départ (lu depuis la vue).</summary>
    public bool IsReadyFor(PredictedComponentID zone) => _viewState.HasReadyZone && _viewState.ReadyZone.Equals(zone);

    /// <summary>Ce pigeon a fini la cinématique du départ numéro <paramref name="count"/> de cette zone (vue, ou demande en cours).</summary>
    public bool HasFinishedStartCinematic(PredictedComponentID zone, int count)
        => (_viewState.StartAckCount == count && _viewState.StartAckZone.Equals(zone))
        || (_startAckCount == count && _startAckZone.Equals(zone));

    /// <summary>Demande le retour au point d'apparition. Ignorée si ce pigeon n'est pas celui du joueur local.</summary>
    public void RequestRespawn()
    {
        if (!isController)
            return;
        _respawnRequested = true;
    }

    /// <summary>
    /// Demande à porter (ou basculer) un équipement. Passe par l'input : la simulation l'applique, puis l'état le
    /// montre à tous les clients. Ignorée si ce pigeon n'est pas contrôlé ici.
    /// </summary>
    public void RequestEquipment(PigeonEquipment.Equipment equipment, bool toggle = false)
    {
        if (!isController)
            return;
        byte bit = PigeonEquipment.Bit(equipment);
        if (toggle)
            _equipmentToggleRequested ^= bit;
        else
            _equipmentWearRequested |= bit;
    }

    /// <summary>
    /// Signale la fin d'une cinématique partagée : départ (MissionStartZone.ReportCinematicFinished, zone = la zone)
    /// ou arrivée d'une course (zone = la course). Passe par l'input : l'orchestrateur avance quand tous les
    /// participants l'ont signalée. Un seul accusé à la fois (le dernier remplace le précédent). Ignorée si ce
    /// pigeon n'est pas contrôlé ici.
    /// </summary>
    public void AcknowledgeStartCinematic(PredictedComponentID zone, int count)
    {
        if (!isController)
            return;
        _startAckZone = zone;
        _startAckCount = count;
    }

    /// <summary>Source d'input actuelle (joueur ou bot). Les scripts qui lisent des appuis passent par ici plutôt que par PlayerControls.</summary>
    public IPigeonControls Controls => _controls;

    /// <summary>Remplace la source d'input (bots, scénarios de test). null rétablit le composant IPigeonControls.</summary>
    public void SetControlsOverride(IPigeonControls controls)
    {
        _controls = controls ?? GetComponent<IPigeonControls>();
    }

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
            Local = null;
    }

    protected override void Update()
    {
        base.Update();

        if (!isOwner)
            return;

        Local = this;
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

    protected override void UpdateInput(ref PigeonInput input)
    {
        if (_controls == null)
            return;

        input.BarrelLeft |= _controls.ConsumeLeftFlex;
        input.BarrelRight |= _controls.ConsumeRightFlex;
        input.Interact |= _controls.ConsumePredictedInteract;
    }

    protected override void GetFinalInput(ref PigeonInput input)
    {
        input.Respawn = _respawnRequested;
        _respawnRequested = false;
        input.EquipmentWear = _equipmentWearRequested;
        input.EquipmentToggle = _equipmentToggleRequested;
        _equipmentWearRequested = _equipmentToggleRequested = 0;
        input.StartAckZone = _startAckZone;
        input.StartAckCount = _startAckCount;

        if (_controls == null)
        {
            input.Movement = Vector2.zero;
            input.RelativeMovement = Vector2.zero;
            input.Flap = input.Dive = input.Brake = false;
            return;
        }

        input.Movement = _controls.Movement;
        input.RelativeMovement = _controls.RelativeMovement;
        input.Flap = _controls.Flap;
        input.Dive = _controls.Dive;
        input.Brake = _controls.Brake;
    }

    protected override void SanitizeInput(ref PigeonInput input)
    {
        input.Movement = Vector2.ClampMagnitude(input.Movement, 1f);
        input.RelativeMovement = Vector2.ClampMagnitude(input.RelativeMovement, 1f);
        input.EquipmentWear &= PigeonEquipment.AllBits;
        input.EquipmentToggle &= PigeonEquipment.AllBits;
    }

    protected override void ModifyExtrapolatedInput(ref PigeonInput input)
    {
        input.BarrelLeft = false;
        input.BarrelRight = false;
        input.Interact = false;
        input.Respawn = false;
        input.EquipmentWear = 0;
        input.EquipmentToggle = 0;
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

        s.InteractPressed = input.Interact;
        if (input.StartAckCount != 0)
        {
            s.StartAckZone = input.StartAckZone;
            s.StartAckCount = input.StartAckCount;
        }

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

    void MoveController(ref PigeonState s, Vector3 velocity, float dt, bool allowSupport = true)
    {
        _velocityBeforeMove = s.LastVelocity;
        _hasStunHit = false;
        _bestStunImpact = 0f;
        _hasStaticHit = false;

        int previousSupport = s.SupportId;
        Vector3 departureVelocity = s.SupportVelocity;
        _simulating = true;

        if (previousSupport != 0 && allowSupport && dt > 0f && MovingSupport.TryGet(previousSupport, out MovingSupport support))
        {
            Physics.SyncTransforms();
            Vector3 carry = support.transform.TransformPoint(s.SupportLocalAnchor) - s.Position;
            bool replaying = predictionManager != null && predictionManager.isReplaying;
            departureVelocity = replaying ? s.SupportVelocity : carry / dt;
            if (velocity.y <= 0f && carry.sqrMagnitude > 0f)
            {
                if (carry.sqrMagnitude > MaxSupportCarry * MaxSupportCarry)
                    WritePosition(s.Position + carry);
                else
                {
                    _carrying = true;
                    _controller.Move(carry);
                    _carrying = false;
                }
            }
        }

        _collectSupport = allowSupport && velocity.y <= 0f;
        _foundSupportId = 0;
        _foundSupportNormalY = 0f;

        Vector3 before = transform.position;
        _controller.Move(velocity * dt);
        _simulating = false;
        _collectSupport = false;
        Vector3 after = transform.position;

        s.Position = after;
        s.LastVelocity = dt > 0f ? (after - before) / dt : Vector3.zero;

        if (_foundSupportId != 0 && MovingSupport.TryGet(_foundSupportId, out MovingSupport found))
        {
            s.SupportLocalAnchor = found.transform.InverseTransformPoint(after);
            s.SupportVelocity = _foundSupportId == previousSupport ? departureVelocity : Vector3.zero;
            s.SupportId = _foundSupportId;
        }
        else
        {
            if (previousSupport != 0)
                s.PendingImpulse += departureVelocity;
            s.SupportId = 0;
            s.SupportVelocity = Vector3.zero;
        }

        s.Grounded = _controller.isGrounded || s.SupportId != 0;
    }

    const float MaxSupportCarry = 1.5f;

    static void ClearSupport(ref PigeonState s)
    {
        s.SupportId = 0;
        s.SupportVelocity = Vector3.zero;
    }

    bool TryRegisterSupport(ControllerColliderHit hit)
    {
        float minimumNormalY = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
        if (hit.normal.y <= 0.01f || hit.normal.y < minimumNormalY
            || hit.point.y > transform.position.y + _controller.center.y)
            return false;

        MovingSupport support = hit.collider.GetComponentInParent<MovingSupport>();
        if (support == null || !support.isActiveAndEnabled || support.Id == 0)
            return false;

        if (_collectSupport && hit.normal.y >= _foundSupportNormalY)
        {
            _foundSupportId = support.Id;
            _foundSupportNormalY = hit.normal.y;
        }
        return true;
    }

    Vector3 GroundMovement(in PigeonInput input, ref PigeonState s)
    {
        s.PreserveTeleportPitch = false;
        s.Pitch = 0f;
        s.PendingImpulse = Vector3.zero;

        Vector2 move = Vector2.ClampMagnitude(input.Movement, 1f);
        Vector3 direction = new Vector3(move.x, 0f, move.y);
        float magnitude = direction.magnitude;

        if (magnitude > 0.01f)
            s.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        Vector3 forward = ForwardOf(0f, s.Yaw);
        Vector3 velocity;
        if (input.Flap)
        {
            s.Pitch = _maxPitch;
            velocity = forward * _flapMinSpeed;
            velocity.y = _takeoffSpeed;
            _takeoffEvent.Invoke();
        }
        else
        {
            velocity = forward * magnitude * _groundSpeed;
            velocity.y = -1f;
        }
        return velocity;
    }

    void ComputePitch(in PigeonInput input, ref PigeonState s, float pitchSpeed, float maxPitch, float dt)
    {
        // Un départ incliné vers l'eau reste orienté jusqu'à une commande de tangage.
        if (s.PreserveTeleportPitch && Mathf.Abs(input.RelativeMovement.y) < 0.001f)
            return;
        s.PreserveTeleportPitch = false;
        float targetPitch = Mathf.Clamp(
            s.Pitch - input.RelativeMovement.y * pitchSpeed * dt,
            -maxPitch,
            maxPitch);
        float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, s.Pitch));
        s.Pitch = Mathf.MoveTowardsAngle(s.Pitch, targetPitch, Mathf.Max(pitchSpeed * dt, deltaPitch * 0.1f * dt));
    }

    void ComputeYaw(in PigeonInput input, ref PigeonState s, float dt)
    {
        s.Yaw += input.RelativeMovement.x * _turnSpeed * dt;
    }

    static void ComputeDrag(ref Vector3 velocity, float targetSpeed, float dragAtMaxSpeed, float dt)
    {
        float speed = velocity.magnitude;
        if (speed > 0.001f)
        {
            float dragAcceleration = dragAtMaxSpeed * speed * speed / (targetSpeed * targetSpeed);
            velocity -= velocity.normalized * dragAcceleration * dt;
        }
    }

    void ApplyFlightForces(ref PigeonState s, ref Vector3 velocity, float dt)
    {
        Vector3 forward = ForwardOf(s.Pitch, s.Yaw);

        float forwardSpeed = Vector3.Dot(velocity, forward);
        float speedRatio = Mathf.Clamp01(forwardSpeed / _maxSpeed);
        float effectiveLift = speedRatio * _liftFactor;

        if (forward.y > 0f)
        {
            float pitchUpIntensity = forward.y;
            float dynamicStallSpeed = _stallSpeed * (1f + pitchUpIntensity * 2f);
            float stallProgress = Mathf.Clamp01(forwardSpeed / dynamicStallSpeed);
            effectiveLift *= stallProgress;
        }

        velocity += Vector3.up * (-Physics.gravity.y) * effectiveLift * dt;

        if (forward.y < 0f)
        {
            float diveAcceleration = -forward.y * Physics.gravity.magnitude * 1.5f;
            velocity += forward * diveAcceleration * dt;
        }
    }

    Vector3 CalculateFlightPhysics(in PigeonInput input, ref PigeonState s, float dt)
    {
        float targetSpeed;
        float dragAtMaxSpeed;

        Vector3 velocity = s.LastVelocity + Physics.gravity * dt + s.PendingImpulse;
        s.PendingImpulse = Vector3.zero;

        float hoverFactor = 0f;
        if (input.Flap && input.Brake)
        {
            float pitchRange = _maxPitch - _minHoverPitch;
            if (pitchRange > 0.01f)
                hoverFactor = Mathf.Clamp01((s.Pitch - _minHoverPitch) / pitchRange);
            else if (s.Pitch >= _minHoverPitch)
                hoverFactor = 1f;
        }

        if (input.Flap)
        {
            targetSpeed = _flapMaxSpeed;
            dragAtMaxSpeed = _flapDragAtMaxSpeed;
            ComputePitch(input, ref s, _pitchSpeed, _maxPitch, dt);
            ComputeYaw(input, ref s, dt);
            velocity += Vector3.up * _flapAccel * dt;
        }
        else if (input.Dive)
        {
            s.PreserveTeleportPitch = false;
            targetSpeed = _diveMaxSpeed;
            dragAtMaxSpeed = _diveDragAtMaxSpeed;
            float targetPitch = -_diveMaxPitch;
            float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, s.Pitch));
            s.Pitch = Mathf.MoveTowardsAngle(s.Pitch, targetPitch, Mathf.Max(_divePitchSpeed * dt, deltaPitch * 0.1f * dt));
            ComputeYaw(input, ref s, dt);
        }
        else
        {
            targetSpeed = _maxSpeed;
            dragAtMaxSpeed = _dragAtMaxSpeed;
            ComputePitch(input, ref s, _pitchSpeed, _maxPitch, dt);
            ComputeYaw(input, ref s, dt);
            ApplyFlightForces(ref s, ref velocity, dt);
        }

        if (!input.Dive && input.Brake)
        {
            targetSpeed = _brakeMaxSpeed;
            dragAtMaxSpeed = _brakeDragAtMaxSpeed;
        }

        if (hoverFactor > 0f)
        {
            velocity -= Physics.gravity * dt;
            velocity.y = Mathf.MoveTowards(velocity.y, 0f, 15f * dt);
            velocity = Vector3.Lerp(velocity, Vector3.zero, hoverFactor * 10f * dt);
        }

        float speed = velocity.magnitude;
        Vector3 forward = ForwardOf(s.Pitch, s.Yaw);
        float forwardSpeed = Vector3.Dot(velocity, forward);

        if (!input.Dive && speed > 0.1f)
        {
            float steeringFactor = 4f;
            if (!input.Flap && forward.y > 0.1f && forwardSpeed < _stallSpeed * 1.3f)
                steeringFactor = 0f;

            velocity = Vector3.Slerp(velocity.normalized, forward, steeringFactor * dt) * speed;
        }

        float speedway = UnderwaterDiveFactor(s);
        if (speedway > 0f)
            dragAtMaxSpeed *= Mathf.Lerp(1f, _underwaterDragFactor, speedway);

        ComputeDrag(ref velocity, targetSpeed, dragAtMaxSpeed, dt);
        return velocity;
    }

    float UnderwaterDiveFactor(in PigeonState s)
    {
        if (_underwaterThrust <= 0f || !UnderWater(s.Position))
            return 0f;
        float down = -ForwardOf(s.Pitch, s.Yaw).y;
        return Mathf.Clamp01(down / Mathf.Sin(_maxPitch * Mathf.Deg2Rad));
    }

    Vector3 UnderwaterThrust(in PigeonState s, Vector3 velocity)
    {
        float factor = UnderwaterDiveFactor(s);
        if (factor <= 0f)
            return Vector3.zero;
        Vector3 forward = ForwardOf(s.Pitch, s.Yaw);
        Vector3 flat = new Vector3(forward.x, 0f, forward.z);
        if (flat.sqrMagnitude < 0.0001f)
            return Vector3.zero;
        flat.Normalize();
        float room = 1f - Mathf.Clamp01(Vector3.Dot(velocity, flat) / _underwaterThrustFadeSpeed);
        return flat * (_underwaterThrust * factor * room);
    }

    void StunStep(ref PigeonState s, float dt)
    {
        _stunPossible = false;

        Vector3 velocity = s.LastVelocity + Physics.gravity * dt;
        if (s.Grounded)
        {
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, _stunFriction * dt);
            velocity = new Vector3(horizontal.x, Mathf.Max(velocity.y, -1f), horizontal.z);
        }

        MoveController(ref s, velocity, dt, allowSupport: false);

        if (_hasStaticHit && -Vector3.Dot(velocity, _staticHitNormal) > _stunBounceMinSpeed)
            s.LastVelocity = Vector3.Reflect(velocity, _staticHitNormal) * _stunBounce;

        Vector3 flat = new Vector3(s.LastVelocity.x, 0f, s.LastVelocity.z);
        if (flat.sqrMagnitude > 0.01f)
        {
            Vector3 worldAxis = Vector3.Cross(Vector3.up, flat).normalized;
            Vector3 localAxis = Quaternion.Inverse(Quaternion.Euler(0f, s.Yaw, 0f)) * worldAxis;
            float degreesPerSecond = flat.magnitude / Mathf.Max(0.01f, _stunRollRadius) * Mathf.Rad2Deg;
            s.StunRotation = Quaternion.AngleAxis(degreesPerSecond * dt, localAxis) * s.StunRotation;
        }
    }

    void Stun(ref PigeonState s, Vector3 hitNormal, Vector3 preImpactVelocity)
    {
        s.Stunned = true;
        s.LastHitNormal = hitNormal;
        s.PreStunVelocity = preImpactVelocity;
        s.LastVelocity = preImpactVelocity + hitNormal * _stunKnockback;
        s.StunRemaining = _stunMinDuration;
        s.StunRotation = Quaternion.identity;
        ClearSupport(ref s);
        _stunnedEvent.Invoke();
    }

    void WakeUp(ref PigeonState s)
    {
        s.Stunned = false;
        s.StunImmuneRemaining = _stunImmuneDuration;
        s.Pitch = 0f;
        s.StunRotation = Quaternion.identity;

        Vector3 horizontalVelocityBeforeStun = new Vector3(s.PreStunVelocity.x, 0f, s.PreStunVelocity.z);
        Vector3 recoveryDir = horizontalVelocityBeforeStun.normalized * 0.7f + s.LastHitNormal * 0.3f;
        if (recoveryDir.sqrMagnitude < 0.01f)
            recoveryDir = s.LastHitNormal;

        if (recoveryDir.x * recoveryDir.x + recoveryDir.z * recoveryDir.z > 0.000001f)
            s.Yaw = Mathf.Atan2(recoveryDir.x, recoveryDir.z) * Mathf.Rad2Deg;

        s.LastVelocity = Vector3.zero;
    }

    void TryBarrelRoll(in PigeonInput input, in PigeonState s, bool isLeft)
    {
        if (input.Flap || input.Dive || s.LastVelocity.magnitude < _minBarrelRollSpeed)
            return;
        _rollEvent.Invoke(isLeft);
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

    void DetectBoostRings(ref PigeonState s, Vector3 start)
    {
        int layers = _boostLayer.value != 0 ? _boostLayer.value : Physics.AllLayers;
        Vector3 center = _controller.center;
        float radius = _controller.radius;

        BoostRing insideRing = null;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position + center, radius, _overlapBuffer, layers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count && insideRing == null; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col.isTrigger)
                insideRing = col.GetComponentInParent<BoostRing>();
        }

        BoostRing ring = insideRing;
        Vector3 path = transform.position - start;
        float distance = path.magnitude;
        if (!s.InsideBoostRing && distance > 0.0001f)
        {
            int hits = Physics.SphereCastNonAlloc(
                start + center, radius, path / distance, _castBuffer, distance, layers, QueryTriggerInteraction.Collide);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _castBuffer[i];
                if (!hit.collider.isTrigger || hit.distance >= closest)
                    continue;
                BoostRing crossed = hit.collider.GetComponentInParent<BoostRing>();
                if (crossed == null)
                    continue;
                ring = crossed;
                closest = hit.distance;
            }
        }

        if (ring != null && !s.InsideBoostRing)
        {
            float speed = s.LastVelocity.magnitude;
            Vector3 direction = speed > 0.1f ? s.LastVelocity / speed : ForwardOf(s.Pitch, s.Yaw);
            float targetSpeed = Mathf.Min(Mathf.Max(speed + ring.BoostAmount, ring.MinExitSpeed), ring.MaxSpeed);
            if (targetSpeed > speed)
                s.PendingImpulse += direction * (targetSpeed - speed);
            _boostEvent.Invoke(ring);
        }
        s.InsideBoostRing = insideRing != null;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!_simulating)
            return;

        if (hit.collider.TryGetComponent(out PigeonController other) && other != this)
        {
            HandlePigeonHit(hit, other);
            return;
        }

        Rigidbody body = hit.collider.attachedRigidbody;
        bool dynamicBody = body != null && !body.isKinematic;

        if (_carrying)
        {
            if (!dynamicBody)
            {
                _hasStaticHit = true;
                _staticHitNormal = hit.normal;
            }
            return;
        }

        if (_stunPossible)
        {
            float impact = ImpactOf(_velocityBeforeMove, hit.normal);
            if (impact > _stunMinImpact && impact > _bestStunImpact)
            {
                _hasStunHit = true;
                _bestStunImpact = impact;
                _stunHitNormal = hit.normal;
            }
        }

        if (TryRegisterSupport(hit))
            return;

        if (dynamicBody)
        {
            PushBody(hit, body);
            return;
        }

        _hasStaticHit = true;
        _staticHitNormal = hit.normal;
    }

    void HandlePigeonHit(ControllerColliderHit hit, PigeonController other)
    {
        ref PigeonState otherState = ref other.currentState;
        Vector3 relativeVelocity = _velocityBeforeMove - otherState.LastVelocity;
        float impact = ImpactOf(relativeVelocity, hit.normal);
        if (impact <= _stunMinImpact)
            return;

        if (_stunPossible && impact > _bestStunImpact)
        {
            _hasStunHit = true;
            _bestStunImpact = impact;
            _stunHitNormal = hit.normal;
        }

        if (!otherState.Stunned && !otherState.Frozen && otherState.StunImmuneRemaining < 0f)
            other.Stun(ref otherState, hit.moveDirection, otherState.LastVelocity);
    }

    void PushBody(ControllerColliderHit hit, Rigidbody body)
    {
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z).normalized;
        Vector3 horizontalVelocity = new Vector3(_velocityBeforeMove.x, 0f, _velocityBeforeMove.z);
        float forwardComponent = Mathf.Max(0f, Vector3.Dot(horizontalVelocity, pushDir));
        float finalPushPower = _pushPower + forwardComponent * _weight;

        if (body.TryGetComponent(out PredictedRigidbody predictedBody))
            predictedBody.AddForce(pushDir * finalPushPower, ForceMode.Force);
        else if (predictionManager != null && !predictionManager.isReplaying)
            body.AddForce(pushDir * finalPushPower, ForceMode.Force);
    }

    float ImpactOf(Vector3 velocity, Vector3 normal)
    {
        float speed = velocity.magnitude;
        if (speed < 0.0001f)
            return 0f;
        float alignment = Mathf.Max(0f, Vector3.Dot(velocity / speed, -normal));
        return speed * alignment * _weight;
    }

    protected override void UpdateView(PigeonState view, PigeonState? verified)
    {
        _viewState = view;

        Vector3 position = view.Position;
        if (view.SupportId != 0 && MovingSupport.TryGet(view.SupportId, out MovingSupport support))
            position = support.ViewTransform.TransformPoint(view.SupportLocalAnchor);

        transform.SetPositionAndRotation(
            position,
            Quaternion.Euler(view.Stunned ? 0f : -view.Pitch, view.Yaw, 0f));

        UpdateRagdollView(view);
        UpdateVisualRotation(view, Time.deltaTime);
        if (_equipment != null)
            _equipment.ApplyWorn(view.Equipment);
    }

    void UpdateRagdollView(in PigeonState view)
    {
        if (view.Stunned == _viewStunned)
            return;
        _viewStunned = view.Stunned;

        if (_ragdoll == null)
            return;
        if (_viewStunned)
            _ragdoll.SetRagdollState(true);
        else
            _ragdoll.EndRagdoll();
    }

    void UpdateVisualRotation(in PigeonState view, float dt)
    {
        if (_visualRoot == null)
            return;

        if (view.Stunned)
        {
            _visualRoot.localRotation = view.StunRotation;
            return;
        }

        bool groundMode = view.Grounded && (view.LastVelocity.y <= 0.001f || view.SupportId != 0);
        Quaternion target = groundMode
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, -currentInput.RelativeMovement.x * _visualRollAmount);
        float speed = groundMode ? 10f : _rollSpeed;
        _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, target, speed * dt);
    }

    void PlayRoll(bool isLeft)
    {
        if (_boostFeedback != null)
            _boostFeedback.Play(restart: true, _rollDuration, isLeft, false);
        OnRoll.Invoke();
    }

    int _ringCombo;
    float _lastRingTime = float.NegativeInfinity;

    /// <summary>Rang du dernier anneau dans l'enchaînement en cours (présentation).</summary>
    public int RingCombo => _ringCombo;

    void PlayBoost(BoostRing ring)
    {
        if (_boostFeedback != null)
            _boostFeedback.Play();
        float now = Time.time;
        _ringCombo = now - _lastRingTime <= BoostRing.ComboWindow ? _ringCombo + 1 : 1;
        _lastRingTime = now;
        ring.PlayRingFeedback(_ringCombo);
        OnBoosted.Invoke();
    }

    void OnTeleported(Vector3 delta)
    {
        ResetInterpolation();
        if (_visualRoot != null)
            _visualRoot.localRotation = Quaternion.identity;

        if (!isOwner)
            return;
        CinemachineCore.OnTargetObjectWarped(transform, delta);
        if (TryGetComponent(out CameraSwapper cameras))
            cameras.SnapNow();
        if (_controls is PlayerControls playerControls)
            playerControls.SnapCameraBehind();
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

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _cursorInitialized)
            SetCursorCaptured(false);
    }

    void UpdateCursorCapture()
    {
        if (_controls is PlayerControls playerControls && playerControls.AreControlsBlocked)
            return;

        if (!Application.isFocused)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorCaptured(false);
        else if (Cursor.lockState != CursorLockMode.Locked && Time.timeScale > 0f
            && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            SetCursorCaptured(true);
    }

    void SetCursorCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    void OnGUI()
    {
        if (_activeDebug && isOwner)
        {
            GUI.Label(new Rect(10, 50, 300, 20), $"Pitch: {_viewState.Pitch:F1}");
            GUI.Label(new Rect(10, 80, 300, 20), $"Speed: {Speed:F1}");
            GUI.Label(new Rect(10, 110, 300, 20), $"HorizontalSpeed: {HorizontalSpeed:F1}");
            GUI.Label(new Rect(10, 140, 300, 20), $"VerticalSpeed: {_viewState.LastVelocity.y:F1}");
        }
    }
}