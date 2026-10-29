#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

// Création de pigeons bots (contrôlés par le serveur, pilotés par un BotControls).
// Hôte uniquement : la création passe par la simulation du pigeon local (PigeonController.RunInSimulation),
// puis PurrNet réplique le bot chez les clients comme n'importe quel pigeon.
public static class BotSpawner
{
    const string PlayerPrefabPath = "Assets/Pigeon/Player.prefab";

    public static bool IsHost => PurrNet.NetworkManager.main != null && PurrNet.NetworkManager.main.isServer;

    /// <summary>Crée un bot et appelle <paramref name="onSpawned"/> avec lui (null après <paramref name="timeout"/>).</summary>
    public static IEnumerator Spawn(Vector3 position, Quaternion rotation, string name, Action<PigeonController, BotControls> onSpawned, float timeout = 3f)
    {
        GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var before = new HashSet<PigeonController>(PigeonController.All);
        PigeonController.RunInSimulation(manager => manager.hierarchy.Create(prefab, position, rotation, null));

        float deadline = Time.realtimeSinceStartup + timeout;
        PigeonController spawned = null;
        while (spawned == null && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            foreach (PigeonController p in PigeonController.All)
                if (!before.Contains(p))
                    spawned = p;
        }

        if (spawned == null)
        {
            onSpawned(null, null);
            yield break;
        }

        spawned.name = name;
        MutePresentation(spawned);
        var controls = new BotControls();
        spawned.SetControlsOverride(controls);
        onSpawned(spawned, controls);
    }

    public static void Delete(PigeonController bot)
    {
        // Peut être demandé deux fois (scénario puis nettoyage) : le bot peut être détruit quand la commande passe.
        if (bot != null)
            PigeonController.RunInSimulation(manager => { if (bot != null) manager.hierarchy.Delete(bot.gameObject); });
    }

    // Le bot ne doit ni prendre la caméra, ni l'audio, ni lire le clavier.
    static void MutePresentation(PigeonController bot)
    {
        foreach (CinemachineCamera cam in bot.GetComponentsInChildren<CinemachineCamera>(true))
            cam.gameObject.SetActive(false);
        foreach (Camera cam in bot.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;
        foreach (AudioListener listener in bot.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
        foreach (PlayerControls controls in bot.GetComponentsInChildren<PlayerControls>(true))
            controls.enabled = false;
    }
}
#endif
