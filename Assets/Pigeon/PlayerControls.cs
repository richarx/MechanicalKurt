using Unity.Cinemachine;
using UnityEngine;

public class PlayerControls : MonoBehaviour, IPigeonControls
{
    Controls _controls;
    CinemachineOrbitalFollow _camera;

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

}
