using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource clearAudioSource;

    [SerializeField] private AudioSource lowPassAudioSource;

    [SerializeField] private AudioClip nightDelivery;
    [SerializeField] private AudioClip nightDeliveryLowPass;

    [SerializeField] private AudioClip wrongWay;
    [SerializeField] private AudioClip wrongWayLowPass;

    [SerializeField]
    private float switchInterval = 120f;

    [SerializeField]
    private float fadeDuration = 3f;

    public static MusicPlayer instance;

    private Sequence currentSequence;
    private Sequence playbackSequence;
    private Songs currentSong = Songs.None;
    private float elapsed;
    private float fadeVolume = 1f;
    private float musicVolume = maxVolume;
    private bool musicStarted;
    [ShowInInspector, ReadOnly]
    public bool IsPlayingCinematicMusic { get; private set; }

    public bool CanPlayCinematicMusic => isActiveAndEnabled && !IsPlayingCinematicMusic
        && clearAudioSource != null && clearAudioSource.isActiveAndEnabled
        && lowPassAudioSource != null && lowPassAudioSource.isActiveAndEnabled
        && clearAudioSource != lowPassAudioSource;

    private SourcePlaybackState previousClearAudio;
    private SourcePlaybackState previousLowPassAudio;
    private bool resumePlaybackTween;
    private float previousFadeVolume;

    private struct SourcePlaybackState
    {
        public AudioClip Clip;
        public int TimeSamples;
        public bool WasPlaying;

        public static SourcePlaybackState Capture(AudioSource source) => new SourcePlaybackState
        {
            Clip = source.clip,
            TimeSamples = source.timeSamples,
            WasPlaying = source.isPlaying
        };

        public void Restore(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = Clip;
            if (Clip == null) return;
            if (WasPlaying && source.isActiveAndEnabled) source.Play();
            source.timeSamples = Mathf.Clamp(TimeSamples, 0, Mathf.Max(0, Clip.samples - 1));
        }
    }

    private const float maxVolume = 0.1f;
    private const float minVolume = 0.03f;

    private bool isLowPassActive;

    private AudioSource currentAudioSource => isLowPassActive ? lowPassAudioSource : clearAudioSource;
    private AudioSource otherAudioSource => isLowPassActive ? clearAudioSource : lowPassAudioSource;

    public enum Songs
    {
        None,
        NightDelivery,
        WrongWay
    }

    private void Awake()
    {
        instance = this;

        clearAudioSource.loop = true;
        lowPassAudioSource.loop = true;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(60.0f);
        yield return new WaitUntil(() => !IsPlayingCinematicMusic);

        if (!musicStarted && !currentAudioSource.isPlaying)
            PlayMusic(Songs.NightDelivery);
    }

    private void Update()
    {
        if (IsPlayingCinematicMusic || !musicStarted || nightDelivery == null || wrongWay == null)
            return;

        elapsed += Time.deltaTime;
        if (elapsed >= switchInterval && !playbackSequence.IsActive())
            PlayMusic(currentSong == Songs.NightDelivery ? Songs.WrongWay : Songs.NightDelivery);
    }

    [ContextMenu("Play Music")]
    public void PlayMusicTest()
    {
        PlayMusic(Songs.NightDelivery);
    }

    [ContextMenu("Lower Music Volume")]
    public void LowerMusicTest()
    {
        LowerMusicVolume();
    }

    public void PlayMusic(Songs song, float delay = 0.0f)
    {
        if (IsPlayingCinematicMusic || song == currentSong)
            return;

        playbackSequence?.Kill();
        playbackSequence = DOTween.Sequence();

        if (delay > 0.0f)
            playbackSequence.AppendInterval(delay);

        if (currentAudioSource.isPlaying)
            playbackSequence.Append(DOTween.To(() => fadeVolume, value => fadeVolume = value, 0f, fadeDuration)
                .SetEase(Ease.Linear).OnUpdate(ApplyVolume));

        playbackSequence.AppendCallback(() =>
        {
            clearAudioSource.Stop();
            lowPassAudioSource.Stop();

            clearAudioSource.clip = GetClearClip(song);
            lowPassAudioSource.clip = GetLowPassClip(song);

            fadeVolume = 0f;
            ApplyVolume();

            clearAudioSource.Play();
            lowPassAudioSource.Play();

            currentSong = song;
            elapsed = 0f;
            musicStarted = true;
        });
        playbackSequence.Append(DOTween.To(() => fadeVolume, value => fadeVolume = value, 1f, fadeDuration)
            .SetEase(Ease.Linear).OnUpdate(ApplyVolume));
    }

    public void LowerMusicVolume(float duration = -1.0f)
    {
        Debug.Log("Lower Music");

        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        currentSequence.Append(DOTween.To(() => musicVolume, value => musicVolume = value, minVolume, 1f)
            .OnUpdate(ApplyVolume));

        if (duration > 0.0f)
        {
            currentSequence.AppendInterval(duration);
            currentSequence.Append(DOTween.To(() => musicVolume, value => musicVolume = value, maxVolume, 1f)
                .OnUpdate(ApplyVolume));
        }
    }

    [ContextMenu("Raise Back Music Volume")]
    public void RaiseBackMusicVolume()
    {
        Debug.Log("Raise Music");

        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        currentSequence.Append(DOTween.To(() => musicVolume, value => musicVolume = value, maxVolume, 1f)
            .OnUpdate(ApplyVolume));
    }

    private void ApplyVolume()
    {
        currentAudioSource.volume = fadeVolume * musicVolume;
        otherAudioSource.volume = 0.0f;
    }

    // Uses the existing sources and mixer. Keep the normal song and its transition intact.
    public bool PlayCinematicMusic(AudioClip clip)
    {
        if (clip == null || !CanPlayCinematicMusic)
            return false;

        previousClearAudio = SourcePlaybackState.Capture(clearAudioSource);
        previousLowPassAudio = SourcePlaybackState.Capture(lowPassAudioSource);
        previousFadeVolume = fadeVolume;
        resumePlaybackTween = playbackSequence != null && playbackSequence.IsPlaying();
        playbackSequence?.Pause();
        IsPlayingCinematicMusic = true;

        clearAudioSource.Stop();
        lowPassAudioSource.Stop();
        clearAudioSource.clip = clip;
        lowPassAudioSource.clip = clip;
        fadeVolume = 1f;
        ApplyVolume();
        clearAudioSource.Play();
        lowPassAudioSource.Play();
        return true;
    }

    public void RestoreMusicAfterCinematic()
    {
        if (!IsPlayingCinematicMusic)
            return;

        previousClearAudio.Restore(clearAudioSource);
        previousLowPassAudio.Restore(lowPassAudioSource);
        fadeVolume = previousFadeVolume;
        IsPlayingCinematicMusic = false;
        if (clearAudioSource != null && lowPassAudioSource != null) ApplyVolume();
        if (resumePlaybackTween && playbackSequence.IsActive()) playbackSequence.Play();
        resumePlaybackTween = false;
        previousClearAudio = previousLowPassAudio = default;
    }

    public void SetLowPassMode(bool isLowPass)
    {
        isLowPassActive = isLowPass;
        ApplyVolume();
    }

    private void OnDestroy()
    {
        RestoreMusicAfterCinematic();
        playbackSequence?.Kill();
        currentSequence?.Kill();
    }

    private void OnDisable() => RestoreMusicAfterCinematic();

    private AudioClip GetClearClip(Songs song)
    {
        switch (song)
        {
            case Songs.NightDelivery:
                return nightDelivery;
            case Songs.WrongWay:
                return wrongWay;
            default:
                return nightDelivery;
        }
    }

    private AudioClip GetLowPassClip(Songs song)
    {
        switch (song)
        {
            case Songs.NightDelivery:
                return nightDeliveryLowPass;
            case Songs.WrongWay:
                return wrongWayLowPass;
            default:
                return nightDeliveryLowPass;
        }
    }
}
