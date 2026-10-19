using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

    [field: SerializeField, MinValue(0f), SuffixLabel("seconds", true),
        Tooltip("Hide the last tutorial step after this delay, even if its achievement is still locked. Set to 0 to keep it visible.")]
    public float LastStepDisplaySeconds { get; private set; } = 10f;

    [field: SerializeField, Range(0.1f, 1f),
        Tooltip("Minimum gamepad input magnitude before showing gamepad instructions. Filters small stick drift.")]
    public float GamepadActivityThreshold { get; private set; } = 0.25f;

    [ShowInInspector, ReadOnly]
    public int CurrentStepIndex { get; private set; } = -1;

    [ShowInInspector, ReadOnly]
    public bool IsComplete { get; private set; }

    private AchievementManager _subscribedManager;
    private float _completionRemaining;
    private bool _configurationWarningLogged;
    private bool _usingGamepad;
    private float _lastStepElapsed;
    private bool _lastStepTimedOut;

    private void OnEnable()
    {
        ResetLastStepTimer();
        _usingGamepad = Gamepad.current != null;
        InputSystem.onEvent += HandleInputEvent;
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
        InputSystem.onEvent -= HandleInputEvent;
        UnbindManager();
        SetVisible(false);
    }

    private void Update()
    {
        if (BindManager()) RefreshProgress();
        if (_usingGamepad && Gamepad.current == null) SetUsingGamepad(false);
        if (!IsComplete && Steps != null && Steps.Count > 0 && CurrentStepIndex == Steps.Count - 1
            && !_lastStepTimedOut && LastStepDisplaySeconds > 0f)
        {
            _lastStepElapsed += Time.deltaTime;
            if (_lastStepElapsed >= LastStepDisplaySeconds)
            {
                _lastStepTimedOut = true;
                SetVisible(false);
            }
        }
        if (_completionRemaining <= 0f) return;
        _completionRemaining -= Time.unscaledDeltaTime;
        if (_completionRemaining <= 0f) SetVisible(false);
    }

    private void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!(device is Gamepad) && !(device is Keyboard)) return;

        bool usingGamepad = device is Gamepad;
        float threshold = usingGamepad ? GamepadActivityThreshold : InputSystem.settings.defaultButtonPressPoint;
        // Read changed controls from the event: a held key or an idle gamepad must not steal focus.
        // Include analog controls so moving a stick also switches the instructions.
        if (eventPtr.HasButtonPress(magnitude: threshold, buttonControlsOnly: false))
            SetUsingGamepad(usingGamepad);
    }

    private void SetUsingGamepad(bool usingGamepad)
    {
        if (_usingGamepad == usingGamepad) return;
        _usingGamepad = usingGamepad;
        RefreshCommands();
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
            _subscribedManager.OnReset += HandleReset;
        }
        return true;
    }

    private void UnbindManager()
    {
        if (_subscribedManager != null)
        {
            _subscribedManager.OnUnlocked -= HandleUnlocked;
            _subscribedManager.OnReset -= HandleReset;
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

    private void HandleReset()
    {
        ResetLastStepTimer();
        RefreshProgress();
    }

    private void ResetLastStepTimer()
    {
        _lastStepElapsed = 0f;
        _lastStepTimedOut = false;
    }

    private void Refresh(bool showCompletion)
    {
        int previousStepIndex = CurrentStepIndex;
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
            if (previousStepIndex != i) ResetLastStepTimer();
            if (ObjectiveText != null)
                ObjectiveText.text = string.IsNullOrWhiteSpace(step.Objective)
                    ? step.Achievement.Description : step.Objective;
            RefreshCommands();
            if (ProgressText != null) ProgressText.text = $"Tutorial · {i + 1} / {Steps.Count}";
            SetVisible(!_lastStepTimedOut);
            return;
        }

        IsComplete = true;
        // Already completed on loading: keep the HUD hidden.
        if (!showCompletion || !hadActiveStep || _lastStepTimedOut || !(CompletionSeconds > 0f)) return;
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
