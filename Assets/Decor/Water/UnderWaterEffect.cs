using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public class UnderWaterEffect : MonoBehaviour
{
    [SerializeField] private Transform waterPlane;
    [SerializeField] private Volume globalVolume;
    [SerializeField] private VolumeProfile cityProfile;
    [SerializeField] private VolumeProfile waterProfile;
    [SerializeField] private MusicPlayer musicPlayer;
    [SerializeField] private PigeonSFX pigeonSFX;
    [SerializeField] private ParticleSystem bubbleParticles;
    [SerializeField] private Material fullScreenEffect;
    private bool isUnderWaterEffectActivated;

    private void Update()
    {
        bool isUnderWater = transform.position.y < waterPlane.position.y;

        if (isUnderWater != isUnderWaterEffectActivated)
        {
            isUnderWaterEffectActivated = isUnderWater;
            globalVolume.profile = isUnderWaterEffectActivated ? waterProfile : cityProfile;
            RenderSettings.fog = isUnderWaterEffectActivated;
            musicPlayer.SetLowPassMode(isUnderWaterEffectActivated);
            pigeonSFX.SetLowPassMode(isUnderWaterEffectActivated);

            if (isUnderWaterEffectActivated)
                bubbleParticles.Play();
            else
                bubbleParticles.Stop();

            fullScreenEffect.SetFloat("_Alpha", isUnderWater ? 0.15f : 0.0f);
        }
    }

    private void OnDestroy()
    {
        fullScreenEffect.SetFloat("_Alpha", 0.0f);
    }
}
