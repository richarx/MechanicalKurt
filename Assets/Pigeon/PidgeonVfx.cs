using UnityEngine;

public class PidgeonVfx : MonoBehaviour
{
    [SerializeField] private ParticleSystem windParticles;
    [SerializeField] private TrailRenderer wingTrailRendererLeft;
    [SerializeField] private TrailRenderer wingTrailRendererRight;

    private CharacterController characterController;
    private PigeonController pigeonController;

    private bool isFlyingVfxActive;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        pigeonController = GetComponent<PigeonController>();
    }

    private void Update()
    {
        bool shouldBeActive = !pigeonController.IsGrounded && !pigeonController.Controls.Brake;

        if (shouldBeActive != isFlyingVfxActive)
        {
            isFlyingVfxActive = shouldBeActive;

            if (isFlyingVfxActive)
                windParticles.Play();
            else
                windParticles.Stop();

            wingTrailRendererLeft.emitting = isFlyingVfxActive;
            wingTrailRendererRight.emitting = isFlyingVfxActive;
        }
    }
}
