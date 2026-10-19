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
        if (Find<AchievementTutorial>(scene) != null)
        {
            Selection.activeGameObject = Find<AchievementTutorial>(scene).gameObject;
            Debug.Log("A tutorial already exists in this scene.");
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
            rect.sizeDelta = new Vector2(560f, 270f);
            Image background = Undo.AddComponent<Image>(panel);
            background.color = new Color(0.04f, 0.06f, 0.09f, 0.9f);
            background.raycastTarget = false;
            CanvasGroup visibility = Undo.AddComponent<CanvasGroup>(panel);
            visibility.interactable = visibility.blocksRaycasts = false;
            TMP_Text progress = CreateText(panel.transform, font, "Progress", 22f, 16f, 32f);
            TMP_Text objective = CreateText(panel.transform, font, "Objective", 28f, 56f, 100f);
            TMP_Text commands = CreateText(panel.transform, font, "Commands", 22f, 170f, 80f);

            AchievementTutorial tutorial = Undo.AddComponent<AchievementTutorial>(root);
            SerializedObject settings = new SerializedObject(tutorial);
            Property(settings, "Manager").objectReferenceValue = manager;
            Property(settings, "Panel").objectReferenceValue = visibility;
            Property(settings, "ObjectiveText").objectReferenceValue = objective;
            Property(settings, "CommandsText").objectReferenceValue = commands;
            Property(settings, "ProgressText").objectReferenceValue = progress;
            SerializedProperty steps = Property(settings, "Steps");
            steps.arraySize = achievements.Length;
            string[] objectives = {
                "Décolle et prends de la hauteur.",
                "Oriente ton vol pour changer de direction.",
                "Pose-toi dans un cercle de mission et reste immobile pour l’activer."
            };
            string[] hints = {
                "Maintiens Espace pour battre des ailes.",
                "Flèches gauche / droite : tourner\nFlèches haut / bas : incliner le vol",
                "Ctrl : freiner · Espace : reprendre de la hauteur\nL’activation du cercle est automatique à l’arrêt."
            };
            for (int i = 0; i < achievements.Length; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("<Achievement>k__BackingField").objectReferenceValue = achievements[i];
                step.FindPropertyRelative("<Objective>k__BackingField").stringValue = objectives[i];
                step.FindPropertyRelative("<Commands>k__BackingField").stringValue = hints[i];
            }
            settings.ApplyModifiedProperties();
            progress.text = "Tuto · 1 / 3";
            objective.text = objectives[0];
            commands.text = hints[0];

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
