using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class FadingMessage : MonoBehaviour
{
    [field: SerializeField, Required]
    public CanvasGroup Group { get; private set; }

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    public float FadeInSeconds { get; private set; } = 0.25f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    public float HoldSeconds { get; private set; } = 1f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    public float FadeOutSeconds { get; private set; } = 0.5f;

    private Sequence _sequence;

    private void Reset() => Group = GetComponent<CanvasGroup>();

    private void Awake()
    {
        if (Group == null)
            Group = GetComponent<CanvasGroup>();
        Hide();
    }

    public void Show()
    {
        if (!isActiveAndEnabled)
            return;
        if (Group == null)
        {
            Debug.LogWarning("FadingMessage : ajouter et assigner un CanvasGroup au message.", this);
            return;
        }

        Hide();
        Group.gameObject.SetActive(true);
        Group.interactable = false;
        Group.blocksRaycasts = false;
        // Temps réel : le message finit son animation même si le jeu est en pause.
        _sequence = DOTween.Sequence()
            .Append(Group.DOFade(1f, Mathf.Max(0f, FadeInSeconds)))
            .AppendInterval(Mathf.Max(0f, HoldSeconds))
            .Append(Group.DOFade(0f, Mathf.Max(0f, FadeOutSeconds)))
            .SetUpdate(true)
            .OnComplete(() => _sequence = null);
    }

    public void Hide()
    {
        _sequence?.Kill();
        _sequence = null;
        if (Group != null)
        {
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }
    }

    private void OnDisable() => Hide();
}
