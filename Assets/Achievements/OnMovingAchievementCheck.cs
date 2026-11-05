using UnityEngine;

/// <summary>Succès « voiture » et « zeppelin » : le pigeon du joueur local reste posé dessus un moment (tag du support).</summary>
[DefaultExecutionOrder(50)]
public class OnMovingAchievementCheck : MonoBehaviour
{
    [SerializeField] private AchievementData carAchievement;
    [SerializeField] private AchievementData zeppelinAchievement;

    private float carTimer;
    private const float carTimeThreshold = 1.0f;

    private float zeppelinTimer;
    private const float zeppelinTimeThreshold = 0.3f;

    private void Update()
    {
        AchievementManager achievements = AchievementManager.Instance;
        PigeonController pigeon = PigeonController.Local;
        if (achievements == null || pigeon == null)
            return;

        string supportTag = MovingSupport.TryGet(pigeon.SupportId, out MovingSupport support) ? support.gameObject.tag : "";
        bool isCarUnlocked = achievements.IsUnlocked(carAchievement);
        bool isZeppelinUnlocked = achievements.IsUnlocked(zeppelinAchievement);

        if (!isCarUnlocked && supportTag == "Car")
        {
            carTimer += Time.deltaTime;
            if (carTimer >= carTimeThreshold)
                achievements.Unlock(carAchievement);
        }

        if (!isZeppelinUnlocked && supportTag == "Zeppelin")
        {
            zeppelinTimer += Time.deltaTime;
            if (zeppelinTimer >= zeppelinTimeThreshold)
                achievements.Unlock(zeppelinAchievement);
        }

        if (isCarUnlocked && isZeppelinUnlocked)
            enabled = false;
    }
}
