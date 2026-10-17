using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public sealed class DeliveryOfferAcceptedEvent : UnityEvent<DeliveryOffer> { }

[DisallowMultipleComponent]
public sealed class DeliveryOfferSelector : MonoBehaviour
{
    [SerializeField, Required] private DeliverySession _session;
    [SerializeField, Required] private Transform _player;
    [InfoBox("$ValidationMessage", InfoMessageType.Error, nameof(HasConfigurationError))]
    [SerializeField] private List<DeliveryOffer> _offers = new List<DeliveryOffer>();
    [SerializeField, AssetsOnly, Tooltip("Optional mesh / visual prefab. Its root is centered on the delivery point; materials, rotation and scale are preserved. Use child objects for visual offsets. Leave empty to use the default circle.")]
    private GameObject _selectionVisualPrefab;
    [SerializeField, Tooltip("Used only when Selection Visual Prefab is empty.")] private Material _circleMaterial;
    [SerializeField, MinValue(0.1f)] private float _holdSeconds = 1f;
    [SerializeField, MinValue(0f), SuffixLabel("units / second", true)] private float _maximumSpeed = 1.5f;
    [SerializeField, MinValue(0f)] private float _verticalTolerance = 2f;
    [SerializeField] private Rigidbody _playerRigidbody;
    [SerializeField] private Color _circleColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color _progressColor = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private DeliveryOfferAcceptedEvent _onOfferAccepted = new DeliveryOfferAcceptedEvent();

    private readonly List<DeliverySelectionCircle> _circles = new List<DeliverySelectionCircle>();
    private readonly List<DeliveryOffer> _circleOffers = new List<DeliveryOffer>();
    private Vector3 _previousPlayerPosition;
    private DeliveryOffer _candidateOffer;
    private DeliveryOffer _acceptedOffer;
    private float _selectionProgress;
    private float _externalSpeed;
    private bool _hasExternalSpeed;
    private bool _externalSpeedInvalid;
    private bool _offerAccepted;
    private bool _previousPositionReady;
    private bool _selectionEnabled = true;
    private DeliveryOffer _excludedOffer;

    public DeliverySession Session => _session;
    public Transform Player => _player;
    public IReadOnlyList<DeliveryOffer> Offers => _offers;
    public float CurrentSpeed { get; private set; } = float.PositiveInfinity;
    public float MaximumSpeed => _maximumSpeed;
    public float VerticalTolerance => _verticalTolerance;

    [ShowInInspector, ReadOnly] public DeliveryOffer CurrentOffer => _acceptedOffer;
    [ShowInInspector, ReadOnly] public DeliveryOffer CandidateOffer => _candidateOffer;
    [ShowInInspector, ReadOnly]
    public float SelectionProgress => _selectionProgress;
    public DeliveryOfferAcceptedEvent OnOfferAccepted => _onOfferAccepted;
    [ShowInInspector, ReadOnly] public string InteractionStatus => GetInteractionStatus();

    public string ValidationMessage
    {
        get
        {
            if (_session == null) return "Assign a DeliverySession.";
            if (!_session.IsValid) return "Set a valid session duration greater than zero.";
            if (_player == null) return "Assign the player Transform.";
            if (_selectionVisualPrefab == null && (_circleMaterial == null || _circleMaterial.shader == null))
                return "Assign a selection visual prefab or a valid circle material. Use Tools > Delivery > Repair Selected Selection Test if needed.";
            if (!IsFinite(_holdSeconds) || _holdSeconds <= 0f) return "Hold duration must be finite and greater than zero.";
            if (!IsFinite(_maximumSpeed) || _maximumSpeed < 0f) return "Maximum speed must be finite and zero or greater.";
            if (!IsFinite(_verticalTolerance) || _verticalTolerance < 0f) return "Vertical tolerance must be finite and zero or greater.";
            for (int i = 0; i < _offers.Count; i++)
                if (_offers[i] != null && _offers[i].IsAvailable) return string.Empty;
            return "Assign at least one valid, enabled DeliveryOffer.";
        }
    }

    private bool HasConfigurationError => !string.IsNullOrEmpty(ValidationMessage);

    private void OnEnable()
    {
        if (Application.isPlaying && HasConfigurationError)
            Debug.LogError("Delivery offer selector configuration: " + ValidationMessage, this);
        BuildCircles();
    }

    private void Awake()
    {
        if (_session == null) return;
        _session.OnStarted.AddListener(HandleSessionStarted);
        _session.OnReset.AddListener(HandleSessionReset);
        _session.OnEnded.AddListener(HandleSessionEnded);
    }

    private void OnDestroy()
    {
        if (_session != null)
        {
            _session.OnStarted.RemoveListener(HandleSessionStarted);
            _session.OnReset.RemoveListener(HandleSessionReset);
            _session.OnEnded.RemoveListener(HandleSessionEnded);
        }
        for (int i = 0; i < _circles.Count; i++)
        {
            if (_circles[i] == null) continue;
            if (Application.isPlaying) Destroy(_circles[i].gameObject);
            else DestroyImmediate(_circles[i].gameObject);
        }
        _circles.Clear();
        _circleOffers.Clear();
    }

    private void OnDisable()
    {
        ReleaseOffer();
        _previousPositionReady = false;
        CurrentSpeed = float.PositiveInfinity;
        SetCirclesVisible(false);
    }

    // Sample after movement controllers and the session timer have updated this frame.
    private void LateUpdate()
    {
        // Keep the destination speed usable after an offer has been accepted.
        CurrentSpeed = GetPlayerSpeed();
        if (!ShouldShowCircles())
        {
            ClearCandidate();
            SetCirclesVisible(false);
            return;
        }

        SetCirclesVisible(true);
        if (_session.IsPaused)
        {
            RefreshCircleColors();
            return;
        }
        if (!_session.CanInteract || _player == null)
        {
            ClearCandidate();
            RefreshCircleColors();
            return;
        }

        float speed = CurrentSpeed;
        DeliveryOffer candidate = !HasConfigurationError && IsFinite(speed) && speed <= _maximumSpeed
            ? FindCandidate() : null;
        if (candidate == null)
        {
            ClearCandidate();
            RefreshCircleColors();
            return;
        }

        if (_candidateOffer != candidate)
        {
            _candidateOffer = candidate;
            _selectionProgress = 0f;
        }

        _selectionProgress += Time.unscaledDeltaTime / _holdSeconds;
        RefreshCircleColors();
        if (_selectionProgress < 1f)
            return;

        if (!_session.CanInteract || !candidate.IsAvailable || !IsInsideDeparture(candidate))
        {
            ClearCandidate();
            return;
        }

        _acceptedOffer = candidate;
        _offerAccepted = true;
        ClearCandidate();
        SetCirclesVisible(false);
        _onOfferAccepted.Invoke(candidate);
    }

    // A movement controller may push its current scalar speed here each frame.
    // Otherwise an assigned Rigidbody is preferred, then Transform displacement is used.
    public void SetExternalSpeed(float speed)
    {
        _hasExternalSpeed = true;
        _externalSpeedInvalid = !IsFinite(speed) || speed < 0f;
        _externalSpeed = _externalSpeedInvalid ? 0f : speed;
        if (_externalSpeedInvalid) _previousPositionReady = false;
    }

    public void ClearExternalSpeed()
    {
        _hasExternalSpeed = false;
        _externalSpeedInvalid = false;
        _previousPositionReady = false;
    }

    public void SetSelectionEnabled(bool enabled)
    {
        _selectionEnabled = enabled;
        if (!enabled)
        {
            ClearCandidate();
            SetCirclesVisible(false);
        }
    }

    // The completed offer is temporarily omitted only while another valid offer exists.
    public void ReleaseOffer(DeliveryOffer excludedOffer = null)
    {
        _acceptedOffer = null;
        _offerAccepted = false;
        _excludedOffer = excludedOffer;
        ClearCandidate();
    }

    private float GetPlayerSpeed()
    {
        if (_player == null)
        {
            _previousPositionReady = false;
            return float.PositiveInfinity;
        }
        if (_hasExternalSpeed)
            return _externalSpeedInvalid ? float.PositiveInfinity : _externalSpeed;
        if (_playerRigidbody != null)
        {
            float rigidbodySpeed = _playerRigidbody.linearVelocity.magnitude;
            if (!IsFinite(rigidbodySpeed))
            {
                _previousPositionReady = false;
                return float.PositiveInfinity;
            }
            return rigidbodySpeed;
        }

        Vector3 position = _player.position;
        if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
        {
            _previousPositionReady = false;
            return float.PositiveInfinity;
        }
        if (!_previousPositionReady || Time.unscaledDeltaTime <= 0f)
        {
            _previousPlayerPosition = position;
            _previousPositionReady = true;
            return float.PositiveInfinity;
        }
        float speed = Vector3.Distance(position, _previousPlayerPosition) / Time.unscaledDeltaTime;
        _previousPlayerPosition = position;
        _previousPositionReady = true;
        if (!IsFinite(speed))
        {
            _previousPositionReady = false;
            return float.PositiveInfinity;
        }
        return speed;
    }

    private DeliveryOffer FindCandidate()
    {
        if (IsEligible(_candidateOffer))
            return _candidateOffer;

        DeliveryOffer closest = null;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < _offers.Count; i++)
        {
            DeliveryOffer offer = _offers[i];
            if (!IsEligible(offer))
                continue;
            float distance = (offer.Departure.Position - _player.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = offer;
                closestDistance = distance;
            }
        }
        return closest;
    }

    private bool IsEligible(DeliveryOffer offer)
    {
        return IsSelectable(offer) && IsInsideDeparture(offer);
    }

    private bool IsSelectable(DeliveryOffer offer)
    {
        if (offer == null || !offer.IsAvailable) return false;
        if (offer != _excludedOffer) return true;
        for (int i = 0; i < _offers.Count; i++)
            if (_offers[i] != null && _offers[i] != offer && _offers[i].IsAvailable)
                return false;
        return true;
    }

    private bool IsInsideDeparture(DeliveryOffer offer)
    {
        if (offer == null || offer.Departure == null || _player == null)
            return false;
        Vector3 delta = _player.position - offer.Departure.Position;
        return Mathf.Abs(delta.y) <= _verticalTolerance
            && delta.x * delta.x + delta.z * delta.z <= offer.Departure.Radius * offer.Departure.Radius;
    }

    private void BuildCircles()
    {
        for (int i = 0; i < _circles.Count; i++)
            if (_circles[i] != null)
            {
                _circles[i].gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_circles[i].gameObject);
                else DestroyImmediate(_circles[i].gameObject);
            }
        _circles.Clear();
        _circleOffers.Clear();
        if (_selectionVisualPrefab == null && (_circleMaterial == null || _circleMaterial.shader == null))
            return;
        for (int i = 0; i < _offers.Count; i++)
        {
            DeliveryOffer offer = _offers[i];
            if (offer == null || offer.Departure == null)
                continue;
            GameObject circleObject = new GameObject("Selection - " + offer.Departure.name);
            circleObject.transform.SetParent(transform, true);
            DeliverySelectionCircle circle = circleObject.AddComponent<DeliverySelectionCircle>();
            circle.Configure(offer.Departure, _circleMaterial, _circleColor, _selectionVisualPrefab);
            _circles.Add(circle);
            _circleOffers.Add(offer);
        }
        SetCirclesVisible(false);
    }

    private void RefreshCircleColors()
    {
        for (int i = 0; i < _circles.Count; i++)
        {
            DeliverySelectionCircle circle = _circles[i];
            if (circle == null) continue;
            float progress = i < _circleOffers.Count && _candidateOffer != null
                && _circleOffers[i] == _candidateOffer
                ? _selectionProgress : 0f;
            circle.SetProgress(progress, _circleColor, _progressColor);
        }
    }

    private void SetCirclesVisible(bool visible)
    {
        for (int i = 0; i < _circles.Count; i++)
            if (_circles[i] != null)
            {
                bool offerAvailable = i < _circleOffers.Count && IsSelectable(_circleOffers[i]);
                _circles[i].gameObject.SetActive(visible && offerAvailable);
            }
    }

    private bool ShouldShowCircles()
    {
        return _selectionEnabled && !_offerAccepted && _session != null && _session.isActiveAndEnabled
            && _session.State != DeliverySessionState.Finished;
    }

    private string GetInteractionStatus()
    {
        if (HasConfigurationError) return ValidationMessage;
        if (_offerAccepted) return "Offer accepted. See On Offer Accepted for the result.";
        if (!_selectionEnabled) return "Delivery in progress.";
        if (_session == null || !_session.isActiveAndEnabled) return "Enable the session to use offers.";
        if (_session.State == DeliverySessionState.Finished) return "Session finished. Restart the session to select another offer.";
        if (_session.IsPaused) return "Paused: selection is suspended.";
        if (!_session.CanInteract) return "Ready: start the session to accept an offer.";
        if (_candidateOffer != null) return "Hold still in the departure circle: " + Mathf.Clamp01(_selectionProgress).ToString("P0");
        return "Move into an available departure circle and stop.";
    }

    private void ClearCandidate()
    {
        _candidateOffer = null;
        _selectionProgress = 0f;
    }

    private void HandleSessionStarted() { ReleaseOffer(); _previousPositionReady = false; }
    private void HandleSessionReset() { ReleaseOffer(); _previousPositionReady = false; }
    private void HandleSessionEnded() { ReleaseOffer(); SetCirclesVisible(false); }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
