using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class AchievementTutorialStep
{
    [field: SerializeField, Required]
    public AchievementData Achievement { get; private set; }

    [field: SerializeField, TextArea(2, 4)]
    public string Objective { get; private set; }

    [field: SerializeField, TextArea(2, 4)]
    public string Commands { get; private set; }

    [field: SerializeField, TextArea(2, 4), Tooltip("Gamepad instructions with TMP sprite tags for Xbox glyphs.")]
    public string GamepadCommands { get; private set; }
}

/// <summary>Displays the first tutorial objective whose achievement is still locked.</summary>
[DisallowMultipleComponent]
public class AchievementTutorial : MonoBehaviour
{
    [field: SerializeField, Tooltip("Optional. Uses the scene's AchievementManager when empty.")]
    public AchievementManager Manager { get; private set; }

    [field: SerializeField, ListDrawerSettings(ShowIndexLabels = true)]
    public List<AchievementTutorialStep> Steps { get; private set; } = new List<AchievementTutorialStep>();

    [field: SerializeField, Required]
    public CanvasGroup Panel { get; private set; }

    [field: SerializeField, Required]
    public TMP_Text ObjectiveText { get; private set; }

    [field: SerializeField]
    public TMP_Text CommandsText { get; private set; }

    [field: SerializeField]
    public TMP_Text ProgressText { get; private set; }

    [field: SerializeField, TextArea(2, 4)]
    public string CompletionMessage { get; private set; } = "The city is yours!";

    [field: SerializeField, MinValue(0f), SuffixLabel("seconds", true)]
    public float CompletionSeconds { get; private set; } = 4f;

    [ShowInInspector, ReadOnly]
    public int CurrentStepIndex { get; private set; } = -1;

    [ShowInInspector, ReadOnly]
    public bool IsComplete { get; private set; }

    private AchievementManager _subscribedManager;
    private float _completionRemaining;
    private bool _configurationWarningLogged;
    private bool _usingGamepad;

    private void OnEnable()
    {
        _usingGamepad = Gamepad.current != null;
        BindManager();
        RefreshProgress();
    }

    private void Start()
    {
        // A manager's Awake may run after this component's OnEnable.
        BindManager();
        RefreshProgress();
    }

    private void OnDisable()
    {
        UnbindManager();
        SetVisible(false);
    }

    private void Update()
    {
        if (BindManager()) RefreshProgress();
        bool usingGamepad = Gamepad.current != null;
        if (usingGamepad != _usingGamepad)
        {
            _usingGamepad = usingGamepad;
            RefreshCommands();
        }
        if (_completionRemaining <= 0f) return;
        _completionRemaining -= Time.unscaledDeltaTime;
        if (_completionRemaining <= 0f) SetVisible(false);
    }

    private bool BindManager()
    {
        AchievementManager manager = Manager != null ? Manager : AchievementManager.Instance;
        if (ReferenceEquals(manager, _subscribedManager)) return false;
        UnbindManager();
        _subscribedManager = manager;
        if (_subscribedManager != null)
        {
            _subscribedManager.OnUnlocked += HandleUnlocked;
            _subscribedManager.OnReset += RefreshProgress;
        }
        return true;
    }

    private void UnbindManager()
    {
        if (_subscribedManager != null)
        {
            _subscribedManager.OnUnlocked -= HandleUnlocked;
            _subscribedManager.OnReset -= RefreshProgress;
        }
        _subscribedManager = null;
    }

    [Button("Refresh Tutorial"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void RefreshProgress()
    {
        Refresh(false);
    }

    private void HandleUnlocked(AchievementData achievement)
    {
        Refresh(true);
    }

    private void Refresh(bool showCompletion)
    {
        bool hadActiveStep = CurrentStepIndex >= 0 && !IsComplete;
        CurrentStepIndex = -1;
        IsComplete = false;
        _completionRemaining = 0f;
        SetVisible(false);
        if (_subscribedManager == null || Steps == null || Steps.Count == 0) return;

        // A missing/unregistered achievement must not silently skip an objective.
        foreach (AchievementTutorialStep step in Steps)
        {
            if (step == null || step.Achievement == null || !IsRegistered(step.Achievement))
            {
                if (!_configurationWarningLogged)
                    Debug.LogError("Tutorial: every step needs an achievement registered in its AchievementManager.", this);
                _configurationWarningLogged = true;
                return;
            }
        }
        _configurationWarningLogged = false;

        for (int i = 0; i < Steps.Count; i++)
        {
            AchievementTutorialStep step = Steps[i];
            if (_subscribedManager.IsUnlocked(step.Achievement)) continue;
            CurrentStepIndex = i;
            if (ObjectiveText != null)
                ObjectiveText.text = string.IsNullOrWhiteSpace(step.Objective)
                    ? step.Achievement.Description : step.Objective;
            RefreshCommands();
            if (ProgressText != null) ProgressText.text = $"Tutorial · {i + 1} / {Steps.Count}";
            SetVisible(true);
            return;
        }

        IsComplete = true;
        // Already completed on loading: keep the HUD hidden.
        if (!showCompletion || !hadActiveStep || !(CompletionSeconds > 0f)) return;
        _completionRemaining = CompletionSeconds;
        if (ObjectiveText != null) ObjectiveText.text = CompletionMessage;
        if (CommandsText != null) CommandsText.text = string.Empty;
        if (ProgressText != null) ProgressText.text = "Tutorial complete!";
        SetVisible(true);
    }

    private bool IsRegistered(AchievementData achievement)
    {
        if (_subscribedManager.Achievements == null) return false;
        foreach (AchievementData registered in _subscribedManager.Achievements)
            if (registered != null && registered.Id == achievement.Id) return true;
        return false;
    }

    private void RefreshCommands()
    {
        if (CommandsText == null || Steps == null || CurrentStepIndex < 0 || CurrentStepIndex >= Steps.Count) return;
        AchievementTutorialStep step = Steps[CurrentStepIndex];
        CommandsText.text = _usingGamepad && CommandsText.spriteAsset != null
            && !string.IsNullOrWhiteSpace(step.GamepadCommands) ? step.GamepadCommands : step.Commands;
    }

    private void SetVisible(bool visible)
    {
        if (Panel == null) return;
        // Never deactivate the object holding this component: it must keep listening.
        Panel.alpha = visible ? 1f : 0f;
        Panel.interactable = false;
        Panel.blocksRaycasts = false;
    }
}
