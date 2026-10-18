using System.Collections.Generic;
using UnityEngine;


public class TrafficPath : MonoBehaviour
{
    [SerializeField] private Color _gizmoColor = new Color(1f, 0.65f, 0.15f, 1f);

    private readonly List<AmbientCar> _cars = new List<AmbientCar>();
    private TrafficRoute _route;
    private TrafficStopPoint[] _stopPoints;
    private TrafficLightPoint[] _lightPoints;

    public float Length => Route.Length;
    public bool IsValid => Route.IsValid;

    private TrafficRoute Route
    {
        get
        {
            if (_route == null)
            {
                Vector3[] points = new Vector3[transform.childCount];
                _stopPoints = new TrafficStopPoint[points.Length];
                _lightPoints = new TrafficLightPoint[points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = transform.GetChild(i).position;
                    _stopPoints[i] = transform.GetChild(i).GetComponent<TrafficStopPoint>();
                    _lightPoints[i] = transform.GetChild(i).GetComponent<TrafficLightPoint>();
                }
                _route = new TrafficRoute(points);
            }
            return _route;
        }
    }

    public Vector3 GetPosition(float distance) => Route.GetPosition(distance);
    public Vector3 GetDirection(float distance, float sampleDistance) => Route.GetDirection(distance, sampleDistance);
    public float GetClosestDistance(Vector3 position) => Route.GetClosestDistance(position);
    public float WrapDistance(float distance) => Route.WrapDistance(distance);

    public void Register(AmbientCar car)
    {
        if (!_cars.Contains(car))
            _cars.Add(car);
    }

    public void Unregister(AmbientCar car)
    {
        _cars.Remove(car);
    }

    public bool CanPlaceCar(float distance, float vehicleLength, float minimumGap, float extraGap)
    {
        foreach (AmbientCar other in _cars)
        {
            if (other == null || !other.isActiveAndEnabled)
                continue;

            float clearance = TrafficSpawnLayout.GetRequiredSpacing(vehicleLength, minimumGap,
                other.VehicleLength, other.MinimumGap, extraGap);
            if (WrapDistance(other.Distance - distance) < clearance
                || WrapDistance(distance - other.Distance) < clearance)
                return false;
        }
        return true;
    }

    public void GetLeader(AmbientCar follower, out float availableDistance, out float leaderSpeed)
    {
        availableDistance = float.PositiveInfinity;
        leaderSpeed = 0f;
        float closestDistance = float.PositiveInfinity;
        foreach (AmbientCar other in _cars)
        {
            if (other == null || other == follower || !other.isActiveAndEnabled)
                continue;

            float distance = WrapDistance(other.Distance - follower.Distance);
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            float clearance = (follower.VehicleLength + other.VehicleLength) * 0.5f + follower.MinimumGap;
            availableDistance = Mathf.Max(0f, distance - clearance);
            leaderSpeed = other.CurrentSpeed;
        }
    }

    public bool TryGetNextStop(float distance, bool includeCurrent, out TrafficStopPoint stop, out float remainingDistance)
    {
        stop = null;
        remainingDistance = float.PositiveInfinity;
        if (!IsValid)
            return false;

        for (int i = 0; i < _stopPoints.Length; i++)
        {
            TrafficStopPoint candidate = _stopPoints[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            float forwardDistance = Route.GetForwardDistance(distance, Route.GetPointDistance(i), includeCurrent);
            if (forwardDistance >= remainingDistance)
                continue;

            stop = candidate;
            remainingDistance = forwardDistance;
        }
        return stop != null;
    }

    public bool TryGetNextRedLight(float distance, out TrafficLightPoint light, out float remainingDistance)
    {
        light = null;
        remainingDistance = float.PositiveInfinity;
        if (!IsValid)
            return false;

        for (int i = 0; i < _lightPoints.Length; i++)
        {
            TrafficLightPoint candidate = _lightPoints[i];
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsStop)
                continue;

            float forwardDistance = Route.GetForwardDistance(distance, Route.GetPointDistance(i), true);
            if (forwardDistance >= remainingDistance)
                continue;

            light = candidate;
            remainingDistance = forwardDistance;
        }
        return light != null;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f || !IsValid)
            return;

        // All cars read the same frame's positions before any car moves.
        foreach (AmbientCar car in _cars)
        {
            if (car != null && car.isActiveAndEnabled)
                car.PlanMovement(deltaTime);
        }
        foreach (AmbientCar car in _cars)
        {
            if (car != null && car.isActiveAndEnabled)
                car.ApplyMovement(deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = _gizmoColor;
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = transform.GetChild(i).position;
            Gizmos.DrawWireSphere(position, 0.25f);
            if (count > 1)
            {
                Vector3 next = transform.GetChild((i + 1) % count).position;
                Gizmos.DrawLine(position, next);
                Vector3 direction = next - position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    Vector3 middle = (position + next) * 0.5f;
                    Vector3 back = -direction.normalized * 0.7f;
                    Vector3 side = Vector3.Cross(direction.normalized, Vector3.up) * 0.3f;
                    Gizmos.DrawLine(middle, middle + back + side);
                    Gizmos.DrawLine(middle, middle + back - side);
                }
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 0.4f, $"{i + 1}");
#endif
        }
        Gizmos.color = previousColor;
    }
}
