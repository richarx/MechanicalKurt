using System;
using Sirenix.OdinInspector;
using UnityEngine;

public enum CityPathPointType
{
    Normal,
    Stop,
    TrafficLight,
    StopAndTrafficLight
}

[Serializable]
public class CityPathPoint
{
    [field: SerializeField, LabelText("Position locale")]
    public Vector3 Position { get; private set; }

    [field: SerializeField, LabelText("Type")]
    public CityPathPointType Type { get; private set; }

    [field: SerializeField, ShowIf(nameof(HasStop)), MinValue(0f), SuffixLabel("s", true)]
    public float MinWaitDuration { get; private set; } = 2f;

    [field: SerializeField, ShowIf(nameof(HasStop)), MinValue(nameof(MinWaitDuration)), SuffixLabel("s", true)]
    public float MaxWaitDuration { get; private set; } = 5f;

    [field: SerializeField, ShowIf(nameof(HasStop)), MinValue(0f), SuffixLabel("s", true)]
    public float Cooldown { get; private set; } = 10f;

    [field: SerializeField, ShowIf(nameof(HasTrafficLight)), MinValue(0.01f), SuffixLabel("s", true)]
    public float PhaseDuration { get; private set; } = 5f;

    [field: SerializeField, ShowIf(nameof(HasTrafficLight))]
    public bool StartWithStop { get; private set; } = true;

    public bool HasStop => Type == CityPathPointType.Stop || Type == CityPathPointType.StopAndTrafficLight;
    public bool HasTrafficLight => Type == CityPathPointType.TrafficLight || Type == CityPathPointType.StopAndTrafficLight;

    [ShowInInspector, ReadOnly, ShowIf(nameof(HasStop))]
    public bool IsInCooldown => Application.isPlaying && Time.timeAsDouble < _cooldownEndTime;

    [ShowInInspector, ReadOnly, ShowIf(nameof(HasTrafficLight))]
    public bool IsStop
    {
        get
        {
            EvaluateLight(out bool isStop, out _, out _);
            return HasTrafficLight && isStop;
        }
    }

    [ShowInInspector, ReadOnly, ShowIf(nameof(HasTrafficLight)), SuffixLabel("s", true)]
    public float TimeElapsed
    {
        get
        {
            EvaluateLight(out _, out float elapsed, out _);
            return elapsed;
        }
    }

    [ShowInInspector, ReadOnly, ShowIf(nameof(HasTrafficLight)), SuffixLabel("s", true)]
    public float RemainingDuration
    {
        get
        {
            EvaluateLight(out _, out _, out float remaining);
            return remaining;
        }
    }

    [NonSerialized] private bool _initialized;
    [NonSerialized] private double _lightStartTime;
    [NonSerialized] private double _cooldownEndTime;

    public CityPathPoint() : this(Vector3.zero) { }

    public CityPathPoint(Vector3 localPosition, CityPathPointType type = CityPathPointType.Normal)
    {
        Position = localPosition;
        Type = type;
    }

    public void SetPosition(Vector3 localPosition) => Position = localPosition;

    public void ResetRuntimeState()
    {
        _initialized = true;
        _cooldownEndTime = 0d;
        _lightStartTime = Time.timeAsDouble;
        // Preserve the random phase offset of the original traffic lights.
        if (HasTrafficLight)
            _lightStartTime -= UnityEngine.Random.Range(0f, Mathf.Max(0.01f, PhaseDuration));
    }

    public float RollWaitDuration()
    {
        EnsureRuntimeState();
        if (!HasStop || IsInCooldown)
            return 0f;

        float minimum = Mathf.Max(0f, MinWaitDuration);
        _cooldownEndTime = Time.timeAsDouble + Mathf.Max(0f, Cooldown);
        return UnityEngine.Random.Range(minimum, Mathf.Max(minimum, MaxWaitDuration));
    }

    public float GetWaitDuration() => IsStop ? RemainingDuration : 0f;

    private void EnsureRuntimeState()
    {
        if (!_initialized)
            ResetRuntimeState();
    }

    private void EvaluateLight(out bool isStop, out float elapsed, out float remaining)
    {
        if (!Application.isPlaying)
        {
            isStop = StartWithStop;
            elapsed = 0f;
            remaining = Mathf.Max(0.01f, PhaseDuration);
            return;
        }

        EnsureRuntimeState();
        TrafficLightCycle.Evaluate(Time.timeAsDouble - _lightStartTime, PhaseDuration, StartWithStop,
            out isStop, out elapsed, out remaining);
    }

    // Old components remain readable so existing scenes can be converted without losing settings.
    public static CityPathPoint FromLegacy(Transform path, Transform child)
    {
        TrafficStopPoint stop = child.GetComponent<TrafficStopPoint>();
        TrafficLightPoint light = child.GetComponent<TrafficLightPoint>();
        bool hasStop = stop != null && stop.enabled && child.gameObject.activeSelf;
        bool hasLight = light != null && light.enabled && child.gameObject.activeSelf;
        CityPathPoint point = new CityPathPoint(path.InverseTransformPoint(child.position),
            hasStop && hasLight ? CityPathPointType.StopAndTrafficLight :
            hasStop ? CityPathPointType.Stop : hasLight ? CityPathPointType.TrafficLight : CityPathPointType.Normal);
        if (stop != null)
        {
            point.MinWaitDuration = stop.MinWaitDuration;
            point.MaxWaitDuration = stop.MaxWaitDuration;
            point.Cooldown = stop.Cooldown;
        }
        if (light != null)
        {
            point.PhaseDuration = light.PhaseDuration;
            point.StartWithStop = light.StartWithStop;
        }
        return point;
    }
}
