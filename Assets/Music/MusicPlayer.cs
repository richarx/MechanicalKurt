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

    public enum Songs
    {
        NightDelivery
    }

    private void Awake()
    {
        instance = this;
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

    public void PlayMusic(Songs song)
    {
        Debug.Log("Play Music");

        Sequence sequence = DOTween.Sequence();

        if (audioSource.isPlaying)
            sequence.Append(audioSource.DOFade(0.0f, 1.0f));

        audioSource.Stop();
        audioSource.clip = GetClip(song);
        audioSource.Play();

        sequence.Append(audioSource.DOFade(1.0f, 3.0f));
    }

    public void LowerMusicVolume(float duration = -1.0f)
    {
        Debug.Log("Lower Music");

        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        if (audioSource.isPlaying)
            currentSequence.Append(audioSource.DOFade(0.2f, 1.0f));

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
            currentSequence.Append(audioSource.DOFade(1.0f, 1.0f));
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
