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
    public bool IsInCooldown => Application.isPlaying && CityClock.Now < _cooldownEndTime
        && _cooldownEndTime - CityClock.Now <= Mathf.Max(0f, Cooldown);

    /// <summary>Fin du délai entre deux arrêts, en temps <see cref="CityClock"/> (fait partie de l'état du trafic).</summary>
    public double CooldownEnd
    {
        get
        {
            EnsureRuntimeState();
            return _cooldownEndTime;
        }
        set
        {
            EnsureRuntimeState();
            _cooldownEndTime = value;
        }
    }

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
        _cooldownEndTime = double.NegativeInfinity;
        // Décalage de phase tiré de la position du feu : le même sur chaque machine.
        _lightStartTime = HasTrafficLight ? -PhaseOffset() * Mathf.Max(0.01f, PhaseDuration) : 0d;
    }

    /// <param name="random01">Tirage dans [0, 1) qui choisit la durée entre le minimum et le maximum.</param>
    public float RollWaitDuration(float random01)
    {
        EnsureRuntimeState();
        if (!HasStop || IsInCooldown)
            return 0f;

        float minimum = Mathf.Max(0f, MinWaitDuration);
        _cooldownEndTime = CityClock.Now + Mathf.Max(0f, Cooldown);
        return Mathf.Lerp(minimum, Mathf.Max(minimum, MaxWaitDuration), random01);
    }

    double PhaseOffset()
    {
        unchecked
        {
            uint hash = 2166136261;
            hash = (hash ^ (uint)Mathf.RoundToInt(Position.x * 100f)) * 16777619;
            hash = (hash ^ (uint)Mathf.RoundToInt(Position.y * 100f)) * 16777619;
            hash = (hash ^ (uint)Mathf.RoundToInt(Position.z * 100f)) * 16777619;
            return (hash & 0xFFFF) / 65536d;
        }
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
        TrafficLightCycle.Evaluate(CityClock.Now - _lightStartTime, PhaseDuration, StartWithStop,
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
