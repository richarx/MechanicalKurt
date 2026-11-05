using UnityEngine;

/// <summary>Succès des anneaux : plusieurs boosts enchaînés par le pigeon du joueur local.</summary>
public class RingsAchievementCheck : MonoBehaviour
{
    [SerializeField] private AchievementData ringAchievement;

    private PigeonController pigeon;
    private float lastBoostTimestamp;
    private int currentBoostCombo;
    private const int targetBoostCombo = 5;

    private void OnEnable()
    {
        PigeonController.LocalChanged += Bind;
        Bind(PigeonController.Local);
    }

    private void OnDisable()
    {
        PigeonController.LocalChanged -= Bind;
        Bind(null);
    }

    private void Bind(PigeonController newPigeon)
    {
        if (pigeon != null)
            pigeon.OnBoosted.RemoveListener(HandleBoost);
        pigeon = newPigeon;
        if (pigeon != null)
            pigeon.OnBoosted.AddListener(HandleBoost);
    }

    private void HandleBoost()
    {
        if (Time.time - lastBoostTimestamp <= 2.0f)
            currentBoostCombo += 1;
        else
            currentBoostCombo = 0;

        lastBoostTimestamp = Time.time;

        AchievementManager achievements = AchievementManager.Instance;
        if (achievements != null && currentBoostCombo >= targetBoostCombo && !achievements.IsUnlocked(ringAchievement))
        {
            achievements.Unlock(ringAchievement);
            enabled = false;
        }
    }
}
