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
        PigeonController.PigeonState pigeonState = _pigeonController.CurrentPigeonState;

        _animator.SetFloat("Speed", _pigeonController.LastVelocity.magnitude);

        _animator.SetFloat("VerticalSpeed", _character.velocity.y);
        _animator.SetBool("IsGrounded", _character.isGrounded);
        _animator.SetBool("Flap", pigeonState == PigeonController.PigeonState.Flapping);
        _animator.SetBool("Dive", pigeonState == PigeonController.PigeonState.Diving);
        _animator.SetBool("Hover", pigeonState == PigeonController.PigeonState.Braking || pigeonState == PigeonController.PigeonState.HoverFlapping);
        _animator.SetBool("Dance", pigeonState == PigeonController.PigeonState.Dancing);
    }
}
