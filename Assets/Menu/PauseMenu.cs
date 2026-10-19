using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuHolder;

    [Space]
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private CanvasGroup volume;
    [SerializeField] private CanvasGroup controls;
    [SerializeField] private CanvasGroup achievements;
    [SerializeField] private TextMeshProUGUI title;

    [Space]
    [SerializeField] private AchievementListDisplayer achievementListDisplayer;

    [Space]
    [SerializeField] private InputActionReference pauseAction;

    private bool isPaused;

    private Sequence currentSequence;

    private void Start()
    {

    }

    private void Update()
    {
        if (pauseAction.action.WasPressedThisFrame())
        {
            isPaused = !isPaused;

            if (isPaused)
                OpenMenu();
            else
                CloseMenu();
        }

        if (isPaused)
            backgroundMaterial.SetFloat("_UnscaledTime", Time.unscaledTime);
    }

    private void OpenMenu()
    {
        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        Time.timeScale = 0.0f;

        achievementListDisplayer.DisplayAchievementList();
        menuHolder.SetActive(true);

        currentSequence.Append(backgroundMaterial.DOColor(Color.white, "_Tint", 0.5f));
        currentSequence.Append(title.DOFade(1.0f, 0.3f));
        currentSequence.Join(volume.DOFade(1.0f, 0.3f));
        currentSequence.Join(controls.DOFade(1.0f, 0.3f));
        currentSequence.Join(achievements.DOFade(1.0f, 0.3f));
        currentSequence.AppendCallback(() => SetCursorState(true));
        currentSequence.SetUpdate(true);
    }

    private void CloseMenu()
    {
        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        SetCursorState(false);
        currentSequence.Join(volume.DOFade(0.0f, 0.1f));
        currentSequence.Join(controls.DOFade(0.0f, 0.1f));
        currentSequence.Join(achievements.DOFade(0.0f, 0.1f));
        currentSequence.Join(title.DOFade(0.0f, 0.1f));
        currentSequence.Append(backgroundMaterial.DOColor(Color.clear, "_Tint", 0.3f));
        currentSequence.AppendCallback(() => menuHolder.SetActive(false));
        currentSequence.AppendCallback(() => Time.timeScale = 1.0f);
        currentSequence.SetUpdate(true);
    }

    private void SetCursorState(bool state)
    {
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;
    }
}
