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
    [SerializeField, Tooltip("Collider de surface qui fait disparaître les bulles (module Trigger des particules).")]
    private Transform bubbleTrigger;
    [SerializeField] private Material fullScreenEffect;
    private bool isUnderWaterEffectActivated;

    private void OnEnable()
    {
        PigeonController.LocalChanged += BindLocalPigeon;
        BindLocalPigeon(PigeonController.Local);
    }

    private void OnDisable()
    {
        PigeonController.LocalChanged -= BindLocalPigeon;
    }

    private void Update()
    {
        bool isUnderWater = transform.position.y < waterPlane.position.y;

        if (isUnderWater != isUnderWaterEffectActivated)
        {
            isUnderWaterEffectActivated = isUnderWater;
            globalVolume.profile = isUnderWaterEffectActivated ? waterProfile : cityProfile;
            RenderSettings.fog = isUnderWaterEffectActivated;
            musicPlayer.SetLowPassMode(isUnderWaterEffectActivated);
            if (pigeonSFX != null)
                pigeonSFX.SetLowPassMode(isUnderWaterEffectActivated);

            if (bubbleParticles != null)
            {
                if (isUnderWaterEffectActivated)
                    bubbleParticles.Play();
                else
                    bubbleParticles.Stop();
            }

            fullScreenEffect.SetFloat("_Alpha", isUnderWater ? 0.15f : 0.0f);
        }
    }

    private void BindLocalPigeon(PigeonController local)
    {
        if (local == null)
            return;
        pigeonSFX = local.GetComponentInChildren<PigeonSFX>();
        bubbleParticles = pigeonSFX != null ? pigeonSFX.UnderwaterBubbles : null;
        if (bubbleParticles == null)
            return;
        if (bubbleTrigger != null)
            bubbleParticles.trigger.SetCollider(0, bubbleTrigger);
        if (isUnderWaterEffectActivated)
            bubbleParticles.Play();
    }

    private void OnDestroy()
    {
        fullScreenEffect.SetFloat("_Alpha", 0.0f);
    }
}
