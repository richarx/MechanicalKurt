
using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;

public class BallRolling : MonoBehaviour
{
    private MovingSupport _movingSupport;
    private Rigidbody _rgbd;
    private SphereCollider _collider;
    private Collider[] _pigeonBodyColliders;
    private readonly List<Collider> _ignoredPigeonColliders = new List<Collider>();
    private bool _bodyCollisionsIgnored;

    [SerializeField] PigeonController _pigeonController;
    [SerializeField] PigeonOnMoving _pigeonOnMoving;

    [SerializeField] float _rotationMax = 1f;
    [SerializeField] float _powerTorque = 1f;
    [SerializeField] float _acceleration = 100f;

    [ReadOnly] public Vector3 rotationAxis = new Vector3();
    [ReadOnly] public Vector3 angularVelocity = new Vector3();

    private bool _isOnBall = false;

    void Awake()
    {
        _movingSupport = GetComponent<MovingSupport>();
        _rgbd = GetComponent<Rigidbody>();
        _collider = GetComponent<SphereCollider>();
        if (_movingSupport == null || _rgbd == null || _collider == null ||
            _pigeonController == null || _pigeonOnMoving == null)
        {
            Debug.LogError("BallRolling nécessite un MovingSupport, un Rigidbody, un SphereCollider et les références du pigeon.", this);
            enabled = false;
            return;
        }

        _pigeonBodyColliders = _pigeonController.GetComponents<Collider>();
        _pigeonOnMoving.PigeonChangedSupport += TryGetOnBall;
    }

    void OnDestroy()
    {
        SetPigeonBodyCollisionsIgnored(false);
        if (_pigeonOnMoving != null)
            _pigeonOnMoving.PigeonChangedSupport -= TryGetOnBall;
    }

    void OnDisable()
    {
        SetPigeonBodyCollisionsIgnored(false);
    }

    public void TryGetOnBall(MovingSupport movingSupport)
    {
        _isOnBall = (movingSupport == _movingSupport);
        SetPigeonBodyCollisionsIgnored(_isOnBall);
    }

    void FixedUpdate()
    {

        if (!_isOnBall)
            return;

        float maxAngularSpeed = Mathf.Max(0f, _rotationMax);
        _rgbd.maxAngularVelocity = maxAngularSpeed;

        angularVelocity = _rgbd.angularVelocity;
        Vector3 rollingVelocity = Vector3.ProjectOnPlane(angularVelocity, Vector3.up);
        _rgbd.angularVelocity = rollingVelocity;

        float radius = _collider.radius;
        if (radius <= 0.0001f)
            return;

        Vector3 walkVelocity = Vector3.ProjectOnPlane(_pigeonController.LastVelocity, Vector3.up);

        rotationAxis = Vector3.Cross(walkVelocity, Vector3.up);

        Vector3 targetAngularVelocity = Vector3.ClampMagnitude(rotationAxis / radius, maxAngularSpeed);

        Vector3 angularAcceleration = (targetAngularVelocity - rollingVelocity) / Time.fixedDeltaTime;
        angularAcceleration = Vector3.ClampMagnitude(angularAcceleration, _powerTorque);

        _rgbd.AddTorque(angularAcceleration, ForceMode.Acceleration);
    }

    private void SetPigeonBodyCollisionsIgnored(bool ignore)
    {
        if (_bodyCollisionsIgnored == ignore)
            return;

        _bodyCollisionsIgnored = ignore;
        if (ignore)
        {
            foreach (Collider bodyCollider in _pigeonBodyColliders)
            {
                if (bodyCollider == null || bodyCollider is CharacterController || bodyCollider.isTrigger ||
                    Physics.GetIgnoreCollision(_collider, bodyCollider))
                    continue;

                Physics.IgnoreCollision(_collider, bodyCollider, true);
                _ignoredPigeonColliders.Add(bodyCollider);
            }
        }
        else
        {
            foreach (Collider bodyCollider in _ignoredPigeonColliders)
            {
                if (_collider != null && bodyCollider != null)
                    Physics.IgnoreCollision(_collider, bodyCollider, false);
            }

            _ignoredPigeonColliders.Clear();
        }
    }

}
