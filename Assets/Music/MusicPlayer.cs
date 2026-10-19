using System.Collections;
using DG.Tweening;
using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private AudioClip nightDelivery;

    [SerializeField]
    private AudioClip wrongWay;

    [SerializeField]
    private float switchInterval = 120f;

    [SerializeField]
    private float fadeDuration = 3f;

    public static MusicPlayer instance;

    private Sequence currentSequence;
    private Sequence playbackSequence;
    private Songs currentSong;
    private float elapsed;
    private float fadeVolume = 1f;
    private float musicVolume = maxVolume;
    private bool musicStarted;

    private const float maxVolume = 0.1f;
    private const float minVolume = 0.03f;

    public enum Songs
    {
        NightDelivery,
        WrongWay
    }

    private void Awake()
    {
        instance = this;
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(120.0f);

        if (!musicStarted && !audioSource.isPlaying)
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
        AudioClip clip = GetClip(song);

        if (clip == null)
            return;

        playbackSequence?.Kill();
        playbackSequence = DOTween.Sequence();

        if (delay > 0.0f)
            playbackSequence.AppendInterval(delay);

        if (audioSource.isPlaying)
            playbackSequence.Append(DOTween.To(() => fadeVolume, value => fadeVolume = value, 0f, fadeDuration)
                .SetEase(Ease.Linear).OnUpdate(ApplyVolume));

        playbackSequence.AppendCallback(() =>
        {
            audioSource.Stop();
            audioSource.clip = clip;
            fadeVolume = 0f;
            ApplyVolume();
            audioSource.Play();

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
        audioSource.volume = fadeVolume * musicVolume;
    }

    private void OnDestroy()
    {
        playbackSequence?.Kill();
        currentSequence?.Kill();
    }

    private AudioClip GetClip(Songs song)
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
}
