using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates or upgrades a small, ready-to-play delivery test scene.</summary>
public static class DeliveryFullSetup
{
    private const string _createMenuPath = "Tools/Delivery/Create Full Delivery Test";
    private const string _upgradeMenuPath = "Tools/Delivery/Upgrade Selected Delivery Test";
    private const string _fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string _circleMaterialPath = "Assets/Delivery/Examples/SelectionCircle.mat";
    private const string _selectionSetupMenuPath = "Tools/Delivery/Create Selection Test";
    private const float _testDurationSeconds = 180f;

    [MenuItem(_createMenuPath)]
    private static void CreateFullTest()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_fontPath);
        if (font == null)
        {
            Debug.LogError("Delivery full setup needs the existing LiberationSans SDF font at " + _fontPath);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Full Delivery Test");
        try
        {
            GameObject cameraObject = CreateObject("Main Camera", null);
            Camera camera = Undo.AddComponent<Camera>(cameraObject);
            camera.tag = "MainCamera";
            Undo.AddComponent<AudioListener>(cameraObject);
            CreateTestLight();
            CreateTestGround();

            if (!EditorApplication.ExecuteMenuItem(_selectionSetupMenuPath))
            {
                Debug.LogError("Could not run Tools > Delivery > Create Selection Test. Check its example assets and URP shader.");
                return;
            }

            GameObject root = GameObject.Find("Delivery - Selection Test");
            if (root == null)
            {
                Debug.LogError("The selection test root was not created.");
                return;
            }

            DeliverySession session = root.GetComponent<DeliverySession>();
            DeliveryOfferSelector selector = root.GetComponent<DeliveryOfferSelector>();
            Transform player = FindChild(root.transform, "Player Placeholder");
            if (session == null || selector == null || player == null)
            {
                Debug.LogError("The selection test is missing its session, offer selector, or player placeholder.", root);
                return;
            }

            Undo.RecordObject(session, "Set delivery test duration");
            SerializedObject sessionSettings = new SerializedObject(session);
            sessionSettings.FindProperty("_durationSeconds").floatValue = _testDurationSeconds;
            sessionSettings.ApplyModifiedProperties();

            Undo.RecordObject(player, "Position delivery player");
            player.position = new Vector3(0f, 0f, -6f);
            Undo.SetTransformParent(cameraObject.transform, player, "Attach delivery test camera");
            Undo.RecordObject(cameraObject.transform, "Position delivery test camera");
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;

            DeliveryNoclipFlightController flight = player.GetComponent<DeliveryNoclipFlightController>();
            if (flight != null)
            {
                SerializedObject flightSettings = new SerializedObject(flight);
                flightSettings.FindProperty("_movementCamera").objectReferenceValue = camera;
                SerializedProperty captureCursor = flightSettings.FindProperty("_captureCursorOnEnable");
                if (captureCursor != null) captureCursor.boolValue = false;
                flightSettings.ApplyModifiedProperties();
            }

            Upgrade(root, session, selector, player, camera, font);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("Full delivery test created. The scene is unsaved; save it manually if you want to keep it.", root);
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
    }

    [MenuItem(_createMenuPath, true)]
    private static bool CanCreateFullTest() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null
        && EditorSceneManager.GetActiveScene().IsValid() && EditorSceneManager.GetActiveScene().isLoaded;

    [MenuItem(_upgradeMenuPath)]
    private static void UpgradeSelectedTest()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("Select the delivery test root or one of its children first.");
            return;
        }

        DeliverySession session = FindInAncestors<DeliverySession>(selected.transform);
        if (session == null) session = selected.GetComponentInChildren<DeliverySession>(true);
        GameObject root = session != null ? session.gameObject : selected.transform.root.gameObject;
        if (session == null) session = root.GetComponent<DeliverySession>();
        DeliveryOfferSelector selector = root.GetComponent<DeliveryOfferSelector>();
        Transform player = GetSelectorPlayer(selector);
        if (player == null) player = FindChild(root.transform, "Player Placeholder");
        if (player == null) player = FindPlayerWithFlight(root.transform);
        Camera camera = FindAssignedCamera(player);
        if (camera == null) camera = Camera.main;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_fontPath);
        Material circleMaterial = AssetDatabase.LoadAssetAtPath<Material>(_circleMaterialPath);
        DeliveryOffer[] offers = root.GetComponentsInChildren<DeliveryOffer>(true);
        if (session == null || selector == null || player == null || offers.Length == 0 || font == null || circleMaterial == null)
        {
            Debug.LogError("Upgrade needs an existing DeliverySession, DeliveryOfferSelector, player, at least one offer, LiberationSans SDF, and SelectionCircle.mat.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Upgrade Selected Delivery Test");
        try
        {
            EnsureFlightReferences(player, selector, session, camera);
            SetIfMissing(new SerializedObject(selector), "_session", session);
            SetIfMissing(new SerializedObject(selector), "_player", player);
            SetIfMissing(new SerializedObject(selector), "_circleMaterial", circleMaterial);
            SerializedObject selectorSettings = new SerializedObject(selector);
            SerializedProperty offerList = selectorSettings.FindProperty("_offers");
            if (offerList != null && offerList.arraySize == 0)
            {
                offerList.arraySize = offers.Length;
                for (int i = 0; i < offers.Length; i++) offerList.GetArrayElementAtIndex(i).objectReferenceValue = offers[i];
                selectorSettings.ApplyModifiedProperties();
            }

            Upgrade(root, session, selector, player, camera, font);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("Delivery test upgraded. Existing scene, offer order, movement settings, and assigned UI references were preserved.", root);
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
    }

    [MenuItem(_upgradeMenuPath, true)]
    private static bool CanUpgradeSelectedTest() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null && Selection.activeGameObject != null
        && EditorSceneManager.GetActiveScene().IsValid() && EditorSceneManager.GetActiveScene().isLoaded;

    private static void Upgrade(GameObject root, DeliverySession session, DeliveryOfferSelector selector,
        Transform player, Camera camera, TMP_FontAsset font)
    {
        DeliveryManager manager = root.GetComponent<DeliveryManager>();
        if (manager == null) manager = Undo.AddComponent<DeliveryManager>(root);
        SetIfMissing(new SerializedObject(manager), "_session", session);
        SetIfMissing(new SerializedObject(manager), "_selector", selector);

        GameObject canvasObject = FindCanvasFor(root, session);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null) canvas = Undo.AddComponent<Canvas>(canvasObject);
        GetOrAdd<GraphicRaycaster>(canvasObject);
        Undo.RecordObject(canvas, "Configure delivery game canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
        Undo.RecordObject(scaler, "Configure delivery game canvas scaling");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        TMP_Text timerText = FindTextOnSessionHUD(session, "_timerText");
        TMP_Text stateText = FindTextOnSessionHUD(session, "_stateText");
        if (timerText == null || stateText == null)
        {
            GameObject timerPanel = FindOrCreate(canvasObject.transform, "Timer Panel", true);
            ConfigureRect(timerPanel.transform as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(420f, 156f), new Vector2(0.5f, 1f));
            Image background = GetOrAdd<Image>(timerPanel);
            Undo.RecordObject(background, "Configure timer background");
            background.color = new Color(0.04f, 0.06f, 0.09f, 0.94f);
            background.raycastTarget = false;
            if (timerText == null) timerText = FindOrCreateText("Timer", timerPanel.transform, font, "03:00", 56f, 26f, 76f);
            if (stateText == null) stateText = FindOrCreateText("Session State", timerPanel.transform, font, "Ready", 26f, -44f, 40f);
        }

        DeliverySessionHUD sessionHUD = FindSessionHUD(session);
        if (sessionHUD == null)
        {
            sessionHUD = Undo.AddComponent<DeliverySessionHUD>(canvasObject);
            SerializedObject settings = new SerializedObject(sessionHUD);
            settings.FindProperty("_session").objectReferenceValue = session;
            settings.FindProperty("_timerText").objectReferenceValue = timerText;
            settings.FindProperty("_stateText").objectReferenceValue = stateText;
            settings.ApplyModifiedProperties();
        }
        else
        {
            SetIfMissing(new SerializedObject(sessionHUD), "_session", session);
            SetIfMissing(new SerializedObject(sessionHUD), "_timerText", timerText);
            SetIfMissing(new SerializedObject(sessionHUD), "_stateText", stateText);
        }

        GameObject leftPanel = FindOrCreate(canvasObject.transform, "Delivery Objectives", true);
        ConfigureRect(leftPanel.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -190f), new Vector2(440f, 270f), new Vector2(0f, 1f));
        AddPanelBackground(leftPanel);
        TMP_Text objective = FindOrCreateText("Objective", leftPanel.transform, font, "Choose a delivery to start", 28f, 82f, 64f, TextAlignmentOptions.Left);
        Undo.RecordObject(objective, "Fit delivery objective text");
        objective.enableAutoSizing = true;
        objective.fontSizeMin = 18f;
        objective.fontSizeMax = 28f;
        TMP_Text cargo = FindOrCreateText("Cargo", leftPanel.transform, font, "", 24f, 28f, 40f, TextAlignmentOptions.Left);
        TMP_Text progress = FindOrCreateText("Progress", leftPanel.transform, font, "", 24f, -20f, 40f, TextAlignmentOptions.Left);
        TMP_Text notification = FindOrCreateText("Notification", leftPanel.transform, font, "Ready", 22f, -68f, 36f, TextAlignmentOptions.Left);
        TMP_Text deliveries = FindOrCreateText("Deliveries Count", leftPanel.transform, font, "Deliveries: 0", 20f, -112f, 32f, TextAlignmentOptions.Left);
        DeliveryHUD hud = FindComponentOnScene<DeliveryHUD>(root.scene, "_manager", manager);
        if (hud == null) hud = Undo.AddComponent<DeliveryHUD>(canvasObject);
        SetIfMissing(new SerializedObject(hud), "_manager", manager);
        SetIfMissing(new SerializedObject(hud), "_objectiveText", objective);
        SetIfMissing(new SerializedObject(hud), "_cargoText", cargo);
        SetIfMissing(new SerializedObject(hud), "_progressText", progress);
        SetIfMissing(new SerializedObject(hud), "_notificationText", notification);
        SetIfMissing(new SerializedObject(hud), "_deliveriesText", deliveries);

        GameObject marker = FindOrCreate(canvasObject.transform, "Destination Marker", true);
        RectTransform markerRect = marker.transform as RectTransform;
        ConfigureRect(markerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 110f));
        TMP_Text arrow = FindOrCreateText("Arrow", marker.transform, font, "^", 56f, 20f, 72f);
        TMP_Text distance = FindOrCreateText("Distance", marker.transform, font, "", 22f, -32f, 32f);
        DeliveryGuidance guidance = FindComponentOnScene<DeliveryGuidance>(root.scene, "_manager", manager);
        if (guidance == null) guidance = Undo.AddComponent<DeliveryGuidance>(canvasObject);
        SetIfMissing(new SerializedObject(guidance), "_manager", manager);
        SetIfMissing(new SerializedObject(guidance), "_camera", camera);
        SetIfMissing(new SerializedObject(guidance), "_canvas", canvas);
        SetIfMissing(new SerializedObject(guidance), "_marker", markerRect);
        SetIfMissing(new SerializedObject(guidance), "_distanceText", distance);
        SetIfMissing(new SerializedObject(guidance), "_arrow", arrow.transform as RectTransform);
        Material circleMaterial = AssetDatabase.LoadAssetAtPath<Material>(_circleMaterialPath);
        SetIfMissing(new SerializedObject(guidance), "_destinationMaterial", circleMaterial);

        CreateControlButton(canvasObject.transform, font, "Start", "START", new Vector2(0f, 48f), session, "StartSession");
        CreateControlButton(canvasObject.transform, font, "Restart", "RESTART", new Vector2(0f, 0f), session, "RestartSession");
        CreateControlButton(canvasObject.transform, font, "Pause Resume", "PAUSE / RESUME", new Vector2(0f, -48f), session, "TogglePause");
    }

    private static void CreateTestGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Test Ground";
        Undo.RegisterCreatedObjectUndo(ground, "Create test ground");
        ground.transform.position = new Vector3(6f, -0.15f, 3f);
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
    }

    private static void CreateTestLight()
    {
        GameObject lightObject = CreateObject("Directional Light", null);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Light light = Undo.AddComponent<Light>(lightObject);
        Undo.RecordObject(light, "Configure delivery test light");
        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    private static GameObject FindCanvasFor(GameObject root, DeliverySession session)
    {
        DeliverySessionHUD hud = FindSessionHUD(session);
        if (hud != null && hud.GetComponent<Canvas>() != null) return hud.gameObject;
        Transform existing = FindChild(root.transform, "Game UI");
        return existing != null ? existing.gameObject : CreateObject("Game UI", root.transform, true);
    }

    private static void EnsureEventSystem()
    {
        EventSystem[] systems = UnityEngine.Object.FindObjectsByType<EventSystem>();
        for (int i = 0; i < systems.Length; i++)
            if (systems[i].isActiveAndEnabled) return;
        GameObject eventObject = CreateObject("EventSystem", null);
        Undo.AddComponent<EventSystem>(eventObject);
        // InputSystemUIInputModule.AssignDefaultActions runs automatically in OnEnable when no actions are assigned.
        Undo.AddComponent<InputSystemUIInputModule>(eventObject);
    }

    private static TMP_Text FindTextOnSessionHUD(DeliverySession session, string fieldName)
    {
        DeliverySessionHUD hud = FindSessionHUD(session);
        if (hud == null) return null;
        return new SerializedObject(hud).FindProperty(fieldName)?.objectReferenceValue as TMP_Text;
    }

    private static DeliverySessionHUD FindSessionHUD(DeliverySession session)
    {
        DeliverySessionHUD[] components = UnityEngine.Object.FindObjectsByType<DeliverySessionHUD>();
        for (int i = 0; i < components.Length; i++)
        {
            SerializedProperty reference = new SerializedObject(components[i]).FindProperty("_session");
            if (reference != null && reference.objectReferenceValue == session) return components[i];
        }
        return null;
    }

    private static T FindComponentOnScene<T>(Scene scene, string field, UnityEngine.Object value) where T : Component
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            T[] components = roots[i].GetComponentsInChildren<T>(true);
            for (int j = 0; j < components.Length; j++)
            {
                SerializedProperty reference = new SerializedObject(components[j]).FindProperty(field);
                if (reference != null && reference.objectReferenceValue == value) return components[j];
            }
        }
        return null;
    }

    private static Camera FindAssignedCamera(Transform player)
    {
        if (player == null) return null;
        DeliveryNoclipFlightController flight = player.GetComponent<DeliveryNoclipFlightController>();
        if (flight == null) return null;
        return new SerializedObject(flight).FindProperty("_movementCamera").objectReferenceValue as Camera;
    }

    private static void EnsureFlightReferences(Transform player, DeliveryOfferSelector selector, DeliverySession session, Camera camera)
    {
        DeliveryNoclipFlightController flight = player.GetComponent<DeliveryNoclipFlightController>();
        bool createdFlight = flight == null;
        if (createdFlight) flight = Undo.AddComponent<DeliveryNoclipFlightController>(player.gameObject);
        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController == null) characterController = Undo.AddComponent<CharacterController>(player.gameObject);
        Undo.RecordObject(characterController, "Configure noclip CharacterController");
        characterController.detectCollisions = false;
        characterController.enabled = false;
        SerializedObject settings = new SerializedObject(flight);
        SetIfMissing(settings, "_characterController", characterController);
        SetIfMissing(settings, "_offerSelector", selector);
        SetIfMissing(settings, "_session", session);
        SetIfMissing(settings, "_movementCamera", camera);
        SerializedProperty captureCursor = settings.FindProperty("_captureCursorOnEnable");
        if (createdFlight && captureCursor != null)
        {
            captureCursor.boolValue = false;
            settings.ApplyModifiedProperties();
        }
    }

    private static void CreateControlButton(Transform parent, TMP_FontAsset font, string objectName, string label,
        Vector2 position, DeliverySession session, string methodName)
    {
        GameObject buttonObject = FindOrCreate(parent, objectName + " Button", true);
        ConfigureRect(buttonObject.transform as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-132f, 122f + position.y), new Vector2(220f, 42f));
        Image image = GetOrAdd<Image>(buttonObject);
        Undo.RecordObject(image, "Configure " + objectName + " button");
        image.color = new Color(0.12f, 0.2f, 0.28f, 0.96f);
        Button button = GetOrAdd<Button>(buttonObject);
        Undo.RecordObject(button, "Configure " + objectName + " button");
        button.targetGraphic = image;
        if (button.onClick.GetPersistentEventCount() == 0)
        {
            UnityEngine.Events.UnityAction action = (UnityEngine.Events.UnityAction)Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction), session, methodName);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }
        FindOrCreateText("Label", buttonObject.transform, font, label, 20f, 0f, 34f);
    }

    private static void AddPanelBackground(GameObject panel)
    {
        Image image = GetOrAdd<Image>(panel);
        Undo.RecordObject(image, "Configure objective panel");
        image.color = new Color(0.04f, 0.06f, 0.09f, 0.84f);
        image.raycastTarget = false;
    }

    private static TMP_Text FindOrCreateText(string objectName, Transform parent, TMP_FontAsset font,
        string content, float fontSize, float verticalOffset, float height,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        Transform existing = FindDirectChild(parent, objectName);
        bool createdObject = existing == null;
        GameObject textObject = existing != null ? existing.gameObject : CreateObject(objectName, parent, true);
        RectTransform rect = textObject.transform as RectTransform;
        bool leftAligned = alignment == TextAlignmentOptions.Left;
        Vector2 anchor = leftAligned ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
        ConfigureRect(rect, anchor, anchor, new Vector2(leftAligned ? 24f : 0f, verticalOffset), new Vector2(leftAligned ? 390f : 400f, height), leftAligned ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        bool createdText = text == null;
        if (createdText) text = Undo.AddComponent<TextMeshProUGUI>(textObject);
        Undo.RecordObject(text, "Configure " + objectName + " text");
        if (text.font == null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        if (createdObject || createdText) text.text = content;
        return text;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPosition, Vector2 size, Vector2? pivot = null)
    {
        Undo.RecordObject(rect, "Position delivery UI");
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static GameObject FindOrCreate(Transform parent, string objectName, bool isUI = false)
    {
        Transform child = FindDirectChild(parent, objectName);
        return child != null ? child.gameObject : CreateObject(objectName, parent, isUI);
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == childName) return parent.GetChild(i);
        return null;
    }

    private static GameObject CreateObject(string objectName, Transform parent, bool isUI = false)
    {
        GameObject created = isUI ? new GameObject(objectName, typeof(RectTransform)) : new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(created, "Create " + objectName);
        if (parent != null) Undo.SetTransformParent(created.transform, parent, "Parent " + objectName);
        return created;
    }

    private static void SetIfMissing(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        if (serialized == null || value == null) return;
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
    }

    private static T FindInAncestors<T>(Transform selected) where T : Component
    {
        for (Transform current = selected; current != null; current = current.parent)
        {
            T component = current.GetComponent<T>();
            if (component != null) return component;
        }
        return null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindPlayerWithFlight(Transform root)
    {
        if (root.GetComponent<DeliveryNoclipFlightController>() != null) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindPlayerWithFlight(root.GetChild(i));
            if (found != null) return found;
        }
        return null;
    }

    private static Transform GetSelectorPlayer(DeliveryOfferSelector selector)
    {
        if (selector == null) return null;
        return new SerializedObject(selector).FindProperty("_player").objectReferenceValue as Transform;
    }
}
