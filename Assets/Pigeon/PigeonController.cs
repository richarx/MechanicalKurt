using System;
using System.Collections.Generic;
using PurrNet.Prediction;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// ─────────────────────────────────────────────────────────────────────────────
//  INPUT : tout ce que le joueur "décide" à un tick. Rejoué à l'identique lors des rollbacks.
//  Les champs "one-shot" (Barrel*, Interact) sont accumulés dans UpdateInput.
// ─────────────────────────────────────────────────────────────────────────────
public struct PigeonInput : IPredictedData
{
    public Vector2 Movement;          // déjà tourné par la caméra (PlayerControls)
    public Vector2 RelativeMovement;  // brut, pour le vol
    public bool Flap;
    public bool Dive;
    public bool Brake;
    public bool BarrelLeft;           // one-shot
    public bool BarrelRight;          // one-shot
    public bool Interact;             // one-shot : bascule WantsStart
    public bool Respawn;              // one-shot : retour au point d'apparition (fail-safe)
    public byte EquipmentWear;        // one-shot : équipements à porter (bit = PigeonEquipment.Equipment)
    public byte EquipmentToggle;      // one-shot : équipements à basculer

    public void Dispose() { }
}

// ─────────────────────────────────────────────────────────────────────────────
//  STATE : uniquement ce qui influence les ticks futurs.
//  Le visuel (roll, ragdoll, particules) se déduit de ce state dans UpdateView.
// ─────────────────────────────────────────────────────────────────────────────
public struct PigeonState : IPredictedData<PigeonState>
{
    public Vector3 Position;
    public float Yaw;
    public float Pitch;
    public Vector3 LastVelocity;       // vitesse réellement mesurée après le dernier Move
    public Vector3 PendingImpulse;     // appliqué au prochain pas de vol
    public bool Grounded;
    public bool PreserveTeleportPitch;

    public bool Stunned;
    public float StunRemaining;
    public float StunImmuneRemaining;
    public Vector3 LastHitNormal;
    public Vector3 PreStunVelocity;
    public Quaternion StunRotation;    // rotation locale du visuel pendant le stun (roulade)

    public bool InsideBoostRing;

    // ── Posés/lus par des orchestrateurs (zones de départ, courses) pendant leur Simulate ──
    public bool Frozen;                // mouvement gelé (compte à rebours, cinématique de course)
    public bool FrozenByZone;          // gel posé par FreezingZone (compte à rebours) : c'est elle qui le lève
    public PredictedComponentID FreezingZone;
    public bool InteractPressed;       // Interact appuyé à ce tick (lu par les zones pendant leur Simulate)
    public bool HasReadyZone;          // le joueur a validé « je suis prêt » pour ReadyZone
    public PredictedComponentID ReadyZone;     // zone de départ validée (une seule à la fois)
    public PredictedComponentID LastStartZone; // dernière MissionStartZone qui a lancé ce pigeon
    public int LastStartCount;                 // numéro de ce départ dans la zone (distingue deux départs)

    // Équipements portés (bit = PigeonEquipment.Equipment) : dans l'état, donc vus par tous les clients,
    // y compris ceux qui arrivent en cours de partie.
    public byte Equipment;

    // Point de réapparition du fail-safe.
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

    // L'ancienne version (calculée à chaque frame) faisait gagner de la vitesse en piquant sous l'eau : la poussée
    // de l'eau, réorientée par le steering, devenait de la vitesse vers l'avant. Cet effet de bord dépendait des FPS
    // et a disparu avec la simulation à 20 ticks/s : il est recréé ici, explicitement et identique pour tous.
    // Valeurs par défaut calées sur l'ancienne version à 144 FPS (20 ticks/s).
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

    // ── Références (hors state) ──────────────────────────────────────────────
    CharacterController _controller;
    IPigeonControls _controls;
    PigeonBoostFeedback _boostFeedback;
    PigeonEquipment _equipment;
    PigeonRagdollHandler _ragdoll;
    Water _water;

    // ── Événements de simulation : filtrés par PurrDiction (pas de double-fire au replay) ──
    PredictedEvent _takeoffEvent;
    PredictedEvent _stunnedEvent;
    PredictedEvent<bool> _rollEvent;
    PredictedEvent<BoostRing> _boostEvent;
    PredictedEvent<Vector3> _teleportEvent;

    // ── Données écrites par OnControllerColliderHit pendant Move (réinitialisées chaque tick) ──
    bool _simulating;
    bool _stunPossible;
    Vector3 _velocityBeforeMove;
    bool _hasStunHit;
    float _bestStunImpact;
    Vector3 _stunHitNormal;
    bool _hasStaticHit;
    Vector3 _staticHitNormal;
    // Assez grand pour contenir aussi les colliders du pigeon (ragdoll) et du décor autour d'un anneau.
    readonly Collider[] _overlapBuffer = new Collider[32];

    // ── Requêtes locales (owner) transformées en input au prochain tick ──────
    bool _respawnRequested;
    byte _equipmentWearRequested;
    byte _equipmentToggleRequested;

    // ── Vue ──────────────────────────────────────────────────────────────────
    PigeonState _viewState;
    bool _viewStunned;
    bool _cursorInitialized;

    // ── Registre des pigeons (pour les orchestrateurs) ───────────────────────
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
    // Commandes des scénarios de test (ScenarioRunner), exécutées au début de la prochaine simulation d'un pigeon.
    // Elles ne passent pas par l'input : valables uniquement en hôte solo, où l'hôte ne fait pas de rollback.
    static readonly Queue<Action<PredictionManager>> _debugCommands = new Queue<Action<PredictionManager>>();

    public static void RunInSimulation(Action<PredictionManager> command) => _debugCommands.Enqueue(command);
#endif

    // Accesseurs lus par les autres scripts : ils lisent la VUE, jamais l'état en cours de simulation.
    public float Speed => _viewState.LastVelocity.magnitude;
    public float HorizontalSpeed => new Vector3(_viewState.LastVelocity.x, 0f, _viewState.LastVelocity.z).magnitude;
    public Vector3 Velocity => _viewState.LastVelocity;
    public bool IsUndeWater => UnderWater(transform.position);
    public bool IsStun => _viewState.Stunned;
    public bool IsGrounded => _viewState.Grounded;
    public bool IsMovementFrozen => _viewState.Frozen;
    /// <summary>Équipements portés, lus depuis la vue (bit = PigeonEquipment.Equipment).</summary>
    public byte WornEquipment => _viewState.Equipment;
    /// <summary>Le joueur a validé une zone de départ (lu depuis la vue).</summary>
    public bool WantsStart => _viewState.HasReadyZone;

    /// <summary>Vrai si ce pigeon a participé au départ numéro <paramref name="count"/> de cette zone.</summary>
    // L'événement de départ peut arriver avant que la vue ne contienne la marque : on regarde aussi l'état simulé.
    public bool WasStartedBy(PredictedComponentID zone, int count)
        => (_viewState.LastStartCount == count && _viewState.LastStartZone.Equals(zone))
        || (currentState.LastStartCount == count && currentState.LastStartZone.Equals(zone));

    // ─────────────────────────────────────────────────────────────────────────
    //  API locale (owner) : appelable depuis n'importe où, par exemple PositionFailSafe.
    //  Ne passe QUE par l'input, donc rejouable ; ne transporte aucune position (pas de téléportation arbitraire).
    // ─────────────────────────────────────────────────────────────────────────

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

    /// <summary>Source d'input actuelle (joueur ou bot). Les scripts qui lisent des appuis passent par ici plutôt que par PlayerControls.</summary>
    public IPigeonControls Controls => _controls;

    /// <summary>Remplace la source d'input (bots, scénarios de test). null rétablit le composant IPigeonControls.</summary>
    public void SetControlsOverride(IPigeonControls controls)
    {
        _controls = controls ?? GetComponent<IPigeonControls>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  API pour orchestrateurs (zones de départ, courses).
    //  À n'appeler que depuis le Simulate d'une autre identité prédite : ça modifie l'état simulé.
    // ─────────────────────────────────────────────────────────────────────────

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
        }
    }

    [SimulationOnly]
    public void ForceTeleport(Vector3 position, Quaternion rotation)
    {
        TeleportTo(ref currentState, position, rotation);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cycle de vie
    // ─────────────────────────────────────────────────────────────────────────
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

    // override (pas un nouveau Update) : celui de PredictedIdentity appelle UpdateInput, qui accumule les
    // one-shots (Interact, tonneaux). Le masquer les faisait disparaître.
    protected override void Update()
    {
        base.Update();

        // Les pigeons contrôlés par le serveur (bots) sont aussi « controller » sur l'hôte :
        // seul celui du joueur local devient Local et capture le curseur.
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

    // ─────────────────────────────────────────────────────────────────────────
    //  State
    // ─────────────────────────────────────────────────────────────────────────
    protected override PigeonState GetInitialState()
    {
        return new PigeonState
        {
            Position = transform.position,
            Yaw = transform.eulerAngles.y,
            StunRotation = Quaternion.identity,
            // Ce que le prefab porte au départ (GetInitialState peut précéder LateAwake : pas de champ en cache).
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

    // La vue interpole la position/les angles ; le reste est pris tel quel sur l'état cible.
    protected override PigeonState Interpolate(PigeonState from, PigeonState to, float t)
    {
        PigeonState result = to;
        result.Position = Vector3.Lerp(from.Position, to.Position, t);
        result.Yaw = Mathf.LerpAngle(from.Yaw, to.Yaw, t);
        result.Pitch = Mathf.LerpAngle(from.Pitch, to.Pitch, t);
        result.LastVelocity = Vector3.Lerp(from.LastVelocity, to.LastVelocity, t);
        result.StunRotation = Quaternion.Slerp(from.StunRotation, to.StunRotation, t);
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Input
    // ─────────────────────────────────────────────────────────────────────────

    // Appelé à chaque frame Unity : on accumule les one-shots pour ne pas perdre un appui entre deux ticks.
    protected override void UpdateInput(ref PigeonInput input)
    {
        if (_controls == null)
            return;

        input.BarrelLeft |= _controls.ConsumeLeftFlex;
        input.BarrelRight |= _controls.ConsumeRightFlex;
        input.Interact |= _controls.ConsumePredictedInteract;
    }

    // Valeurs continues, échantillonnées au tick.
    protected override void GetFinalInput(ref PigeonInput input)
    {
        input.Respawn = _respawnRequested;
        _respawnRequested = false;
        input.EquipmentWear = _equipmentWearRequested;
        input.EquipmentToggle = _equipmentToggleRequested;
        _equipmentWearRequested = _equipmentToggleRequested = 0;

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

    // Quand le serveur répète le dernier input connu, on ne répète pas les actions ponctuelles.
    protected override void ModifyExtrapolatedInput(ref PigeonInput input)
    {
        input.BarrelLeft = false;
        input.BarrelRight = false;
        input.Interact = false;
        input.Respawn = false;
        input.EquipmentWear = 0;
        input.EquipmentToggle = 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Simulation (déterministe : n'utilise que input, state, dt)
    // ─────────────────────────────────────────────────────────────────────────
    protected override void Simulate(PigeonInput input, ref PigeonState s, float dt)
    {
#if UNITY_EDITOR
        while (_debugCommands.Count > 0)
            _debugCommands.Dequeue()(predictionManager);
#endif
        WritePosition(s.Position);

        if (input.Respawn)
            TeleportTo(ref s, s.SpawnPosition, s.SpawnRotation);

        // Avant le gel : un joueur gelé pendant un compte à rebours peut encore annuler.
        // La zone concernée interprète l'appui (valider / annuler) dans son propre Simulate.
        s.InteractPressed = input.Interact;

        s.Equipment = (byte)((s.Equipment | input.EquipmentWear) ^ input.EquipmentToggle);

        // Gelé : aucune physique. Pas de Move(0) non plus, qui faisait perdre isGrounded : au dégel le pigeon
        // retombait d'un cheveu et dépassait le seuil d'arrêt des zones pendant un tick.
        if (s.Frozen)
        {
            s.LastVelocity = Vector3.zero;
            s.PendingImpulse = Vector3.zero;
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
        if (s.Grounded && s.LastVelocity.y <= 0.001f)
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

        _stunPossible = !UnderWater(s.Position)
            && s.StunImmuneRemaining < 0f
            && new Vector2(s.LastVelocity.x, s.LastVelocity.z).magnitude > _stunMinHorizontalSpeed;

        Vector3 velocityBeforeStun = s.LastVelocity;
        MoveController(ref s, velocity, dt);

        if (_hasStunHit)
            Stun(ref s, _stunHitNormal, velocityBeforeStun);

        DetectBoostRings(ref s);
    }

    void MoveController(ref PigeonState s, Vector3 velocity, float dt)
    {
        _velocityBeforeMove = s.LastVelocity;
        _hasStunHit = false;
        _bestStunImpact = 0f;
        _hasStaticHit = false;

        Vector3 before = transform.position;
        _simulating = true;
        _controller.Move(velocity * dt);
        _simulating = false;
        Vector3 after = transform.position;

        s.Position = after;
        s.LastVelocity = dt > 0f ? (after - before) / dt : Vector3.zero;
        s.Grounded = _controller.isGrounded;
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

        // Speedway : en piqué sous l'eau, la traînée de vol est réduite (voir UnderwaterThrust).
        float speedway = UnderwaterDiveFactor(s);
        if (speedway > 0f)
            dragAtMaxSpeed *= Mathf.Lerp(1f, _underwaterDragFactor, speedway);

        ComputeDrag(ref velocity, targetSpeed, dragAtMaxSpeed, dt);
        return velocity;
    }

    // 0 hors de l'eau, effet désactivé ou nez relevé ; 1 au piqué maximal du plané (ou plus, en Dive).
    float UnderwaterDiveFactor(in PigeonState s)
    {
        if (_underwaterThrust <= 0f || !UnderWater(s.Position))
            return 0f;
        float down = -ForwardOf(s.Pitch, s.Yaw).y;
        return Mathf.Clamp01(down / Mathf.Sin(_maxPitch * Mathf.Deg2Rad));
    }

    // Poussée horizontale vers l'avant, qui s'éteint en approchant de _underwaterThrustFadeSpeed.
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

    // ── Stun : chute / glissade / roulade simulées à la main (plus de Rigidbody) ──
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

        MoveController(ref s, velocity, dt);

        // Rebond sur ce qu'on a touché pendant le Move.
        if (_hasStaticHit && -Vector3.Dot(velocity, _staticHitNormal) > _stunBounceMinSpeed)
            s.LastVelocity = Vector3.Reflect(velocity, _staticHitNormal) * _stunBounce;

        // Roulade : vitesse angulaire déduite de la vitesse horizontale (rouler sans glisser).
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

    // Utilisé par la réapparition (input.Respawn) et par les orchestrateurs (ForceTeleport).
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
        // L'état « au sol » de l'ancien emplacement ne vaut plus : le prochain Move le recalcule.
        s.Grounded = false;
        // Une téléportation (respawn, ligne de départ) repart d'un pigeon sain : pas d'étourdissement résiduel.
        s.Stunned = false;
        s.StunRemaining = 0f;
        s.LastVelocity = Vector3.zero;
        s.PendingImpulse = Vector3.zero;
        s.StunRotation = Quaternion.identity;

        transform.rotation = rotation;
        _teleportEvent.Invoke(position - previous);
    }

    // Anneaux de boost : détectés par le pigeon pendant la simulation (le trigger Unity serait rejoué à chaque replay).
    void DetectBoostRings(ref PigeonState s)
    {
        BoostRing ring = null;
        int layers = _boostLayer.value != 0 ? _boostLayer.value : Physics.AllLayers;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position + _controller.center, _controller.radius,
            _overlapBuffer, layers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count && ring == null; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col.isTrigger)
                ring = col.GetComponentInParent<BoostRing>();
        }

        bool inside = ring != null;
        if (inside && !s.InsideBoostRing)
        {
            float speed = s.LastVelocity.magnitude;
            Vector3 direction = speed > 0.1f ? s.LastVelocity / speed : ForwardOf(s.Pitch, s.Yaw);
            float targetSpeed = Mathf.Min(Mathf.Max(speed + ring.BoostAmount, ring.MinExitSpeed), ring.MaxSpeed);
            if (targetSpeed > speed)
                s.PendingImpulse += direction * (targetSpeed - speed);
            _boostEvent.Invoke(ring);
        }
        s.InsideBoostRing = inside;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Collisions : appelé par Unity PENDANT CharacterController.Move (donc pendant Simulate).
    //  On ne fait que collecter ; la décision de stun est prise dans Simulate, juste après le Move.
    // ─────────────────────────────────────────────────────────────────────────
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!_simulating)
            return;

        // Autre pigeon : impact calculé sur la vitesse relative, les deux sont étourdis.
        if (hit.collider.TryGetComponent(out PigeonController other) && other != this)
        {
            HandlePigeonHit(hit, other);
            return;
        }

        Rigidbody body = hit.collider.attachedRigidbody;
        if (body != null && !body.isKinematic)
        {
            PushBody(hit, body);
            return;
        }

        _hasStaticHit = true;
        _staticHitNormal = hit.normal;

        if (!_stunPossible)
            return;

        float impact = ImpactOf(_velocityBeforeMove, hit.normal);
        if (impact > _stunMinImpact && impact > _bestStunImpact)
        {
            _hasStunHit = true;
            _bestStunImpact = impact;
            _stunHitNormal = hit.normal;
        }
    }

    void HandlePigeonHit(ControllerColliderHit hit, PigeonController other)
    {
        // Attention : lit/écrit l'état d'une autre identité. L'ordre de simulation entre pigeons n'est pas garanti.
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

        // Un rigidbody non prédit serait poussé à chaque replay : on ne pousse que les PredictedRigidbody.
        if (body.TryGetComponent(out PredictedRigidbody predictedBody))
            predictedBody.AddForce(pushDir * finalPushPower, ForceMode.Force);
    }

    float ImpactOf(Vector3 velocity, Vector3 normal)
    {
        float speed = velocity.magnitude;
        if (speed < 0.0001f)
            return 0f;
        float alignment = Mathf.Max(0f, Vector3.Dot(velocity / speed, -normal));
        return speed * alignment * _weight;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Vue (visuel uniquement : aucune décision de gameplay ici)
    // ─────────────────────────────────────────────────────────────────────────
    protected override void UpdateView(PigeonState view, PigeonState? verified)
    {
        _viewState = view;

        transform.SetPositionAndRotation(
            view.Position,
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

        bool groundMode = view.Grounded && view.LastVelocity.y <= 0.001f;
        Quaternion target = groundMode
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, -currentInput.RelativeMovement.x * _visualRollAmount);
        float speed = groundMode ? 10f : _rollSpeed;
        _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, target, speed * dt);
    }

    // Listeners des PredictedEvent (présentation uniquement).
    void PlayRoll(bool isLeft)
    {
        if (_boostFeedback != null)
            _boostFeedback.Play(restart: true, _rollDuration, isLeft, false);
        OnRoll.Invoke();
    }

    void PlayBoost(BoostRing ring)
    {
        if (_boostFeedback != null)
            _boostFeedback.Play();
        ring.PlayRingFeedback();
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
        if (_controls is PlayerControls playerControls)
            playerControls.SnapCameraBehind();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Utilitaires
    // ─────────────────────────────────────────────────────────────────────────
    static Vector3 ForwardOf(float pitch, float yaw) => Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

    bool UnderWater(Vector3 position) => _water != null && position.y < _water.transform.position.y - _waterLevelModifier;

    // CharacterController garde sa propre copie de la position : toute écriture directe doit être synchronisée.
    void WritePosition(Vector3 position)
    {
        if ((transform.position - position).sqrMagnitude < 1e-10f)
            return;
        transform.position = position;
        Physics.SyncTransforms();
    }

    // ── Curseur (owner uniquement) ───────────────────────────────────────────
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