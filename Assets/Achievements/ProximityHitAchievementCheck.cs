using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AchievementManager))]
public class ProximityHitAchievementCheck : MonoBehaviour
{
    [SerializeField] private AchievementData _achievement;
    [SerializeField] private int _requiredHits = 10;

    private int _hitCount = 0;

    private AchievementManager _manager;

    private void Awake()
    {
        _manager = GetComponent<AchievementManager>();
    }

    private void OnEnable()
    {
        PigeonProximityHit.HitPerformed += HandleHit;
        _manager.OnReset += ResetProgress;
    }

    private void OnDisable()
    {
        PigeonProximityHit.HitPerformed -= HandleHit;
        if (_manager != null)
            _manager.OnReset -= ResetProgress;
    }

    private void HandleHit(PigeonProximityHit hit)
    {
        if (hit.gameObject.scene != gameObject.scene)
            return;

        _hitCount++;
        if (_hitCount >= _requiredHits && !_manager.IsUnlocked(_achievement))
            _manager.Unlock(_achievement);
    }

    private void ResetProgress()
    {
        _hitCount = 0;
    }
}
