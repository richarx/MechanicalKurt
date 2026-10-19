using UnityEngine;

public class SpeedAchievementCheck : MonoBehaviour
{
    [SerializeField] private PigeonController _pigeon;
    [SerializeField] private AchievementData _achievement;
    [SerializeField] private float _speedThreshold = 40f;

    private void Start()
    {
        if (AchievementManager.Instance != null && AchievementManager.Instance.IsUnlocked(_achievement))
            enabled = false;
    }

    private void Update()
    {
        if (_pigeon == null || _pigeon.Speed <= _speedThreshold) return;

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.Unlock(_achievement);
        enabled = false;
    }
}
