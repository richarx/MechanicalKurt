using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class AchievementProgressResetter : MonoBehaviour
{
    [field: SerializeField, LabelText("Reset Progress On Start")]
    public bool ResetProgressOnStart { get; private set; } = true;

    [field: SerializeField, LabelText("Launch Tuto")]
    public bool LaunchTuto { get; private set; } = true;

    private void Start()
    {
        if (ResetProgressOnStart)
        {
            AchievementManager manager = AchievementManager.Instance;
            if (manager != null) manager.ResetAll();
            else Debug.LogWarning("Cannot reset progress: no AchievementManager in the scene.", this);
        }

        foreach (AchievementTutorial tutorial in FindObjectsByType<AchievementTutorial>(FindObjectsInactive.Include))
        {
            if (tutorial.gameObject.scene != gameObject.scene) continue;
            if (LaunchTuto) tutorial.gameObject.SetActive(true);
            tutorial.enabled = LaunchTuto;
            if (LaunchTuto) tutorial.RefreshProgress();
        }
    }
}
