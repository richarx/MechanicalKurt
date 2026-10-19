using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public class UnderWaterEffect : MonoBehaviour
{
    [SerializeField] private Transform waterPlane;
    [SerializeField] private Volume globalVolume;
    [SerializeField] private VolumeProfile cityProfile;
    [SerializeField] private VolumeProfile waterProfile;
    [SerializeField] private AudioMixer musicMixer;

    private bool isUnderWaterEffectActivated;

    private void Update()
    {
        bool isUnderWater = transform.position.y < waterPlane.position.y;

        if (isUnderWater != isUnderWaterEffectActivated)
        {
            isUnderWaterEffectActivated = isUnderWater;
            globalVolume.profile = isUnderWaterEffectActivated ? waterProfile : cityProfile;
            RenderSettings.fog = isUnderWaterEffectActivated;
            musicMixer.SetFloat("LowPass", isUnderWaterEffectActivated ? 600.0f : 5000.0f);
        }
    }
}
