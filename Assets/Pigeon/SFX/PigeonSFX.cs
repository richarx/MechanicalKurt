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

    // Son propre pigeon : 2D, toujours au même volume. Les autres (joueurs distants, bots) : localisés en 3D.
    // Pigeon local réévalué à chaque son : il peut être désigné après le Start de ce composant.
    private bool IsSpatial => pigeonController == null || pigeonController != PigeonController.Local;

    // Les sources suivent le pigeon (enfants de son transform) pour être localisées au bon endroit.
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

        // La source d'input peut être remplacée en cours de route (bot des scénarios).
        if (pigeonController != null && pigeonController.Controls != null)
            controls = pigeonController.Controls;

        // Boucles de vol créées avant que le pigeon local soit connu : les repasser en 2D / 3D.
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

        // Les sources de vol ne sont créées qu'au premier vol plané.
        if (currentAudioSource == null || otherAudioSource == null)
            return;

        currentAudioSource.volume = otherAudioSource.volume;
        currentAudioSource.pitch = otherAudioSource.pitch;

        otherAudioSource.volume = 0.0f;
    }

    // Les boucles de vol sont des enfants du pigeon ; les détruire explicitement coupe aussi leurs tweens.
    private void OnDestroy()
    {
        flySequence?.Kill();
        if (clearFlySource != null)
            Destroy(clearFlySource.gameObject);
        if (lowPassFlySource != null)
            Destroy(lowPassFlySource.gameObject);
    }
}
