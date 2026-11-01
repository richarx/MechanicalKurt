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

    public readonly struct FlightPlaybackState
    {
        public int ClearSamples { get; }
        public int LowPassSamples { get; }
        public bool ClearPlaying { get; }
        public bool LowPassPlaying { get; }
        public float ClearVolume { get; }
        public float LowPassVolume { get; }
        public float ClearPitch { get; }
        public float LowPassPitch { get; }
        public bool LowPassActive { get; }
        public bool UnderWater { get; }

        public FlightPlaybackState(PigeonSFX sfx)
        {
            AudioSource clear = sfx.clearFlySource;
            AudioSource lowPass = sfx.lowPassFlySource;
            ClearSamples = clear != null ? clear.timeSamples : 0;
            LowPassSamples = lowPass != null ? lowPass.timeSamples : 0;
            ClearPlaying = clear != null && clear.isPlaying;
            LowPassPlaying = lowPass != null && lowPass.isPlaying;
            ClearVolume = clear != null ? clear.volume : 0f;
            LowPassVolume = lowPass != null ? lowPass.volume : 0f;
            ClearPitch = clear != null ? clear.pitch : 1f;
            LowPassPitch = lowPass != null ? lowPass.pitch : 1f;
            LowPassActive = sfx.isLowPassActive;
            UnderWater = sfx.isCurrentlyUnderWater;
        }
    }

    public FlightPlaybackState CaptureFlightPlaybackState() => new FlightPlaybackState(this);

    public void RestoreFlightPlaybackState(FlightPlaybackState state)
    {
        flySequence?.Kill();
        if (state.ClearPlaying || state.LowPassPlaying)
            EnsureFlightSources();

        isLowPassActive = state.LowPassActive;
        isCurrentlyUnderWater = state.UnderWater;
        RestoreFlightSource(clearFlySource, state.ClearSamples, state.ClearPlaying, state.ClearVolume, state.ClearPitch);
        RestoreFlightSource(lowPassFlySource, state.LowPassSamples, state.LowPassPlaying, state.LowPassVolume, state.LowPassPitch);
    }

    private static void RestoreFlightSource(AudioSource source, int samples, bool playing, float volume, float pitch)
    {
        // Les sources de vol sont créées à la demande et peuvent ne pas encore exister.
        if (source == null)
            return;

        source.Stop();
        source.volume = volume;
        source.pitch = pitch;
        if (source.clip == null)
            return;

        source.timeSamples = Mathf.Clamp(samples, 0, source.clip.samples - 1);
        if (playing)
            source.Play();
    }

    private void EnsureFlightSources()
    {
        if (clearFlySource == null)
            clearFlySource = SFXManager.Instance.PlaySFX(flyLoopClip, isLowPassActive ? 0f : 0.5f, loop: true);
        if (lowPassFlySource == null)
            lowPassFlySource = SFXManager.Instance.PlaySFX(lowPassFlyLoopClip, isLowPassActive ? 0.5f : 0f, loop: true);
    }

    private void Start()
    {
        controls = GetComponent<IPigeonControls>();
        pigeonController = GetComponent<PigeonController>();
        buttHoleManager = GetComponent<ButtHoleManager>();

        pigeonController.OnTakeoff.AddListener(() => SFXManager.Instance.PlaySFX(takeOffClip));
        pigeonController.OnRoll.AddListener(() => SFXManager.Instance.PlayRandomSFX(rollClips));
        pigeonController.OnStunned.AddListener(() => SFXManager.Instance.PlaySFX(hurtClip));
        buttHoleManager.OnShit.AddListener(() => SFXManager.Instance.PlaySFX(poopClip));
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

        if (pigeonController.CurrentPigeonState == PigeonController.PigeonState.Gliding && !pigeonController.IsStun)
        {
            EnsureFlightSources();
            if (!clearFlySource.isPlaying)
                clearFlySource.Play();
            if (!lowPassFlySource.isPlaying)
                lowPassFlySource.Play();

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
        if (isLowPassActive == isLowPass)
            return;

        isLowPassActive = isLowPass;

        if (currentAudioSource == null || otherAudioSource == null)
            return;

        currentAudioSource.volume = otherAudioSource.volume;
        currentAudioSource.pitch = otherAudioSource.pitch;

        otherAudioSource.volume = 0.0f;
    }

    private void OnDisable()
    {
        flySequence?.Kill();
        if (clearFlySource != null)
            clearFlySource.Stop();
        if (lowPassFlySource != null)
            lowPassFlySource.Stop();
    }

    private void OnDestroy()
    {
        if (clearFlySource != null)
            Destroy(clearFlySource.gameObject);
        if (lowPassFlySource != null)
            Destroy(lowPassFlySource.gameObject);
    }
}
