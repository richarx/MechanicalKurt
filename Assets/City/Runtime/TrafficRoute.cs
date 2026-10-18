using System;
using UnityEngine;

// Cached world-space polyline shared by every car on a circuit.
public class TrafficRoute
{
    private readonly Vector3[] _points;
    private readonly float[] _distances;

    public float Length { get; }
    public bool IsValid => _points.Length >= 2 && Length > 0.01f;

    public TrafficRoute(Vector3[] points)
    {
        _points = (Vector3[])points.Clone();
        _distances = new float[_points.Length + 1];
        for (int i = 0; i < _points.Length; i++)
        {
            float segmentLength = Vector3.Distance(_points[i], _points[(i + 1) % _points.Length]);
            _distances[i + 1] = _distances[i] + segmentLength;
        }
        Length = _distances[_points.Length];
    }

    public float WrapDistance(float distance)
    {
        if (!IsValid)
            return 0f;

        float wrapped = distance % Length;
        return wrapped < 0f ? wrapped + Length : wrapped;
    }

    public Vector3 GetPosition(float distance)
    {
        if (!IsValid)
            return _points.Length > 0 ? _points[0] : Vector3.zero;

        distance = WrapDistance(distance);
        for (int i = 0; i < _points.Length; i++)
        {
            float segmentLength = _distances[i + 1] - _distances[i];
            if (segmentLength <= 0f || distance >= _distances[i + 1])
                continue;

            float t = (distance - _distances[i]) / segmentLength;
            return Vector3.LerpUnclamped(_points[i], _points[(i + 1) % _points.Length], t);
        }

        return _points[0];
    }

    public float GetClosestDistance(Vector3 position)
    {
        float closestDistance = 0f;
        float bestSqrDistance = float.PositiveInfinity;
        for (int i = 0; i < _points.Length; i++)
        {
            Vector3 start = _points[i];
            Vector3 segment = _points[(i + 1) % _points.Length] - start;
            float segmentSqrLength = segment.sqrMagnitude;
            if (segmentSqrLength <= 0f)
                continue;

            float t = Mathf.Clamp01(Vector3.Dot(position - start, segment) / segmentSqrLength);
            float sqrDistance = (position - (start + segment * t)).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closestDistance = _distances[i] + t * (_distances[i + 1] - _distances[i]);
            }
        }

        return WrapDistance(closestDistance);
    }

    public float GetPointDistance(int index)
    {
        return WrapDistance(_distances[index]);
    }

    public float GetForwardDistance(float distance, float targetDistance, bool includeCurrent)
    {
        float forwardDistance = WrapDistance(targetDistance - distance);
        // Treat tiny rounding errors on either side of a point as the same location.
        // A served stop at that location is due again on the next lap.
        if (forwardDistance <= 0.0001f || Length - forwardDistance <= 0.0001f)
            return includeCurrent ? 0f : Length;
        return forwardDistance;
    }

    public Vector3 GetDirection(float distance, float sampleDistance)
    {
        if (!IsValid)
            return Vector3.forward;

        // Sampling on both sides also captures pitch at the foot and crest of a bridge.
        float sample = Math.Min(Math.Max(sampleDistance, 0.01f), Length * 0.1f);
        Vector3 direction = GetPosition(distance + sample) - GetPosition(distance - sample);
        if (direction.sqrMagnitude <= 0.000001f)
            direction = GetPosition(distance + sample) - GetPosition(distance);

        return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
    }
}
