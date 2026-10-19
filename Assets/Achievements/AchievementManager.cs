using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class AchievementManager : MonoBehaviour
{
    private const string SaveKeyPrefix = "achievement_";

    public static AchievementManager Instance { get; private set; }

    [SerializeField] private AchievementData[] _achievements;

    public event Action<AchievementData> OnUnlocked;
    public event Action OnReset;

    private readonly HashSet<string> _unlocked = new HashSet<string>();
    private readonly HashSet<string> _knownIds = new HashSet<string>();

    public int UnlockedCount => _unlocked.Count;
    public int TotalCount => _knownIds.Count;
    public IReadOnlyList<AchievementData> Achievements => _achievements;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsUnlocked(AchievementData achievement)
    {
        return achievement != null && _unlocked.Contains(achievement.Id);
    }

    public void Unlock(AchievementData achievement)
    {
        if (achievement == null) return;

        string id = achievement.Id;
        if (!_knownIds.Contains(id))
        {
            Debug.LogWarning($"[Achievements] '{id}' is not registered in the AchievementManager list.", this);
            return;
        }
        if (!_unlocked.Add(id)) return;

        PlayerPrefs.SetInt(SaveKeyPrefix + id, 1);
        PlayerPrefs.Save();
        OnUnlocked?.Invoke(achievement);
    }

    [ContextMenu("Reset All Achievements")]
    public void ResetAll()
    {
        if (_achievements != null)
        {
            foreach (AchievementData achievement in _achievements)
            {
                if (achievement != null)
                    PlayerPrefs.DeleteKey(SaveKeyPrefix + achievement.Id);
            }
        }
        PlayerPrefs.Save();
        _unlocked.Clear();
        OnReset?.Invoke();
    }

    private void Load()
    {
        _knownIds.Clear();
        _unlocked.Clear();
        if (_achievements == null) return;

        foreach (AchievementData achievement in _achievements)
        {
            if (achievement == null) continue;

            string id = achievement.Id;
            if (!_knownIds.Add(id))
            {
                Debug.LogWarning($"[Achievements] Duplicate id '{id}' ({achievement.name}).", achievement);
                continue;
            }
            if (PlayerPrefs.GetInt(SaveKeyPrefix + id, 0) == 1)
                _unlocked.Add(id);
        }
    }
}
