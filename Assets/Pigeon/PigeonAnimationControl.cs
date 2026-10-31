using UnityEngine;

public class PigeonAnimationControl : MonoBehaviour
{
    Animator _animator;
    PigeonController _pigeonController;

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
        _animator.SetBool("Flap", _pigeonController.IsFlapping && !hovering);
        _animator.SetBool("Dive", _pigeonController.IsDiving && !hovering);
        _animator.SetBool("Hover", hovering);
        _animator.SetBool("Dance", controls != null && controls.Flex);
    }
}
