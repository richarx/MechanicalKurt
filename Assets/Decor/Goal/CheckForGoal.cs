using UnityEngine;

public class CheckForGoal : MonoBehaviour
{
    [SerializeField] private AchievementData achievement;

    private void Start()
    {
        if (AchievementManager.Instance != null && AchievementManager.Instance.IsUnlocked(achievement))
            enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BeachBall"))
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.Unlock(achievement);
        }
    }
}
