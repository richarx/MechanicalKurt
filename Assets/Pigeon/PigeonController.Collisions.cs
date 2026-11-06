using PurrNet.Prediction;
using UnityEngine;

public partial class PigeonController
{
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

    void MoveController(ref PigeonState s, Vector3 velocity, float dt, bool allowSupport = true, bool snapToGround = false)
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
        if (snapToGround && !_controller.isGrounded && TryFindGroundBelow(out float drop))
            _controller.Move(Vector3.down * drop);
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

    /// <summary>
    /// Sol praticable à moins d'une marche sous le pigeon. Le Character Controller monte les marches (stepOffset) mais
    /// ne redescend pas : sans recalage, marcher dans une descente le ferait décoller.
    /// </summary>
    bool TryFindGroundBelow(out float distance)
    {
        distance = 0f;
        float radius = _controller.radius * 0.95f;
        Vector3 origin = transform.position + _controller.center;
        // Le pigeon lui-même et les autres ne comptent pas comme un sol.
        int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
        if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, _controller.stepOffset, mask, QueryTriggerInteraction.Ignore))
            return false;
        if (hit.normal.y < Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad))
            return false;
        distance = hit.distance;
        return true;
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
}
