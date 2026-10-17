using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class DeliverySession : MonoBehaviour
{
    [InfoBox("Duration must be a finite number greater than zero.", InfoMessageType.Error, nameof(HasConfigurationError))]
    [SerializeField, MinValue(0.01f), SuffixLabel("real seconds", true)] private float _durationSeconds = 60f;
    [SerializeField] private UnityEvent _onStarted = new UnityEvent();
    [SerializeField] private UnityEvent _onEnded = new UnityEvent();
    [SerializeField] private UnityEvent _onReset = new UnityEvent();

    private readonly DeliverySessionClock _clock = new DeliverySessionClock();
    private bool _isTransitioning;

    public float DurationSeconds => _durationSeconds;
    public UnityEvent OnStarted => _onStarted;
    public UnityEvent OnEnded => _onEnded;
    public UnityEvent OnReset => _onReset;

    [ShowInInspector, ReadOnly] public DeliverySessionState State => _clock.State;
    [ShowInInspector, ReadOnly] public double RemainingSeconds => State == DeliverySessionState.Ready
        ? (IsValid ? _durationSeconds : 0d) : _clock.RemainingSeconds;
    [ShowInInspector, ReadOnly] public bool IsRunning => isActiveAndEnabled && State == DeliverySessionState.Running;
    [ShowInInspector, ReadOnly] public bool IsPaused => State == DeliverySessionState.Running
        && (_clock.IsPaused || Time.timeScale <= 0f || !isActiveAndEnabled);
    [ShowInInspector, ReadOnly] public bool CanInteract => IsRunning && !IsPaused;
    public bool IsValid => DeliverySessionClock.IsValidDuration(_durationSeconds);
    private bool HasConfigurationError => !IsValid;
    private bool CanChangeSession => Application.isPlaying && !_isTransitioning;

    #region Life Cycle

    private void Awake()
    {
        _clock.Reset(_durationSeconds);
    }

    private void Update()
    {
        if (!CanInteract || _isTransitioning)
            return;

        if (!_clock.Advance(Time.unscaledDeltaTime))
            return;

        _isTransitioning = true;
        try
        {
            _onEnded.Invoke();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    #endregion

    #region Session Controls

    [Button("Start Session"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void StartSession()
    {
        if (!CanChangeSession || !isActiveAndEnabled || !IsValid)
            return;

        if (!_clock.Start(_durationSeconds))
            return;

        _isTransitioning = true;
        try
        {
            _onStarted.Invoke();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    [Button("Restart Session"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void RestartSession()
    {
        if (!CanChangeSession || !isActiveAndEnabled || !IsValid)
            return;

        _isTransitioning = true;
        try
        {
            _clock.Reset(_durationSeconds);
            _onReset.Invoke();

            // A reset listener may disable or destroy this component.
            if (this == null || !isActiveAndEnabled || !_clock.Start(_durationSeconds))
                return;

            _onStarted.Invoke();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    [Button("Reset Session"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void ResetSession()
    {
        if (!CanChangeSession)
            return;

        _isTransitioning = true;
        try
        {
            _clock.Reset(_durationSeconds);
            _onReset.Invoke();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    [Button("Toggle Pause"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void TogglePause()
    {
        SetPaused(!_clock.IsPaused);
    }

    public void SetPaused(bool paused)
    {
        if (!CanChangeSession)
            return;

        _clock.SetPaused(paused);
    }

    #endregion
}
