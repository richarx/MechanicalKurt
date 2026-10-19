using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;

    [Space]
    [SerializeField] private AudioClip nightDelivery;

    public static MusicPlayer instance;

    private Sequence currentSequence;

    private const float maxVolume = 0.1f;
    private const float minVolume = 0.03f;

    public enum Songs
    {
        NightDelivery
    }

    private void Awake()
    {
        instance = this;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(120.0f);

        if (!audioSource.isPlaying)
            PlayMusic(Songs.NightDelivery);
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
        Debug.Log("Play Music");

        Sequence sequence = DOTween.Sequence();

        if (delay > 0.0f)
            sequence.AppendInterval(delay);

        if (audioSource.isPlaying)
            sequence.Append(audioSource.DOFade(0.0f, 1.0f));

        audioSource.Stop();
        audioSource.clip = GetClip(song);
        audioSource.Play();

        sequence.Append(audioSource.DOFade(maxVolume, 3.0f));
    }

    public void LowerMusicVolume(float duration = -1.0f)
    {
        Debug.Log("Lower Music");

        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        if (audioSource.isPlaying)
            currentSequence.Append(audioSource.DOFade(minVolume, 1.0f));

        if (duration > 0.0f)
        {
            currentSequence.AppendInterval(duration);
            RaiseBackMusicVolume();
        }
    }

    [ContextMenu("Raise Back Music Volume")]
    public void RaiseBackMusicVolume()
    {
        Debug.Log("Raise Music");

        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        if (audioSource.isPlaying)
            currentSequence.Append(audioSource.DOFade(maxVolume, 1.0f));
    }

    private AudioClip GetClip(Songs song)
    {
        switch (song)
        {
            case Songs.NightDelivery:
                return nightDelivery;
            default:
                return nightDelivery;
        }
    }
}
