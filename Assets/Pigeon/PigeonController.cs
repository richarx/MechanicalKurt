using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PigeonController : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] float _groundSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] float _turnSpeed = 90f;
    [SerializeField] float _pitchSpeed = 60f;
    [SerializeField] float _visualRollAmount = 20f;
    [SerializeField] Transform _visualRoot;

    [Header("Flight")]
    [SerializeField] float _maxSpeed = 25f;
    [SerializeField] float _dragAtMaxSpeed = 3f;
    [SerializeField] float _liftFactor = 0.85f;

    [Header("Gliding")]
    [SerializeField] float _maxPitch = 45f;

    [Header("Flapping")]
    [SerializeField] float _takeoffSpeed = 6f;
    [SerializeField] float _flapAccel = 12f;
    [SerializeField] float _flapMinSpeed = 1.5f;
    [SerializeField] float _flapSpeedPenalty = 0.5f;
    [SerializeField] float _flapMaxSpeed = 15f;
    [SerializeField] float _flapDragAtMaxSpeed = 3f;

    [Header("Diving")]
    [SerializeField] float _divePitchSpeed = 120f;
    [SerializeField] float _diveMaxPitch = 70f;
    [SerializeField] float _diveMaxSpeed = 40f;
    [SerializeField] float _diveDragAtMaxSpeed = 1.5f;

    Vector3 _velocity;
    float _currentPitch;
    float _yaw;

    CharacterController _controller;
    IPigeonControls _controls;

    public float Speed => _controller != null ? _controller.velocity.magnitude : 0f;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _controls = GetComponent<IPigeonControls>();
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

        float dt = Time.deltaTime;
        _velocity = Vector3.zero;

        if (_controller.isGrounded && _controller.velocity.y <= 0f)
            GroundMovement();
        else
        {
            CalculateFlightPhysics(dt);
            UpdateVisualRotation(dt);
        }

        _controller.Move(_velocity * dt);
    }

    void GroundMovement()
    {
        _currentPitch = 0f;
        Vector2 input = Vector2.ClampMagnitude(_controls.Movement, 1f);
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        float directionMagnitude = direction.magnitude;

        if (directionMagnitude > 0.01f)
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        if (_controls.Flap)
        {
            _velocity = transform.forward * _flapMinSpeed;
            _velocity.y = _takeoffSpeed;
        }
        else
        {
            _velocity = transform.forward * directionMagnitude * _groundSpeed;
            _velocity.y = -1f;
        }
    }

    void ComputeRotation(float pitchSpeed, float maxPitch, float dt)
    {
        float targetPitch = Mathf.Clamp(
                _currentPitch - _controls.RelativeMovement.y * pitchSpeed * dt,
                -maxPitch,
                maxPitch
            );
        float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, _currentPitch));
        _currentPitch = Mathf.MoveTowardsAngle(_currentPitch, targetPitch, Mathf.Max(pitchSpeed * dt, deltaPitch * 0.1f * dt));

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

        float speedRatio = Mathf.Clamp01(speed / _maxSpeed);
        float effectiveLift = speedRatio * _liftFactor;

        Vector3 liftForce = Vector3.up * (-Physics.gravity.y) * effectiveLift;
        _velocity += liftForce * dt;

        if (forward.y < 0f)
        {
            float diveAcceleration = -forward.y * Physics.gravity.magnitude * 1.5f;
            _velocity += forward * diveAcceleration * dt;
        }
        else
        {
            if (speed < _flapMinSpeed * 1.5f && forward.y > 0.2f)
            {
                _velocity += Vector3.down * 12f * dt;

                _velocity *= (1f - 2f * dt);
            }
        }
    }

    void CalculateFlightPhysics(float dt)
    {
        float targetSpeed;
        float dragAtMaxSpeed;

        _velocity = _controller.velocity + Physics.gravity * dt;
        if (_controls.Flap)
        {
            targetSpeed = _flapMaxSpeed;
            dragAtMaxSpeed = _flapDragAtMaxSpeed;
            ComputeRotation(_pitchSpeed, _maxPitch, dt);

            _velocity += Vector3.up * _flapAccel * dt;
        }
        else if (_controls.Dive)
        {
            targetSpeed = _diveMaxSpeed;
            dragAtMaxSpeed = _diveDragAtMaxSpeed;
            ComputeRotation(_divePitchSpeed, _diveMaxPitch, dt);
        }
        else
        {
            targetSpeed = _maxSpeed;
            dragAtMaxSpeed = _dragAtMaxSpeed;
            ComputeRotation(_pitchSpeed, _maxPitch, dt);
            ApplyFlightForces(dt);
        }

        float speed = _velocity.magnitude;
        if (speed > 0.1f)
        {
            float steeringFactor = 4f;

            if (transform.forward.y > 0.15f && speed < _flapMinSpeed * 2f)
            {
                steeringFactor = 0f;
            }

            _velocity = Vector3.Slerp(_velocity.normalized, transform.forward, steeringFactor * dt) * speed;
        }

        ComputeDrag(targetSpeed, dragAtMaxSpeed, dt);
    }

    void UpdateVisualRotation(float dt)
    {
        float targetRoll = -_controls.RelativeMovement.x * _visualRollAmount;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetRoll);

        _visualRoot.localRotation = Quaternion.Slerp(
            _visualRoot.localRotation,
            targetRotation,
            10f * dt
        );
    }
}
