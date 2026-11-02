using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>État d'une voiture porté par le trafic synchronisé (<see cref="TrafficNetwork"/>).</summary>
public struct CarSimState
{
    public float Distance;
    public float Speed;
    public float MaxSpeed;
    public float StopTimeRemaining;
    public float DistanceToStop;
    public int NextStop;
    public uint Random;
    public Quaternion Rotation;
}

public class AmbientCar : MonoBehaviour
{
    [field: SerializeField, BoxGroup("Circuit"), Required]
    [field: Tooltip("Circuit independant de la voiture. Sa liste de points definit la boucle en 3D.")]
    public TrafficPath Path { get; private set; }

    [field: SerializeField, BoxGroup("Conduite"), MinValue(0f)]
    public float MaxSpeed { get;  set; } = 6f;

    [field: SerializeField, BoxGroup("Conduite"), MinValue(0.01f)]
    public float Acceleration { get; private set; } = 2f;

    [field: SerializeField, BoxGroup("Conduite"), MinValue(0.01f)]
    public float Braking { get; private set; } = 5f;

    [field: SerializeField, BoxGroup("Conduite"), MinValue(0f)]
    [field: Tooltip("Vitesse de rotation en degres/seconde, y compris l'inclinaison dans les pentes.")]
    public float TurnSpeed { get; private set; } = 180f;

    [field: SerializeField, BoxGroup("Conduite"), MinValue(0.01f)]
    [field: Tooltip("Distance d'echantillonnage devant et derriere le centre pour adoucir l'orientation.")]
    public float DirectionSampleDistance { get; private set; } = 0.75f;

    [field: SerializeField, BoxGroup("Espacement"), MinValue(0.1f)]
    [field: Tooltip("Longueur totale du vehicule en unites monde, centree sur le pivot de la racine.")]
    public float VehicleLength { get; private set; } = 4f;

    [field: SerializeField, BoxGroup("Espacement"), MinValue(0f)]
    [field: Tooltip("Espace minimum entre les pare-chocs sur le meme circuit, en unites monde.")]
    public float MinimumGap { get; private set; } = 2f;

    [ShowInInspector, ReadOnly]
    public float CurrentSpeed { get; private set; }

    [ShowInInspector, ReadOnly]
    public float Distance { get; private set; }

    [ShowInInspector, ReadOnly, BoxGroup("Arret temporaire")]
    public float StopTimeRemaining { get; private set; }

    [ShowInInspector, ReadOnly, BoxGroup("Feu")]
    public float LightWaitRemaining { get; private set; }

    private TrafficPath _registeredPath;
    private float _plannedTravel;
    private float _plannedSpeed;
    private CityPathPoint _nextStop;
    private int _nextStopIndex = -1;
    private float _distanceToStop = float.PositiveInfinity;
    private float? _initialDistance;
    private CityPathPoint _nextLight;
    private float _distanceToLight = float.PositiveInfinity;
    private uint _random = 1;
    private Transform _view;

    private void OnEnable()
    {
        JoinPath();
    }

    public void Initialize(TrafficPath path, float distance)
    {
        if (_registeredPath != null)
            _registeredPath.Unregister(this);
        _registeredPath = null;
        Path = path;
        _initialDistance = distance;
        if (isActiveAndEnabled)
            JoinPath();
    }

    private void JoinPath()
    {
        CurrentSpeed = 0f;
        LightWaitRemaining = 0f;
        _nextLight = null;
        _distanceToLight = float.PositiveInfinity;
        StopTimeRemaining = 0f;
        _nextStop = null;
        _distanceToStop = float.PositiveInfinity;
        if (Path == null || !Path.IsValid || Path.transform.IsChildOf(transform))
        {
            Debug.LogWarning("AmbientCar : assigner un TrafficPath independant avec au moins deux points distincts.", this);
            return;
        }

        _registeredPath = Path;
        Distance = _initialDistance.HasValue
            ? _registeredPath.WrapDistance(_initialDistance.Value)
            : _registeredPath.GetClosestDistance(transform.position);
        _initialDistance = null;
        transform.SetPositionAndRotation(_registeredPath.GetPosition(Distance), GetTargetRotation());
        _registeredPath.Register(this);
        SelectNextStop(true);
        _registeredPath.GetLeader(this, out float availableDistance, out _);
        if (availableDistance <= 0f)
            Debug.LogWarning("AmbientCar : voitures trop proches au depart. Les repartir avec une marge sur le circuit.", this);
    }

    private void OnDisable()
    {
        if (_registeredPath != null)
            _registeredPath.Unregister(this);
        _registeredPath = null;
        CurrentSpeed = 0f;
        LightWaitRemaining = 0f;
        _nextLight = null;
        _distanceToLight = float.PositiveInfinity;
        StopTimeRemaining = 0f;
        _nextStop = null;
        _nextStopIndex = -1;
        _distanceToStop = float.PositiveInfinity;
    }

    public void PlanMovement(float deltaTime)
    {
        _plannedTravel = 0f;
        _plannedSpeed = 0f;
        LightWaitRemaining = 0f;
        _nextLight = null;
        _distanceToLight = float.PositiveInfinity;
        if (StopTimeRemaining > 0f)
        {
            StopTimeRemaining = Mathf.Max(0f, StopTimeRemaining - deltaTime);
            return;
        }

        if (_nextStop == null || !_nextStop.HasStop)
            SelectNextStop(true);

        _registeredPath.GetLeader(this, out float availableDistance, out float leaderSpeed);
        _registeredPath.TryGetNextRedLight(Distance, out _nextLight, out _distanceToLight);
        TrafficMotion.PlanStep(CurrentSpeed, MaxSpeed, Acceleration, Braking,
            availableDistance, leaderSpeed, deltaTime, out _plannedTravel, out _plannedSpeed,
            Mathf.Min(_distanceToStop, _distanceToLight));
    }

    public void ApplyMovement(float deltaTime)
    {
        Distance = _registeredPath.WrapDistance(Distance + _plannedTravel);
        CurrentSpeed = _plannedSpeed;
        transform.position = _registeredPath.GetPosition(Distance);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, GetTargetRotation(), TurnSpeed * deltaTime);

        if (_nextLight != null && _distanceToLight - _plannedTravel <= 0.0001f)
        {
            LightWaitRemaining = _nextLight.GetWaitDuration();
            if (LightWaitRemaining > 0f)
                CurrentSpeed = 0f;
        }

        if (_nextStop == null)
            return;

        _distanceToStop = Mathf.Max(0f, _distanceToStop - _plannedTravel);
        if (_distanceToStop <= 0.0001f)
        {
            CurrentSpeed = 0f;
            StopTimeRemaining = _nextStop.RollWaitDuration(NextRandom01());
            SelectNextStop(false);
        }
    }

    private void SelectNextStop(bool includeCurrent)
    {
        _registeredPath.TryGetNextStop(Distance, includeCurrent, out _nextStopIndex, out _distanceToStop);
        _nextStop = _nextStopIndex >= 0 ? _registeredPath.GetPoint(_nextStopIndex) : null;
    }

    /// <summary>Graine des tirages de la voiture (durées d'arrêt), pour qu'ils soient les mêmes sur chaque machine.</summary>
    public void SetRandomSeed(uint seed) => _random = seed == 0 ? 1u : seed;

    private float NextRandom01()
    {
        // xorshift32 : même suite partout, et la graine fait partie de l'état synchronisé.
        _random ^= _random << 13;
        _random ^= _random >> 17;
        _random ^= _random << 5;
        return (_random >> 8) / 16777216f;
    }

    public CarSimState CaptureSimState() => new CarSimState
    {
        Distance = Distance,
        Speed = CurrentSpeed,
        MaxSpeed = MaxSpeed,
        StopTimeRemaining = StopTimeRemaining,
        DistanceToStop = _distanceToStop,
        NextStop = _nextStopIndex,
        Random = _random,
        Rotation = transform.rotation,
    };

    public void ApplySimState(in CarSimState state)
    {
        Distance = state.Distance;
        CurrentSpeed = state.Speed;
        MaxSpeed = state.MaxSpeed;
        StopTimeRemaining = state.StopTimeRemaining;
        _distanceToStop = state.DistanceToStop;
        _random = state.Random;
        if (_registeredPath == null)
            return;
        _nextStopIndex = state.NextStop >= 0 && state.NextStop < _registeredPath.PointCount ? state.NextStop : -1;
        _nextStop = _nextStopIndex >= 0 ? _registeredPath.GetPoint(_nextStopIndex) : null;
        transform.SetPositionAndRotation(_registeredPath.GetPosition(Distance), state.Rotation);
    }

    /// <summary>
    /// Affiche la voiture à une pose lissée entre deux ticks, sans bouger ses colliders qui restent à la pose simulée.
    /// </summary>
    public void ShowView(float distance, Quaternion rotation)
    {
        if (_registeredPath == null)
            return;
        EnsureViewPivot();
        _view.SetPositionAndRotation(_registeredPath.GetPosition(distance), rotation);
    }

    private void EnsureViewPivot()
    {
        if (_view != null)
            return;
        _view = new GameObject("View").transform;
        _view.SetParent(transform, false);
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != _view && child.GetComponentInChildren<Collider>(true) == null)
                child.SetParent(_view, true);
        }
        if (TryGetComponent(out MovingSupport support))
            support.ViewTransform = _view;
    }

    private Quaternion GetTargetRotation()
    {
        Vector3 direction = _registeredPath.GetDirection(Distance, DirectionSampleDistance);

        if (Vector3.Cross(direction, Vector3.up).sqrMagnitude <= 0.000001f)
            return transform.rotation;

        return Quaternion.LookRotation(direction, Vector3.up);
    }

    private void OnDrawGizmos()
    {
        Vector3 forward = transform.forward;
        Vector3 rear = transform.position - forward * (VehicleLength * 0.5f);
        Vector3 front = transform.position + forward * (VehicleLength * 0.5f);
        Vector3 marker = transform.right * 0.15f;
        Color previousColor = Gizmos.color;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(rear, front);
        Gizmos.DrawLine(rear - marker, rear + marker);
        Gizmos.DrawLine(front - marker, front + marker);

        Gizmos.color = new Color(1f, 0.65f, 0.15f, 1f);
        Vector3 rearGapEnd = rear - forward * MinimumGap;
        Vector3 frontGapEnd = front + forward * MinimumGap;
        Gizmos.DrawLine(rearGapEnd, rear);
        Gizmos.DrawLine(front, frontGapEnd);
        Gizmos.DrawLine(rearGapEnd - marker, rearGapEnd + marker);
        Gizmos.DrawLine(frontGapEnd - marker, frontGapEnd + marker);

        Gizmos.color = previousColor;
    }
}

public static class TrafficMotion
{
    public static void PlanStep(float currentSpeed, float maxSpeed, float acceleration, float braking,
        float availableDistance, float leaderSpeed, float deltaTime, out float travel, out float nextSpeed,
        float stopDistance = float.PositiveInfinity)
    {
        if (deltaTime <= 0f)
        {
            travel = 0f;
            nextSpeed = currentSpeed;
            return;
        }

        float targetSpeed = maxSpeed;
        if (!float.IsPositiveInfinity(stopDistance))
        {
            stopDistance = Mathf.Max(0f, stopDistance);
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(2f * braking * stopDistance));
            availableDistance = Mathf.Min(availableDistance, stopDistance);
        }
        if (!float.IsPositiveInfinity(availableDistance))
        {
            availableDistance = Mathf.Max(0f, availableDistance);
            float safeSpeed = Mathf.Sqrt(leaderSpeed * leaderSpeed + 2f * braking * availableDistance);
            targetSpeed = Mathf.Min(targetSpeed, safeSpeed);
        }

        float rate = targetSpeed >= currentSpeed ? acceleration : braking;
        nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
        travel = (currentSpeed + nextSpeed) * 0.5f * deltaTime;

        if (travel > availableDistance)
        {
            travel = Mathf.Max(0f, availableDistance);
            nextSpeed = Mathf.Min(nextSpeed, travel / deltaTime);
        }
    }
}
