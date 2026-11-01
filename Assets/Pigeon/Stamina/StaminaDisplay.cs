using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class StaminaDisplay : MonoBehaviour
{
    [SerializeField] private PigeonStamina pigeonStamina;

    [Space]
    [SerializeField] private Image background;
    [SerializeField] private Image bar;

    [Space]
    [SerializeField] private Gradient colorGradient;
    [SerializeField] private float maxAlpha;

    [Space]
    [SerializeField] private float timeBeforeHide;
    [SerializeField] private float barAnimationSmoothSpeed;

    [Space]
    [SerializeField] private Vector3 positionOffset;

    private CanvasGroup canvasGroup;
    private RectTransform canvasGroupRect;
    private Sequence canvasGroupSequence;
    private Sequence squashSequence;

    private float lastForcedUpdateTimestamp = -1.0f;
    private float barAnimationVelocity;

    private bool isDisplayed;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroupRect = GetComponent<RectTransform>();
        canvasGroup.alpha = 0.0f;

        if (pigeonStamina != null)
            Bind(pigeonStamina);
    }

    /// <summary>Les pigeons apparaissent par le réseau : la jauge suit celui du joueur local.</summary>
    private void Bind(PigeonStamina stamina)
    {
        if (pigeonStamina != null)
        {
            pigeonStamina.OnBoostStamina.RemoveListener(OnBoostStamina);
            pigeonStamina.OnCannotUseStamina.RemoveListener(OnCannotUseStamina);
            pigeonStamina.OnUseStamina.RemoveListener(OnUseStamina);
        }
        pigeonStamina = stamina;
        lastForcedUpdateTimestamp = -1.0f;
        if (pigeonStamina == null)
            return;
        pigeonStamina.OnBoostStamina.AddListener(OnBoostStamina);
        pigeonStamina.OnCannotUseStamina.AddListener(OnCannotUseStamina);
        pigeonStamina.OnUseStamina.AddListener(OnUseStamina);
    }

    private void OnBoostStamina()
    {
        lastForcedUpdateTimestamp = Time.time;
        SquashDislay(true);
    }

    private void OnCannotUseStamina()
    {
        lastForcedUpdateTimestamp = Time.time;
        SquashDislay(false);
    }

    private void OnUseStamina() => lastForcedUpdateTimestamp = Time.time;

    private void Update()
    {
        PigeonController local = PigeonController.Local;
        PigeonStamina localStamina = local != null && local.UsesStamina ? local.GetComponent<PigeonStamina>() : null;
        if (localStamina != pigeonStamina)
            Bind(localStamina);
        if (pigeonStamina == null)
        {
            canvasGroup.alpha = 0.0f;
            isDisplayed = false;
            return;
        }

        bool shouldBeDisplayed = lastForcedUpdateTimestamp >= 0.0f && Time.time - lastForcedUpdateTimestamp <= timeBeforeHide;

        if (shouldBeDisplayed != isDisplayed)
        {
            isDisplayed = shouldBeDisplayed;

            if (isDisplayed)
            {
                SetStaminaValue(pigeonStamina.CurrentStaminaNormalized);
                SetCanvasGroupVisibility(true);
            }
            else
            {
                SetCanvasGroupVisibility(false);
            }
        }

        UpdateStaminaValue(pigeonStamina.CurrentStaminaNormalized);
        UpdateBarColor();
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        canvasGroupRect.position = Camera.main.WorldToScreenPoint(pigeonStamina.transform.position) + positionOffset;
    }

    private void UpdateStaminaValue(float targetAmount)
    {
        bar.fillAmount = Mathf.SmoothDamp(bar.fillAmount, targetAmount, ref barAnimationVelocity, barAnimationSmoothSpeed);
    }

    private void SetStaminaValue(float targetAmount)
    {
        bar.fillAmount = targetAmount;
    }

    private void SetCanvasGroupVisibility(bool visible)
    {
        canvasGroupSequence?.Kill();

        canvasGroupSequence = DOTween.Sequence()
        .Append(canvasGroup.DOFade(visible ? maxAlpha : 0.0f, 1.0f));
    }

    private void UpdateBarColor()
    {
        bar.color = colorGradient.Evaluate(bar.fillAmount);
    }

    private void SquashDislay(bool isPositive)
    {
        if (squashSequence.IsActive())
            return;

        squashSequence?.Kill();

        float squashValue = isPositive ? 0.15f : -0.15f;
        float squashDuration = isPositive ? 0.5f : 0.3f;

        squashSequence = DOTween.Sequence()
            .Append(canvasGroup.transform.DOPunchScale(Vector3.one * squashValue, squashDuration));
    }
}
