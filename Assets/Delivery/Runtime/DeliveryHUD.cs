using TMPro;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class DeliveryHUD : MonoBehaviour
{
    [SerializeField] private DeliveryManager _manager;
    [SerializeField] private TMP_Text _objectiveText;
    [SerializeField] private TMP_Text _cargoText;
    [SerializeField] private TMP_Text _progressText;
    [SerializeField] private TMP_Text _notificationText;
    [SerializeField] private RectTransform _notificationTransform;
    [SerializeField] private TMP_Text _deliveriesText;
    [SerializeField, Min(0.1f)] private float _notificationSeconds = 2f;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pickupClip;
    [SerializeField] private AudioClip _deliveryClip;
    [SerializeField] private AudioClip _cancelClip;

    private float _notificationTime;
    private string _lastObjective;
    private string _lastCargo;
    private string _lastProgress;
    private string _lastDeliveries;
    private Tween _notificationTween;
    private Vector3 _notificationScale = Vector3.one;

    private void OnEnable()
    {
        if (_notificationTransform == null && _notificationText != null)
            _notificationTransform = _notificationText.rectTransform;
        if (_notificationTransform != null) _notificationScale = _notificationTransform.localScale;
        Subscribe(true);
        Refresh(true);
        ShowNotification(_manager != null && _manager.Session != null
            && _manager.Session.State == DeliverySessionState.Finished ? "Time's up" : "Ready");
    }

    private void OnDisable()
    {
        Subscribe(false);
        KillNotificationTween();
        if (_notificationText != null) _notificationText.text = string.Empty;
        if (_notificationTransform != null) _notificationTransform.localScale = _notificationScale;
    }

    private void LateUpdate()
    {
        Refresh(false);
        if (_notificationTime <= 0f || _notificationText == null) return;
        _notificationTime -= Time.unscaledDeltaTime;
        if (_notificationTime <= 0f)
        {
            _notificationText.text = string.Empty;
            KillNotificationTween();
            if (_notificationTransform != null) _notificationTransform.localScale = _notificationScale;
        }
    }

    private void Subscribe(bool add)
    {
        if (_manager == null) return;
        if (add)
        {
            _manager.OnPickedUp.AddListener(HandlePickedUp);
            _manager.OnDelivered.AddListener(HandleDelivered);
            _manager.OnCancelled.AddListener(HandleCancelled);
            if (_manager.Session != null)
            {
                _manager.Session.OnStarted.AddListener(HandleSessionStarted);
                _manager.Session.OnReset.AddListener(HandleSessionReset);
                _manager.Session.OnEnded.AddListener(HandleSessionEnded);
            }
        }
        else
        {
            _manager.OnPickedUp.RemoveListener(HandlePickedUp);
            _manager.OnDelivered.RemoveListener(HandleDelivered);
            _manager.OnCancelled.RemoveListener(HandleCancelled);
            if (_manager.Session != null)
            {
                _manager.Session.OnStarted.RemoveListener(HandleSessionStarted);
                _manager.Session.OnReset.RemoveListener(HandleSessionReset);
                _manager.Session.OnEnded.RemoveListener(HandleSessionEnded);
            }
        }
    }

    private void HandlePickedUp(DeliveryOffer offer) => Play(_pickupClip, "Delivery picked up");
    private void HandleDelivered(DeliveryOffer offer) => Play(_deliveryClip, "Delivery complete");
    private void HandleCancelled(DeliveryOffer offer)
    {
        if (_manager != null && _manager.Session != null && _manager.Session.State == DeliverySessionState.Finished)
        {
            ShowNotification("Time's up");
            Refresh(true);
            return;
        }
        Play(_cancelClip, "Delivery cancelled");
    }
    private void HandleSessionStarted() { ShowNotification("Deliveries available"); Refresh(true); }
    private void HandleSessionReset() { ShowNotification("Ready"); Refresh(true); }
    private void HandleSessionEnded() { ShowNotification("Time's up"); Refresh(true); }

    private void Play(AudioClip clip, string message)
    {
        ShowNotification(message);
        if (_audioSource != null && clip != null) _audioSource.PlayOneShot(clip);
        Refresh(true);
    }

    private void Refresh(bool force)
    {
        string objective = GetObjective();
        string cargo = GetCargo();
        string progress = GetProgress();
        string deliveries = _manager == null ? string.Empty : $"Deliveries: {_manager.CompletedDeliveries}";
        SetText(_objectiveText, objective, ref _lastObjective, force);
        SetText(_cargoText, cargo, ref _lastCargo, force);
        SetText(_progressText, progress, ref _lastProgress, force);
        SetText(_deliveriesText, deliveries, ref _lastDeliveries, force);
    }

    private string GetObjective()
    {
        if (_manager == null || _manager.Session == null) return string.Empty;
        if (!_manager.isActiveAndEnabled) return "Deliveries unavailable";
        if (!_manager.Session.isActiveAndEnabled) return "Session unavailable";
        if (_manager.Session.State == DeliverySessionState.Finished) return "Time's up";
        if (_manager.Session.State == DeliverySessionState.Ready) return "Press Start to begin";
        if (_manager.Session.IsPaused) return "Paused";
        switch (_manager.State)
        {
            case DeliveryState.PickingUp: return "Picking up...";
            case DeliveryState.Carrying:
            case DeliveryState.DroppingOff:
                DeliveryOffer offer = _manager.CurrentOffer;
                if (offer == null || offer.Destination == null) return "Find your destination";
                if (_manager.State == DeliveryState.DroppingOff) return "Delivering...";
                return $"Deliver {offer.Label} to {offer.Destination.DisplayName}";
            case DeliveryState.Renewing: return "Preparing new deliveries...";
            default:
                if (_manager.Selector == null || !_manager.Selector.isActiveAndEnabled
                    || !string.IsNullOrEmpty(_manager.Selector.ValidationMessage)) return "No deliveries available";
                return "Move into a blue circle and stop.";
        }
    }

    private string GetCargo()
    {
        if (_manager == null || _manager.Session == null || _manager.Session.State != DeliverySessionState.Running
            || _manager.Session.IsPaused || _manager.State == DeliveryState.Idle) return string.Empty;
        DeliveryOffer offer = _manager.CurrentOffer;
        return offer != null ? offer.Label : string.Empty;
    }

    private string GetProgress()
    {
        if (_manager == null || _manager.Session == null || !_manager.Session.CanInteract) return string.Empty;
        if (_manager.State == DeliveryState.Carrying && !_manager.IsAtDestination) return "Stop in the destination circle...";
        if (_manager.State == DeliveryState.Carrying) return $"{Mathf.RoundToInt(Mathf.Clamp01(_manager.InteractionProgress) * 100f)}%";
        if (_manager.State != DeliveryState.PickingUp && _manager.State != DeliveryState.DroppingOff) return string.Empty;
        return $"{Mathf.RoundToInt(Mathf.Clamp01(_manager.InteractionProgress) * 100f)}%";
    }

    private void ShowNotification(string message)
    {
        if (_notificationText == null) return;
        KillNotificationTween();
        _notificationText.text = message;
        _notificationTime = Mathf.Max(0.1f, _notificationSeconds);
        if (_notificationTransform != null)
        {
            _notificationTransform.localScale = _notificationScale;
            _notificationTween = _notificationTransform.DOPunchScale(_notificationScale * 0.12f, 0.25f, 1, 0.5f)
                .SetUpdate(true);
        }
    }

    private void KillNotificationTween()
    {
        if (_notificationTween != null && _notificationTween.IsActive()) _notificationTween.Kill();
        _notificationTween = null;
    }

    private static void SetText(TMP_Text target, string value, ref string previous, bool force)
    {
        if (target == null || (!force && previous == value)) return;
        target.text = value;
        previous = value;
    }
}
