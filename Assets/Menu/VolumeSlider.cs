using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private AudioMixer musicMixer;

    [Space]
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider musicSlider;

    private void Start()
    {
        sfxMixer.GetFloat("Volume", out float sfxDBVolume);
        sfxSlider.value = FromDBToNormalized(sfxDBVolume);

        musicMixer.GetFloat("Volume", out float musicDBVolume);
        musicSlider.value = FromDBToNormalized(musicDBVolume);

        sfxSlider.onValueChanged.AddListener((volume) => UpdateMixerVolume(sfxMixer, volume));
        musicSlider.onValueChanged.AddListener((volume) => UpdateMixerVolume(musicMixer, volume));
    }

    private void UpdateMixerVolume(AudioMixer mixer, float normalizedVolume)
    {
        mixer.SetFloat("Volume", FromNormalizedToDB(normalizedVolume));
    }

    private float FromDBToNormalized(float db)
    {
        return Mathf.Pow(10f, db / 20f);
    }

    private float FromNormalizedToDB(float normalized)
    {
        normalized = Mathf.Max(normalized, 0.0001f);

        return Mathf.Log10(normalized) * 20f;
    }
}
