using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AchievementPopup : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Image _iconImage;
    [Tooltip("Offset from the panel's laid-out position where it sits while hidden.")]
    [SerializeField] private Vector2 _hiddenOffset = new Vector2(0f, 200f);
    [SerializeField, Min(0f)] private float _slideSeconds = 0.35f;
    [SerializeField, Min(0f)] private float _holdSeconds = 2.5f;

    [SerializeField] private AudioClip _unlockClip;
    [SerializeField] private Sprite defaultSprite;

    private readonly Queue<AchievementData> _queue = new Queue<AchievementData>();
    private Vector2 _shownPosition;
    private Sequence _sequence;
    private bool _subscribed;

    private void Awake()
    {
        _shownPosition = _panel.anchoredPosition;
        _panel.anchoredPosition = _shownPosition + _hiddenOffset;
        _panel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (_subscribed && AchievementManager.Instance != null)
            AchievementManager.Instance.OnUnlocked -= Enqueue;
        _subscribed = false;
        _sequence?.Kill();
        _sequence = null;
        _queue.Clear();
        _panel.anchoredPosition = _shownPosition + _hiddenOffset;
        _panel.gameObject.SetActive(false);
    }

    private void TrySubscribe()
    {
        if (_subscribed || AchievementManager.Instance == null) return;
        AchievementManager.Instance.OnUnlocked += Enqueue;
        _subscribed = true;
    }

    private void Enqueue(AchievementData achievement)
    {
        _queue.Enqueue(achievement);
        if (_sequence == null) ShowNext();
    }

    private void ShowNext()
    {
        if (_queue.Count == 0)
        {
            _sequence = null;
            _panel.gameObject.SetActive(false);
            return;
        }

        AchievementData achievement = _queue.Dequeue();
        if (_titleText != null)
            _titleText.text = achievement.Title;

        if (_descriptionText != null)
            _descriptionText.text = achievement.Description;

        if (_iconImage != null)
            _iconImage.sprite = achievement.Icon != null ? achievement.Icon : defaultSprite;

        _panel.gameObject.SetActive(true);
        _panel.anchoredPosition = _shownPosition + _hiddenOffset;

        if (_unlockClip != null && SFXManager.Instance != null)
            SFXManager.Instance.PlaySFX(_unlockClip);

        _sequence = DOTween.Sequence()
            .Append(_panel.DOAnchorPos(_shownPosition, _slideSeconds).SetEase(Ease.OutBack))
            .AppendInterval(_holdSeconds)
            .Append(_panel.DOAnchorPos(_shownPosition + _hiddenOffset, _slideSeconds).SetEase(Ease.InBack))
            .SetUpdate(true)
            .OnComplete(ShowNext);
    }
}
