using UnityEngine;

public class PigeonAnimationControl : MonoBehaviour
{
    Animator _animator;
    IPigeonControls _pigeonControls;
    CharacterController _character;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _pigeonControls = GetComponentInParent<IPigeonControls>();
        _character = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        _animator.SetFloat("Speed", _character.velocity.magnitude);
        _animator.SetFloat("VerticalSpeed", _character.velocity.y);
        _animator.SetBool("IsGrounded", _character.isGrounded);
        _animator.SetBool("Flap", _pigeonControls.Flap);
        _animator.SetBool("Dive", _pigeonControls.Dive);
        _animator.SetBool("Dance", _pigeonControls.Flex);
    }
}
