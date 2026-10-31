using UnityEngine;

public partial class PigeonController
{
    readonly Collider[] _overlapBuffer = new Collider[32];
    readonly RaycastHit[] _castBuffer = new RaycastHit[32];

    void DetectBoostRings(ref PigeonState s, Vector3 start)
    {
        int layers = _boostLayer.value != 0 ? _boostLayer.value : Physics.AllLayers;
        Vector3 center = _controller.center;
        float radius = _controller.radius;

        BoostRing insideRing = null;
        int count = Physics.OverlapSphereNonAlloc(
            transform.position + center, radius, _overlapBuffer, layers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count && insideRing == null; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col.isTrigger)
                insideRing = col.GetComponentInParent<BoostRing>();
        }

        BoostRing ring = insideRing;
        Vector3 path = transform.position - start;
        float distance = path.magnitude;
        if (!s.InsideBoostRing && distance > 0.0001f)
        {
            int hits = Physics.SphereCastNonAlloc(
                start + center, radius, path / distance, _castBuffer, distance, layers, QueryTriggerInteraction.Collide);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _castBuffer[i];
                if (!hit.collider.isTrigger || hit.distance >= closest)
                    continue;
                BoostRing crossed = hit.collider.GetComponentInParent<BoostRing>();
                if (crossed == null)
                    continue;
                ring = crossed;
                closest = hit.distance;
            }
        }

        if (ring != null && !s.InsideBoostRing)
        {
            float speed = s.LastVelocity.magnitude;
            Vector3 direction = speed > 0.1f ? s.LastVelocity / speed : ForwardOf(s.Pitch, s.Yaw);
            float targetSpeed = Mathf.Min(Mathf.Max(speed + ring.BoostAmount, ring.MinExitSpeed), ring.MaxSpeed);
            if (targetSpeed > speed)
                s.PendingImpulse += direction * (targetSpeed - speed);
            _boostEvent.Invoke(ring);
        }
        s.InsideBoostRing = insideRing != null;
    }

    int _ringCombo;
    float _lastRingTime = float.NegativeInfinity;

    /// <summary>Rang du dernier anneau dans l'enchaînement en cours (présentation).</summary>
    public int RingCombo => _ringCombo;

    void PlayBoost(BoostRing ring)
    {
        if (_boostFeedback != null)
            _boostFeedback.Play();
        float now = Time.time;
        _ringCombo = now - _lastRingTime <= BoostRing.ComboWindow ? _ringCombo + 1 : 1;
        _lastRingTime = now;
        ring.PlayRingFeedback(_ringCombo, this == Local);
        OnBoosted.Invoke();
    }
}
