#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sons des pigeons : ceux de son propre pigeon en 2D, ceux des autres pigeons localisés en 3D autour d'eux.
public partial class ScenarioRunner
{
    public const string PigeonAudioScenario = "Sons : 2D pour soi, 3D pour les autres";

    IEnumerator RunPigeonAudio()
    {
        yield return Check("SFXManager présent", SFXManager.Instance != null);

        var bots = new List<SpawnedBot>();
        Vector3 botPosition = Pigeon.transform.position + Pigeon.transform.right * 10f + Vector3.up * 0.5f;
        yield return SpawnBot(botPosition, Pigeon.transform.rotation, bots);
        PigeonController other = bots[0].Pigeon;
        yield return Check("Le joueur est le pigeon local", PigeonController.Local == Pigeon);

        // Même chemin que le jeu : l'événement de décollage joue le son via PigeonSFX.
        Pigeon.OnTakeoff.Invoke();
        other.OnTakeoff.Invoke();
        yield return Expect("Un son est joué sur chaque pigeon",
            () => Pigeon.GetComponentsInChildren<AudioSource>().Length > 0 && other.GetComponentsInChildren<AudioSource>().Length > 0, 1f,
            () => $"sources : joueur {Pigeon.GetComponentsInChildren<AudioSource>().Length}, bot {other.GetComponentsInChildren<AudioSource>().Length}");

        AudioSource[] own = Pigeon.GetComponentsInChildren<AudioSource>();
        AudioSource[] others = other.GetComponentsInChildren<AudioSource>();
        yield return Check("Sons de son propre pigeon en 2D", System.Array.TrueForAll(own, s => s.spatialBlend == 0f),
            DescribeSources(own));
        yield return Check("Sons de l'autre pigeon localisés en 3D", System.Array.TrueForAll(others, s => s.spatialBlend == 1f
            && s.rolloffMode == AudioRolloffMode.Linear && s.maxDistance > s.minDistance && s.dopplerLevel == 0f), DescribeSources(others));
        yield return Check("Le son de l'autre pigeon part de sa position",
            System.Array.TrueForAll(others, s => Vector3.Distance(s.transform.position, other.transform.position) < 0.5f));
    }

    static string DescribeSources(AudioSource[] sources)
    {
        var sb = new System.Text.StringBuilder();
        foreach (AudioSource s in sources)
            sb.Append($"[{(s.clip != null ? s.clip.name : "?")} : 3D {s.spatialBlend:0.##}, {s.minDistance:0}-{s.maxDistance:0} m, {s.rolloffMode}] ");
        return sb.ToString();
    }
}
#endif
