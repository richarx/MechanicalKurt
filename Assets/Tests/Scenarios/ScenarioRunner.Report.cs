#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public partial class ScenarioRunner
{
    public const string ReportPath = "Logs/ScenarioReport.txt";
    readonly Dictionary<string, int> _achievementBackup = new Dictionary<string, int>();
    bool _achievementsRestored = true;
    Vector2 _scroll;

    void Finish()
    {
        _finished = true;
        UnityEditor.SessionState.EraseBool(ActiveKey);
        _currentScenario = null;
        RestoreAchievements();
        _link?.Dispose();
        _link = null;

        int ok = 0, failed = 0;
        foreach (bool? status in _scenarioStatus.Values)
        {
            if (status == true) ok++;
            else failed++;
        }

        var report = new StringBuilder();
        report.AppendLine($"Scénarios — {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {ok} OK, {failed} en échec");
        string scenario = null;
        foreach (StepResult r in _results)
        {
            if (r.Scenario != scenario)
            {
                scenario = r.Scenario;
                report.AppendLine();
                report.AppendLine($"== {scenario}");
            }
            report.AppendLine($"  {(r.Ok ? "OK  " : "FAIL")} t={r.Time,6:0.00}s  {r.Step}{(r.Detail != null ? "  — " + r.Detail : "")}");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
        }
        catch (Exception e) { Debug.LogException(e); }

        string summary = $"[Scénario] Terminé : {ok} OK, {failed} en échec. Rapport : {ReportPath}";
        if (failed > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);

        if (UnityEditor.SessionState.GetBool(ExitOnFinishKey, false))
        {
            UnityEditor.SessionState.EraseBool(ExitOnFinishKey);
            UnityEditor.EditorApplication.Exit(failed > 0 ? 1 : 0);
        }
    }

    const string AchievementKeyPrefix = "achievement_";

    void BackupAndResetAchievements()
    {
        AchievementManager manager = AchievementManager.Instance;
        if (manager == null || manager.Achievements == null)
            return;
        foreach (AchievementData a in manager.Achievements)
        {
            if (a == null) continue;
            string key = AchievementKeyPrefix + a.Id;
            _achievementBackup[key] = PlayerPrefs.GetInt(key, 0);
        }
        _achievementsRestored = false;
        manager.ResetAll();
    }

    void RestoreAchievements()
    {
        if (_achievementsRestored)
            return;
        _achievementsRestored = true;
        foreach (KeyValuePair<string, int> entry in _achievementBackup)
        {
            if (entry.Value != 0)
                PlayerPrefs.SetInt(entry.Key, entry.Value);
            else
                PlayerPrefs.DeleteKey(entry.Key);
        }
        PlayerPrefs.Save();
    }

    void OnGUI()
    {
        const float width = 460f;
        GUILayout.BeginArea(new Rect(Screen.width - width - 10f, 10f, width, Screen.height * 0.6f), GUI.skin.box);
        GUILayout.Label(_finished ? "<b>Scénarios terminés</b>" : "<b>Scénarios en cours…</b>", RichLabel);
        if (!_finished && _currentScenario != null)
            GUILayout.Label($"▶ {_currentScenario} — {_currentStep} ({Time.realtimeSinceStartup - _scenarioStart:0.0} s)", RichLabel);

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (KeyValuePair<string, bool?> entry in _scenarioStatus)
        {
            string color = entry.Value == null ? "#BBBBBB" : entry.Value == true ? "#66FF66" : "#FF5555";
            string mark = entry.Value == null ? "…" : entry.Value == true ? "✔" : "✘";
            GUILayout.Label($"<color={color}>{mark} {entry.Key}</color>", RichLabel);
            foreach (StepResult r in _results)
            {
                if (r.Scenario != entry.Key || r.Ok)
                    continue;
                GUILayout.Label($"<color=#FF9999>    ✘ {r.Step} (t={r.Time:0.0} s){(r.Detail != null ? "\n       " + r.Detail : "")}</color>", RichLabel);
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    static GUIStyle _richLabel;
    static GUIStyle RichLabel => _richLabel ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
}
#endif
