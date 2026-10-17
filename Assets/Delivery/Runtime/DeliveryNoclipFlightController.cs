using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class DeliveryNoclipFlightController : MonoBehaviour
{
    [SerializeField, Required] private CharacterController _characterController;
    [SerializeField, Required] private DeliveryOfferSelector _offerSelector;
    [SerializeField] private DeliverySession _session;
    [SerializeField] private Camera _movementCamera;
    [SerializeField] private bool _captureCursorOnEnable = true;
    [SerializeField, MinValue(0f), SuffixLabel("units / second", true)] private float _moveSpeed = 8f;
    [SerializeField, MinValue(1f)] private float _boostMultiplier = 2f;
    [SerializeField, MinValue(0.01f), SuffixLabel("degrees / pixel", true)] private float _mouseSensitivity = 0.12f;
    [SerializeField, Range(0f, 89f)] private float _maximumPitch = 80f;

    public float CurrentSpeed { get; private set; }

    private float _yaw;
    private float _pitch;
    private bool _cursorCaptured;
    private CursorLockMode _previousCursorLockMode;
    private bool _previousCursorVisible;
    private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

    private void OnEnable()
    {
        _previousCursorLockMode = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        if (_captureCursorOnEnable) CaptureCursor();
    }

    private void Awake()
    {
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
            _characterController = gameObject.AddComponent<CharacterController>();

        // CharacterController.Move always resolves contacts. Noclip therefore moves the
        // Transform directly while keeping the real component present but disabled.
        _characterController.detectCollisions = false;
        _characterController.enabled = false;
        if (_movementCamera == null)
            _movementCamera = Camera.main;
        if (_movementCamera != null)
        {
            Vector3 angles = _movementCamera.transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        }
    }

    private void Update()
    {
        if (_cursorCaptured && Cursor.lockState != CursorLockMode.Locked)
            _cursorCaptured = false;

        Keyboard keyboard = Keyboard.current;
        if (_cursorCaptured && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            ReleaseCursor();

        bool paused = IsPaused();
        Mouse mouse = Mouse.current;
        if (!paused && Application.isFocused && !_cursorCaptured && mouse != null
            && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUI(mouse.position.ReadValue()))
            CaptureCursor();

        if (paused || !_cursorCaptured || !Application.isFocused)
        {
            CurrentSpeed = 0f;
            PushSpeedToSelector();
            return;
        }

        if (keyboard == null)
        {
            CurrentSpeed = 0f;
            PushSpeedToSelector();
            return;
        }

        if (_cursorCaptured && mouse != null && _movementCamera != null)
            UpdateMouseLook(mouse);

        float horizontal = ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
        float forward = ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
        float vertical = ReadAxis(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed,
            keyboard.spaceKey.isPressed);

        Vector3 forwardDirection = _movementCamera != null ? _movementCamera.transform.forward : Vector3.forward;
        Vector3 rightDirection = _movementCamera != null ? _movementCamera.transform.right : Vector3.right;
        forwardDirection = Vector3.ProjectOnPlane(forwardDirection, Vector3.up).normalized;
        rightDirection = Vector3.ProjectOnPlane(rightDirection, Vector3.up).normalized;
        if (forwardDirection.sqrMagnitude < 0.001f) forwardDirection = Vector3.forward;
        if (rightDirection.sqrMagnitude < 0.001f) rightDirection = Vector3.right;

        Vector3 inputDirection = rightDirection * horizontal + forwardDirection * forward + Vector3.up * vertical;
        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        float speed = _moveSpeed;
        if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            speed *= _boostMultiplier;

        Vector3 displacement = inputDirection * speed * Time.unscaledDeltaTime;
        transform.position += displacement;
        CurrentSpeed = Time.unscaledDeltaTime > 0f ? displacement.magnitude / Time.unscaledDeltaTime : 0f;
        PushSpeedToSelector();
    }

    private void OnDisable()
    {
        CurrentSpeed = 0f;
        if (_offerSelector != null)
            _offerSelector.ClearExternalSpeed();
        if (_characterController != null)
            _characterController.enabled = false;
        RestoreCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _cursorCaptured && Cursor.lockState == CursorLockMode.Locked)
            RestoreCursor();
    }

    private void UpdateMouseLook(Mouse mouse)
    {
        Vector2 delta = mouse.delta.ReadValue();
        _yaw += delta.x * _mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch - delta.y * _mouseSensitivity, -_maximumPitch, _maximumPitch);
        _movementCamera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void CaptureCursor()
    {
        if (_movementCamera != null)
        {
            Vector3 angles = _movementCamera.transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        }
        _previousCursorLockMode = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _cursorCaptured = true;
    }

    private void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _cursorCaptured = false;
    }

    private void RestoreCursor()
    {
        if (!_cursorCaptured)
            return;
        Cursor.lockState = _previousCursorLockMode;
        Cursor.visible = _previousCursorVisible;
        _cursorCaptured = false;
    }

    private bool IsPaused()
    {
        return Time.timeScale <= 0f || (_session != null && _session.IsPaused);
    }

    private void PushSpeedToSelector()
    {
        if (_offerSelector != null)
            _offerSelector.SetExternalSpeed(CurrentSpeed);
    }

    private bool IsPointerOverUI(Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        _uiHits.Clear();
        eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, _uiHits);
        for (int i = 0; i < _uiHits.Count; i++)
            if (_uiHits[i].gameObject.GetComponentInParent<Canvas>() != null) return true;
        return false;
    }

    private static float ReadAxis(bool negative, bool positive)
    {
        return (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
}
