using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>Delivery-specific sequencing. ManagerCinematic only handles the shot.</summary>
[DisallowMultipleComponent]
public class DeliveryPickupCinematic : MonoBehaviour
{
    [field: SerializeField, Required]
    public ManagerCinematic CinematicManager { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_deliveryManager"), Required]
    public DeliveryManager DeliveryManager { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_defaultSendOffSeconds"), MinValue(0f), SuffixLabel("s", true)]
    public float DefaultSendOffSeconds { get; private set; } = 1f;

    [field: SerializeField, FormerlySerializedAs("_waitForSendOffSignal")]
    public bool WaitForSendOffSignal { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_onCinematicReady")]
    public UnityEvent<int> OnCinematicReady { get; private set; } = new UnityEvent<int>();

    [field: SerializeField, FormerlySerializedAs("_onSendOffStarted")]
    public UnityEvent<int> OnSendOffStarted { get; private set; } = new UnityEvent<int>();

    [field: SerializeField, FormerlySerializedAs("_onCinematicEnded")]
    public UnityEvent<int> OnCinematicEnded { get; private set; } = new UnityEvent<int>();

    private enum Phase { Idle, Entering, Waiting, SendOff, Returning }
    private Phase _phase;
    private DeliverySession _session;
    private DeliveryOffer _offer;
    private DeliveryPointCinematic _actor;
    private Coroutine _routine;
    private int _token;
    private bool _wasPaused;
    private bool _sendOffFinished;

    [ShowInInspector, ReadOnly] public bool IsPlaying => _phase != Phase.Idle;
    [ShowInInspector, ReadOnly] public int CurrentToken => IsPlaying ? _token : 0;

    // Wire DeliveryManager.OnPickupStarted using the dynamic int entry.
    public void BeginPickUp(int token)
    {
        if (!isActiveAndEnabled) return;
        if (DeliveryManager == null) DeliveryManager = global::DeliveryManager.instance;
        if (DeliveryManager == null || DeliveryManager.State != DeliveryState.PickingUp
            || token == 0 || token != DeliveryManager.CurrentInteractionToken) return;
        DeliveryOffer offer = DeliveryManager.CurrentOffer;
        if (offer == null || !offer.IsAvailable || DeliveryManager.Session == null
            || !DeliveryManager.Session.IsRunning) return;
        // A session can restart while the previous shot is still returning. Do not
        // leave a newer External pickup waiting for a request we cannot accept.
        if (IsPlaying)
        {
            if (token != _token) DeliveryManager.CompleteInteraction(token);
            return;
        }

        _session = DeliveryManager.Session;
        _offer = offer;
        _actor = offer.Departure.GetComponent<DeliveryPointCinematic>();
        _token = token;
        _wasPaused = _session.IsPaused;
        _sendOffFinished = false;
        _phase = Phase.Entering;
        _session.OnReset.AddListener(CancelPickup);
        _session.OnEnded.AddListener(CancelPickup);
        DeliveryManager.OnCancelled.AddListener(HandleCancelled);
        _session.SetPaused(true);

        if (CinematicManager == null || !CinematicManager.BeginCinematic(offer.Departure.CameraCinematic,
                () => HandleReady(token), () => HandleEnded(token)))
        {
            Debug.LogWarning("Pickup cinematic unavailable: completing the pickup without a cinematic.", this);
            HandleEnded(token);
        }
    }

    private void HandleReady(int token)
    {
        if (_phase != Phase.Entering || token != _token) return;
        if (!HasValidInteraction()) { CancelPickup(); return; }
        _phase = Phase.Waiting;
        OnCinematicReady.Invoke(token);
    }

    public void ContinuePickup(int token)
    {
        if (_phase != Phase.Waiting || token != _token || !HasValidInteraction()) return;
        _phase = Phase.SendOff;
        if (_actor != null) _actor.PlaySendOff();
        OnSendOffStarted.Invoke(token);
        if (_phase == Phase.SendOff && token == _token) _routine = StartCoroutine(SendOff(token));
    }

    public void FinishSendOff(int token)
    {
        if (_phase == Phase.SendOff && token == _token && HasValidInteraction()) _sendOffFinished = true;
    }

    private IEnumerator SendOff(int token)
    {
        // Ensure the coroutine handle is assigned even if the end signal is immediate.
        yield return null;
        if (WaitForSendOffSignal)
            while (!_sendOffFinished) yield return null;
        else
            yield return new WaitForSecondsRealtime(_actor != null ? _actor.SendOffSeconds : DefaultSendOffSeconds);

        _routine = null;
        if (token != _token || !IsPlaying) yield break;
        _phase = Phase.Returning;
        CinematicManager.CancelCinematic();
    }

    public void CancelPickup()
    {
        if (!IsPlaying) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        _phase = Phase.Returning;
        if (CinematicManager != null && CinematicManager.IsPlaying) CinematicManager.CancelCinematic();
        else HandleEnded(_token);
    }

    private bool HasValidInteraction()
    {
        return DeliveryManager != null && DeliveryManager.isActiveAndEnabled
            && DeliveryManager.Selector != null && DeliveryManager.Selector.isActiveAndEnabled
            && _session != null && _session.IsRunning && _offer != null && _offer.IsAvailable
            && DeliveryManager.State == DeliveryState.PickingUp
            && DeliveryManager.CurrentOffer == _offer && DeliveryManager.CurrentInteractionToken == _token;
    }

    private void HandleEnded(int token)
    {
        if (!IsPlaying || token != _token) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (HasValidInteraction()) DeliveryManager.CompleteInteraction(token);
        if (_session != null)
        {
            _session.OnReset.RemoveListener(CancelPickup);
            _session.OnEnded.RemoveListener(CancelPickup);
            _session.SetPaused(_wasPaused);
        }
        if (DeliveryManager != null) DeliveryManager.OnCancelled.RemoveListener(HandleCancelled);
        _phase = Phase.Idle;
        _token = 0;
        _session = null;
        _offer = null;
        _actor = null;
        OnCinematicEnded.Invoke(token);
    }

    private void Update()
    {
        if (IsPlaying && !HasValidInteraction()) CancelPickup();
    }

    private void OnDisable() => CancelPickup();
    private void HandleCancelled(DeliveryOffer offer) { if (offer == _offer) CancelPickup(); }
}
