using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public enum DeliveryState { Idle, PickingUp, Carrying, DroppingOff, Renewing }
public enum DeliveryInteractionMode { Instant, DebugDelay, External }

[System.Serializable]
public sealed class DeliveryInteractionStartedEvent : UnityEvent<int> { }

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class DeliveryManager : MonoBehaviour
{
    [SerializeField, Required] private DeliverySession _session;
    [SerializeField, Required] private DeliveryOfferSelector _selector;
    [SerializeField, MinValue(0.01f), SuffixLabel("seconds", true)] private float _dropOffHoldSeconds = 0.75f;
    [SerializeField, MinValue(0f), SuffixLabel("seconds", true)] private float _renewalDelaySeconds = 1f;
    [SerializeField] private DeliveryInteractionMode _pickupMode = DeliveryInteractionMode.Instant;
    [SerializeField] private DeliveryInteractionMode _dropOffMode = DeliveryInteractionMode.Instant;
    [SerializeField, MinValue(0f), SuffixLabel("seconds", true)] private float _pickupDelaySeconds = 1f;
    [SerializeField, MinValue(0f), SuffixLabel("seconds", true)] private float _dropOffDelaySeconds = 1f;
    [SerializeField] private DeliveryInteractionStartedEvent _onPickupStarted = new DeliveryInteractionStartedEvent();
    [SerializeField] private DeliveryInteractionStartedEvent _onDropOffStarted = new DeliveryInteractionStartedEvent();
    [SerializeField] private DeliveryOfferAcceptedEvent _onPickedUp = new DeliveryOfferAcceptedEvent();
    [SerializeField] private DeliveryOfferAcceptedEvent _onDelivered = new DeliveryOfferAcceptedEvent();
    [SerializeField] private DeliveryOfferAcceptedEvent _onCancelled = new DeliveryOfferAcceptedEvent();

    private DeliveryOffer _currentOffer;
    private DeliveryOffer _lastCompletedOffer;
    private float _holdElapsed;
    private float _sequenceElapsed;
    private float _renewalElapsed;
    private int _interactionToken;
    private bool _interactionActive;
    private bool _completionRequested;
    private bool _subscribed;

    public DeliverySession Session => _session;
    public DeliveryOfferSelector Selector => _selector;
    public Transform Player => _selector != null ? _selector.Player : null;
    [ShowInInspector, ReadOnly] public DeliveryState State { get; private set; }
    [ShowInInspector, ReadOnly] public DeliveryOffer CurrentOffer => _currentOffer;
    [ShowInInspector, ReadOnly] public int CompletedDeliveries { get; private set; }
    public DeliveryOfferAcceptedEvent OnPickedUp => _onPickedUp;
    public DeliveryOfferAcceptedEvent OnDelivered => _onDelivered;
    public DeliveryOfferAcceptedEvent OnCancelled => _onCancelled;
    public DeliveryInteractionStartedEvent OnPickupStarted => _onPickupStarted;
    public DeliveryInteractionStartedEvent OnDropOffStarted => _onDropOffStarted;
    [ShowInInspector, ReadOnly] public int CurrentInteractionToken => _interactionActive ? _interactionToken : 0;

    public float InteractionProgress
    {
        get
        {
            if (State == DeliveryState.Carrying) return Mathf.Clamp01(_holdElapsed / SafeHoldSeconds);
            if (State != DeliveryState.PickingUp && State != DeliveryState.DroppingOff) return 0f;
            if (_completionRequested) return 1f;
            float delay = SequenceDelay;
            return SequenceMode == DeliveryInteractionMode.DebugDelay && delay > 0f
                ? Mathf.Clamp01(_sequenceElapsed / delay) : 0f;
        }
    }

    public bool IsAtDestination
    {
        get
        {
            if (_currentOffer == null || !_currentOffer.IsAvailable || Player == null) return false;
            Vector3 delta = Player.position - _currentOffer.Destination.Position;
            float radius = _currentOffer.Destination.Radius;
            return Mathf.Abs(delta.y) <= _selector.VerticalTolerance
                && delta.x * delta.x + delta.z * delta.z <= radius * radius;
        }
    }

    public string StatusMessage
    {
        get
        {
            if (_session == null || _selector == null) return "Assign a session and an offer selector.";
            if (_selector.Session != _session) return "Manager and selector must use the same session.";
            if (_session.State == DeliverySessionState.Finished) return "Time is up! Restart to deliver again.";
            if (_session.IsPaused) return "Paused";
            if (!_session.CanInteract) return "Start the session to deliver.";
            switch (State)
            {
                case DeliveryState.PickingUp: return "Picking up the delivery...";
                case DeliveryState.Carrying: return IsAtDestination ? "Stop and hold to deliver." : "Fly to the delivery destination.";
                case DeliveryState.DroppingOff: return "Dropping off the delivery...";
                case DeliveryState.Renewing: return "Delivered! New offers coming...";
                default: return _selector.InteractionStatus;
            }
        }
    }

    private float SafeHoldSeconds => IsFinite(_dropOffHoldSeconds) && _dropOffHoldSeconds > 0f ? _dropOffHoldSeconds : 0.75f;
    private float RenewalDelay => SafeDelay(_renewalDelaySeconds);
    private DeliveryInteractionMode SequenceMode => State == DeliveryState.PickingUp ? _pickupMode : _dropOffMode;
    private float SequenceDelay => SafeDelay(State == DeliveryState.PickingUp ? _pickupDelaySeconds : _dropOffDelaySeconds);
    private bool CanRun => isActiveAndEnabled && _session != null && _selector != null
        && _selector.isActiveAndEnabled && _selector.Session == _session;
    private bool CanDropOff => IsAtDestination && IsFinite(_selector.CurrentSpeed)
        && _selector.CurrentSpeed <= _selector.MaximumSpeed;

    private void OnEnable()
    {
        if (_session == null && _selector != null) _session = _selector.Session;
        Subscribe();
        ClearDelivery(false, true);
    }

    private void OnDisable()
    {
        ClearDelivery(true, false);
    }

    // Keep listening to session resets while disabled, so its delivery count cannot
    // survive a new session simply because this component was temporarily off.
    private void OnDestroy() { Unsubscribe(); }

    private void Subscribe()
    {
        if (_subscribed || _session == null || _selector == null) return;
        _session.OnStarted.AddListener(HandleSessionStarted);
        _session.OnReset.AddListener(HandleSessionReset);
        _session.OnEnded.AddListener(HandleSessionEnded);
        _selector.OnOfferAccepted.AddListener(HandleOfferAccepted);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (_session != null)
        {
            _session.OnStarted.RemoveListener(HandleSessionStarted);
            _session.OnReset.RemoveListener(HandleSessionReset);
            _session.OnEnded.RemoveListener(HandleSessionEnded);
        }
        if (_selector != null) _selector.OnOfferAccepted.RemoveListener(HandleOfferAccepted);
        _subscribed = false;
    }

    // The session Update expires first; the selector LateUpdate samples movement next.
    private void LateUpdate()
    {
        if (!CanRun || !_session.isActiveAndEnabled || _session.State != DeliverySessionState.Running)
        {
            if (State != DeliveryState.Idle) ClearDelivery(true, true);
            return;
        }
        if ((State == DeliveryState.PickingUp || State == DeliveryState.Carrying || State == DeliveryState.DroppingOff)
            && (_currentOffer == null || !_currentOffer.IsAvailable || _selector.CurrentOffer != _currentOffer))
        {
            ClearDelivery(true, true);
            return;
        }
        // Pause freezes hold, sequence and renewal timers. Completion remains queued.
        if (!_session.CanInteract) return;
        float elapsed = Time.unscaledDeltaTime;
        switch (State)
        {
            case DeliveryState.PickingUp:
                TickSequence(elapsed);
                break;
            case DeliveryState.Carrying:
                _holdElapsed = CanDropOff ? _holdElapsed + elapsed : 0f;
                if (_holdElapsed >= SafeHoldSeconds)
                {
                    State = DeliveryState.DroppingOff;
                    BeginInteraction();
                    if (State == DeliveryState.DroppingOff) TickSequence(0f);
                }
                break;
            case DeliveryState.DroppingOff:
                if (!CanDropOff)
                {
                    InvalidateInteraction();
                    _holdElapsed = 0f;
                    State = DeliveryState.Carrying;
                    return;
                }
                TickSequence(elapsed);
                break;
            case DeliveryState.Renewing:
                _renewalElapsed += elapsed;
                if (_renewalElapsed >= RenewalDelay)
                {
                    State = DeliveryState.Idle;
                    _selector.ReleaseOffer(_lastCompletedOffer);
                    _selector.SetSelectionEnabled(true);
                }
                break;
        }
    }

    private void HandleOfferAccepted(DeliveryOffer offer)
    {
        if (!CanRun || !_session.CanInteract || State != DeliveryState.Idle
            || offer == null || !offer.IsAvailable || _selector.CurrentOffer != offer) return;
        _currentOffer = offer;
        _holdElapsed = 0f;
        _selector.SetSelectionEnabled(false);
        State = DeliveryState.PickingUp;
        BeginInteraction();
    }

    private void BeginInteraction()
    {
        InvalidateInteraction();
        _interactionActive = true;
        int token = _interactionToken;
        if (State == DeliveryState.PickingUp) _onPickupStarted.Invoke(token);
        else _onDropOffStarted.Invoke(token);
        // A listener may reset the session or complete a sequence immediately.
        if (!_interactionActive || _interactionToken != token) return;
        if (SequenceMode == DeliveryInteractionMode.Instant) CompleteInteraction(token);
    }

    /// <summary>
    /// Capture the token supplied by OnPickupStarted / OnDropOffStarted in your
    /// animation or dialogue callback, then pass that exact token here when it ends.
    /// Never look up CurrentInteractionToken later: a newer delivery may have begun.
    /// Completion is queued for LateUpdate so expiration always has priority.
    /// </summary>
    [Button("Complete Interaction"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void CompleteInteraction(int token)
    {
        if (!CanRun || !_session.IsRunning || !_interactionActive || token != _interactionToken
            || (State != DeliveryState.PickingUp && State != DeliveryState.DroppingOff)) return;
        _completionRequested = true;
    }

    private void TickSequence(float elapsed)
    {
        if (!_interactionActive) return;
        if (SequenceMode == DeliveryInteractionMode.DebugDelay)
        {
            _sequenceElapsed += elapsed;
            if (_sequenceElapsed >= SequenceDelay) _completionRequested = true;
        }
        if (!_completionRequested || !CanRun || !_session.CanInteract
            || _currentOffer == null || !_currentOffer.IsAvailable) return;
        if (State == DeliveryState.DroppingOff && !CanDropOff) return;
        DeliveryOffer offer = _currentOffer;
        DeliveryState completedState = State;
        InvalidateInteraction();
        if (completedState == DeliveryState.PickingUp)
        {
            State = DeliveryState.Carrying;
            _onPickedUp.Invoke(offer);
        }
        else if (completedState == DeliveryState.DroppingOff)
        {
            State = DeliveryState.Renewing;
            _currentOffer = null;
            _lastCompletedOffer = offer;
            _renewalElapsed = 0f;
            _holdElapsed = 0f;
            CompletedDeliveries++;
            _onDelivered.Invoke(offer);
        }
    }

    private void InvalidateInteraction()
    {
        _interactionActive = false;
        _completionRequested = false;
        _sequenceElapsed = 0f;
        unchecked { _interactionToken++; }
        if (_interactionToken == 0) _interactionToken = 1;
    }

    private void ClearDelivery(bool notify, bool enableSelection)
    {
        DeliveryOffer cancelledOffer = _currentOffer;
        InvalidateInteraction();
        State = DeliveryState.Idle;
        _currentOffer = null;
        _holdElapsed = 0f;
        _renewalElapsed = 0f;
        if (_selector != null)
        {
            _selector.ReleaseOffer();
            _selector.SetSelectionEnabled(enableSelection);
        }
        if (notify && cancelledOffer != null) _onCancelled.Invoke(cancelledOffer);
    }

    private void HandleSessionStarted()
    {
        CompletedDeliveries = 0;
        _lastCompletedOffer = null;
        ClearDelivery(true, isActiveAndEnabled);
    }

    private void HandleSessionReset() { HandleSessionStarted(); }
    private void HandleSessionEnded() { ClearDelivery(true, false); }
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float SafeDelay(float value) => IsFinite(value) && value >= 0f ? value : 1f;
}
