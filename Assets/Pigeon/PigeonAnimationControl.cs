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
        _animator.SetBool("Flap", controls != null && controls.Flap);
        _animator.SetBool("Dive", controls != null && controls.Dive);
        _animator.SetBool("Dance", controls != null && controls.Flex);
    }
}
