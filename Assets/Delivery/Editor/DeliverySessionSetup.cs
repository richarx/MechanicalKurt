using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DeliverySessionSetup
{
    private const string _menuPath = "Tools/Delivery/Create Session Test";
    private const string _fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    #region Test Setup

    [MenuItem(_menuPath)]
    private static void CreateTest()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_fontPath);
        if (font == null)
        {
            Debug.LogError("Delivery setup requires the existing LiberationSans SDF font at " + _fontPath);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Delivery Session Test");

        try
        {
            GameObject root = CreateObject("Delivery - Session Test (15 seconds)", null);
            DeliverySession session = Undo.AddComponent<DeliverySession>(root);
            SerializedObject sessionSettings = new SerializedObject(session);
            sessionSettings.FindProperty("_durationSeconds").floatValue = 15f;
            sessionSettings.ApplyModifiedProperties();

            GameObject canvasObject = CreateObject("Session HUD", root.transform, true);
            Canvas canvas = Undo.AddComponent<Canvas>(canvasObject);
            Undo.RecordObject(canvas, "Configure session canvas");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasObject);
            Undo.RecordObject(scaler, "Configure session canvas scaling");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateObject("Timer Panel", canvasObject.transform, true);
            RectTransform panelRect = (RectTransform)panel.transform;
            Undo.RecordObject(panelRect, "Position timer panel");
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -24f);
            panelRect.sizeDelta = new Vector2(420f, 156f);
            Image background = Undo.AddComponent<Image>(panel);
            Undo.RecordObject(background, "Configure timer background");
            background.color = new Color(0.04f, 0.06f, 0.09f, 0.94f);
            background.raycastTarget = false;

            TMP_Text timerText = CreateText("Timer", panel.transform, font, "00:15", 56f, 26f, 76f);
            TMP_Text stateText = CreateText("State", panel.transform, font, "Ready", 26f, -44f, 40f);
            DeliverySessionHUD hud = Undo.AddComponent<DeliverySessionHUD>(canvasObject);
            SerializedObject hudSettings = new SerializedObject(hud);
            hudSettings.FindProperty("_session").objectReferenceValue = session;
            hudSettings.FindProperty("_timerText").objectReferenceValue = timerText;
            hudSettings.FindProperty("_stateText").objectReferenceValue = stateText;
            hudSettings.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    [MenuItem(_menuPath, true)]
    private static bool CanCreateTest()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && PrefabStageUtility.GetCurrentPrefabStage() == null
            && EditorSceneManager.GetActiveScene().IsValid()
            && EditorSceneManager.GetActiveScene().isLoaded;
    }

    #endregion

    #region Objects

    private static GameObject CreateObject(string objectName, Transform parent, bool isUI = false)
    {
        GameObject createdObject = isUI
            ? new GameObject(objectName, typeof(RectTransform)) : new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(createdObject, "Create " + objectName);
        if (parent != null)
            Undo.SetTransformParent(createdObject.transform, parent, "Parent " + objectName);
        return createdObject;
    }

    private static TMP_Text CreateText(string objectName, Transform parent, TMP_FontAsset font,
        string content, float fontSize, float verticalOffset, float height)
    {
        GameObject textObject = CreateObject(objectName, parent, true);
        RectTransform rect = (RectTransform)textObject.transform;
        Undo.RecordObject(rect, "Position " + objectName);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, verticalOffset);
        rect.sizeDelta = new Vector2(400f, height);

        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(textObject);
        Undo.RecordObject(text, "Configure " + objectName);
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = content;
        return text;
    }

    #endregion
}
