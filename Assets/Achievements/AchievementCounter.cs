using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AchievementCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text _counterText;
    [SerializeField] private Squeeze _squeeze;

    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void Start()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        if (_subscribed && AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnUnlocked -= HandleUnlocked;
            AchievementManager.Instance.OnReset -= Refresh;
        }
        _subscribed = false;
    }

    private void TrySubscribe()
    {
        if (_subscribed || AchievementManager.Instance == null) return;
        AchievementManager.Instance.OnUnlocked += HandleUnlocked;
        AchievementManager.Instance.OnReset += Refresh;
        _subscribed = true;
    }

    private void HandleUnlocked(AchievementData achievement)
    {
        Refresh();
        if (_squeeze != null) _squeeze.Trigger();
    }

    private void Refresh()
    {
        AchievementManager manager = AchievementManager.Instance;
        if (_counterText == null || manager == null) return;
        _counterText.text = $"{manager.UnlockedCount} / {manager.TotalCount}";
    }
}
