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
    [SerializeField] private List<AudioClip> StartDiveClips;
    [SerializeField] private List<AudioClip> StopDiveClips;

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
    private bool isFlySequencePlaying;

    private void Start()
    {
        Water water = FindAnyObjectByType<Water>();
        waterPlane = water != null ? water.transform : null;
        pigeonController = GetComponent<PigeonController>();
        controls = pigeonController != null && pigeonController.Controls != null
            ? pigeonController.Controls : GetComponent<IPigeonControls>();
        buttHoleManager = GetComponent<ButtHoleManager>();

        pigeonController.OnCoo.AddListener(() => PlayRandom(coohClips, 1.0f));
        pigeonController.OnTakeoff.AddListener(() => Play(takeOffClip));
        pigeonController.OnRoll.AddListener(() => PlayRandom(rollClips));
        pigeonController.OnStunned.AddListener(() => Play(hurtClip));
        buttHoleManager.OnShit.AddListener(() => Play(poopClip));
        PauseMenu.OnPauseGame.AddListener(HandlePause);
        pigeonController.OnChangeState.AddListener((from, to) =>
        {
            if (to == PigeonController.MovementState.Diving)
                PlayRandom(StartDiveClips);
            else if (from == PigeonController.MovementState.Diving)
                PlayRandom(StopDiveClips);
        });
    }

    /// <summary>La pause n'arrête que le son du vent de son propre pigeon : les autres continuent de voler.</summary>
    private void HandlePause()
    {
        if (!IsSpatial)
            StopGlideSFX();
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

        bool pausedHere = !IsSpatial && PauseMenu.instance != null && PauseMenu.instance.IsPaused;
        if (pigeonController.CurrentPigeonState == PigeonController.MovementState.Gliding && !pigeonController.IsStun && !pausedHere)
            StartGlideSFX();
        else
            StopGlideSFX();

        // Borné : sans limite, la hauteur devenait négative à basse vitesse (son à l'envers) et montait à ×2,5 en piqué.
        if (isFlySequencePlaying)
            currentAudioSource.pitch = Mathf.Clamp(Tools.NormalizeValue(pigeonController.Speed, 5.0f, 25.0f), 0.5f, 1.5f);
    }

    private void StartGlideSFX()
    {
        if (isFlySequencePlaying)
            return;

        isFlySequencePlaying = true;

        if (currentAudioSource == null)
        {
            clearFlySource = Play(flyLoopClip, 0.5f, loop: true);
            lowPassFlySource = Play(lowPassFlyLoopClip, 0.0f, loop: true);
        }

        if (currentAudioSource.volume < 0.45f)
        {
            flySequence?.Kill();
            flySequence = DOTween.Sequence();

            if (currentAudioSource.isPlaying)
                flySequence.Append(currentAudioSource.DOFade(0.5f, 0.5f));
        }
    }

    private void StopGlideSFX()
    {
        if (!isFlySequencePlaying)
            return;

        isFlySequencePlaying = false;

        if (currentAudioSource != null)
        {
            flySequence?.Kill();
            flySequence = DOTween.Sequence();

            if (currentAudioSource.isPlaying)
                flySequence.Append(currentAudioSource.DOFade(0.0f, 0.5f)).SetUpdate(true);
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
        PauseMenu.OnPauseGame.RemoveListener(HandlePause);
        flySequence?.Kill();
        if (clearFlySource != null)
            Destroy(clearFlySource.gameObject);
        if (lowPassFlySource != null)
            Destroy(lowPassFlySource.gameObject);
    }
}
