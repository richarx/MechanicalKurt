using UnityEngine;

public class PidgeonVfx : MonoBehaviour
{
    [SerializeField] private ParticleSystem windParticles;
    [SerializeField] private TrailRenderer wingTrailRendererLeft;
    [SerializeField] private TrailRenderer wingTrailRendererRight;
    [SerializeField] private GameObject waterSplashPrefab;

    private CharacterController characterController;
    private PigeonController pigeonController;

    private bool isFlyingVfxActive;

    private bool isUnderWater;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        pigeonController = GetComponent<PigeonController>();
    }

    private void Update()
    {
        bool shouldBeActive = !pigeonController.IsGrounded && !pigeonController.IsHovering;

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

        if (isUnderWater != pigeonController.IsUndeWater)
        {
            isUnderWater = pigeonController.IsUndeWater;
            Destroy(Instantiate(waterSplashPrefab, transform.position, Quaternion.identity), 3.0f);
        }
    }
}
