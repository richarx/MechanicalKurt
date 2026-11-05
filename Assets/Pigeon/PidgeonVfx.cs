using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PidgeonVfx : MonoBehaviour
{
    [SerializeField] private TrailRenderer wingTrailRendererLeft;
    [SerializeField] private TrailRenderer wingTrailRendererRight;
    [SerializeField] private GameObject waterSplashPrefab;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float shakeForce;

    private CharacterController characterController;
    private PigeonController pigeonController;

    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
    private Volume postProcessVolume;
    private VolumeProfile postProcessProfile;

    private Sequence postProcessSequence;

    private bool isFlyingVfxActive;
    private bool isUnderWater;
    private bool isDiving;

    private float lastScreenShakeTimestamp;
    private float preventEarlyWingEmissionTimestamp;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        pigeonController = GetComponent<PigeonController>();

        wingTrailRendererLeft.emitting = false;
        wingTrailRendererRight.emitting = false;
        preventEarlyWingEmissionTimestamp = Time.time;
    }

    private void Update()
    {
        UpdateWingTrails();
        UpdateUnderWaterEffects();

        // Écran et caméra : seulement pour le pigeon de ce joueur, pas pour ceux des autres.
        if (pigeonController == PigeonController.Local)
        {
            EnsurePostProcess();
            UpdatePostProcess();
            UpdateScreenShake();
        }
    }

    private void EnsurePostProcess()
    {
        if (lensDistortion != null)
            return;

        // Volume propre au piqué : UnderWaterEffect remplace le profil du volume de la scène en entrant dans l'eau,
        // ce qui ferait perdre l'effet ; et il ne faut pas écrire dans les profils partagés.
        postProcessProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        lensDistortion = postProcessProfile.Add<LensDistortion>(true);
        chromaticAberration = postProcessProfile.Add<ChromaticAberration>(true);

        postProcessVolume = new GameObject("Dive Post Process").AddComponent<Volume>();
        postProcessVolume.isGlobal = true;
        postProcessVolume.priority = 100.0f;
        postProcessVolume.sharedProfile = postProcessProfile;
    }

    /// <summary>Déformation d'écran appliquée par le piqué.</summary>
    public float DiveLensDistortion => lensDistortion != null ? lensDistortion.intensity.value : 0.0f;

    private void UpdateScreenShake()
    {
        if (isDiving)
        {
            if (Time.time - lastScreenShakeTimestamp >= impulseSource.ImpulseDefinition.ImpulseDuration)
            {
                float speed = pigeonController.Velocity.magnitude;
                float clampedSpeed = Mathf.Clamp(speed, 20.0f, 60.0f);
                float force = Tools.NormalizeValueInRange(clampedSpeed, 20.0f, 60.0f, 0.0f, shakeForce);

                impulseSource.GenerateImpulseWithForce(force);
                lastScreenShakeTimestamp = Time.time;
            }
        }
    }

    private void UpdatePostProcess()
    {
        bool isPigeonDiving = pigeonController.CurrentPigeonState == PigeonController.MovementState.Diving;

        if (isDiving != isPigeonDiving)
        {
            isDiving = isPigeonDiving;

            postProcessSequence?.Kill();

            if (!isDiving)
            {
                postProcessSequence = DOTween.Sequence()
                    .Join(
                        DOTween.To(() => lensDistortion.intensity.value,
                            x => lensDistortion.intensity.value = x,
                            0.0f,
                            3.0f
                        )
                        .SetEase(Ease.InOutCirc)
                    )
                    .Join(
                        DOTween.To(() => chromaticAberration.intensity.value,
                            x => chromaticAberration.intensity.value = x,
                            0.0f,
                            2.0f
                        )
                        .SetEase(Ease.InOutCirc)
                    )
                    .Join(
                        DOTween.To(() => cinemachineCamera.Lens.FieldOfView,
                            x => cinemachineCamera.Lens.FieldOfView = x,
                            60.0f,
                            3.0f
                        )
                        .SetEase(Ease.InOutCirc)
                    );
            }
        }

        if (isDiving)
        {
            float speed = pigeonController.Velocity.magnitude;
            float clampedSpeed = Mathf.Clamp(speed, 20.0f, 60.0f);

            lensDistortion.intensity.value = Tools.NormalizeValueInRange(clampedSpeed, 20.0f, 60.0f, 0.0f, -0.35f);
            chromaticAberration.intensity.value = Tools.NormalizeValueInRange(clampedSpeed, 20.0f, 60.0f, 0.0f, 0.8f);
            cinemachineCamera.Lens.FieldOfView = Tools.NormalizeValueInRange(clampedSpeed, 20.0f, 60.0f, 60.0f, 75.0f);
        }

        if (pigeonController.CurrentPigeonState != PigeonController.MovementState.Gliding)
        {
            if (postProcessSequence.IsActive())
                postProcessSequence.Complete();
        }
    }

    private void UpdateWingTrails()
    {
        bool shouldBeActive = !pigeonController.IsGrounded && !pigeonController.IsHovering && Time.time - preventEarlyWingEmissionTimestamp >= 0.3f;

        if (shouldBeActive != isFlyingVfxActive)
        {
            isFlyingVfxActive = shouldBeActive;

            wingTrailRendererLeft.emitting = isFlyingVfxActive;
            wingTrailRendererRight.emitting = isFlyingVfxActive;
        }
    }

    private void UpdateUnderWaterEffects()
    {
        if (isUnderWater != pigeonController.IsUndeWater)
        {
            isUnderWater = pigeonController.IsUndeWater;
            Destroy(Instantiate(waterSplashPrefab, transform.position, Quaternion.identity), 3.0f);
        }
    }

    private void OnDestroy()
    {
        postProcessSequence?.Kill();

        if (postProcessVolume != null)
            Destroy(postProcessVolume.gameObject);
        if (postProcessProfile != null)
            Destroy(postProcessProfile);
    }
}
