using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Uses the delivery compass visual with the standalone test's current target.</summary>
[DisallowMultipleComponent]
public class RandomFlightCompassArrow : MonoBehaviour
{
    [field: SerializeField, BoxGroup("References"), Required]
    public RandomFlightTargetTest Objectives { get; private set; }

    [field: SerializeField, BoxGroup("References"), Required]
    public Camera ViewCamera { get; private set; }

    [field: SerializeField, BoxGroup("References"), Required, ChildGameObjectsOnly]
    public GameObject Visual { get; private set; }

    [field: SerializeField, BoxGroup("References"), Required, ChildGameObjectsOnly]
    public Transform ArrowPivot { get; private set; }

    [field: SerializeField, BoxGroup("Placement"), Range(0f, 1f)]
    public float ViewportY { get; private set; } = 0.93f;

    [field: SerializeField, BoxGroup("Placement"), MinValue(0.05f)]
    public float Distance { get; private set; } = 2f;

    [field: SerializeField, BoxGroup("Placement"), MinValue(0.001f)]
    public float BaseScale { get; private set; } = 0.13f;

    [field: SerializeField, BoxGroup("Placement")]
    public bool KeepConstantScreenSize { get; private set; } = true;

    [field: SerializeField, BoxGroup("Placement"), Range(1f, 179f)]
    public float ReferenceFov { get; private set; } = 60f;

    [field: SerializeField, BoxGroup("Direction"), Range(0f, 89f)]
    public float MaxPitch { get; private set; } = 60f;

    [field: SerializeField, BoxGroup("Direction"), MinValue(1f)]
    public float TurnSpeed { get; private set; } = 350f;

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PopDuration { get; private set; } = 0.25f;

    [field: SerializeField, BoxGroup("Animation")]
    public AnimationCurve PopCurve { get; private set; } = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.6f, 1.15f), new Keyframe(1f, 1f));

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PulseBaseAmplitude { get; private set; } = 0.15f;

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PulseBaseFrequency { get; private set; } = 1f;

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PulseRampStartDistance { get; private set; } = 30f;

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PulseRampFullDistance { get; private set; } = 3f;

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PulseMaxAmplitude { get; private set; } = 0.3f;

    [field: SerializeField, BoxGroup("Animation"), MinValue(0f)]
    public float PulseMaxFrequency { get; private set; } = 3f;

    private Vector3 _visualScale;
    private Vector3 _pivotScale;
    private int _targetVersion = -1;
    private float _popElapsed;
    private float _pulsePhase;
    private bool _snap;
    private int _animatedFrame = -1;

    private void Awake()
    {
        if (ViewCamera == null)
            ViewCamera = GetComponentInParent<Camera>();
        if (ViewCamera == null)
            ViewCamera = Camera.main;
        if (Visual == null || Visual == gameObject || ArrowPivot == null || Objectives == null
            || !Visual.transform.IsChildOf(transform) || !ArrowPivot.IsChildOf(Visual.transform)
            || ArrowPivot == Visual.transform)
        {
            Debug.LogError("Flight compass : assigner le test, un enfant Visual et son enfant ArrowPivot.", this);
            enabled = false;
            return;
        }
        _visualScale = Visual.transform.localScale;
        _pivotScale = ArrowPivot.localScale;
        Visual.SetActive(false);
    }

    private void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        if (Visual != null && Visual != gameObject)
            Visual.SetActive(false);
    }

    private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (ViewCamera == null)
            ViewCamera = Camera.main;
        if (camera != ViewCamera || Visual == null || Objectives == null)
            return;

        Transform target = Objectives.isActiveAndEnabled ? Objectives.CurrentTarget : null;
        bool visible = target != null && Objectives.Player != null;
        if (Visual.activeSelf != visible)
            Visual.SetActive(visible);
        if (!visible)
            return;

        if (_targetVersion != Objectives.TargetVersion)
        {
            _targetVersion = Objectives.TargetVersion;
            _popElapsed = 0f;
            _pulsePhase = 0f;
            _snap = true;
        }

        // Multiple render passes must not advance the animation more than once per frame.
        float dt = _animatedFrame != Time.frameCount ? Time.unscaledDeltaTime : 0f;
        _animatedFrame = Time.frameCount;
        float halfFov = Mathf.Tan(ViewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distance = Mathf.Max(ViewCamera.nearClipPlane + 0.1f, Distance);
        transform.SetPositionAndRotation(ViewCamera.transform.TransformPoint(
            new Vector3(0f, halfFov * distance * (2f * ViewportY - 1f), distance)), ViewCamera.transform.rotation);
        float scale = BaseScale;
        if (KeepConstantScreenSize)
            scale *= halfFov / Mathf.Tan(ReferenceFov * 0.5f * Mathf.Deg2Rad);
        transform.localScale = Vector3.one * scale;

        Vector3 direction = target.position - Objectives.Player.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
        float horizontalLength = horizontal.magnitude;
        if (horizontalLength < 0.001f)
            horizontal = Vector3.ProjectOnPlane(ViewCamera.transform.forward, Vector3.up);
        if (horizontal.sqrMagnitude < 0.000001f)
            horizontal = Vector3.forward;
        float pitch = Mathf.Clamp(Mathf.Atan2(direction.y, horizontalLength) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
        Quaternion rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up) * Quaternion.Euler(-pitch, 0f, 0f);
        ArrowPivot.rotation = _snap ? rotation : Quaternion.RotateTowards(ArrowPivot.rotation, rotation, TurnSpeed * dt);
        _snap = false;

        _popElapsed += dt;
        float pop = PopDuration > 0f && _popElapsed < PopDuration && PopCurve != null
            ? PopCurve.Evaluate(_popElapsed / PopDuration) : 1f;
        Visual.transform.localScale = _visualScale * pop;
        float closeness = 1f - Mathf.InverseLerp(PulseRampFullDistance, PulseRampStartDistance, direction.magnitude);
        float amplitude = Mathf.Lerp(PulseBaseAmplitude, PulseMaxAmplitude, closeness);
        _pulsePhase = Mathf.Repeat(_pulsePhase + Mathf.Lerp(PulseBaseFrequency, PulseMaxFrequency, closeness) * dt, 1f);
        float wave = 0.5f - 0.5f * Mathf.Cos(_pulsePhase * 2f * Mathf.PI);
        ArrowPivot.localScale = _pivotScale * (1f + amplitude * wave);
    }
}
