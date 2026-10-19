using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PigeonController : MonoBehaviour
{
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

    Vector3 _velocity;
    Vector3 _pendingImpulse;
    float _currentPitch;
    float _yaw;
    Water _water;

    CharacterController _controller;
    IPigeonControls _controls;
    PigeonBoostFeedback _boostFeedback;

    public float Speed => _controller != null ? _controller.velocity.magnitude : 0f;
    public Vector3 Velocity => _controller != null ? _controller.velocity : Vector3.zero;

    // Applied once on the next flight physics step (cleared while grounded).
    public void AddImpulse(Vector3 impulse) => _pendingImpulse += impulse;

    void Awake()
    {
        _water = FindAnyObjectByType<Water>();
        _controller = GetComponent<CharacterController>();
        _controls = GetComponent<IPigeonControls>();
        _boostFeedback = GetComponent<PigeonBoostFeedback>();
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
        GUI.Label(new Rect(10, 50, 300, 20), $"Pitch: {_currentPitch:F1}");
        GUI.Label(new Rect(10, 80, 300, 20), $"Speed: {_controller.velocity.magnitude:F1}");
        GUI.Label(new Rect(10, 110, 300, 20), $"HorizontalSpeed: {Vector3.ProjectOnPlane(_controller.velocity, Vector3.up).magnitude:F1}");
        GUI.Label(new Rect(10, 140, 300, 20), $"VerticalSpeed: {_controller.velocity.y:F1}");
    }

    void Update()
    {
        UpdateCursorCapture();

        // Consume every frame so a press made while too slow doesn't fire later.
        if (_controls.ConsumeLeftFlex)
            BarrelRoll();

        float dt = Time.deltaTime;
        _velocity = Vector3.zero;

        if (_controller.isGrounded && _controller.velocity.y <= 0.001f)
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
        if (_water != null && transform.position.y < _water.transform.position.y - _waterLevelModifier)
        {
            float deep = _water.transform.position.y - _waterLevelModifier - transform.position.y;
            _velocity += Vector3.up * _buoyancyForce * deep * deep * dt;
        }
        _controller.Move(_velocity * dt);
    }

    void GroundMovement()
    {
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
        }
        else
        {
            _velocity = transform.forward * directionMagnitude * _groundSpeed;
            _velocity.y = -1f;
        }
    }

    void ComputePitch(float pitchSpeed, float maxPitch, float dt)
    {
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

        _velocity = _controller.velocity + Physics.gravity * dt + _pendingImpulse;
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
            targetSpeed = _flapMaxSpeed;
            dragAtMaxSpeed = _flapDragAtMaxSpeed;
            ComputePitch(_pitchSpeed, _maxPitch, dt);
            ComputeRotation(dt);

            _velocity += Vector3.up * _flapAccel * dt;
        }
        else if (_controls.Dive)
        {
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
    public void BarrelRoll()
    {
        if (_boostFeedback == null || Speed < _minBarrelRollSpeed)
            return;

        _boostFeedback.Play(restart: false, _rollDuration);
    }
}