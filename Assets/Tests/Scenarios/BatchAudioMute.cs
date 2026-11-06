#if UNITY_EDITOR
using UnityEngine;

/// <summary>Les lancements en batch (scénarios, sonde du menu, second joueur) ne font aucun son ; l'éditeur ouvert à la main n'est pas touché.</summary>
static class BatchAudioMute
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Mute()
    {
        if (Application.isBatchMode)
            AudioListener.volume = 0f;
    }
}
#endif
