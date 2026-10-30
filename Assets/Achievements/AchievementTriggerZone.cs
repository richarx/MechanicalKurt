using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class AchievementTriggerZone : MonoBehaviour
{
    [SerializeField] private AchievementData _achievement;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PigeonController pigeon = other.GetComponentInParent<PigeonController>();
        if (pigeon == null || pigeon != PigeonController.Local) return;
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.Unlock(_achievement);
    }
}
