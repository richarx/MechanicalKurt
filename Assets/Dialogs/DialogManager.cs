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

    [HideInInspector] public UnityEvent OnDialogStarted;
    [HideInInspector] public UnityEvent OnDialogEnded;

    private DialogSequence currentSequence;
    private int lineIndex;
    private bool isOpen;

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
    }

    private void Update()
    {
        if (Keyboard.current.pKey.wasPressedThisFrame)
            StartDialog(testSequence);

        if (!isOpen)
            return;

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

        currentSequence = null;
        lineIndex = 0;
        isTyping = false;
        SetPromptVisible(false);

        PlaySound(closeClip);
        canvasGroup.DOFade(0f, fadeDuration).OnComplete(() => OnDialogEnded.Invoke());
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

    // Temporary input, to be replaced once the Input Actions are decided.
    private bool ConfirmPressed()
    {
        return Keyboard.current.spaceKey.wasPressedThisFrame;
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
