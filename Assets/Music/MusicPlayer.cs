using System.Collections;
using DG.Tweening;
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

        if (!musicStarted && !currentAudioSource.isPlaying)
            PlayMusic(Songs.NightDelivery);
    }

    private void Update()
    {
        if (!musicStarted || nightDelivery == null || wrongWay == null)
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
        if (song == currentSong)
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

    public void SetLowPassMode(bool isLowPass)
    {
        isLowPassActive = isLowPass;
        ApplyVolume();
    }

    private void OnDestroy()
    {
        playbackSequence?.Kill();
        currentSequence?.Kill();
    }

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
