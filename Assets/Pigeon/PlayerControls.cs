using Unity.Cinemachine;
using UnityEngine;

public class PlayerControls : MonoBehaviour, IPigeonControls
{
    Controls _controls;
    CinemachineOrbitalFollow _camera;

    bool _interact;

    bool _cooing;

    bool _shit;

    bool _leftFlex;

    bool _rightFlex;

    void Awake()
    {
        _controls = new Controls();
        _camera = GetComponentInChildren<CinemachineOrbitalFollow>();
    }

    void OnEnable()
    {
        _controls.Enable();
    }

    void OnDisable()
    {
        _controls.Disable();
    }

    void OnDestroy()
    {
        _controls?.Dispose();
    }

    void Update()
    {
        _interact |= _controls.Player.Interact.WasPressedThisFrame();
        _cooing |= _controls.Player.Cooing.WasPressedThisFrame();
        _shit |= _controls.Player.Shit.WasPressedThisFrame();
        _leftFlex |= _controls.Player.LeftFlex.WasPressedThisFrame();
        _rightFlex |= _controls.Player.RightFlex.WasPressedThisFrame();
    }

    Vector2 IPigeonControls.RelativeMovement => _controls.Player.Move.ReadValue<Vector2>();
    Vector2 IPigeonControls.Movement
    {
        get
        {
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
    bool IPigeonControls.Flap => _controls.Player.Flap.IsPressed();
    bool IPigeonControls.Dive => _controls.Player.Dive.IsPressed();

    public bool Brake => _controls.Player.Brake.IsPressed();

    public bool Flex => _controls.Player.LeftFlex.IsPressed() || _controls.Player.RightFlex.IsPressed();

    public bool ConsumeInteract
    {
        get
        {
            bool value = _interact;
            _interact = false;
            return value;
        }
    }

    public bool ConsumeCooing
    {
        get
        {
            bool value = _cooing;
            _cooing = false;
            return value;
        }
    }

    public bool ConsumeShit
    {
        get
        {
            bool value = _shit;
            _shit = false;
            return value;
        }
    }

    public bool ConsumeLeftFlex
    {
        get
        {
            bool value = _leftFlex;
            _leftFlex = false;
            return value;
        }
    }

    public bool ConsumeRightFlex
    {
        get
        {
            bool value = _rightFlex;
            _rightFlex = false;
            return value;
        }
    }
}
