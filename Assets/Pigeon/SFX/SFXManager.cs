using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class SFXManager : MonoBehaviour
{
    [SerializeField] private AudioSource audioSourcePrefab;

    [Header("Sons localisés (pigeons des autres joueurs)")]
    [SerializeField, Tooltip("Volume plein jusqu'à cette distance.")] private float spatialMinDistance = 3f;
    [SerializeField, Tooltip("Inaudible au-delà (atténuation linéaire).")] private float spatialMaxDistance = 50f;

    public static SFXManager Instance;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    public AudioSource PlayRandomSFX(List<AudioClip> clips, float volume = 0.3f, float delay = 0.0f, bool loop = false, float pitch = 1f)
    {
        return PlayRandomSFXAtLocation(clips, null, volume, delay, loop, pitch);
    }

    public AudioSource PlayRandomSFXAtLocation(List<AudioClip> clips, Transform target, float volume = 0.3f, float delay = 0.0f, bool loop = false, float pitch = 1f)
    {
        int index = Random.Range(0, clips.Count);

        return PlaySFXAtLocation(clips[index], target, volume, delay, loop, pitch);
    }

    public AudioSource PlaySFXAtLocation(AudioClip clip, Transform target, float volume = 0.3f, float delay = 0.0f, bool loop = false, float pitch = 1f)
    {
        Transform parent = target != null ? target : transform;

        AudioSource source = Instantiate(audioSourcePrefab, parent.position, Quaternion.identity, parent);

        source.clip = clip;
        source.volume = volume;
        source.loop = loop;
        source.pitch = pitch + Random.Range(-0.05f, 0.05f);
        if (delay <= 0.0f)
            source.Play();
        else
            source.PlayDelayed(delay);

        if (!loop)
            Destroy(source.gameObject, clip.length + delay);

        return source;
    }

    /// <summary>2D (son de son propre pigeon) ou localisé en 3D autour de la source (pigeon d'un autre joueur).</summary>
    public void SetSpatial(AudioSource source, bool spatial)
    {
        if (source == null)
            return;
        source.spatialBlend = spatial ? 1f : 0f;
        if (!spatial)
            return;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = spatialMinDistance;
        source.maxDistance = Mathf.Max(spatialMinDistance + 0.1f, spatialMaxDistance);
        source.dopplerLevel = 0f;
    }

    public AudioSource PlaySFX(AudioClip clip, float volume = 0.3f, float delay = 0.0f, bool loop = false, float pitch = 1f)
    {
        return PlaySFXAtLocation(clip, null, volume, delay, loop, pitch);
    }
    public AudioSource PlaySFXAtLocationNoPitchModifier(AudioClip clip, Transform target, float volume = 0.3f, float delay = 0.0f, bool loop = false)
    {
        Transform parent = target != null ? target : transform;

        AudioSource source = Instantiate(audioSourcePrefab, parent.position, Quaternion.identity, parent);

        source.clip = clip;
        source.volume = volume;
        source.loop = loop;
        if (delay <= 0.0f)
            source.Play();
        else
            source.PlayDelayed(delay);

        if (!loop)
            Destroy(source.gameObject, clip.length + delay);

        return source;
    }
    public AudioSource PlaySFXNoPitchModifier(AudioClip clip, float volume = 0.3f, float delay = 0.0f, bool loop = false)
    {
        return PlaySFXAtLocationNoPitchModifier(clip, null, volume, delay, loop);
    }
}