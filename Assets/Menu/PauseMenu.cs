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
    private PlayerControls pausedControls;

    private void Start()
    {
        pauseAction.action.Enable();
    }

    private void Update()
    {
        if (pauseAction.action.WasPressedThisFrame())
            SetPaused(!isPaused);

        if (isPaused)
            backgroundMaterial.SetFloat("_UnscaledTime", Time.unscaledTime);
    }

    public bool IsPaused => isPaused;

    public void SetPaused(bool paused)
    {
        if (paused == isPaused)
            return;
        isPaused = paused;
        if (paused)
            OpenMenu();
        else
            CloseMenu();
    }

    private void OpenMenu()
    {
        if (currentSequence != null && currentSequence.IsPlaying())
            currentSequence.Kill();

        currentSequence = DOTween.Sequence();

        SetLocalPigeonPaused(true);

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
        currentSequence.AppendCallback(() => SetLocalPigeonPaused(false));
        currentSequence.SetUpdate(true);
    }

    /// <summary>La pause n'arrête pas le temps (partie partagée) : le pigeon du joueur se met en vol stationnaire.</summary>
    private void SetLocalPigeonPaused(bool paused)
    {
        if (paused)
            pausedControls = PigeonController.Local != null ? PigeonController.Local.GetComponent<PlayerControls>() : null;
        if (pausedControls != null)
            pausedControls.SetPaused(paused);
        if (!paused)
            pausedControls = null;
    }

    private void SetCursorState(bool state)
    {
        Cursor.lockState = state ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = state;
    }

    private void OnDestroy()
    {
        backgroundMaterial.SetFloat("_UnscaledTime", 0.0f);
    }
}
