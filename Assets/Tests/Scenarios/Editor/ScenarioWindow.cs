using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Cases à cocher pour choisir les scénarios à lancer ; la sélection est gardée entre les sessions.</summary>
public class ScenarioWindow : EditorWindow
{
    const string SelectionPref = "ScenarioWindow.Selection";

    readonly HashSet<string> _selected = new HashSet<string>();
    Vector2 _scroll;

    public static void Open()
    {
        var window = GetWindow<ScenarioWindow>("Scénarios");
        window.minSize = new Vector2(320f, 300f);
    }

    void OnEnable()
    {
        _selected.Clear();
        foreach (string name in EditorPrefs.GetString(SelectionPref, "").Split(ScenarioRunner.FilterSeparator))
            if (name.Length > 0)
                _selected.Add(name);
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawGroup("Solo (l'hôte et des bots)", ScenarioRunner.SoloScenarioNames);
        EditorGUILayout.Space();
        DrawGroup("Multi (avec Player 2)", ScenarioRunner.MultiScenarioNames);
        EditorGUILayout.HelpBox("Scénarios multi : activer Player 2 (Window > Multiplayer > Multiplayer Play Mode) avant de lancer. "
            + "Pendant les scénarios solo, Player 2 est déconnecté de la partie, puis reconnecté.", MessageType.Info);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            List<string> ordered = OrderedSelection();
            using (new EditorGUI.DisabledScope(ordered.Count == 0))
                if (GUILayout.Button($"Lancer la sélection ({ordered.Count})", GUILayout.Height(28f)))
                    ScenarioMenu.Run(string.Join(ScenarioRunner.FilterSeparator.ToString(), ordered));
        }
        if (GUILayout.Button("Ouvrir le dernier rapport"))
            ScenarioMenu.OpenReport();
    }

    void DrawGroup(string title, IEnumerable<string> names)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (GUILayout.Button("Tout", EditorStyles.miniButtonLeft, GUILayout.Width(50f)))
                SetAll(names, true);
            if (GUILayout.Button("Aucun", EditorStyles.miniButtonRight, GUILayout.Width(50f)))
                SetAll(names, false);
        }
        foreach (string name in names)
        {
            bool selected = _selected.Contains(name);
            if (EditorGUILayout.ToggleLeft(name, selected) != selected)
                Set(name, !selected);
        }
    }

    void SetAll(IEnumerable<string> names, bool selected)
    {
        foreach (string name in names)
            Set(name, selected);
    }

    void Set(string name, bool selected)
    {
        if (selected)
            _selected.Add(name);
        else
            _selected.Remove(name);
        EditorPrefs.SetString(SelectionPref, string.Join(ScenarioRunner.FilterSeparator.ToString(), _selected));
    }

    /// <summary>Sélection dans l'ordre du catalogue (les noms disparus du catalogue sont ignorés).</summary>
    List<string> OrderedSelection()
    {
        var ordered = new List<string>();
        foreach (string name in ScenarioRunner.SoloScenarioNames)
            if (_selected.Contains(name))
                ordered.Add(name);
        foreach (string name in ScenarioRunner.MultiScenarioNames)
            if (_selected.Contains(name))
                ordered.Add(name);
        return ordered;
    }
}
