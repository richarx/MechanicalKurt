using System;
using System.Collections;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10)]
public class ManagerCinematic : MonoBehaviour
{
    [field: SerializeField, FormerlySerializedAs("_brain"), Required]
    public CinemachineBrain Brain { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_gameplayCamera"), Required]
    public CinemachineVirtualCameraBase GameplayCamera { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_prototypeViewSource")]
    [field: Tooltip("Prototype only: copy the noclip view into the separate gameplay Cinemachine camera. Leave empty for a normal rig.")]
    public Camera PrototypeViewSource { get; private set; }

    [field: SerializeField]
    [field: Tooltip("Commandes du pigeon à bloquer pendant la cinématique. Recherche automatique dans la scène si vide.")]
    public PlayerControls PlayerControls { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_cinematicPriority"), MinValue(1)]
    public int CinematicPriority { get; private set; } = 100;

    [field: SerializeField, FormerlySerializedAs("_cameraTimeoutSeconds"), MinValue(0.1f), SuffixLabel("s", true)]
    public float CameraTimeoutSeconds { get; private set; } = 10f;

    [field: SerializeField, FormerlySerializedAs("_unlockCursor")]
    public bool UnlockCursor { get; private set; } = true;

    [field: SerializeField, FormerlySerializedAs("_letterbox")]
    [field: Tooltip("Optional black-bar group. If empty, an overlay is created automatically.")]
    public CanvasGroup Letterbox { get; private set; }

    [field: SerializeField, FormerlySerializedAs("_barHeight"), MinValue(0.01f), MaxValue(0.4f)]
    public float BarHeight { get; private set; } = 0.1f;

    [field: SerializeField, FormerlySerializedAs("_fadeSeconds"), MinValue(0f), SuffixLabel("s", true)]
    public float FadeSeconds { get; private set; } = 0.2f;

    private enum Phase { Idle, Entering, Waiting, Returning }
    private Phase _phase;
    private Coroutine _routine;
    private Coroutine _switchRoutine;
    private bool _overridesBlend;
    private CinemachineBlendDefinition _previousBlend;
    private CinemachineBlenderSettings _previousCustomBlends;
    private CinemachineVirtualCameraBase _cinematicCamera;
    private CinemachineVirtualCameraBase _returnCamera;
    private PrioritySettings _previousPriority;
    private Action _onReady;
    private Action _onEnded;
    private Action _onCancelFinished;
    private bool _cancelRequested;
    private bool _usesCamera;
    private bool _cursorChanged;
    private PlayerControls _blockedControls;
    private bool _wereControlsBlocked;
    private float _previousBarAlpha;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;

    [ShowInInspector, ReadOnly] public bool IsPlaying => _phase != Phase.Idle;
    [ShowInInspector, ReadOnly] public bool IsReady => _phase == Phase.Waiting && !_cancelRequested;

    /// <summary>
    /// Returns false if the request cannot start. onReady runs after the camera blend and
    /// letterbox fade. The shot then stays active until CancelCinematic is called.
    /// Optional onEnded also reports forced interruption, even if onReady never ran.
    /// A null camera keeps the current view.
    /// </summary>
    public bool BeginCinematic(CinemachineVirtualCameraBase camera, Action onReady = null, Action onEnded = null)
    {
        if (!isActiveAndEnabled || IsPlaying) return false;
        if (camera != null && !HasValidCameraSetup(camera))
        {
            Debug.LogWarning("Cinematic refused: assign an active Brain on the rendering Camera, a separate gameplay camera and an active cinematic camera on the same output channel.", this);
            return false;
        }

        _cinematicCamera = camera;
        _usesCamera = camera != null;
        _returnCamera = _usesCamera ? Brain.ActiveVirtualCamera as CinemachineVirtualCameraBase : null;
        if (_usesCamera && (_returnCamera == null || _returnCamera == camera)) _returnCamera = GameplayCamera;
        _previousPriority = _usesCamera ? camera.Priority : default;
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _cursorChanged = UnlockCursor;
        EnsureLetterbox();
        _previousBarAlpha = Letterbox.alpha;
        _onReady = onReady;
        _onEnded = onEnded;
        _onCancelFinished = null;
        _cancelRequested = false;
        _phase = Phase.Entering;

        _blockedControls = PlayerControls != null ? PlayerControls : FindAnyObjectByType<PlayerControls>();
        if (_blockedControls != null)
        {
            _wereControlsBlocked = _blockedControls.AreControlsBlocked;
            _blockedControls.SetControlsBlocked(true);
        }

        if (_cursorChanged) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (_usesCamera)
        {
            int livePriority = _returnCamera.Priority.Value;
            int raisedPriority = livePriority < int.MaxValue ? livePriority + 1 : int.MaxValue;
            camera.Priority = Mathf.Max(CinematicPriority, raisedPriority);
        }
        _routine = StartCoroutine(PlayCinematic());
        return true;
    }

    public void CancelCinematic() => CancelCinematic(null);

    /// <summary>
    /// Returns to the previous shot. onFinished runs only after the return blend,
    /// letterbox fade and cursor restoration. It runs immediately if already idle.
    /// Multiple cancellation requests each receive their own completion callback.
    /// </summary>
    public void CancelCinematic(Action onFinished)
    {
        if (!IsPlaying)
        {
            InvokeCallbacks(onFinished);
            return;
        }
        _onCancelFinished += onFinished;
        StopCameraSwitch();
        _cancelRequested = true;
    }

    /// <summary>Changes shot while keeping controls locked and the original return camera.</summary>
    public bool SwitchCinematicCamera(CinemachineVirtualCameraBase camera, bool cut = false)
        => SwitchCamera(camera, cut
            ? new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f)
            : (CinemachineBlendDefinition?)null);

    /// <summary>Overrides only this transition. Zero cuts; positive durations use EaseInOut.</summary>
    public bool SwitchCinematicCamera(CinemachineVirtualCameraBase camera, float transitionSeconds)
    {
        if (transitionSeconds < 0f || float.IsNaN(transitionSeconds) || float.IsInfinity(transitionSeconds))
            return false;
        return SwitchCamera(camera, new CinemachineBlendDefinition(transitionSeconds == 0f
            ? CinemachineBlendDefinition.Styles.Cut : CinemachineBlendDefinition.Styles.EaseInOut,
            transitionSeconds));
    }

    private bool SwitchCamera(CinemachineVirtualCameraBase camera, CinemachineBlendDefinition? blend)
    {
        if (!IsReady || !_usesCamera || !HasValidCameraSetup(camera))
            return false;
        if (camera == _cinematicCamera)
            return true;

        int livePriority = _cinematicCamera.Priority.Value;
        _cinematicCamera.Priority = _previousPriority;
        _cinematicCamera = camera;
        _previousPriority = camera.Priority;
        camera.Priority = Mathf.Max(CinematicPriority,
            livePriority < int.MaxValue ? livePriority + 1 : int.MaxValue);
        camera.Prioritize();
        if (blend.HasValue)
        {
            _previousBlend = Brain.DefaultBlend;
            _previousCustomBlends = Brain.CustomBlends;
            _overridesBlend = true;
            Brain.DefaultBlend = blend.Value;
            Brain.CustomBlends = null;
        }
        _phase = Phase.Entering;
        _switchRoutine = StartCoroutine(FinishCameraSwitch(camera, blend.HasValue ? blend.Value.Time : 0f));
        return true;
    }

    private IEnumerator FinishCameraSwitch(CinemachineVirtualCameraBase camera, float transitionSeconds)
    {
        yield return WaitForCamera(camera, true, transitionSeconds);
        RestoreBlend();
        _switchRoutine = null;
        if (!_cancelRequested)
            _phase = Phase.Waiting;
    }

    private void RestoreBlend()
    {
        if (_overridesBlend && Brain != null)
        {
            Brain.DefaultBlend = _previousBlend;
            Brain.CustomBlends = _previousCustomBlends;
        }
        _overridesBlend = false;
        _previousCustomBlends = null;
    }

    private void StopCameraSwitch()
    {
        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = null;
        RestoreBlend();
    }

    private IEnumerator PlayCinematic()
    {
        // Keep callbacks asynchronous, including a shot without a camera or a fade.
        yield return null;
        yield return FadeBars(1f, true);
        if (!_cancelRequested) yield return WaitForCamera(_cinematicCamera, true);
        if (!_cancelRequested)
        {
            _phase = Phase.Waiting;
            Action ready = _onReady;
            _onReady = null;
            InvokeCallbacks(ready);
        }
        while (!_cancelRequested) yield return null;

        _phase = Phase.Returning;
        RestoreCamera();
        yield return WaitForCamera(_returnCamera, false);
        yield return FadeBars(_previousBarAlpha, false);
        _routine = null;
        RestoreScene();
    }

    private bool HasValidCameraSetup(CinemachineVirtualCameraBase camera)
    {
        return Brain != null && Brain.isActiveAndEnabled && Brain.OutputCamera != null
            && GameplayCamera != null && GameplayCamera.isActiveAndEnabled
            && GameplayCamera.gameObject != Brain.OutputCamera.gameObject
            && camera != null && camera.isActiveAndEnabled && camera != GameplayCamera
            && Brain.IsValidChannel(camera) && Brain.IsValidChannel(GameplayCamera);
    }

    private IEnumerator WaitForCamera(CinemachineVirtualCameraBase target, bool entering, float transitionSeconds = 0f)
    {
        if (!_usesCamera) yield break;
        // Let the Brain evaluate the changed priority in LateUpdate first.
        yield return null;
        float elapsed = 0f;
        float timeout = transitionSeconds > 0f
            ? Mathf.Max(CameraTimeoutSeconds, transitionSeconds + 1f) : CameraTimeoutSeconds;
        while (!entering || !_cancelRequested)
        {
            if (Brain == null || !Brain.isActiveAndEnabled || target == null || !target.isActiveAndEnabled)
            {
                if (entering) _cancelRequested = true;
                yield break;
            }
            if (ReferenceEquals(Brain.ActiveVirtualCamera, target) && !Brain.IsBlending) yield break;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogWarning("Cinematic camera transition timed out. Check priorities, output channels and Brain settings.", this);
                // Never announce a shot as ready if the camera did not reach it.
                if (entering) _cancelRequested = true;
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator FadeBars(float target, bool entering)
    {
        if (Letterbox == null) yield break;
        float from = Letterbox.alpha;
        float elapsed = 0f;
        while (elapsed < FadeSeconds && Letterbox != null)
        {
            if (entering && _cancelRequested) yield break;
            elapsed += Time.unscaledDeltaTime;
            Letterbox.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / FadeSeconds));
            yield return null;
        }
        if (Letterbox != null && (!entering || !_cancelRequested)) Letterbox.alpha = target;
    }

    private void Update()
    {
        if (IsPlaying && _usesCamera && !HasValidCameraSetup(_cinematicCamera))
            CancelCinematic();
    }

    private void LateUpdate()
    {
        // The prototype controller writes to Main Camera in Update. Keep the saved
        // gameplay shot still during the cinematic, before the Brain evaluates it.
        if (!IsPlaying && PrototypeViewSource != null && GameplayCamera != null
            && PrototypeViewSource.transform != GameplayCamera.transform)
            GameplayCamera.transform.SetPositionAndRotation(
                PrototypeViewSource.transform.position, PrototypeViewSource.transform.rotation);
    }

    private void OnDisable()
    {
        if (!IsPlaying) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        // A disabled component cannot animate the return: restore and notify now.
        RestoreScene();
    }

    private void RestoreCamera()
    {
        if (_cinematicCamera != null)
        {
            _cinematicCamera.Priority = _previousPriority;
            _cinematicCamera.Prioritize();
        }
        if (_returnCamera != null && _returnCamera.isActiveAndEnabled)
            _returnCamera.Prioritize();
    }

    private void RestoreScene()
    {
        StopCameraSwitch();
        RestoreCamera();
        if (Letterbox != null) Letterbox.alpha = _previousBarAlpha;
        if (_blockedControls != null) _blockedControls.SetControlsBlocked(_wereControlsBlocked);
        _blockedControls = null;
        if (_cursorChanged) { Cursor.lockState = _previousCursorLock; Cursor.visible = _previousCursorVisible; }
        Action ended = _onEnded;
        Action cancelled = _onCancelFinished;
        _onReady = _onEnded = _onCancelFinished = null;
        _cinematicCamera = _returnCamera = null;
        _usesCamera = _cancelRequested = _cursorChanged = false;
        _phase = Phase.Idle;
        InvokeCallbacks(ended);
        InvokeCallbacks(cancelled);
    }

    private void InvokeCallbacks(Action callbacks)
    {
        if (callbacks == null) return;
        foreach (Action callback in callbacks.GetInvocationList())
        {
            try { callback(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private void EnsureLetterbox()
    {
        if (Letterbox != null) return;
        GameObject overlay = new GameObject("Cinematic Letterbox", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        Letterbox = overlay.GetComponent<CanvasGroup>();
        Letterbox.alpha = 0f;
        Letterbox.interactable = false;
        Letterbox.blocksRaycasts = false;
        CreateBar(overlay.transform, "Top Bar", 1f - BarHeight, 1f);
        CreateBar(overlay.transform, "Bottom Bar", 0f, BarHeight);
    }

    private static void CreateBar(Transform parent, string name, float bottom, float top)
    {
        GameObject bar = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bar.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)bar.transform;
        rect.anchorMin = new Vector2(0f, bottom);
        rect.anchorMax = new Vector2(1f, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = bar.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }
}
