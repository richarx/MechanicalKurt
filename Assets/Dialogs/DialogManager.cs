using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class DialogManager : MonoBehaviour
{
    public static DialogManager instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private GameObject confirmPrompt;   // Shown on the last line.
    [SerializeField] private GameObject nextPrompt;      // Shown between lines.

    [SerializeField] private float charactersPerSecond = 40f;
    [SerializeField] private float promptDelay = 1f;
    [SerializeField] private float fadeDuration = 0.15f;
    [SerializeField] private float promptPulseScale = 1.1f;
    [SerializeField] private float promptPulseDuration = 0.6f;

    //Sound
    [SerializeField] private AudioClip typingClip;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    //Testing
    [SerializeField] private DialogSequence testSequence;

    [HideInInspector] public UnityEvent OnDialogStarted = new UnityEvent();
    [HideInInspector] public UnityEvent OnDialogEnded = new UnityEvent();

    [field: SerializeField, HideInInspector]
    public UnityEvent<int> OnLineStarted { get; private set; } = new UnityEvent<int>();

    public bool IsPlaying => isOpen || isClosing;
    public DialogSequence CurrentSequence => currentSequence;
    public bool IsScriptControlled { get; private set; }

    private DialogSequence currentSequence;
    private int lineIndex;
    private bool isOpen;
    private bool isClosing;

    private bool isTyping;
    private int totalCharacters;
    private float revealProgress;
    private float promptTimer;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        SetVisible(false);
        canvasGroup.alpha = 0f;
        SetPromptText(confirmPrompt, ConfirmPromptText);
        SetPromptText(nextPrompt, NextPromptText);
    }

    private const string ConfirmPromptText = "Press any key";
    private const string NextPromptText = "Press any key to continue";

    private static void SetPromptText(GameObject prompt, string text)
    {
        TMP_Text label = prompt != null ? prompt.GetComponentInChildren<TMP_Text>(true) : null;
        if (label == null)
            return;
        label.text = text;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    /// <summary>Texte de l'invite affichée en ce moment (null si aucune).</summary>
    public string ShownPromptText
    {
        get
        {
            GameObject prompt = confirmPrompt.activeSelf ? confirmPrompt : nextPrompt.activeSelf ? nextPrompt : null;
            return prompt != null ? prompt.GetComponentInChildren<TMP_Text>(true).text : null;
        }
    }

    private void Update()
    {
        if (!IsScriptControlled && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            StartDialog(testSequence);

        if (!isOpen)
            return;

        // Scripted timing keeps the typewriter, but never accepts input or shows prompts.
        if (IsScriptControlled)
        {
            if (isTyping)
                UpdateTyping();
            return;
        }

        if (isTyping)
        {
            if (ConfirmPressed())
                FinishTyping();
            else
                UpdateTyping();
        }
        else if (promptTimer < promptDelay)
        {
            // Small delay before confirm is accepted, so spamming doesn't skip the whole dialog.
            promptTimer += Time.deltaTime;
            if (promptTimer >= promptDelay)
                SetPromptVisible(true);
        }
        else if (ConfirmPressed())
        {
            NextLine();
        }
    }

    public void StartDialog(DialogSequence sequence)
    {
        confirmRequested = false;
        if (IsScriptControlled)
            return;
        OpenDialog(sequence);
    }

    // The caller owns timing and closure; player input and prompts stay disabled.
    public bool StartScriptedDialog(DialogSequence sequence)
    {
        if (!isActiveAndEnabled || IsPlaying || sequence == null
            || sequence.lines == null || sequence.lines.Count == 0)
            return false;

        IsScriptControlled = true;
        OpenDialog(sequence);
        return true;
    }

    public bool ShowScriptedLine(DialogSequence sequence, int index)
    {
        if (!isOpen || !IsScriptControlled || currentSequence != sequence
            || index < 0 || index >= sequence.lines.Count)
            return false;

        lineIndex = index;
        ShowLine(sequence.lines[index].text);
        return true;
    }

    private void OpenDialog(DialogSequence sequence)
    {
        SetVisible(true);
        canvasGroup.DOFade(1f, fadeDuration);
        PlaySound(openClip);
        OnDialogStarted.Invoke();

        if (sequence == null || sequence.lines.Count == 0)
        {
            currentSequence = null;
            SetSpeaker("");
            ShowLine("NO_DIALOG");
            return;
        }

        currentSequence = sequence;
        lineIndex = 0;
        SetSpeaker(sequence.speaker);
        ShowLine(sequence.lines[0].text);
    }

    private void NextLine()
    {
        lineIndex++;

        if (currentSequence == null || lineIndex >= currentSequence.lines.Count)
            EndDialog();
        else
            ShowLine(currentSequence.lines[lineIndex].text);
    }

    private void EndDialog()
    {
        SetVisible(false);
        IsScriptControlled = false;

        currentSequence = null;
        lineIndex = 0;
        isTyping = false;
        SetPromptVisible(false);

        PlaySound(closeClip);
        isClosing = true;
        canvasGroup.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            isClosing = false;
            OnDialogEnded.Invoke();
        });
    }

    // Only close the requested sequence, leaving any replacement dialog alone.
    public void CancelDialog(DialogSequence sequence)
    {
        if (isOpen && currentSequence == sequence)
            EndDialog();
    }

    private void SetSpeaker(string speaker)
    {
        speakerText.text = speaker;
        speakerText.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
    }

    private void ShowLine(string text)
    {
        bodyText.text = text;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        totalCharacters = bodyText.textInfo.characterCount;

        revealProgress = 0f;
        isTyping = true;
        SetPromptVisible(false);
        OnLineStarted.Invoke(lineIndex);
    }

    private void SetPromptVisible(bool visible)
    {
        bool isLastLine = currentSequence == null || lineIndex >= currentSequence.lines.Count - 1;

        SetPulsing(confirmPrompt, visible && isLastLine);
        SetPulsing(nextPrompt, visible && !isLastLine);
    }

    private void SetPulsing(GameObject prompt, bool visible)
    {
        prompt.transform.DOKill();
        prompt.transform.localScale = Vector3.one;
        prompt.SetActive(visible);

        if (visible)
            prompt.transform.DOScale(promptPulseScale, promptPulseDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    // ---- Typewriter reveal ----

    private void UpdateTyping()
    {
        revealProgress += Time.deltaTime * charactersPerSecond;
        int visible = Mathf.Min(Mathf.FloorToInt(revealProgress), totalCharacters);

        if (visible > bodyText.maxVisibleCharacters)
        {
            bodyText.maxVisibleCharacters = visible;
            PlayTypingSound(visible - 1);
        }

        if (visible >= totalCharacters)
            FinishTyping();
    }

    private void FinishTyping()
    {
        bodyText.maxVisibleCharacters = totalCharacters;
        isTyping = false;
        promptTimer = 0f;
    }

    private void PlayTypingSound(int charIndex)
    {
        if (typingClip == null)
            return;

        if (char.IsWhiteSpace(bodyText.textInfo.characterInfo[charIndex].character))
            return;

        SFXManager.Instance.PlaySFX(typingClip);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null)
            return;

        SFXManager.Instance.PlaySFX(clip);
    }

    // ---- Helpers ----

    /// <summary>Simule un appui de validation (bots des scénarios, bouton UI…), lu à la prochaine frame.</summary>
    public void RequestConfirm() => confirmRequested = true;

    private bool confirmRequested;

    private bool ConfirmPressed()
    {
        bool requested = confirmRequested;
        confirmRequested = false;
        return requested || KeyboardConfirmPressed() || MouseConfirmPressed() || GamepadConfirmPressed() || TouchConfirmPressed();
    }

    /// <summary>
    /// N'importe quelle touche fait avancer le dialogue, sauf celles qu'on tient sans y penser ou qui ont un autre rôle :
    /// déplacement, pause et modificateurs.
    /// </summary>
    public static bool CanConfirmDialog(Key key)
    {
        switch (key)
        {
            case Key.W:
            case Key.A:
            case Key.S:
            case Key.D:
            case Key.UpArrow:
            case Key.DownArrow:
            case Key.LeftArrow:
            case Key.RightArrow:
            case Key.Escape:
            case Key.Tab:
            case Key.LeftAlt:
            case Key.RightAlt:
            case Key.LeftCtrl:
            case Key.RightCtrl:
            case Key.LeftMeta:
            case Key.RightMeta:
                return false;
            default:
                return true;
        }
    }

    private static bool KeyboardConfirmPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.anyKey.wasPressedThisFrame)
            return false;
        foreach (UnityEngine.InputSystem.Controls.KeyControl key in keyboard.allKeys)
            if (key != null && key.wasPressedThisFrame && CanConfirmDialog(key.keyCode))
                return true;
        return false;
    }

    private static bool MouseConfirmPressed()
    {
        Mouse mouse = Mouse.current;
        return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
    }

    private static bool GamepadConfirmPressed()
    {
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
            || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame
            || gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame
            || gamepad.leftTrigger.wasPressedThisFrame || gamepad.rightTrigger.wasPressedThisFrame);
    }

    private static bool TouchConfirmPressed()
    {
        Touchscreen touchscreen = Touchscreen.current;
        return touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame;
    }

    // Show/hide only through the CanvasGroup, the alpha is faded by the caller.
    // Completing a running fade first means a pending OnDialogEnded still fires before a new dialog starts.
    private void SetVisible(bool visible)
    {
        canvasGroup.DOKill(true);
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
        isOpen = visible;
    }
}
