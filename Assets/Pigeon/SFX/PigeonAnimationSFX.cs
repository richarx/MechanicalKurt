using System.Collections.Generic;
using UnityEngine;

public class PigeonAnimationSFX : MonoBehaviour
{
    [SerializeField] private AudioClip flapSound;
    [SerializeField] private List<AudioClip> footstepSounds;
    [SerializeField] private List<AudioClip> landingSounds;
    [SerializeField] private AudioClip landingSound;

    public void PlayFlapSound()
    {
        SFXManager.Instance.PlaySFX(flapSound, 0.15f);
    }

    public void PlayFootStepSound()
    {
        SFXManager.Instance.PlayRandomSFX(footstepSounds, 0.15f);
    }

    public void PlayLandingSound()
    {
        SFXManager.Instance.PlayRandomSFX(landingSounds);
        SFXManager.Instance.PlaySFX(landingSound);
    }
}
