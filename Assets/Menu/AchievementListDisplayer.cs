using System;
using System.Collections.Generic;
using UnityEngine;

public class AchievementListDisplayer : MonoBehaviour
{
    [SerializeField] private Transform holder;
    [SerializeField] private AchievementSlot achievementPrefab;

    public void DisplayAchievementList()
    {
        RemoveAllAchievements();

        IReadOnlyList<AchievementData> achievements = AchievementManager.Instance.Achievements;

        foreach (AchievementData data in achievements)
        {
            AchievementSlot slot = Instantiate(achievementPrefab, holder);
            slot.Setup(data, AchievementManager.Instance.IsUnlocked(data));
        }
    }

    private void RemoveAllAchievements()
    {
        for (int i = holder.childCount - 1; i >= 0; i--)
        {
            Destroy(holder.GetChild(i).gameObject);
        }
    }
}
