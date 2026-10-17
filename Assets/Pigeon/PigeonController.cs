using UnityEngine;

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
    [SerializeField] float _maxSpeed = 20f;
    [SerializeField] float _airDrag = 0.03f;
    [SerializeField] float _verticalDrag = 0.02f;

    [Header("Gliding")]
    [SerializeField] float _glideSpeed = 8f;
    [SerializeField] float _maxPitch = 45f;
    [SerializeField] float _convertionRate = 1.5f;
    [SerializeField] float _stallSpeed = 3f;

    [Header("Flapping")]
    [SerializeField] float _flapForce = 6f;
    [SerializeField, Range(0f, 1f)] float _flapForwardPenalty = 0.5f;

    [Header("Diving")]
    [SerializeField] float _diveAccel = 12f;
    [SerializeField] float _divePitchSpeed = 120f;
    [SerializeField] float _diveTargetPitch = 70f;
    [SerializeField] float _maxDiveSpeed = 20f;

    Vector3 _velocity;
    float _currentForwardSpeed;
    float _currentPitch;
    float _yaw;

    CharacterController _controller;
    IPigeonControls _controls;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _controls = GetComponent<IPigeonControls>();
        _yaw = transform.eulerAngles.y;
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 400, 20), $"Velocity: {_velocity}");
        GUI.Label(new Rect(10, 30, 300, 20), $"Speed: {_velocity.magnitude:F2}");
        GUI.Label(new Rect(10, 50, 300, 20), $"Pitch: {_currentPitch:F1}");
        GUI.Label(new Rect(10, 70, 300, 20), $"Forward speed: {_currentForwardSpeed:F2}");
    }

    void Update()
    {
        float dt = Time.deltaTime;

        HandleRotation(dt);

        if (_controller.isGrounded && _velocity.y <= 0f)
            GroundMovement(dt);
        else
            CalculateFlightPhysics(dt);

        _controller.Move(_velocity * dt);

        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -1f;
    }

    void GroundMovement(float dt)
    {
        Vector2 input = Vector2.ClampMagnitude(_controls.Movement, 1f);
        Vector3 direction = new Vector3(input.x, 0f, input.y);

        if (direction.sqrMagnitude > 0.01f)
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        _velocity.x = direction.x * _groundSpeed;
        _velocity.z = direction.z * _groundSpeed;
        _velocity.y = -1f;

        _currentPitch = 0f;
        _currentForwardSpeed = 0f;

        if (_controls.ConsumeFlap)
        {
            Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;

            _velocity.x = forward.x * _glideSpeed;
            _velocity.z = forward.z * _glideSpeed;
            _velocity.y = _flapForce;

            _currentForwardSpeed = _glideSpeed;
        }

        UpdateVisualRotation(dt);
    }

    void CalculateFlightPhysics(float dt)
    {
        bool diving = _controls.Dive;
        bool flapping = _controls.ConsumeFlap;

        if (flapping)
        {
            _velocity.y += _flapForce;

            Vector3 horizontalVelocity = new Vector3(
                _velocity.x,
                0f,
                _velocity.z
            );

            horizontalVelocity *= 1f - _flapForwardPenalty;

            _velocity.x = horizontalVelocity.x;
            _velocity.z = horizontalVelocity.z;
        }
        else
        {
            if (diving)
            {
                _currentPitch = Mathf.MoveTowards(
                    _currentPitch,
                    -_diveTargetPitch,
                    _divePitchSpeed * dt
                );
            }
            else
            {
                _currentPitch = Mathf.Clamp(
                    _currentPitch - _controls.RelativeMovement.y * _pitchSpeed * dt,
                    -_maxPitch,
                    _maxPitch
                );
            }

            Vector3 forward = Quaternion.Euler(
                -_currentPitch,
                _yaw,
                0f
            ) * Vector3.forward;

            Vector3 horizontalForward = Quaternion.Euler(
                0f,
                _yaw,
                0f
            ) * Vector3.forward;

            _velocity += Physics.gravity * dt;

            float forwardSpeed = Vector3.Dot(_velocity, horizontalForward);

            if (diving)
                _velocity += forward * _diveAccel * dt;
            else if (forwardSpeed > 0f && forwardSpeed < _glideSpeed)
                _velocity += horizontalForward * (_glideSpeed - forwardSpeed) * dt;

            float speed = _velocity.magnitude;
            float forwardAirSpeed = Vector3.Dot(_velocity, forward);
            float stallFactor = Mathf.InverseLerp(
                _stallSpeed,
                _stallSpeed * 2f,
                Mathf.Max(0f, forwardAirSpeed)
            );

            if (speed > 0.01f && stallFactor > 0f)
            {
                float pitchFactor = Mathf.Clamp01(
                    Mathf.Abs(_currentPitch) / _maxPitch
                );

                float turnRate = _convertionRate * pitchFactor * stallFactor;

                _velocity = Vector3.RotateTowards(
                    _velocity,
                    forward * speed,
                    turnRate * dt,
                    0f
                );
            }

            _velocity -= _velocity * _airDrag * dt;
            _velocity.y -= _verticalDrag * _velocity.y * dt;

            Vector3 horizontalVelocity = new Vector3(
                _velocity.x,
                0f,
                _velocity.z
            );

            float horizontalSpeed = horizontalVelocity.magnitude;

            if (horizontalSpeed > 0.01f)
            {
                horizontalVelocity = Vector3.RotateTowards(
                    horizontalVelocity,
                    horizontalForward * horizontalSpeed,
                    _turnSpeed * Mathf.Deg2Rad * dt,
                    0f
                );

                _velocity.x = horizontalVelocity.x;
                _velocity.z = horizontalVelocity.z;
            }
        }

        float maxSpeed = diving ? _maxDiveSpeed : _maxSpeed;

        if (_velocity.magnitude > maxSpeed)
            _velocity = _velocity.normalized * maxSpeed;

        _currentForwardSpeed = new Vector3(
            _velocity.x,
            0f,
            _velocity.z
        ).magnitude;

        UpdateVisualRotation(dt);
    }

    void HandleRotation(float dt)
    {
        if (_controller.isGrounded && _velocity.y <= 0f)
            return;

        _yaw += _controls.RelativeMovement.x * _turnSpeed * dt;
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    void UpdateVisualRotation(float dt)
    {
        if (_visualRoot == null)
            return;

        float targetRoll =
            -_controls.RelativeMovement.x * _visualRollAmount;

        Quaternion targetRotation = Quaternion.Euler(
            -_currentPitch,
            0f,
            targetRoll
        );

        _visualRoot.localRotation = Quaternion.Slerp(
            _visualRoot.localRotation,
            targetRotation,
            10f * dt
        );
    }
}