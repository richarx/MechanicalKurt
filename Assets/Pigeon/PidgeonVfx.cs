using UnityEngine;

public class PidgeonVfx : MonoBehaviour
{
    [SerializeField] private ParticleSystem windParticles;
    [SerializeField] private TrailRenderer wingTrailRendererLeft;
    [SerializeField] private TrailRenderer wingTrailRendererRight;

    private CharacterController characterController;

    private bool isFlyingVfxActive;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (characterController.isGrounded == isFlyingVfxActive)
        {
            isFlyingVfxActive = !characterController.isGrounded;

            if (isFlyingVfxActive)
                windParticles.Play();
            else
                windParticles.Stop();

            wingTrailRendererLeft.emitting = isFlyingVfxActive;
            wingTrailRendererRight.emitting = isFlyingVfxActive;
        }
    }
}
