using UnityEngine;
using UnityEngine.Rendering;

// HUD compass: a 3D arrow parented to the Main Camera, kept at top-center of the screen,
// pointing from the player toward the active delivery's destination.
// Never deactivate this GameObject: toggle visibility with SetArrowVisible instead.
[DisallowMultipleComponent]
public sealed class DeliveryCompassArrow : MonoBehaviour
{
    [Header("References")]
    [SerializeField, Tooltip("Falls back to DeliveryManager.instance when empty.")]
    private DeliveryManager _manager;
    [SerializeField, Tooltip("Falls back to the parent Camera, then Camera.main.")]
    private Camera _camera;
    [SerializeField, Tooltip("Child shown / hidden. Must not be this GameObject.")]
    private GameObject _visual;
    [SerializeField, Tooltip("Rotated by code. The mesh inside must point toward +Z.")]
    private Transform _arrowPivot;

    [Header("Placement")]
    [SerializeField, Range(0f, 1f)] private float _viewportY = 0.85f;
    [SerializeField, Min(0.05f), Tooltip("Meters in front of the camera.")] private float _distance = 2f;
    [SerializeField, Min(0.001f)] private float _baseScale = 0.15f;
    [SerializeField, Tooltip("Off: the arrow shrinks / grows with FOV changes. On: same size on screen at any FOV.")]
    private bool _keepConstantScreenSize = false;
    [SerializeField, Range(1f, 179f), Tooltip("FOV at which Base Scale applies when Keep Constant Screen Size is on.")]
    private float _referenceFov = 60f;

    [Header("Pointing")]
    [SerializeField, Range(0f, 89f), Tooltip("Max pitch above / below the horizontal plane.")]
    private float _maxPitch = 70f;
    [SerializeField, Min(1f), Tooltip("Degrees per second.")] private float _turnSpeed = 540f;

    [Header("Show Pop")]
    [SerializeField, Min(0f)] private float _popDuration = 0.25f;
    [SerializeField] private AnimationCurve _popCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.6f, 1.15f), new Keyframe(1f, 1f));

    [Header("Pulse")]
    [SerializeField, Min(0f), Tooltip("Extra scale at the peak of a pulse when far (0.1 = +10%). Always active.")]
    private float _pulseBaseAmplitude = 0.1f;
    [SerializeField, Min(0f), Tooltip("Pulses per second when far. Always active.")]
    private float _pulseBaseFrequency = 1f;
    [SerializeField, Min(0f), Tooltip("Pulse starts ramping up below this distance to the destination (meters).")]
    private float _pulseRampStartDistance = 30f;
    [SerializeField, Min(0f), Tooltip("Pulse reaches max intensity below this distance (meters).")]
    private float _pulseRampFullDistance = 10f;
    [SerializeField, Min(0f), Tooltip("Extra scale at the peak of a pulse when close (0.3 = +30%).")]
    private float _pulseMaxAmplitude = 0.3f;
    [SerializeField, Min(0f), Tooltip("Pulses per second when close. Set equal to Base Frequency to only ramp the size.")]
    private float _pulseMaxFrequency = 3f;

    private Transform _target;
    private bool _allowedVisible = true;
    private bool _snapNextFrame;
    private bool _subscribed;
    private float _popElapsed;
    private Vector3 _visualBaseScale = Vector3.one;
    private Vector3 _pivotBaseScale = Vector3.one;
    private float _pulsePhase;

    public bool HasTarget => _target != null;

    /// <summary>
    /// false hides the arrow even during a delivery. true allows it again;
    /// it only actually shows while a delivery is being carried.
    /// </summary>
    public void SetArrowVisible(bool visible)
    {
        if (_allowedVisible == visible) return;
        _allowedVisible = visible;
        RefreshVisual(true);
    }

    private void Awake()
    {
        if (_camera == null) _camera = GetComponentInParent<Camera>();
        if (_camera == null) _camera = Camera.main;
        if (_visual != null) _visualBaseScale = _visual.transform.localScale;
        if (_arrowPivot != null) _pivotBaseScale = _arrowPivot.localScale;
        if (_visual == gameObject)
        {
            Debug.LogError("Compass arrow: Visual must be a child, not the controller's own GameObject.", this);
            _visual = null;
        }
        RefreshVisual(false);
    }

    // The manager registers its singleton in OnEnable with a late execution order,
    // so subscribe in Start where it is guaranteed to exist.
    private void Start()
    {
        if (_manager == null) _manager = DeliveryManager.instance;
        if (_manager == null)
        {
            Debug.LogError("Compass arrow: no DeliveryManager assigned or found.", this);
            return;
        }
        _manager.OnPickedUp.AddListener(HandlePickedUp);
        _manager.OnDelivered.AddListener(HandleEnded);
        _manager.OnCancelled.AddListener(HandleEnded);
        _subscribed = true;

        // In case a delivery was already being carried before this component started.
        if ((_manager.State == DeliveryState.Carrying || _manager.State == DeliveryState.DroppingOff)
            && _manager.CurrentOffer != null)
            HandlePickedUp(_manager.CurrentOffer);
    }

    private void OnDestroy()
    {
        if (!_subscribed || _manager == null) return;
        _manager.OnPickedUp.RemoveListener(HandlePickedUp);
        _manager.OnDelivered.RemoveListener(HandleEnded);
        _manager.OnCancelled.RemoveListener(HandleEnded);
    }

    // Applied right before the camera renders, after Cinemachine has moved it and
    // set its FOV this frame, so the arrow never lags a frame behind.
    private void OnEnable() => RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
    private void OnDisable() => RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;

    private void HandlePickedUp(DeliveryOffer offer)
    {
        _target = offer != null && offer.Destination != null ? offer.Destination.transform : null;
        _snapNextFrame = true;
        RefreshVisual(true);
    }

    private void HandleEnded(DeliveryOffer offer)
    {
        _target = null;
        RefreshVisual(false);
    }

    private void RefreshVisual(bool pop)
    {
        if (_visual == null) return;
        bool show = _allowedVisible && _target != null;
        if (show && pop && !_visual.activeSelf) _popElapsed = 0f;
        else if (!show) _popElapsed = _popDuration;
        if (_visual.activeSelf != show) _visual.SetActive(show);
        if (!show)
        {
            _visual.transform.localScale = _visualBaseScale;
            if (_arrowPivot != null) _arrowPivot.localScale = _pivotBaseScale;
            _pulsePhase = 0f;
        }
    }

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != _camera || _camera == null) return;
        if (_visual == null || !_visual.activeSelf) return;
        float deltaTime = Time.unscaledDeltaTime;
        UpdatePlacement();
        UpdatePop(deltaTime);
        UpdatePulse(deltaTime);
        UpdateRotation(deltaTime);
    }

    private void UpdatePlacement()
    {
        float halfFovTan = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 viewOffset = new Vector3(0f, halfFovTan * _distance * (2f * _viewportY - 1f), _distance);
        // Placed in camera space, so this works whatever the parent hierarchy is.
        transform.SetPositionAndRotation(
            _camera.transform.TransformPoint(viewOffset), _camera.transform.rotation);

        float scale = _baseScale;
        if (_keepConstantScreenSize)
            scale *= halfFovTan / Mathf.Tan(_referenceFov * 0.5f * Mathf.Deg2Rad);
        transform.localScale = Vector3.one * scale;
    }

    private void UpdatePop(float deltaTime)
    {
        if (_popElapsed >= _popDuration) return;
        _popElapsed += deltaTime;
        float t = _popDuration > 0f ? Mathf.Clamp01(_popElapsed / _popDuration) : 1f;
        _visual.transform.localScale = _visualBaseScale * _popCurve.Evaluate(t);
    }

    // Scales ArrowPivot (the pop scales Visual), so both animations stack.
    private void UpdatePulse(float deltaTime)
    {
        if (_arrowPivot == null || _target == null || _manager == null || _manager.Player == null) return;
        float distance = Vector3.Distance(_target.position, _manager.Player.position);
        // 0 at Ramp Start Distance and beyond, 1 at Ramp Full Distance and closer.
        float closeness = 1f - Mathf.InverseLerp(_pulseRampFullDistance, _pulseRampStartDistance, distance);
        float amplitude = Mathf.Lerp(_pulseBaseAmplitude, _pulseMaxAmplitude, closeness);
        float frequency = Mathf.Lerp(_pulseBaseFrequency, _pulseMaxFrequency, closeness);
        // Accumulate the phase so changing the frequency never makes the pulse jump.
        _pulsePhase = Mathf.Repeat(_pulsePhase + frequency * deltaTime, 1f);
        float wave = 0.5f - 0.5f * Mathf.Cos(_pulsePhase * 2f * Mathf.PI); // 0 → 1 → 0, only grows
        _arrowPivot.localScale = _pivotBaseScale * (1f + amplitude * wave);
    }

    private void UpdateRotation(float deltaTime)
    {
        if (_arrowPivot == null || _target == null || _manager == null || _manager.Player == null) return;
        Vector3 direction = _target.position - _manager.Player.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        // Yaw from the horizontal direction; if the target is straight above / below, keep the last yaw.
        Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
        float horizontalLength = horizontal.magnitude;
        if (horizontalLength < 0.001f)
        {
            horizontal = Vector3.ProjectOnPlane(_arrowPivot.forward, Vector3.up);
            if (horizontal.sqrMagnitude < 0.0001f) return;
        }
        // Pitch relative to world up, clamped so the arrow never points fully vertical.
        float pitch = Mathf.Atan2(direction.y, horizontalLength) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, -_maxPitch, _maxPitch);
        Quaternion desired = Quaternion.LookRotation(horizontal.normalized, Vector3.up)
            * Quaternion.Euler(-pitch, 0f, 0f);

        if (_snapNextFrame)
        {
            _arrowPivot.rotation = desired;
            _snapNextFrame = false;
        }
        else
        {
            _arrowPivot.rotation = Quaternion.RotateTowards(_arrowPivot.rotation, desired, _turnSpeed * deltaTime);
        }
    }
}
