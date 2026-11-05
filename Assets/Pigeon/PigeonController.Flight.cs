using UnityEngine;

public partial class PigeonController
{
    Vector3 GroundMovement(in PigeonInput input, ref PigeonState s, MovementState mode, float dt)
    {
        s.PreserveTeleportPitch = false;
        s.Pitch = 0f;
        s.PendingImpulse = Vector3.zero;

        Vector2 move = Vector2.ClampMagnitude(input.Movement, 1f);
        Vector3 direction = new Vector3(move.x, 0f, move.y);
        float magnitude = direction.magnitude;

        if (s.JoystickNeedsReset && input.RelativeMovement.y >= 0f)
            s.JoystickNeedsReset = false;
        bool canWalk = mode != MovementState.Dancing && !s.JoystickNeedsReset;

        if (magnitude > 0.01f && canWalk)
            s.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        Vector3 forward = ForwardOf(0f, s.Yaw);
        Vector3 velocity;
        if (input.Flap)
        {
            velocity = forward * _flapMinSpeed;
            velocity.y = _takeoffSpeed;
            _takeoffEvent.Invoke();
        }
        else
        {
            Vector3 current = new Vector3(s.LastVelocity.x, 0f, s.LastVelocity.z);
            velocity = magnitude > 0.0001f && canWalk
                ? Vector3.MoveTowards(current, forward * magnitude * _groundSpeed, _groundMovementAcceleration * dt)
                : Vector3.MoveTowards(current, Vector3.zero, _groundMovementDeceleration * dt);
            velocity.y = -1f;
        }
        return velocity;
    }

    void ComputePitch(in PigeonInput input, ref PigeonState s, MovementState mode, float pitchSpeed, float maxPitch, float dt)
    {
        bool hasVerticalInput = Mathf.Abs(input.RelativeMovement.y) >= 0.001f;

        // Un départ incliné vers l'eau reste orienté jusqu'à une commande de tangage.
        if (s.PreserveTeleportPitch && !hasVerticalInput)
            return;
        s.PreserveTeleportPitch = false;
        float targetPitch;
        if (mode == MovementState.Braking || (mode == MovementState.Flapping && !hasVerticalInput))
            targetPitch = maxPitch;
        else if (UnderWater(s.Position) && !hasVerticalInput)
            targetPitch = -maxPitch;
        else
            targetPitch = Mathf.Clamp(s.Pitch - input.RelativeMovement.y * pitchSpeed * dt, -maxPitch, maxPitch);
        float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, s.Pitch));
        s.Pitch = Mathf.MoveTowardsAngle(s.Pitch, targetPitch, Mathf.Max(pitchSpeed * dt, deltaPitch * 0.1f * dt));
    }

    void ComputeYaw(in PigeonInput input, ref PigeonState s, float dt, float turnSpeed)
    {
        s.Yaw += input.RelativeMovement.x * turnSpeed * dt;
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

    Vector3 CalculateFlightPhysics(in PigeonInput input, ref PigeonState s, MovementState mode, float dt)
    {
        float targetSpeed;
        float dragAtMaxSpeed;

        Vector3 velocity = s.LastVelocity + Physics.gravity * dt + s.PendingImpulse;
        s.PendingImpulse = Vector3.zero;

        if (mode == MovementState.Braking)
        {
            targetSpeed = _brakeMaxSpeed;
            dragAtMaxSpeed = _brakeDragAtMaxSpeed;

            // À l'arrivée d'une course, freinage net plutôt que la lente glissade du frein.
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, (FinishedRace(s) ? 40f : 5f) * dt);
            // Tirer vers le bas fait descendre ; une fois posé, le joystick devra revenir avant de marcher.
            s.HoverTime += dt;
            if (input.RelativeMovement.y < -0.15f)
            {
                velocity.y = Mathf.MoveTowards(velocity.y, -2f, 15f * dt);
                s.JoystickNeedsReset = true;
            }
            else
            {
                velocity.y = Mathf.MoveTowards(velocity.y, Mathf.Sin(s.HoverTime * _hoverWobbleFrequency) * _hoverWobbleAmplitude, 15f * dt);
                s.JoystickNeedsReset = false;
            }
            if (input.RelativeMovement.y > 0.0001f)
                velocity += ForwardOf(s.Pitch, s.Yaw) * input.RelativeMovement.y * _hoverMoveSpeed * dt;

            ComputePitch(input, ref s, mode, _pitchSpeed, _maxPitch, dt);
            ComputeYaw(input, ref s, dt, _turnSpeed * 2f);
        }
        else if (mode == MovementState.HoverFlapping)
        {
            // Vol stationnaire en battant des ailes : le pigeon monte et consomme de l'endurance.
            targetSpeed = _brakeMaxSpeed;
            dragAtMaxSpeed = _brakeDragAtMaxSpeed;

            velocity = Vector3.MoveTowards(velocity, Vector3.zero, (FinishedRace(s) ? 40f : 5f) * dt);
            velocity.y = Mathf.MoveTowards(velocity.y, 2f, 25f * dt);
            if (input.RelativeMovement.y > 0.0001f)
                velocity += ForwardOf(s.Pitch, s.Yaw) * input.RelativeMovement.y * _hoverMoveSpeed * dt;

            ComputePitch(input, ref s, mode, _pitchSpeed, _maxPitch, dt);
            ComputeYaw(input, ref s, dt, _turnSpeed * 2f);
        }
        else if (mode == MovementState.Flapping)
        {
            targetSpeed = _flapMaxSpeed;
            dragAtMaxSpeed = _flapDragAtMaxSpeed;
            ComputePitch(input, ref s, mode, _pitchSpeed, _maxPitch, dt);
            ComputeYaw(input, ref s, dt, _turnSpeed);
            velocity += Vector3.up * _flapAccel * dt;
        }
        else if (mode == MovementState.Diving)
        {
            s.PreserveTeleportPitch = false;
            targetSpeed = _diveMaxSpeed;
            dragAtMaxSpeed = _diveDragAtMaxSpeed;
            float targetPitch = -_diveMaxPitch;
            float deltaPitch = Mathf.Abs(Mathf.DeltaAngle(targetPitch, s.Pitch));
            s.Pitch = Mathf.MoveTowardsAngle(s.Pitch, targetPitch, Mathf.Max(_divePitchSpeed * dt, deltaPitch * 0.1f * dt));
            ComputeYaw(input, ref s, dt, _turnSpeed);
            Vector3 diveForward = ForwardOf(s.Pitch, s.Yaw);
            velocity += diveForward * (-diveForward.y * Physics.gravity.magnitude * _divePower) * dt;
            s.SinceDive = 0f;
        }
        else
        {
            targetSpeed = _maxSpeed;
            dragAtMaxSpeed = _dragAtMaxSpeed;
            // Juste après un piqué, le pigeon se redresse vite.
            float pitchSpeed = s.SinceDive <= 0.5f ? _diveReleasePitchSpeed : _pitchSpeed;
            ComputePitch(input, ref s, mode, pitchSpeed, _maxPitch, dt);
            ComputeYaw(input, ref s, dt, _turnSpeed);
            ApplyFlightForces(ref s, ref velocity, dt);
        }

        float speed = velocity.magnitude;
        Vector3 forward = ForwardOf(s.Pitch, s.Yaw);
        float forwardSpeed = Vector3.Dot(velocity, forward);

        if (mode != MovementState.Diving && mode != MovementState.Braking && mode != MovementState.HoverFlapping && speed > 0.1f)
        {
            float steeringFactor = 4f;
            if (mode != MovementState.Flapping && forward.y > 0.1f && forwardSpeed < _stallSpeed * 1.3f)
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

    void TryBarrelRoll(in PigeonInput input, ref PigeonState s, bool isLeft)
    {
        if (input.Flap || input.Dive || s.LastVelocity.magnitude < _minBarrelRollSpeed)
            return;
        _rollEvent.Invoke(isLeft);
        if (_stamina != null)
            AddStaminaBoost(ref s, _stamina.InstantRecoveryRoll);
    }
}
