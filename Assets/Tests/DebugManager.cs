using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Options de debug réglées dans l'inspecteur avant Play. Éditeur uniquement : ce composant ne fait rien en build.
// N'agit que sur l'hôte : un client (ex. joueur virtuel de Multiplayer Play Mode) ignore ces options,
// les bots créés par l'hôte lui sont répliqués automatiquement.
[DisallowMultipleComponent]
public class DebugManager : MonoBehaviour
{
    // Champs lus seulement dans l'éditeur.
#pragma warning disable CS0414
    [Header("Scénarios au lancement")]
    [Tooltip("Joue les scénarios cochés dès le Play (même chose que Tools > Scénarios).")]
    [SerializeField] bool _runScenariosOnPlay;
    [SerializeField] bool _ringScenario = true;
    [SerializeField] bool _tutoScenario = true;
    [SerializeField] bool _raceStartScenario = true;
    [SerializeField] bool _raceHeightScenario = true;

    [Header("Bots en plus")]
    [Tooltip("Pigeons bots immobiles créés autour du joueur. Ignoré quand des scénarios tournent (ils créent leurs propres bots).")]
    [SerializeField, Range(0, 8)] int _extraBots;
    [SerializeField, Min(1f)] float _botSpacing = 2.5f;
#pragma warning restore CS0414

#if UNITY_EDITOR
    readonly List<PigeonController> _bots = new List<PigeonController>();

    IEnumerator Start()
    {
        float deadline = Time.time + 20f;
        while (PigeonController.Local == null && Time.time < deadline)
            yield return null;
        if (PigeonController.Local == null)
            yield break;

        if (!BotSpawner.IsHost)
        {
            if (_runScenariosOnPlay || _extraBots > 0)
                Debug.Log("DebugManager : client, options ignorées (c'est l'hôte qui crée les bots et lance les scénarios).", this);
            yield break;
        }

        if (_runScenariosOnPlay)
        {
            string filter = BuildFilter();
            if (string.IsNullOrEmpty(filter))
                Debug.LogWarning("DebugManager : aucun scénario coché.", this);
            else
                ScenarioRunner.Launch(filter);
        }

        if (_extraBots > 0)
        {
            // Laisser un éventuel ScenarioRunner (menu ou option ci-dessus) démarrer avant de décider.
            yield return null;
            if (ScenarioRunner.IsRunning)
                Debug.Log("DebugManager : scénarios en cours, bots en plus ignorés.", this);
            else
                yield return SpawnExtraBots();
        }
    }

    string BuildFilter()
    {
        var names = new List<string>();
        if (_ringScenario) names.Add(ScenarioRunner.RingScenario);
        if (_tutoScenario) names.Add(ScenarioRunner.TutoScenario);
        if (_raceStartScenario) names.Add(ScenarioRunner.RaceStartScenario);
        if (_raceHeightScenario) names.Add(ScenarioRunner.RaceHeightScenario);
        return names.Count == 4 ? ScenarioRunner.AllScenarios : string.Join(",", names);
    }

    IEnumerator SpawnExtraBots()
    {
        Transform player = PigeonController.Local.transform;
        for (int i = 0; i < _extraBots; i++)
        {
            float angle = i * Mathf.PI * 2f / _extraBots;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _botSpacing;
            Vector3 position = player.position + offset + Vector3.up * 0.5f;
            Quaternion rotation = Quaternion.LookRotation(-offset, Vector3.up);

            PigeonController bot = null;
            yield return BotSpawner.Spawn(position, rotation, $"Debug Bot {i + 1}", (p, _) => bot = p);
            if (bot != null)
                _bots.Add(bot);
            else
                Debug.LogWarning($"DebugManager : le bot {i + 1} n'est pas apparu.", this);
        }
        Debug.Log($"DebugManager : {_bots.Count} bot(s) en plus créés.", this);
    }
#endif
}
