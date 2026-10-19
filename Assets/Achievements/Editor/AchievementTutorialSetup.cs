using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AchievementTutorialSetup
{
    private const string Menu = "Tools/Tutorial/Create Achievement Tutorial";
    private static readonly string[] Objectives = {
        "Take off and gain some height.",
        "Steer to change direction in flight.",
        "Land in a mission circle and stay still to activate it."
    };
    private static readonly string[] KeyboardHints = {
        "Hold Space to flap your wings.",
        "Left / Right arrows: turn\nUp / Down arrows: pitch",
        "Ctrl: brake · Space: gain height\nStay still in the circle to activate it."
    };
    private static readonly string[] GamepadHints = {
        "Hold <sprite name=\"xbox_a\" tint=0> to flap your wings.",
        "<sprite name=\"xbox_left_stick\" tint=0> Left / right: turn\n<sprite name=\"xbox_left_stick\" tint=0> Up / down: pitch",
        "Hold <sprite name=\"xbox_b\" tint=0> to brake and land.\nStay still in the circle to activate it."
    };
    private const string Completion = "The city is yours! Explore and pick your next mission.";

    [MenuItem(Menu)]
    public static void Create()
    {
        Scene scene = SceneManager.GetActiveScene();
        AchievementManager manager = Find<AchievementManager>(scene);
        DeliveryOfferSelector zones = Find<DeliveryOfferSelector>(scene);
        PigeonController player = zones != null && zones.Player != null
            ? zones.Player.GetComponent<PigeonController>() : Find<PigeonController>(scene);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        AchievementData[] achievements = { Load("Takeoff"), Load("Steering"), Load("Interaction") };
        if (manager == null || player == null || zones == null || zones.Player != player.transform
            || font == null || achievements.Any(a => a == null))
        {
            Debug.LogError("Tutorial: open a scene with an active AchievementManager, pigeon and DeliveryOfferSelector. Check tutorial assets and TMP font.");
            return;
        }
        TMP_SpriteAsset glyphs = TutorialGlyphAsset.GetOrCreate();
        AchievementTutorial existing = Find<AchievementTutorial>(scene);
        if (existing != null)
        {
            Undo.IncrementCurrentGroup();
            int updateGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update Tutorial Text and Glyphs");
            ApplyEnglishTextAndGlyphs(existing, glyphs);
            Undo.CollapseUndoOperations(updateGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("Tutorial updated with English text and Xbox glyphs. Save the scene.", existing);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Achievement Tutorial");
        try
        {
            SerializedObject managerSettings = new SerializedObject(manager);
            SerializedProperty registered = managerSettings.FindProperty("_achievements");
            foreach (AchievementData achievement in achievements)
            {
                bool exists = false;
                for (int i = 0; i < registered.arraySize; i++)
                    if (registered.GetArrayElementAtIndex(i).objectReferenceValue is AchievementData data
                        && data.Id == achievement.Id) exists = true;
                if (exists) continue;
                registered.InsertArrayElementAtIndex(registered.arraySize);
                registered.GetArrayElementAtIndex(registered.arraySize - 1).objectReferenceValue = achievement;
            }
            managerSettings.ApplyModifiedProperties();

            GameObject root = CreateObject("Achievement Tutorial", null);
            SceneManager.MoveGameObjectToScene(root, scene);
            Canvas canvas = Undo.AddComponent<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = CreateObject("Tutorial Panel", root.transform);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -200f);
            rect.sizeDelta = new Vector2(560f, 294f);
            Image background = Undo.AddComponent<Image>(panel);
            background.color = new Color(0.04f, 0.06f, 0.09f, 0.9f);
            background.raycastTarget = false;
            CanvasGroup visibility = Undo.AddComponent<CanvasGroup>(panel);
            visibility.interactable = visibility.blocksRaycasts = false;
            TMP_Text progress = CreateText(panel.transform, font, "Progress", 22f, 16f, 32f);
            TMP_Text objective = CreateText(panel.transform, font, "Objective", 28f, 56f, 100f);
            TMP_Text commands = CreateText(panel.transform, font, "Commands", 22f, 170f, 100f);

            AchievementTutorial tutorial = Undo.AddComponent<AchievementTutorial>(root);
            SerializedObject settings = new SerializedObject(tutorial);
            Property(settings, "Manager").objectReferenceValue = manager;
            Property(settings, "Panel").objectReferenceValue = visibility;
            Property(settings, "ObjectiveText").objectReferenceValue = objective;
            Property(settings, "CommandsText").objectReferenceValue = commands;
            Property(settings, "ProgressText").objectReferenceValue = progress;
            SerializedProperty steps = Property(settings, "Steps");
            steps.arraySize = achievements.Length;
            for (int i = 0; i < achievements.Length; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("<Achievement>k__BackingField").objectReferenceValue = achievements[i];
            }
            settings.ApplyModifiedProperties();
            ApplyEnglishTextAndGlyphs(tutorial, glyphs);

            PigeonTutorialAchievements detector = Undo.AddComponent<PigeonTutorialAchievements>(root);
            SerializedObject detection = new SerializedObject(detector);
            Property(detection, "Manager").objectReferenceValue = manager;
            Property(detection, "Player").objectReferenceValue = player;
            Property(detection, "MissionZones").objectReferenceValue = zones;
            Property(detection, "Takeoff").objectReferenceValue = achievements[0];
            Property(detection, "Steering").objectReferenceValue = achievements[1];
            Property(detection, "Interaction").objectReferenceValue = achievements[2];
            detection.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root;
            Debug.Log("Tutorial added. Save the scene, then Play. Objectives, commands and thresholds are editable in the Inspector.", root);
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    [MenuItem(Menu, true)]
    private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null && SceneManager.GetActiveScene().isLoaded;

    private static AchievementData Load(string step) => AssetDatabase.LoadAssetAtPath<AchievementData>(
        "Assets/Achievements/Data/Tutorial" + step + "_Achievement.asset");

    private static void ApplyEnglishTextAndGlyphs(AchievementTutorial tutorial, TMP_SpriteAsset glyphs)
    {
        string[] ids = { "tutorial_takeoff", "tutorial_steering", "tutorial_interaction" };
        SerializedObject settings = new SerializedObject(tutorial);
        Property(settings, "CompletionMessage").stringValue = Completion;
        SerializedProperty steps = Property(settings, "Steps");
        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            AchievementData achievement = step.FindPropertyRelative("<Achievement>k__BackingField").objectReferenceValue as AchievementData;
            int index = achievement == null ? -1 : Array.IndexOf(ids, achievement.Id);
            if (index < 0) continue;
            step.FindPropertyRelative("<Objective>k__BackingField").stringValue = Objectives[index];
            step.FindPropertyRelative("<Commands>k__BackingField").stringValue = KeyboardHints[index];
            step.FindPropertyRelative("<GamepadCommands>k__BackingField").stringValue = GamepadHints[index];
        }
        settings.ApplyModifiedProperties();
        if (tutorial.CommandsText != null)
        {
            Undo.RecordObject(tutorial.CommandsText, "Set tutorial glyphs");
            tutorial.CommandsText.spriteAsset = glyphs;
            tutorial.CommandsText.richText = true;
            tutorial.CommandsText.tintAllSprites = false;
            if (tutorial.Steps.Count > 0) tutorial.CommandsText.text = tutorial.Steps[0].GamepadCommands;
            PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial.CommandsText);
        }
        if (tutorial.ObjectiveText != null && tutorial.Steps.Count > 0)
        {
            Undo.RecordObject(tutorial.ObjectiveText, "Translate tutorial objective");
            tutorial.ObjectiveText.text = tutorial.Steps[0].Objective;
            PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial.ObjectiveText);
        }
        if (tutorial.ProgressText != null)
        {
            Undo.RecordObject(tutorial.ProgressText, "Translate tutorial progress");
            tutorial.ProgressText.text = $"Tutorial · 1 / {tutorial.Steps.Count}";
            PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial.ProgressText);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(tutorial);
    }

    private static T Find<T>(Scene scene) where T : Behaviour => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault(c => c.isActiveAndEnabled);

    private static GameObject CreateObject(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, "Create " + name);
        if (parent != null) Undo.SetTransformParent(created.transform, parent, "Parent " + name);
        return created;
    }

    private static TMP_Text CreateText(Transform parent, TMP_FontAsset font, string name, float size, float top, float height)
    {
        GameObject created = CreateObject(name, parent);
        RectTransform rect = (RectTransform)created.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20f, -top);
        rect.sizeDelta = new Vector2(520f, height);
        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(created);
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static SerializedProperty Property(SerializedObject settings, string name)
        => settings.FindProperty("<" + name + ">k__BackingField")
            ?? throw new InvalidOperationException("Missing serialized property: " + name);
}
