using System;
using UnityEngine;

[DefaultExecutionOrder(50)]
public class OnMovingAchievementCheck : MonoBehaviour
{
    [SerializeField] private PigeonOnMoving pigeonOnMoving;
    [SerializeField] private AchievementData carAchievement;
    [SerializeField] private AchievementData zeppelinAchievement;

    private bool isCarUnlocked => AchievementManager.Instance.IsUnlocked(carAchievement);
    private bool isZeppelinUnlocked => AchievementManager.Instance.IsUnlocked(zeppelinAchievement);

    private float carTimer;
    private const float carTimeThreshold = 1.0f;

    private float zeppelinTimer;
    private const float zeppelinTimeThreshold = 0.3f;

    private void Update()
    {
        if (pigeonOnMoving != null)
        {
            if (!isCarUnlocked)
            {
                if (pigeonOnMoving.SupportTag == "Car")
                {
                    carTimer += Time.deltaTime;
                    if (carTimer >= carTimeThreshold)
                        AchievementManager.Instance.Unlock(carAchievement);
                }
            }

            if (!isZeppelinUnlocked)
            {
                if (pigeonOnMoving.SupportTag == "Zeppelin")
                {
                    zeppelinTimer += Time.deltaTime;
                    if (zeppelinTimer >= zeppelinTimeThreshold)
                        AchievementManager.Instance.Unlock(zeppelinAchievement);
                }
            }
        }
    }
}
