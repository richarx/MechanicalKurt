using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Commandes tactiles, seulement sur téléphone et tablette. Les boutons et l'inclinaison passent par une manette virtuelle
/// (OnScreenControl) : le code du pigeon et les actions d'entrée ne changent pas. Le joystick à l'écran n'apparaît que si
/// le navigateur ne donne pas l'inclinaison.
/// </summary>
public class MobileControls : MonoBehaviour
{
    public static MobileControls Instance { get; private set; }

    /// <summary>Le jeu tourne sur un appareil mobile (ou un scénario force ces commandes).</summary>
    public static bool IsEnabled => Application.isMobilePlatform || Instance != null;

    static Sprite _circle;

    GameObject _gameplay;
    GameObject _joystick;
    GameObject _recenter;
    GameObject _pause;
    GameObject _rotateHint;
    MobileMoveStick _stick;

    public MobileMoveStick Stick => _stick;
    public bool IsGameplayShown => _gameplay != null && _gameplay.activeSelf;
    public bool IsJoystickShown => _joystick != null && _joystick.activeSelf;
    public OnScreenButton FlapButton { get; private set; }
    public OnScreenButton DiveButton { get; private set; }
    public OnScreenButton BrakeButton { get; private set; }
    public OnScreenButton InteractButton { get; private set; }
    public OnScreenButton CooButton { get; private set; }
    public OnScreenButton PoopButton { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Application.isMobilePlatform)
            Create();
    }

    public static MobileControls Create()
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject("[Mobile Controls]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);
        go.SetActive(false);
        MobileControls controls = go.AddComponent<MobileControls>();
        controls.Build();
        Instance = controls;
        go.SetActive(true);
        TiltBridge.Begin();
#if UNITY_WEBGL && !UNITY_EDITOR
        // Clavier du téléphone pour saisir le code de la partie.
        WebGLInput.mobileKeyboardSupport = true;
#endif
        return controls;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _gameplay = NewPanel("Gameplay");

        // À gauche : piquer et freiner. À droite : battre des ailes et interagir. Le pilotage est dans les mains.
        DiveButton = AddButton("Dive", "<Gamepad>/leftStickPress", new Vector2(0f, 0f), new Vector2(150f, 170f), 210f);
        BrakeButton = AddButton("Brake", "<Gamepad>/rightTrigger", new Vector2(0f, 0f), new Vector2(150f, 410f), 170f);
        FlapButton = AddButton("Flap", "<Gamepad>/buttonSouth", new Vector2(1f, 0f), new Vector2(-170f, 180f), 250f);
        InteractButton = AddButton("Y", "<Gamepad>/buttonNorth", new Vector2(1f, 0f), new Vector2(-150f, 430f), 150f);
        CooButton = AddButton("Coo", "<Gamepad>/buttonWest", new Vector2(0f, 1f), new Vector2(90f, -90f), 120f);
        PoopButton = AddButton("Poop", "<Gamepad>/buttonEast", new Vector2(0f, 1f), new Vector2(230f, -90f), 120f);

        BuildJoystick();

        _recenter = NewRound(_gameplay.transform, "Center", new Vector2(1f, 1f), new Vector2(-230f, -90f), 110f);
        _recenter.AddComponent<Button>().onClick.AddListener(TiltBridge.Recenter);

        _pause = NewRound(transform, "II", new Vector2(1f, 1f), new Vector2(-90f, -90f), 110f);
        _pause.AddComponent<Button>().onClick.AddListener(TogglePause);

        _rotateHint = NewPanel("Rotate Hint");
        AddLabel(_rotateHint.transform, "Rotate your phone", 64f);
    }

    GameObject NewPanel(string name)
    {
        var panel = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)panel.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return panel;
    }

    OnScreenButton AddButton(string label, string controlPath, Vector2 corner, Vector2 position, float size)
    {
        GameObject go = NewRound(_gameplay.transform, label, corner, position, size);
        go.SetActive(false);
        OnScreenButton button = go.AddComponent<OnScreenButton>();
        button.controlPath = controlPath;
        go.SetActive(true);
        return button;
    }

    void BuildJoystick()
    {
        _joystick = new GameObject("Joystick", typeof(RectTransform), typeof(Image));
        var area = (RectTransform)_joystick.transform;
        area.SetParent(_gameplay.transform, false);
        area.anchorMin = area.anchorMax = Vector2.zero;
        area.anchoredPosition = new Vector2(500f, 230f);
        area.sizeDelta = new Vector2(300f, 300f);
        Image background = _joystick.GetComponent<Image>();
        background.sprite = Circle();
        background.color = new Color(1f, 1f, 1f, 0.15f);

        var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image));
        var knobRect = (RectTransform)knob.transform;
        knobRect.SetParent(area, false);
        knobRect.sizeDelta = new Vector2(120f, 120f);
        Image knobImage = knob.GetComponent<Image>();
        knobImage.sprite = Circle();
        knobImage.color = new Color(1f, 1f, 1f, 0.45f);
        knobImage.raycastTarget = false;

        // Le composant vit sur son propre objet pour rester actif (inclinaison) quand le joystick est caché.
        var holder = new GameObject("Move Stick", typeof(RectTransform));
        holder.transform.SetParent(_gameplay.transform, false);
        holder.SetActive(false);
        _stick = holder.AddComponent<MobileMoveStick>();
        _stick.controlPath = "<Gamepad>/leftStick";
        _stick.Setup(area, knobRect, 110f);
        holder.SetActive(true);
        _joystick.AddComponent<JoystickForwarder>().Target = _stick;
    }

    /// <summary>Le toucher sur le joystick à l'écran est transmis au stick, qui reste actif quand ce visuel est caché.</summary>
    class JoystickForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public MobileMoveStick Target;
        public void OnPointerDown(PointerEventData eventData) => Target.OnPointerDown(eventData);
        public void OnDrag(PointerEventData eventData) => Target.OnDrag(eventData);
        public void OnPointerUp(PointerEventData eventData) => Target.OnPointerUp(eventData);
    }

    static GameObject NewRound(Transform parent, string label, Vector2 corner, Vector2 position, float size)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = corner;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.GetComponent<Image>();
        image.sprite = Circle();
        image.color = new Color(1f, 1f, 1f, 0.3f);
        AddLabel(go.transform, label, Mathf.Max(30f, size * 0.26f));
        return go;
    }

    static void AddLabel(Transform parent, string text, float fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static void TogglePause()
    {
        if (PauseMenu.instance != null)
            PauseMenu.instance.SetPaused(!PauseMenu.instance.IsPaused);
    }

    void Update()
    {
        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        bool inGame = PigeonController.Local != null && !GameSession.InMenu;
        bool paused = PauseMenu.instance != null && PauseMenu.instance.IsPaused;
        bool tilt = TiltBridge.IsAvailable;
        SetActive(_gameplay, inGame && !paused);
        SetActive(_joystick, !tilt);
        SetActive(_recenter, tilt);
        SetActive(_pause, inGame);
        SetActive(_rotateHint, inGame && Screen.height > Screen.width);

        // Le jeu verrouille le curseur pour la souris ; sur téléphone, cela ne sert à rien et peut gêner les touchers.
        if (Application.isMobilePlatform && Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
    }

    static void SetActive(GameObject go, bool active)
    {
        if (go.activeSelf != active)
            go.SetActive(active);
    }

    /// <summary>Disque aux bords adoucis, généré une seule fois.</summary>
    static Sprite Circle()
    {
        if (_circle != null)
            return _circle;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance)));
            }
        }
        texture.Apply();
        _circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return _circle;
    }
}
