using UnityEngine;

public class PigeonAnimationControl : MonoBehaviour
{
    Animator _animator;
    IPigeonControls _pigeonControls;
    CharacterController _character;
    PigeonController _pigeonController;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _pigeonControls = GetComponentInParent<IPigeonControls>();
        _character = GetComponentInParent<CharacterController>();
        _pigeonController = GetComponentInParent<PigeonController>();
    }

    void Update()
    {
        _animator.SetFloat("Speed", _pigeonController.LastVelocity.magnitude);

        _animator.SetFloat("VerticalSpeed", _character.velocity.y);
        _animator.SetBool("IsGrounded", _character.isGrounded);
        _animator.SetBool("Flap", _pigeonController.CurrentPigeonState == PigeonController.PigeonState.Flapping);
        _animator.SetBool("Dive", _pigeonController.CurrentPigeonState == PigeonController.PigeonState.Diving);
        _animator.SetBool("Hover", _pigeonController.CurrentPigeonState == PigeonController.PigeonState.Braking);
        _animator.SetBool("Dance", _pigeonControls.Flex);
    }
}
