using UnityEngine;

public class RingsAchievementCheck : MonoBehaviour
{
    [SerializeField] private PigeonController pigeonController;
    [SerializeField] private AchievementData ringAchievement;

    private float lastBoostTimestamp;
    private int currentBoostCombo;
    private const int targetBoostCombo = 5;

    private void Start()
    {
        pigeonController.OnBoost.AddListener(() =>
        {
            if (Time.time - lastBoostTimestamp <= 2.0f)
                currentBoostCombo += 1;
            else
                currentBoostCombo = 0;

            lastBoostTimestamp = Time.time;

            if (currentBoostCombo >= targetBoostCombo && !AchievementManager.Instance.IsUnlocked(ringAchievement))
            {
                AchievementManager.Instance.Unlock(ringAchievement);
                enabled = false;
            }
        });
    }
}
