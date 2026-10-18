using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class TrafficLightPoint : MonoBehaviour
{
    [field: SerializeField, MinValue(0.01f), SuffixLabel("s", true)]
    [field: Tooltip("Duree de chaque phase : stop pendant X secondes, puis passage pendant X secondes.")]
    public float PhaseDuration { get; private set; } = 5f;

    [field: SerializeField]
    public bool StartWithStop { get; private set; } = true;

    [ShowInInspector, ReadOnly]
    public bool IsStop { get; private set; }

    [ShowInInspector, ReadOnly]
    public float TimeElapsed => Application.isPlaying
        ? Mathf.Clamp((float)(Time.timeAsDouble - _phaseStartTime), 0f, _currentPhaseDuration)
        : 0f;

    [ShowInInspector, ReadOnly]
    public float RemainingDuration => Mathf.Max(0f, _currentPhaseDuration - TimeElapsed);

    private double _startTime;
    private double _phaseStartTime;
    private float _currentPhaseDuration;
    private CancellationTokenSource _cycleCancellation;

    private void OnEnable()
    {
        _startTime = Time.timeAsDouble;
        _cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        RunCycle(_cycleCancellation.Token).Forget();
    }

    private void OnDisable()
    {
        _cycleCancellation?.Cancel();
        _cycleCancellation?.Dispose();
        _cycleCancellation = null;
    }

    public float GetWaitDuration()
    {
        if (!isActiveAndEnabled)
            return 0f;

        return IsStop ? RemainingDuration : 0f;
    }

    private async UniTaskVoid RunCycle(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            double now = Time.timeAsDouble;
            _currentPhaseDuration = Mathf.Max(0.01f, PhaseDuration);
            TrafficLightCycle.Evaluate(now - _startTime, _currentPhaseDuration, StartWithStop,
                out bool isStop, out float timeElapsed, out float remainingDuration);
            IsStop = isStop;
            _phaseStartTime = now - timeElapsed;

            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(remainingDuration),
                cancellationToken: cancellationToken).SuppressCancellationThrow();
            if (cancelled)
                return;
        }
    }

    private void OnValidate()
    {
        PhaseDuration = Mathf.Max(0.01f, PhaseDuration);
    }

    private void OnDrawGizmos()
    {
        if (!isActiveAndEnabled)
            return;

        Color previousColor = Gizmos.color;
        bool isStop = Application.isPlaying ? IsStop : StartWithStop;
        Gizmos.color = isStop ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.75f, Vector3.one * 0.8f);
        Gizmos.color = previousColor;
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
