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
    [SerializeField] private Transform waterPlane;
    [SerializeField] private AudioClip enterWater;
    [SerializeField] private AudioClip exitWater;

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
        controls = GetComponent<IPigeonControls>();
        pigeonController = GetComponent<PigeonController>();
        buttHoleManager = GetComponent<ButtHoleManager>();

        pigeonController.OnTakeoff.AddListener(() => SFXManager.Instance.PlaySFX(takeOffClip));
        pigeonController.OnRoll.AddListener(() => SFXManager.Instance.PlayRandomSFX(rollClips));
        pigeonController.OnStunned.AddListener(() => SFXManager.Instance.PlaySFX(hurtClip));
        buttHoleManager.OnShit.AddListener(() => SFXManager.Instance.PlaySFX(poopClip));
        PauseMenu.OnPauseGame.AddListener(StopGlideSFX);
        pigeonController.OnChangeState.AddListener((from, to) =>
        {
            if (to == PigeonController.PigeonState.Diving)
                SFXManager.Instance.PlayRandomSFX(StartDiveClips);
            else if (from == PigeonController.PigeonState.Diving)
                SFXManager.Instance.PlayRandomSFX(StopDiveClips);
        });
    }

    private void Update()
    {
        if (controls.ConsumeCooing)
            SFXManager.Instance.PlayRandomSFX(coohClips, 1.0f);

        bool isUnderWater = transform.position.y < waterPlane.position.y;
        if (isUnderWater != isCurrentlyUnderWater)
        {
            isCurrentlyUnderWater = isUnderWater;

            if (isCurrentlyUnderWater)
                SFXManager.Instance.PlaySFX(enterWater);
            else
                SFXManager.Instance.PlaySFX(exitWater);
        }

        if (pigeonController.CurrentPigeonState == PigeonController.PigeonState.Gliding && !pigeonController.IsStun && !PauseMenu.instance.IsPaused)
            StartGlideSFX();
        else
            StopGlideSFX();

        if (isFlySequencePlaying)
            currentAudioSource.pitch = Tools.NormalizeValue(pigeonController.Speed, 5.0f, 25.0f);
    }

    private void StartGlideSFX()
    {
        if (isFlySequencePlaying)
            return;

        isFlySequencePlaying = true;

        if (currentAudioSource == null)
        {
            clearFlySource = SFXManager.Instance.PlaySFX(flyLoopClip, 0.5f, loop: true);
            lowPassFlySource = SFXManager.Instance.PlaySFX(lowPassFlyLoopClip, 0.0f, loop: true);
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
            {
                flySequence.Append(currentAudioSource.DOFade(0.0f, 0.5f))
                .SetUpdate(true);
            }
        }
    }

    public void SetLowPassMode(bool isLowPass)
    {
        isLowPassActive = isLowPass;

        currentAudioSource.volume = otherAudioSource.volume;
        currentAudioSource.pitch = otherAudioSource.pitch;

        otherAudioSource.volume = 0.0f;
    }
}
