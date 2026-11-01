using UnityEngine;

public class CheckForGoal : MonoBehaviour
{
    [SerializeField] private AchievementData achievement;
    [SerializeField] private AudioClip successSfx;

    private bool isUnlocked => AchievementManager.Instance.IsUnlocked(achievement);

    private void Start()
    {
        if (AchievementManager.Instance != null && isUnlocked)
            enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BeachBall"))
        {
            if (AchievementManager.Instance != null && !isUnlocked)
            {
                AchievementManager.Instance.Unlock(achievement);
                SFXManager.Instance.PlaySFX(successSfx);
            }
        }
    }
}
