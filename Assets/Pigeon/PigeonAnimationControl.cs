using UnityEngine;

public class PigeonAnimationControl : MonoBehaviour
{
    private static readonly int FallState = Animator.StringToHash("Base Layer.Fall");
    Animator _animator;
    PigeonController _pigeonController;
    private bool _wasFalling;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _pigeonController = GetComponentInParent<PigeonController>();
    }

    void Update()
    {
        Vector3 velocity = _pigeonController.LastVelocity;
        _animator.SetFloat("Speed", velocity.magnitude);
        _animator.SetFloat("VerticalSpeed", velocity.y);
        _animator.SetBool("IsGrounded", _pigeonController.IsGrounded);

        IPigeonControls controls = _pigeonController.Controls;
        bool hovering = _pigeonController.IsHovering;
        bool falling = _pigeonController.CurrentPigeonState == PigeonController.MovementState.Falling;
        _animator.SetBool("Flap", _pigeonController.IsFlapping && !hovering);
        // Le contrôleur existant utilise Dive pour conserver son animation Fall.
        _animator.SetBool("Dive", (_pigeonController.IsDiving || falling) && !hovering);
        _animator.SetBool("Hover", hovering);
        _animator.SetBool("Dance", controls != null && controls.Flex);
        if (falling && !_wasFalling)
            _animator.CrossFadeInFixedTime(FallState, 0.1f);
        _wasFalling = falling;
    }
}
