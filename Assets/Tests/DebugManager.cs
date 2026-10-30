using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class DebugManager : MonoBehaviour
{
#pragma warning disable CS0414
    [Header("Scénarios au lancement")]
    [Tooltip("Joue les scénarios choisis dès le Play (même chose que Tools > Scénarios).")]
    [SerializeField, PropertyOrder(0)] bool _runScenariosOnPlay;
    [Tooltip("Un par un avec le menu déroulant, ou d'un coup avec les boutons. Liste vide = tous les scénarios solo.")]
    [SerializeField, ShowIf(nameof(_runScenariosOnPlay)), PropertyOrder(1)]
    [ValueDropdown("ScenarioOptions", IsUniqueList = true, ExcludeExistingValuesInList = true, DrawDropdownForListElements = false)]
    [InfoBox("Scénarios multi : activer Player 2 (Window > Multiplayer > Multiplayer Play Mode) avant Play. "
        + "Pendant les scénarios solo, Player 2 est déconnecté de la partie, puis reconnecté.", "HasMultiScenario")]
    List<string> _scenarios = new List<string>();

    [Header("Bots en plus")]
    [Tooltip("Pigeons bots immobiles créés autour du joueur. Ignoré quand des scénarios tournent (ils créent leurs propres bots).")]
    [SerializeField, Range(0, 8), PropertyOrder(3)] int _extraBots;
    [SerializeField, Min(1f), PropertyOrder(4)] float _botSpacing = 2.5f;
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
            ScenarioRunner.Launch(BuildFilter());
        }

        if (_extraBots > 0)
        {
            yield return null;
            if (ScenarioRunner.IsRunning)
                Debug.Log("DebugManager : scénarios en cours, bots en plus ignorés.", this);
            else
                yield return SpawnExtraBots();
        }
    }

    static IEnumerable ScenarioOptions()
    {
        var options = new ValueDropdownList<string>();
        foreach (string name in ScenarioRunner.SoloScenarioNames)
            options.Add("Solo/" + name, name);
        foreach (string name in ScenarioRunner.MultiScenarioNames)
            options.Add("Multi/" + name, name);
        return options;
    }

    bool HasMultiScenario()
    {
        foreach (string name in _scenarios)
            if (ScenarioRunner.IsMultiScenario(name))
                return true;
        return false;
    }

    [ShowIf(nameof(_runScenariosOnPlay)), HorizontalGroup("ScenarioButtons"), PropertyOrder(2), Button("+ Tous les solo")]
    void AddAllSolo() => AddScenarios(ScenarioRunner.SoloScenarioNames, "Ajouter tous les scénarios solo");

    [ShowIf(nameof(_runScenariosOnPlay)), HorizontalGroup("ScenarioButtons"), PropertyOrder(2), Button("+ Tous les multi")]
    void AddAllMulti() => AddScenarios(ScenarioRunner.MultiScenarioNames, "Ajouter tous les scénarios multi");

    [ShowIf(nameof(_runScenariosOnPlay)), HorizontalGroup("ScenarioButtons"), PropertyOrder(2), Button("Vider")]
    void ClearScenarios()
    {
        UnityEditor.Undo.RecordObject(this, "Vider la liste des scénarios");
        _scenarios.Clear();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    void AddScenarios(IEnumerable<string> names, string undoName)
    {
        UnityEditor.Undo.RecordObject(this, undoName);
        foreach (string name in names)
            if (!_scenarios.Contains(name))
                _scenarios.Add(name);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    string BuildFilter() => _scenarios == null || _scenarios.Count == 0
        ? ScenarioRunner.AllSolo
        : string.Join(ScenarioRunner.FilterSeparator.ToString(), _scenarios);

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
