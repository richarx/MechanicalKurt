using System.Collections.Generic;
using UnityEngine;


public class TrafficPath : CityPath
{
    private readonly List<AmbientCar> _cars = new List<AmbientCar>();
    private TrafficRoute _route;
    private Matrix4x4 _routeMatrix;

    public float Length => Route.Length;
    public bool IsValid => Route.IsValid;

    private TrafficRoute Route
    {
        get
        {
            if (_route == null || _routeMatrix != transform.localToWorldMatrix)
            {
                Vector3[] points = new Vector3[PointCount];
                for (int i = 0; i < points.Length; i++)
                    points[i] = GetPointPosition(i);
                _route = new TrafficRoute(points);
                _routeMatrix = transform.localToWorldMatrix;
            }
            return _route;
        }
    }

    public Vector3 GetPosition(float distance) => Route.GetPosition(distance);
    public Vector3 GetDirection(float distance, float sampleDistance) => Route.GetDirection(distance, sampleDistance);
    public float GetClosestDistance(Vector3 position) => Route.GetClosestDistance(position);
    public float WrapDistance(float distance) => Route.WrapDistance(distance);

    public override void InvalidatePath() => _route = null;

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

    public bool TryGetNextStop(float distance, bool includeCurrent, out CityPathPoint stop, out float remainingDistance)
    {
        stop = null;
        remainingDistance = float.PositiveInfinity;
        if (!IsValid)
            return false;

        for (int i = 0; i < PointCount; i++)
        {
            CityPathPoint candidate = GetPoint(i);
            if (!candidate.HasStop)
                continue;

            float forwardDistance = Route.GetForwardDistance(distance, Route.GetPointDistance(i), includeCurrent);
            if (forwardDistance >= remainingDistance)
                continue;

            stop = candidate;
            remainingDistance = forwardDistance;
        }
        return stop != null;
    }

    public bool TryGetNextRedLight(float distance, out CityPathPoint light, out float remainingDistance)
    {
        light = null;
        remainingDistance = float.PositiveInfinity;
        if (!IsValid)
            return false;

        for (int i = 0; i < PointCount; i++)
        {
            CityPathPoint candidate = GetPoint(i);
            if (!candidate.HasTrafficLight || !candidate.IsStop)
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

}
