using System.Collections.Generic;
using UnityEngine;

public class PigeonAnimationSFX : MonoBehaviour
{
    [SerializeField] private AudioClip flapSound;
    [SerializeField] private List<AudioClip> footstepSounds;
    [SerializeField] private List<AudioClip> landingSounds;
    [SerializeField] private AudioClip landingSound;

    private PigeonController pigeonController;

    private void Awake()
    {
        pigeonController = GetComponentInParent<PigeonController>();
    }

    /// <summary>2D pour le pigeon du joueur local, localisé en 3D pour les autres.</summary>
    private bool IsSpatial => pigeonController == null || pigeonController != PigeonController.Local;

    public void PlayFlapSound()
    {
        Play(flapSound, 0.15f);
    }

    public void PlayFootStepSound()
    {
        if (footstepSounds != null && footstepSounds.Count > 0)
            Play(footstepSounds[Random.Range(0, footstepSounds.Count)], 0.15f);
    }

    public void PlayLandingSound()
    {
        if (landingSounds != null && landingSounds.Count > 0)
            Play(landingSounds[Random.Range(0, landingSounds.Count)], 0.3f);
        Play(landingSound, 0.3f);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || SFXManager.Instance == null)
            return;
        AudioSource source = SFXManager.Instance.PlaySFXAtLocation(clip, transform, volume);
        SFXManager.Instance.SetSpatial(source, IsSpatial);
    }
}
