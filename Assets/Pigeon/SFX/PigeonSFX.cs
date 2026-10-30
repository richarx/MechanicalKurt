using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PigeonSFX : MonoBehaviour
{
    [SerializeField] private List<AudioClip> coohClips;
    [SerializeField] private AudioClip takeOffClip;
    [SerializeField] private AudioClip flyLoopClip;
    [SerializeField] private AudioClip lowPassFlyLoopClip;
    [SerializeField] private AudioClip poopClip;
    [SerializeField] private AudioClip hurtClip;
    [SerializeField] private List<AudioClip> rollClips;

    [Space]
    [SerializeField] private AudioClip enterWater;
    [SerializeField] private AudioClip exitWater;

    [Space]
    [SerializeField, Tooltip("Bulles autour du pigeon sous l'eau, jouées par UnderWaterEffect (la scène ne peut pas référencer le prefab réseau).")]
    private ParticleSystem underwaterBubbles;

    public ParticleSystem UnderwaterBubbles => underwaterBubbles;

    private Transform waterPlane;
    private IPigeonControls controls;
    private PigeonController pigeonController;
    private ButtHoleManager buttHoleManager;

    private bool isCurrentlyUnderWater;

    private AudioSource clearFlySource;
    private AudioSource lowPassFlySource;

    private bool isLowPassActive;

    private AudioSource currentAudioSource => isLowPassActive ? lowPassFlySource : clearFlySource;
    private AudioSource otherAudioSource => isLowPassActive ? clearFlySource : lowPassFlySource;

    private Sequence flySequence;

    private void Start()
    {
        Water water = FindAnyObjectByType<Water>();
        waterPlane = water != null ? water.transform : null;
        pigeonController = GetComponent<PigeonController>();
        controls = pigeonController != null && pigeonController.Controls != null
            ? pigeonController.Controls : GetComponent<IPigeonControls>();
        buttHoleManager = GetComponent<ButtHoleManager>();

        pigeonController.OnTakeoff.AddListener(() => Play(takeOffClip));
        pigeonController.OnRoll.AddListener(() => PlayRandom(rollClips));
        pigeonController.OnStunned.AddListener(() => Play(hurtClip));
        buttHoleManager.OnShit.AddListener(() => Play(poopClip));
    }

    private bool IsSpatial => pigeonController == null || pigeonController != PigeonController.Local;

    private AudioSource Play(AudioClip clip, float volume = 0.3f, bool loop = false)
    {
        AudioSource source = SFXManager.Instance.PlaySFXAtLocation(clip, transform, volume, loop: loop);
        SFXManager.Instance.SetSpatial(source, IsSpatial);
        return source;
    }

    private void PlayRandom(List<AudioClip> clips, float volume = 0.3f)
    {
        if (clips != null && clips.Count > 0)
            Play(clips[Random.Range(0, clips.Count)], volume);
    }

    private void Update()
    {
        if (controls.ConsumeCooing)
            PlayRandom(coohClips, 1.0f);

        if (pigeonController != null && pigeonController.Controls != null)
            controls = pigeonController.Controls;

        if (clearFlySource != null && (clearFlySource.spatialBlend > 0.5f) != IsSpatial)
        {
            SFXManager.Instance.SetSpatial(clearFlySource, IsSpatial);
            SFXManager.Instance.SetSpatial(lowPassFlySource, IsSpatial);
        }

        bool isUnderWater = waterPlane != null && transform.position.y < waterPlane.position.y;
        if (isUnderWater != isCurrentlyUnderWater)
        {
            isCurrentlyUnderWater = isUnderWater;

            if (isCurrentlyUnderWater)
                Play(enterWater);
            else
                Play(exitWater);
        }

        if (!pigeonController.IsGrounded && !controls.Flap && !pigeonController.IsStun)
        {
            if (currentAudioSource == null)
            {
                clearFlySource = Play(flyLoopClip, 0.5f, loop: true);
                lowPassFlySource = Play(lowPassFlyLoopClip, 0.0f, loop: true);
            }

            flySequence?.Kill();

            if (currentAudioSource.volume < 0.5f)
            {
                flySequence = DOTween.Sequence();

                if (currentAudioSource.isPlaying)
                    flySequence.Append(currentAudioSource.DOFade(0.5f, 0.5f));
            }

            currentAudioSource.pitch = Tools.NormalizeValue(pigeonController.Speed, 5.0f, 25.0f);
        }
        else
        {
            if (currentAudioSource != null)
            {
                flySequence?.Kill();

                flySequence = DOTween.Sequence();

                if (currentAudioSource.isPlaying)
                    flySequence.Append(currentAudioSource.DOFade(0.0f, 0.5f));
            }
        }
    }

    public void SetLowPassMode(bool isLowPass)
    {
        isLowPassActive = isLowPass;

        if (currentAudioSource == null || otherAudioSource == null)
            return;

        currentAudioSource.volume = otherAudioSource.volume;
        currentAudioSource.pitch = otherAudioSource.pitch;

        otherAudioSource.volume = 0.0f;
    }

    private void OnDestroy()
    {
        flySequence?.Kill();
        if (clearFlySource != null)
            Destroy(clearFlySource.gameObject);
        if (lowPassFlySource != null)
            Destroy(lowPassFlySource.gameObject);
    }
}
