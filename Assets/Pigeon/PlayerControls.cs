using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerControls : MonoBehaviour, IPigeonControls
{
    Controls _controls;
    [SerializeField] CinemachineOrbitalFollow _camera;

    bool _interact;

    bool _cooing;

    bool _shit;

    bool _leftFlex;

    bool _rightFlex;

    bool _forceFlex;

    [ShowInInspector, ReadOnly]
    public bool AreControlsBlocked { get; private set; }

    private bool CanReadControls => isActiveAndEnabled && !AreControlsBlocked;

    void Awake()
    {
        _controls = new Controls();
    }

    void OnEnable()
    {
        _controls.Enable();
        if (AreControlsBlocked)
            _controls.Player.Disable();
    }

    void OnDisable()
    {
        _controls.Disable();
        ClearPendingActions();
    }

    void OnDestroy()
    {
        _controls?.Dispose();
    }

    void Update()
    {
        if (!CanReadControls)
            return;

        _interact |= _controls.Player.Interact.WasPressedThisFrame();
        _cooing |= _controls.Player.Cooing.WasPressedThisFrame();
        _shit |= _controls.Player.Shit.WasPressedThisFrame();
        _leftFlex |= _controls.Player.LeftFlex.WasPressedThisFrame();
        _rightFlex |= _controls.Player.RightFlex.WasPressedThisFrame();
    }

    public void SetControlsBlocked(bool blocked)
    {
        AreControlsBlocked = blocked;
        ClearPendingActions();
        if (_controls == null)
            return;

        if (blocked || !isActiveAndEnabled)
            _controls.Player.Disable();
        else
            _controls.Player.Enable();
    }

    public void SetForceFlex(bool isForcing)
    {
        _forceFlex = isForcing;
    }

    public void SnapCameraBehind()
    {
        if (_camera == null)
            _camera = GetComponentInChildren<CinemachineOrbitalFollow>();
        if (_camera == null || _camera.VirtualCamera == null || _camera.FollowTarget == null)
            return;

        CinemachineVirtualCameraBase virtualCamera = _camera.VirtualCamera;
        // Initialiser l'orbite et annuler les anciennes vitesses d'entrée.
        virtualCamera.PreviousStateIsValid = false;
        virtualCamera.InternalUpdateCameraState(Vector3.up, -1f);
        Vector3 targetPosition = _camera.FollowTargetPosition
            + _camera.FollowTargetRotation * _camera.TargetOffset;
        float distance = Mathf.Max(0.1f, Vector3.Distance(virtualCamera.State.GetFinalPosition(), targetPosition));
        virtualCamera.ForceCameraPosition(targetPosition - transform.forward * distance, transform.rotation);
        _camera.HorizontalAxis.CancelRecentering();
        _camera.VerticalAxis.CancelRecentering();
        // Replacer la caméra immédiatement, sans amortissement depuis l'ancien emplacement.
        virtualCamera.PreviousStateIsValid = false;
        virtualCamera.InternalUpdateCameraState(Vector3.up, 0f);
    }

    private void ClearPendingActions()
    {
        _interact = _cooing = _shit = _leftFlex = _rightFlex = false;
    }

    Vector2 IPigeonControls.RelativeMovement => CanReadControls ? _controls.Player.Move.ReadValue<Vector2>() : Vector2.zero;
    Vector2 IPigeonControls.Movement
    {
        get
        {
            if (!CanReadControls)
                return Vector2.zero;

            Vector2 movement = _controls.Player.Move.ReadValue<Vector2>();
            float angle = _camera.HorizontalAxis.Value;
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rotatedX = movement.x * cos + movement.y * sin;
            float rotatedY = -movement.x * sin + movement.y * cos;
            return new Vector2(rotatedX, rotatedY);
        }
    }
    bool IPigeonControls.Flap => CanReadControls && _controls.Player.Flap.IsPressed();
    bool IPigeonControls.Dive => CanReadControls && _controls.Player.Dive.IsPressed();

    public bool Brake => CanReadControls && _controls.Player.Brake.IsPressed();

    public bool Flex => (CanReadControls && (_controls.Player.LeftFlex.IsPressed() || _controls.Player.RightFlex.IsPressed())) || _forceFlex;

    public bool ConsumeInteract
    {
        get
        {
            bool value = CanReadControls && _interact;
            _interact = false;
            return value;
        }
    }

    public bool ConsumeCooing
    {
        get
        {
            bool value = CanReadControls && _cooing;
            _cooing = false;
            return value;
        }
    }

    public bool ConsumeShit
    {
        get
        {
            bool value = CanReadControls && _shit;
            _shit = false;
            return value;
        }
    }

    public bool ConsumeLeftFlex
    {
        get
        {
            bool value = CanReadControls && _leftFlex;
            _leftFlex = false;
            return value;
        }
    }

    public bool ConsumeRightFlex
    {
        get
        {
            bool value = CanReadControls && _rightFlex;
            _rightFlex = false;
            return value;
        }
    }
}
