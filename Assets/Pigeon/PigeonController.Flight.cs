using UnityEngine;

public partial class PigeonController
{
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

    void TryBarrelRoll(in PigeonInput input, in PigeonState s, bool isLeft)
    {
        if (input.Flap || input.Dive || s.LastVelocity.magnitude < _minBarrelRollSpeed)
            return;
        _rollEvent.Invoke(isLeft);
    }
}
