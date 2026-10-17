using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10)]
public class ManagerCinematic : MonoBehaviour
{
    [SerializeField] private DeliveryManager _deliveryManager;
    [SerializeField] private CinemachineBrain _brain;
    [SerializeField] private CinemachineVirtualCameraBase _gameplayCamera;
    [SerializeField, Tooltip("Prototype only: copy the view driven by the noclip controller into the separate gameplay Cinemachine camera. Leave empty for a normal Cinemachine rig.")]
    private Camera _prototypeViewSource;
    [SerializeField, Min(1)] private int _cinematicPriority = 100;
    [SerializeField, Min(0.1f)] private float _cameraTimeoutSeconds = 10f;
    [SerializeField] private bool _unlockCursor = true;

    [SerializeField, Tooltip("Optional existing black-bar group. If empty, an overlay is created automatically.")]
    private CanvasGroup _letterbox;
    [SerializeField, Range(0.01f, 0.4f)] private float _barHeight = 0.1f;
    [SerializeField, Min(0f)] private float _fadeSeconds = 0.2f;
    [SerializeField, Min(0f)] private float _defaultSendOffSeconds = 1f;
    [SerializeField, Tooltip("When enabled, FinishSendOff(token) must be called instead of using the configured animation duration.")]
    private bool _waitForSendOffSignal;

    [SerializeField, Tooltip("Connect to CinematicDelay.Wait(int), or to a dialogue that later calls ContinuePickup with the same token.")]
    private UnityEvent<int> _onCinematicReady = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> _onSendOffStarted = new UnityEvent<int>();
    [SerializeField, Tooltip("Fires after completion or interruption. Can cancel a pending delay/dialogue.")]
    private UnityEvent<int> _onCinematicEnded = new UnityEvent<int>();

    private enum Phase { Idle, Entering, Waiting, SendOff, Returning }
    private Phase _phase;
    private Coroutine _routine;
    private DeliverySession _session;
    private DeliveryOffer _offer;
    private CinemachineCamera _pointCamera;
    private CinemachineVirtualCameraBase _returnCamera;
    private PrioritySettings _previousPriority;
    private DeliveryPointCinematic _actor;
    private int _token;
    private bool _continueRequested;
    private bool _sendOffFinished;
    private bool _wasPaused;
    private float _previousBarAlpha;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private bool _restoring;
    private bool _usesPointCamera;

    public bool IsPlaying => _phase != Phase.Idle;
    public int CurrentToken => IsPlaying ? _token : 0;

    // Wire DeliveryManager.OnPickupStarted using the dynamic int entry.
    public void BeginPickUp(int token)
    {
        if (!isActiveAndEnabled || IsPlaying || _restoring) return;
        if (_deliveryManager == null) _deliveryManager = DeliveryManager.instance;
        if (_deliveryManager == null || _deliveryManager.State != DeliveryState.PickingUp
            || token == 0 || token != _deliveryManager.CurrentInteractionToken) return;

        DeliveryOffer offer = _deliveryManager.CurrentOffer;
        if (offer == null || !offer.IsAvailable || _deliveryManager.Session == null
            || !_deliveryManager.Session.IsRunning) return;
        CinemachineCamera camera = offer.Departure.CameraCinematic;
        if (camera != null && _brain != null && (_brain.OutputCamera == null
            || (_gameplayCamera != null && _gameplayCamera.gameObject == _brain.OutputCamera.gameObject)))
        {
            Debug.LogWarning("Cinematic camera setup: the Brain must be on the rendering Main Camera, and the gameplay Cinemachine camera must be a separate object. Use Tools > Delivery > Repair Cinematic Camera Setup outside Play mode.", this);
            _deliveryManager.CompleteInteraction(token);
            return;
        }
        if (camera != null && (_brain == null || !_brain.isActiveAndEnabled || _gameplayCamera == null
            || !_gameplayCamera.isActiveAndEnabled || !camera.isActiveAndEnabled
            || camera == _gameplayCamera || !_brain.IsValidChannel(camera)
            || !_brain.IsValidChannel(_gameplayCamera)))
        {
            Debug.LogWarning("Pickup cinematic skipped: assign an active Brain, gameplay camera and a different Camera Cinematic on the departure (same output channel).", this);
            _deliveryManager.CompleteInteraction(token);
            return;
        }

        _session = _deliveryManager.Session;
        _offer = offer;
        _token = token;
        _pointCamera = camera;
        _usesPointCamera = camera != null;
        _returnCamera = _usesPointCamera ? _brain.ActiveVirtualCamera as CinemachineVirtualCameraBase : null;
        if (_usesPointCamera && (_returnCamera == null || _returnCamera == camera)) _returnCamera = _gameplayCamera;
        _actor = offer.Departure.GetComponent<DeliveryPointCinematic>();
        _wasPaused = _session.IsPaused;
        _previousPriority = camera != null ? camera.Priority : default;
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        EnsureLetterbox();
        _previousBarAlpha = _letterbox.alpha;
        _continueRequested = _sendOffFinished = false;
        _phase = Phase.Entering;
        _session.OnReset.AddListener(CancelCinematic);
        _session.OnEnded.AddListener(CancelCinematic);
        _deliveryManager.OnCancelled.AddListener(HandleCancelled);
        _session.SetPaused(true);
        if (_unlockCursor) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        if (_usesPointCamera)
        {
            int previousLivePriority = _returnCamera.Priority.Value;
            int raisedPriority = previousLivePriority < int.MaxValue ? previousLivePriority + 1 : int.MaxValue;
            camera.Priority = Mathf.Max(_cinematicPriority, raisedPriority);
        }
        _routine = StartCoroutine(PlayPickup(token));
    }

    public void ContinuePickup(int token)
    {
        if (_phase == Phase.Waiting && token == _token && HasValidInteraction())
            _continueRequested = true;
    }

    public void FinishSendOff(int token)
    {
        if (_phase == Phase.SendOff && token == _token && HasValidInteraction())
            _sendOffFinished = true;
    }

    private IEnumerator PlayPickup(int token)
    {
        yield return FadeBars(1f);
        yield return WaitForCamera(_pointCamera);
        _phase = Phase.Waiting;
        _onCinematicReady.Invoke(_token);
        if (!IsPlaying || _token != token) yield break;
        while (!_continueRequested) yield return null;

        _phase = Phase.SendOff;
        if (_actor != null) _actor.PlaySendOff();
        _onSendOffStarted.Invoke(_token);
        if (!IsPlaying || _token != token) yield break;
        if (_waitForSendOffSignal)
            while (!_sendOffFinished) yield return null;
        else
            yield return new WaitForSecondsRealtime(_actor != null ? _actor.SendOffSeconds : _defaultSendOffSeconds);

        _phase = Phase.Returning;
        RestoreCamera();
        yield return WaitForCamera(_returnCamera);
        yield return FadeBars(_previousBarAlpha);
        if (HasValidInteraction()) _deliveryManager.CompleteInteraction(_token);
        _routine = null;
        RestoreScene();
    }

    private IEnumerator WaitForCamera(CinemachineVirtualCameraBase target)
    {
        if (target == null || _brain == null) yield break;
        // Let the Brain evaluate the changed priority in LateUpdate before checking a cut/blend.
        yield return null;
        float elapsed = 0f;
        while (_brain != null && target != null
            && (!ReferenceEquals(_brain.ActiveVirtualCamera, target) || _brain.IsBlending))
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= _cameraTimeoutSeconds)
            {
                Debug.LogWarning("Cinematic camera transition timed out. Check priorities, output channels and Brain settings.", this);
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator FadeBars(float target)
    {
        if (_letterbox == null) yield break;
        float from = _letterbox.alpha;
        float elapsed = 0f;
        while (elapsed < _fadeSeconds && _letterbox != null)
        {
            elapsed += Time.unscaledDeltaTime;
            _letterbox.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / _fadeSeconds));
            yield return null;
        }
        if (_letterbox != null) _letterbox.alpha = target;
    }

    private bool HasValidInteraction()
    {
        return _deliveryManager != null && _deliveryManager.isActiveAndEnabled
            && _deliveryManager.Selector != null && _deliveryManager.Selector.isActiveAndEnabled
            && _session != null && _session.IsRunning && _offer != null && _offer.IsAvailable
            && _deliveryManager.State == DeliveryState.PickingUp
            && _deliveryManager.CurrentOffer == _offer && _deliveryManager.CurrentInteractionToken == _token;
    }

    private void Update()
    {
        if (IsPlaying && (!HasValidInteraction() || (_usesPointCamera
            && (_brain == null || !_brain.isActiveAndEnabled || _pointCamera == null || !_pointCamera.isActiveAndEnabled))))
            CancelCinematic();
    }

    private void LateUpdate()
    {
        // The prototype controller writes to Main Camera in Update. Copy that view before
        // the Brain runs, and keep this gameplay shot still throughout the cutscene.
        if (!IsPlaying && _prototypeViewSource != null && _gameplayCamera != null
            && _prototypeViewSource.transform != _gameplayCamera.transform)
            _gameplayCamera.transform.SetPositionAndRotation(
                _prototypeViewSource.transform.position, _prototypeViewSource.transform.rotation);
    }

    private void OnDisable() => CancelCinematic();
    private void HandleCancelled(DeliveryOffer offer) { if (offer == _offer) CancelCinematic(); }

    public void CancelCinematic()
    {
        if (!IsPlaying) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        // Skipping an otherwise valid cutscene must not leave External pickup locked.
        if (HasValidInteraction()) _deliveryManager.CompleteInteraction(_token);
        RestoreScene();
    }

    private void RestoreCamera()
    {
        if (_pointCamera != null) _pointCamera.Priority = _previousPriority;
    }

    private void RestoreScene()
    {
        _restoring = true;
        int finishedToken = _token;
        _phase = Phase.Idle;
        RestoreCamera();
        if (_letterbox != null) _letterbox.alpha = _previousBarAlpha;
        if (_session != null)
        {
            _session.OnReset.RemoveListener(CancelCinematic);
            _session.OnEnded.RemoveListener(CancelCinematic);
            _session.SetPaused(_wasPaused);
        }
        if (_deliveryManager != null) _deliveryManager.OnCancelled.RemoveListener(HandleCancelled);
        if (_unlockCursor) { Cursor.lockState = _previousCursorLock; Cursor.visible = _previousCursorVisible; }
        _token = 0;
        _offer = null;
        _session = null;
        _pointCamera = null;
        _usesPointCamera = false;
        try { _onCinematicEnded.Invoke(finishedToken); }
        finally { _restoring = false; }
    }

    private void EnsureLetterbox()
    {
        if (_letterbox != null) return;
        GameObject overlay = new GameObject("Cinematic Letterbox", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        overlay.transform.SetParent(transform, false);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        _letterbox = overlay.GetComponent<CanvasGroup>();
        _letterbox.alpha = 0f;
        _letterbox.interactable = false;
        _letterbox.blocksRaycasts = false;
        CreateBar(overlay.transform, "Top Bar", 1f - _barHeight, 1f);
        CreateBar(overlay.transform, "Bottom Bar", 0f, _barHeight);
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
