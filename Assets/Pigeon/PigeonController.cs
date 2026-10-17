using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PigeonController : MonoBehaviour
{
    [Header("Ground Movement")]
    [SerializeField] float _groundSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] float _turnSpeed = 90f;
    [SerializeField] float _pitchSpeed = 60f;
    [SerializeField] float _visualRollAmount = 5f;

    [Header("Gliding")]
    [SerializeField] float _glideGlideSlope = 1.5f;
    [SerializeField] float _baseGlideSpeed = 6f;
    [SerializeField] float _decelerationRate = 4f;

    [Header("Flapping")]
    [SerializeField] float _flapForce = 6f;
    [SerializeField] float _flapForwardPenalty = 0.5f;

    [Header("Diving")]
    [SerializeField] float _diveAccel = 12f;
    [SerializeField] float _maxDiveSpeed = 20f;

    Vector3 _velocity;
    float _currentForwardSpeed;
    float _currentPitch;
    CharacterController _controller;
    IPigeonControls _controls;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _controls = GetComponent<IPigeonControls>();
    }

    void Update()
    {
        if (_controller.isGrounded)
        {
            GroundMovement();
        }
        else
        {
            HandleRotation();
            CalculateFlightPhysics();
        }
        _controller.Move(_velocity * Time.deltaTime);
    }

    void GroundMovement()
    {
        Vector3 moveDirection = new Vector3(_controls.Movement.x, 0, _controls.Movement.y);
        _velocity = moveDirection * _groundSpeed;

        if (moveDirection.magnitude > 0.1f)
        {
            transform.forward = moveDirection;
        }

        _currentPitch = 0f;
        _velocity.y = -1f;

        if (_controls.ConsumeFlap)
        {
            _velocity.y = _flapForce;
            _currentForwardSpeed = _baseGlideSpeed;
        }

    }

    private void CalculateFlightPhysics()
    {
        if (_controls.ConsumeFlap)
        {
            _currentPitch = 0f;
            _velocity.y = _flapForce;
            _currentForwardSpeed = Mathf.Max(_baseGlideSpeed * 0.5f, _currentForwardSpeed - _flapForwardPenalty);
        }
        else if (_controls.Dive)
        {
            _currentPitch += _pitchSpeed * 3 * Time.deltaTime;
            _velocity.y += Physics.gravity.y * _diveAccel * 0.3f * Time.deltaTime;
            _currentForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, _maxDiveSpeed, _diveAccel * Time.deltaTime);
        }
        else
        {
            _velocity.y = Mathf.MoveTowards(_velocity.y, -_glideGlideSlope, 5f * Time.deltaTime);

            float lookAngleNormalized = -_currentPitch / 60f;

            if (_currentForwardSpeed > _baseGlideSpeed && lookAngleNormalized > 0.1f)
            {
                float conversionEffect = lookAngleNormalized * (_currentForwardSpeed - _baseGlideSpeed);
                _velocity.y += conversionEffect * 2f * Time.deltaTime;
                _currentForwardSpeed -= _decelerationRate * 2f * Time.deltaTime;
            }
            else
            {
                _currentForwardSpeed = Mathf.MoveTowards(_currentForwardSpeed, _baseGlideSpeed, _decelerationRate * Time.deltaTime);
            }
        }

        Vector3 forwardMovement = transform.forward * _currentForwardSpeed;

        _velocity.x = forwardMovement.x;
        _velocity.z = forwardMovement.z;

        if (!_controls.ConsumeFlap && !_controls.Dive)
        {
            _velocity.y += Physics.gravity.y * 0.2f * Time.deltaTime;
        }
    }

    private void HandleRotation()
    {
        float yaw = _controls.RelativeMovement.x * _turnSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, yaw);

        float pitchInput = -_controls.RelativeMovement.y * _pitchSpeed * Time.deltaTime;
        _currentPitch -= pitchInput;
        _currentPitch = Mathf.Clamp(_currentPitch, -45f, 45f);

        float targetRoll = -_controls.RelativeMovement.x * _visualRollAmount;

        transform.localEulerAngles = new Vector3(_currentPitch, transform.localEulerAngles.y, targetRoll);
    }
}
