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

    // Tout vient du pigeon : sa vue (vitesse propre, sol ou support) et sa source d'input actuelle (joueur ou bot).
    // Le CharacterController ne bouge que pendant la simulation (replays compris) : il ne reflète pas la vue.
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
