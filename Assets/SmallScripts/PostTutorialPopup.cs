using System.Collections;
using Cysharp.Threading.Tasks.Triggers;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Shows a one-shot notification after the tutorial and its final cinematic.</summary>
[DisallowMultipleComponent]
public class PostTutorialPopup : MonoBehaviour
{
    [InfoBox("Placer la fenêtre à sa position visible. Le script la masque au lancement. " +
        "Le composant et ses parents doivent rester actifs.")]
    [field: SerializeField, Required]
    public RectTransform Panel { get; private set; }

    [field: SerializeField, Tooltip("Recherche dans la scène si vide. Attend IsComplete.")]
    public AchievementTutorial Tutorial { get; private set; }

    [field: SerializeField, Tooltip("Recherche dans la scène si vide. Suspend le délai et l'affichage pendant les cinématiques.")]
    public ManagerCinematic CinematicManager { get; private set; }

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("X : secondes de jeu après la fin du tuto et de sa cinématique.")]
    public float DelaySeconds { get; private set; } = 30f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Y : durée à la position visible, hors animations d'entrée et de sortie.")]
    public float DisplaySeconds { get; private set; } = 5f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    public float SlideSeconds { get; private set; } = 0.35f;

    [field: SerializeField, Tooltip("Décalage depuis la position visible, en unités du Canvas. À ajuster pour sortir entièrement de l'écran.")]
    public Vector2 HiddenOffset { get; private set; } = new Vector2(800f, 0f);

    [SerializeField] string _keyboardText;

    [SerializeField] string _joypadText;

    [SerializeField] TMP_Text _textBox;

    [ShowInInspector, ReadOnly]
    public bool HasShown { get; private set; }

    private CanvasGroup _visibility;
    private Vector2 _shownPosition;
    private Coroutine _routine;
    private bool _initialized;

    private bool CanAdvance => Time.timeScale > 0f
        && (CinematicManager == null || !CinematicManager.IsPlaying);

    private void Awake()
    {
        if (Panel == null) Panel = GetComponent<RectTransform>();
        if (Panel == null)
        {
            Debug.LogError("PostTutorialPopup : assigner le RectTransform de la fenêtre.", this);
            enabled = false;
            return;
        }

        _visibility = Panel.GetComponent<CanvasGroup>();
        if (_visibility == null) _visibility = Panel.gameObject.AddComponent<CanvasGroup>();
        _visibility.alpha = 0f;
        _visibility.interactable = false;
        _visibility.blocksRaycasts = false;
    }



    private void Start()
    {
        if (Tutorial == null) Tutorial = FindInScene<AchievementTutorial>();
        if (CinematicManager == null) CinematicManager = FindInScene<ManagerCinematic>();
        if (Tutorial == null)
        {
            Debug.LogError("PostTutorialPopup : assigner l'AchievementTutorial pour détecter la fin du tuto.", this);
            enabled = false;
            return;
        }

        // Capture the final layout before moving the window off-screen.
        Canvas.ForceUpdateCanvases();
        _shownPosition = Panel.anchoredPosition;
        _initialized = true;
        Panel.anchoredPosition = _shownPosition + HiddenOffset;
        _routine = StartCoroutine(ShowOnce());
    }

    private void OnEnable()
    {
        if (_initialized && !HasShown) _routine = StartCoroutine(ShowOnce());
        InputSystem.onEvent += HandleInputEvent;
    }



    private IEnumerator ShowOnce()
    {
        while (Tutorial != null && (!Tutorial.IsComplete || !CanAdvance)) yield return null;
        if (Tutorial == null) yield break;

        yield return WaitForGameplaySeconds(DelaySeconds);
        if (Panel == null || _visibility == null) yield break;
        HasShown = true;
        _visibility.alpha = 1f;
        yield return Slide(_shownPosition);
        yield return WaitForGameplaySeconds(DisplaySeconds);
        yield return Slide(_shownPosition + HiddenOffset);
        if (_visibility != null) _visibility.alpha = 0f;
        _routine = null;
    }

    private void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
    {
        if (!(device is Gamepad) && !(device is Keyboard)) return;

        bool usingGamepad = device is Gamepad;
        float threshold = 0.1f;
        // Read changed controls from the event: a held key or an idle gamepad must not steal focus.
        // Include analog controls so moving a stick also switches the instructions.
        if (eventPtr.HasButtonPress(magnitude: threshold, buttonControlsOnly: false))
            SetUsingGamepad(usingGamepad);
    }
    private bool _usingGamepad = false;

    private void SetUsingGamepad(bool usingGamepad)
    {
        if (_usingGamepad == usingGamepad) return;
        _usingGamepad = usingGamepad;
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        _textBox.text = _usingGamepad ? _joypadText : _keyboardText;
    }

    private IEnumerator WaitForGameplaySeconds(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds || !CanAdvance)
        {
            yield return null;
            if (CanAdvance) elapsed += Time.deltaTime;
        }
    }

    private IEnumerator Slide(Vector2 target)
    {
        if (Panel == null) yield break;
        Vector2 from = Panel.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < SlideSeconds && Panel != null)
        {
            yield return null;
            if (Panel == null) yield break;
            if (!CanAdvance) continue;
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SlideSeconds));
            Panel.anchoredPosition = Vector2.LerpUnclamped(from, target, progress);
        }
        if (Panel != null) Panel.anchoredPosition = target;
    }

    private void OnDisable()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (_visibility != null) _visibility.alpha = 0f;
        if (_initialized && Panel != null) Panel.anchoredPosition = _shownPosition + HiddenOffset;
        InputSystem.onEvent -= HandleInputEvent;
    }

    private T FindInScene<T>() where T : Component
    {
        foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include))
            if (component.gameObject.scene == gameObject.scene) return component;
        return null;
    }
}
