using System;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class TrafficLightPoint : MonoBehaviour
{
    [field: SerializeField, BoxGroup("Feu"), MinValue(0.01f), SuffixLabel("s", true)]
    [field: Tooltip("Duree de chaque phase : stop pendant X secondes, puis passage pendant X secondes.")]
    public float PhaseDuration { get; private set; } = 5f;

    [field: SerializeField, BoxGroup("Feu")]
    public bool StartWithStop { get; private set; } = true;

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public bool IsStop
    {
        get
        {
            GetState(out bool isStop, out _, out _);
            return isStop;
        }
    }

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public float TimeElapsed
    {
        get
        {
            GetState(out _, out float timeElapsed, out _);
            return timeElapsed;
        }
    }

    [ShowInInspector, ReadOnly, BoxGroup("Etat")]
    public float RemainingDuration
    {
        get
        {
            GetState(out _, out _, out float remainingDuration);
            return remainingDuration;
        }
    }

    private double _startTime;

    private void OnEnable()
    {
        _startTime = Time.timeAsDouble;
    }

    public float GetWaitDuration()
    {
        if (!isActiveAndEnabled)
            return 0f;

        GetState(out bool isStop, out _, out float remainingDuration);
        return isStop ? remainingDuration : 0f;
    }

    private void GetState(out bool isStop, out float timeElapsed, out float remainingDuration)
    {
        double elapsed = Application.isPlaying ? Time.timeAsDouble - _startTime : 0d;
        TrafficLightCycle.Evaluate(elapsed, PhaseDuration, StartWithStop,
            out isStop, out timeElapsed, out remainingDuration);
    }

    private void OnValidate()
    {
        PhaseDuration = Mathf.Max(0.01f, PhaseDuration);
    }

    private void OnDrawGizmos()
    {
        if (!isActiveAndEnabled)
            return;

        GetState(out bool isStop, out _, out float remainingDuration);
        Color previousColor = Gizmos.color;
        Gizmos.color = isStop ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.75f, Vector3.one * 0.8f);
        Gizmos.color = previousColor;
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.3f,
            $"{(isStop ? "STOP" : "PASS")} {remainingDuration:0.0} s");
#endif
    }
}

public static class TrafficLightCycle
{
    public static void Evaluate(double elapsed, float phaseDuration, bool startWithStop,
        out bool isStop, out float timeElapsed, out float remainingDuration)
    {
        double duration = Math.Max(0.01d, phaseDuration);
        elapsed = Math.Max(0d, elapsed);
        bool isFirstPhase = Math.Floor(elapsed / duration) % 2d == 0d;
        isStop = isFirstPhase ? startWithStop : !startWithStop;
        double phaseElapsed = elapsed % duration;
        timeElapsed = (float)phaseElapsed;
        remainingDuration = (float)(duration - phaseElapsed);
    }
}
