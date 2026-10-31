using UnityEngine;

public partial class PigeonController
{
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

        MoveController(ref s, velocity, dt, allowSupport: false);

        if (_hasStaticHit && -Vector3.Dot(velocity, _staticHitNormal) > _stunBounceMinSpeed)
            s.LastVelocity = Vector3.Reflect(velocity, _staticHitNormal) * _stunBounce;

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
        ClearSupport(ref s);
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
}
