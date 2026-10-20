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
        }
    }
}
