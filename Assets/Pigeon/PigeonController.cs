using System.Xml;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[RequireComponent(typeof(CharacterController))]
public class PigeonController : MonoBehaviour
{
    [Header("Interactions / Physics")]
    [SerializeField] float _pushPower = 5f;
    [SerializeField] float _weight = 1.5f;
    [SerializeField] float _maxImpact = 10f;
    [SerializeField] float _stunMinDuration = 0.5f;
    [SerializeField] float _stunImmuneDuration = 0.5f;

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

    [Header("Debug")]
    [SerializeField] bool _activeDebug = false;

    [HideInInspector] public UnityEvent OnTakeoff = new UnityEvent();
    [HideInInspector] public UnityEvent OnRoll = new UnityEvent();

    Vector3 _velocity;
    Vector3 _lastVelocity;
    Vector3 _pendingImpulse;
    float _currentPitch;
    float _yaw;
    Water _water;
    bool _lastFramePaused;
    bool _skipVelocityRead;
    bool _preserveTeleportPitch;
    bool _stunned;
    float _stunRemaining;
    float _stunImmuneRemaining;

    Rigidbody _rigidbody;
    CharacterController _controller;
    IPigeonControls _controls;
    PigeonBoostFeedback _boostFeedback;
    PigeonRagdollHandler _pigeonRagdollHandler;
    Vector3 _lastHitNormal;

    public float Speed => _lastVelocity.magnitude;
    public Vector3 Velocity => _lastVelocity;
    public bool IsUndeWater => _water != null && transform.position.y < _water.transform.position.y - _waterLevelModifier;

    public bool IsMovementFrozen { get; private set; }

    public bool IsGrounded => _controller.isGrounded;

    // Applied once on the next flight physics step (cleared while grounded).
    public void AddImpulse(Vector3 impulse) => _pendingImpulse += impulse;

    public void SetMovementFrozen(bool frozen)
    {
        IsMovementFrozen = frozen;
        _velocity = Vector3.zero;
        _lastVelocity = Vector3.zero;
        _pendingImpulse = Vector3.zero;
        _skipVelocityRead = true;
        if (frozen && _controller != null && _controller.enabled && _controller.gameObject.activeInHierarchy)
            _controller.Move(Vector3.zero);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
        bool wasEnabled = _controller.enabled;
        Vector3 previousPosition = transform.position;
        _controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        _controller.enabled = wasEnabled;

        _velocity = Vector3.zero;
        _lastVelocity = Vector3.zero;
        _pendingImpulse = Vector3.zero;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        _yaw = horizontalForward.sqrMagnitude > 0.000001f
            ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : rotation.eulerAngles.y;
        _currentPitch = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        _preserveTeleportPitch = true;
        if (_visualRoot != null)
            _visualRoot.localRotation = Quaternion.identity;
        // Ignorer l'ancienne vitesse du CharacterController jusqu'au prochain Move.
        _skipVelocityRead = true;

        CinemachineCore.OnTargetObjectWarped(transform, position - previousPosition);
        if (_controls is PlayerControls playerControls)
            playerControls.SnapCameraBehind();
    }

    void Awake()
    {
        _water = FindAnyObjectByType<Water>();
        _rigidbody = GetComponent<Rigidbody>();
        _controller = GetComponent<CharacterController>();
        _controls = GetComponent<IPigeonControls>();
        _boostFeedback = GetComponent<PigeonBoostFeedback>();
        _pigeonRagdollHandler = GetComponent<PigeonRagdollHandler>();
        _yaw = transform.eulerAngles.y;
    }

    void OnEnable()
    {
        SetCursorCaptured(true);
    }

    void OnDisable()
    {
        SetCursorCaptured(false);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
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
        if (_activeDebug)
        {
            GUI.Label(new Rect(10, 50, 300, 20), $"Pitch: {_currentPitch:F1}");
            GUI.Label(new Rect(10, 80, 300, 20), $"Speed: {_lastVelocity.magnitude:F1}");
            GUI.Label(new Rect(10, 110, 300, 20), $"HorizontalSpeed: {Vector3.ProjectOnPlane(_lastVelocity, Vector3.up).magnitude:F1}");
            GUI.Label(new Rect(10, 140, 300, 20), $"VerticalSpeed: {_lastVelocity.y:F1}");
        }
    }

    public Vector3 CalculateImpactForce(ControllerColliderHit hit)
    {
        Vector3 impactDir = -hit.normal;
        float speedMagnitude = _lastVelocity.magnitude;
        float velocityAlignment = Mathf.Max(0f, Vector3.Dot(_lastVelocity.normalized, impactDir));
        float impactIntensity = speedMagnitude * velocityAlignment * _weight;
        return impactDir * impactIntensity;
    }

    void Stun(Vector3 hitnormal)
    {
        Debug.Log("STUN");
        _lastHitNormal = hitnormal;
        _stunned = true;
        _controller.enabled = false;
        _rigidbody.isKinematic = false;
        _pigeonRagdollHandler.SetRagdollState(true);
        _rigidbody.linearVelocity = _lastVelocity;
        _stunRemaining = _stunMinDuration;
        _rigidbody.AddForce(_lastHitNormal * 10f);
    }

    void WakeUp()
    {
        _stunned = false;
        _rigidbody.isKinematic = true;
        _pigeonRagdollHandler.AlignAndGetUp();
        _stunImmuneRemaining = _stunImmuneDuration;
        _currentPitch = 0f;

        Vector3 horizontalVelocityBeforeStun = new Vector3(_lastVelocity.x, 0f, _lastVelocity.z);

        Vector3 recoveryDir = (horizontalVelocityBeforeStun.normalized * 0.7f) + (_lastHitNormal * 0.3f);

        if (recoveryDir.sqrMagnitude < 0.01f)
        {
            recoveryDir = _lastHitNormal;
        }

        transform.forward = recoveryDir.normalized;
        _yaw = transform.eulerAngles.y;

        float retainedSpeed = _flapMinSpeed;

        _lastVelocity = transform.forward * retainedSpeed;
        _lastVelocity.y = _takeoffSpeed;

        _controller.enabled = true;
    }



    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic)
        {
            Debug.Log($"Impact: {CalculateImpactForce(hit).magnitude:F1}");
            if (!IsUndeWater && _stunImmuneRemaining < 0 && CalculateImpactForce(hit).magnitude > _maxImpact)
            {
                Stun(hit.normal);
            }
            return;
        }

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z).normalized;

        Vector3 pigeonHorizontalVelocity = new Vector3(_lastVelocity.x, 0f, _lastVelocity.z);

        float forwardVelocityComponent = Mathf.Max(0f, Vector3.Dot(pigeonHorizontalVelocity, pushDir));

        float finalPushPower = _pushPower + (forwardVelocityComponent * _weight);

        body.AddForce(pushDir * finalPushPower, ForceMode.Force);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        UpdateCursorCapture();
        if (IsMovementFrozen)
            return;
        if (Time.timeScale == 0f)
        {
            _lastFramePaused = true;
            return;
        }
        _stunImmuneRemaining -= dt;
        if (_stunned)
        {
            _stunRemaining -= dt;
            if (_stunRemaining > 0 || !_controls.Flap)
                return;
            WakeUp();
        }
        else if (!_lastFramePaused && !_skipVelocityRead)
        {
            _lastVelocity = _controller.velocity;
        }
        _lastFramePaused = false;
        _skipVelocityRead = false;


        // Consume every frame so a press made while too slow doesn't fire later.
        if (_controls.ConsumeLeftFlex)
            BarrelRoll(true);
        if (_controls.ConsumeRightFlex)
            BarrelRoll(false);


        _velocity = Vector3.zero;

        if (_controller.isGrounded && _lastVelocity.y <= 0.001f)
        {
            GroundMovement();
            ResetVisualRotation(dt);
        }
        else
        {
            CalculateFlightPhysics(dt);
            UpdateVisualRotation(dt);
        }

        _velocity = PigeonFan.ApplyUpdrafts(transform.position, _velocity, dt, _controller.isGrounded);
        if (IsUndeWater)
        {
            float deep = _water.transform.position.y - _waterLevelModifier - transform.position.y;
            _velocity += Vector3.up * _buoyancyForce * deep * deep * dt;
        }
        _controller.Move(_velocity * dt);
    }

    void GroundMovement()
    {
        _preserveTeleportPitch = false;
        _currentPitch = 0f;
        _pendingImpulse = Vector3.zero;
        Vector2 input = Vector2.ClampMagnitude(_controls.Movement, 1f);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        float directionMagnitude = direction.magnitude;

        if (directionMagnitude > 0.01f)
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_controls.Flap)
        {
            _currentPitch = _maxPitch;
            _velocity = transform.forward * _flapMinSpeed;
            _velocity.y = _takeoffSpeed;
            OnTakeoff?.Invoke();
        }
        else
        {
            _velocity = transform.forward * directionMagnitude * _groundSpeed;
            _velocity.y = -1f;
        }
    }

    void ComputePitch(float pitchSpeed, float maxPitch, float dt)
    {
        // Un départ incliné vers l'eau reste orienté jusqu'à une commande de tangage.
        if (_preserveTeleportPitch && Mathf.Abs(_controls.RelativeMovement.y) < 0.001f)
            return;
        _preserveTeleportPitch = false;
        float targetPitch = Mathf.Clamp(
                _currentPitch - _controls.RelativeMovement.y * pitchSpeed * dt,
                -maxPitch,
                maxPitch
            );
        float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, _currentPitch));
        _currentPitch = Mathf.MoveTowardsAngle(_currentPitch, targetPitch, Mathf.Max(pitchSpeed * dt, deltaPitch * 0.1f * dt));
    }

    void ComputeRotation(float dt)
    {
        _yaw += _controls.RelativeMovement.x * _turnSpeed * dt;
        transform.rotation = Quaternion.Euler(-_currentPitch, _yaw, 0f);
    }

    void ComputeDrag(float targetSpeed, float dragAtMaxSpeed, float dt)
    {
        float speed = _velocity.magnitude;

        if (speed > 0.001f)
        {
            float dragAcceleration =
                dragAtMaxSpeed * speed * speed
                / (targetSpeed * targetSpeed);

            _velocity -= _velocity.normalized * dragAcceleration * dt;
        }
    }

    void ApplyFlightForces(float dt)
    {
        float speed = _velocity.magnitude;
        Vector3 forward = transform.forward;

        float forwardSpeed = Vector3.Dot(_velocity, forward);
        float speedRatio = Mathf.Clamp01(forwardSpeed / _maxSpeed);
        float effectiveLift = speedRatio * _liftFactor;

        if (forward.y > 0f)
        {
            float pitchUpIntensity = forward.y;
            float dynamicStallSpeed = _stallSpeed * (1f + pitchUpIntensity * 2f);

            float stallProgress = Mathf.Clamp01(forwardSpeed / dynamicStallSpeed);
            effectiveLift *= stallProgress;
        }

        Vector3 liftForce = Vector3.up * (-Physics.gravity.y) * effectiveLift;
        _velocity += liftForce * dt;

        if (forward.y < 0f)
        {
            float diveAcceleration = -forward.y * Physics.gravity.magnitude * 1.5f;
            _velocity += forward * diveAcceleration * dt;
        }
    }

    void CalculateFlightPhysics(float dt)
    {
        float targetSpeed;
        float dragAtMaxSpeed;

        _velocity = _lastVelocity + Physics.gravity * dt + _pendingImpulse;
        _pendingImpulse = Vector3.zero;

        float hoverFactor = 0f;
        if (_controls.Flap && _controls.Brake)
        {
            float pitchRange = _maxPitch - _minHoverPitch;
            if (pitchRange > 0.01f)
            {
                hoverFactor = Mathf.Clamp01((_currentPitch - _minHoverPitch) / pitchRange);
            }
            else if (_currentPitch >= _minHoverPitch)
            {
                hoverFactor = 1f;
            }
        }

        if (_controls.Flap)
        {
            if (_stunned) // TODO
            {
                WakeUp();
            }

            targetSpeed = _flapMaxSpeed;
            dragAtMaxSpeed = _flapDragAtMaxSpeed;
            ComputePitch(_pitchSpeed, _maxPitch, dt);
            ComputeRotation(dt);

            _velocity += Vector3.up * _flapAccel * dt;
        }
        else if (_controls.Dive)
        {
            _preserveTeleportPitch = false;
            targetSpeed = _diveMaxSpeed;
            dragAtMaxSpeed = _diveDragAtMaxSpeed;
            float targetPitch = -_diveMaxPitch;
            float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, _currentPitch));
            _currentPitch = Mathf.MoveTowardsAngle(_currentPitch, targetPitch, Mathf.Max(_divePitchSpeed * dt, deltaPitch * 0.1f * dt));
            ComputeRotation(dt);
        }
        else
        {
            targetSpeed = _maxSpeed;
            dragAtMaxSpeed = _dragAtMaxSpeed;
            ComputePitch(_pitchSpeed, _maxPitch, dt);
            ComputeRotation(dt);
            ApplyFlightForces(dt);
        }

        if (!_controls.Dive && _controls.Brake)
        {
            targetSpeed = _brakeMaxSpeed;
            dragAtMaxSpeed = _brakeDragAtMaxSpeed;
        }

        if (hoverFactor > 0f)
        {
            _velocity -= Physics.gravity * dt;

            _velocity.y = Mathf.MoveTowards(_velocity.y, 0f, 15f * dt);

            _velocity = Vector3.Lerp(_velocity, Vector3.zero, hoverFactor * 10f * dt);
        }

        float speed = _velocity.magnitude;
        float forwardSpeed = Vector3.Dot(_velocity, transform.forward);

        if (!_controls.Dive && speed > 0.1f)
        {
            float steeringFactor = 4f;

            if (!_controls.Flap && transform.forward.y > 0.1f && forwardSpeed < _stallSpeed * 1.3f)
            {
                steeringFactor = 0f;
            }

            _velocity = Vector3.Slerp(_velocity.normalized, transform.forward, steeringFactor * dt) * speed;
        }

        ComputeDrag(targetSpeed, dragAtMaxSpeed, dt);
    }

    void ResetVisualRotation(float dt)
    {
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, 0.0f);

        _visualRoot.localRotation = Quaternion.Slerp(
            _visualRoot.localRotation,
            targetRotation,
            10f * dt
        );
    }

    void UpdateVisualRotation(float dt)
    {
        float targetRootRoll = -_controls.RelativeMovement.x * _visualRollAmount;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetRootRoll);

        _visualRoot.localRotation = Quaternion.Slerp(
            _visualRoot.localRotation,
            targetRotation,
            _rollSpeed * dt
        );
    }

    // Purely visual; ignored while too slow or while a roll is already playing.
    public void BarrelRoll(bool isLeft)
    {
        if (_boostFeedback == null || _controls.Flap || _controls.Dive || Speed < _minBarrelRollSpeed)
            return;

        _boostFeedback.Play(restart: true, _rollDuration, isLeft);
        OnRoll?.Invoke();
    }
}
