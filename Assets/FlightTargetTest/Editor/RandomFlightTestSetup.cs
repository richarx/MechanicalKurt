using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>All changes are saved in a new scene, never in the source scene or prefabs.</summary>
public static class RandomFlightTestSetup
{
    private const string Folder = "Assets/FlightTargetTest";
    private const string Menu = "Tools/Flight Target Test/Create From Current City";
    private const string RingPath = "Assets/Delivery/Prefabs/End.prefab";
    private const string CompassPath = "Assets/Compass/CompassArrow_Prefab.prefab";

    [MenuItem(Menu)]
    public static void CreateFromCurrentCity()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        CreateCopy(SceneManager.GetActiveScene());
    }

    [MenuItem(Menu, true)]
    private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null
        && SceneManager.GetActiveScene().IsValid() && SceneManager.GetActiveScene().isLoaded;

    // Also callable with Unity -batchmode -executeMethod RandomFlightTestSetup.CreateDefaultScene.
    public static void CreateDefaultScene()
    {
        Scene source = EditorSceneManager.OpenScene("Assets/Scenes/TataCityScene.unity", OpenSceneMode.Single);
        CreateCopy(source);
        RandomFlightTargetChecks.RunAll();
    }

    private static void CreateCopy(Scene source)
    {
        if (FindInScene<PigeonController>(source).Count == 0)
            throw new InvalidOperationException("Ouvrir une scene de ville contenant le pigeon avant de creer le test.");

        GameObject ringPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RingPath);
        GameObject compassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompassPath);
        if (ringPrefab == null || compassPrefab == null)
            throw new InvalidOperationException("Le prefab d'anneau End ou le prefab CompassArrow est introuvable.");

        if (!AssetDatabase.IsValidFolder(Folder + "/Scenes"))
            AssetDatabase.CreateFolder(Folder, "Scenes");
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Scenes/RandomFlightCityTest.unity");
        if (!EditorSceneManager.SaveScene(source, path, true))
            throw new InvalidOperationException("Impossible de creer la copie de la scene.");

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        PigeonController player = FindInScene<PigeonController>(scene).Find(p => p.isActiveAndEnabled);
        if (player == null)
            throw new InvalidOperationException("Activer un pigeon dans la scene source avant de creer le test.");

        Camera camera = FindInScene<Camera>(scene).Find(c => c.isActiveAndEnabled && c.CompareTag("MainCamera"));
        if (camera == null)
            camera = FindInScene<Camera>(scene).Find(c => c.isActiveAndEnabled);
        if (camera == null)
            throw new InvalidOperationException("La scene doit contenir une camera active.");

        // Recreating a test from a previous test copy should not create a second loop.
        foreach (RandomFlightTargetTest previous in FindInScene<RandomFlightTargetTest>(scene))
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
        foreach (RandomFlightCompassArrow previous in FindInScene<RandomFlightCompassArrow>(scene))
            UnityEngine.Object.DestroyImmediate(previous.gameObject);

        DisableDeliverySystems(scene);

        GameObject root = new GameObject("Flight Target Test");
        SceneManager.MoveGameObjectToScene(root, scene);
        RandomFlightTargetTest objectives = root.AddComponent<RandomFlightTargetTest>();
        SerializedObject settings = new SerializedObject(objectives);
        Property(settings, "Player").objectReferenceValue = player.transform;
        Property(settings, "RingPrefab").objectReferenceValue = ringPrefab;
        Property(settings, "SpawnBounds").boundsValue = new Bounds(player.transform.position + Vector3.up * 40f,
            new Vector3(300f, 120f, 300f));
        settings.ApplyModifiedPropertiesWithoutUndo();

        List<DeliveryCompassArrow> arrows = FindInScene<DeliveryCompassArrow>(scene);
        DeliveryCompassArrow oldArrow = arrows.Find(a => a.gameObject.activeInHierarchy);
        if (oldArrow == null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(compassPrefab, scene);
            instance.transform.SetParent(camera.transform, false);
            oldArrow = instance.GetComponent<DeliveryCompassArrow>();
        }
        foreach (DeliveryCompassArrow other in arrows)
        {
            if (other != oldArrow)
                other.gameObject.SetActive(false);
        }
        ConfigureCompass(oldArrow, objectives, camera);

        Physics.SyncTransforms();
        if (!objectives.TryFindTargetPosition(out _))
            Debug.LogWarning("Test cree, mais aucun emplacement trouve avec les valeurs initiales. Ajuster Spawn Bounds et les layers.", objectives);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Impossible de sauvegarder la scene de test.");
        Selection.activeGameObject = root;
        Debug.Log("Flight Target Test cree : " + path + ". Lancer Play pour enchainer les objectifs.", root);
    }

    private static void ConfigureCompass(DeliveryCompassArrow oldArrow, RandomFlightTargetTest objectives, Camera camera)
    {
        if (oldArrow == null)
            throw new InvalidOperationException("Le prefab de boussole ne contient pas DeliveryCompassArrow.");
        SerializedObject original = new SerializedObject(oldArrow);
        GameObject arrowObject = oldArrow.gameObject;
        RandomFlightCompassArrow arrow = arrowObject.AddComponent<RandomFlightCompassArrow>();
        SerializedObject settings = new SerializedObject(arrow);
        Property(settings, "Objectives").objectReferenceValue = objectives;
        Property(settings, "ViewCamera").objectReferenceValue = camera;
        Property(settings, "Visual").objectReferenceValue = original.FindProperty("_visual").objectReferenceValue;
        Property(settings, "ArrowPivot").objectReferenceValue = original.FindProperty("_arrowPivot").objectReferenceValue;
        string[] parameters = { "ViewportY", "Distance", "BaseScale", "KeepConstantScreenSize", "ReferenceFov",
            "MaxPitch", "TurnSpeed", "PopDuration", "PopCurve", "PulseBaseAmplitude", "PulseBaseFrequency",
            "PulseRampStartDistance", "PulseRampFullDistance", "PulseMaxAmplitude", "PulseMaxFrequency" };
        foreach (string name in parameters)
        {
            SerializedProperty from = original.FindProperty("_" + char.ToLowerInvariant(name[0]) + name.Substring(1));
            SerializedProperty to = Property(settings, name);
            if (from.propertyType == SerializedPropertyType.Boolean)
                to.boolValue = from.boolValue;
            else if (from.propertyType == SerializedPropertyType.AnimationCurve)
                to.animationCurveValue = from.animationCurveValue;
            else
                to.floatValue = from.floatValue;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        // Only this scene instance receives a removed-component override.
        UnityEngine.Object.DestroyImmediate(oldArrow);
        arrowObject.name = "Flight Test Compass";
        arrowObject.SetActive(true);
        arrow.enabled = true;
        if (arrow.Visual != null)
            arrow.Visual.SetActive(true);
        RecordPrefabOverride(arrowObject);
    }

    private static void DisableDeliverySystems(Scene scene)
    {
        foreach (MonoBehaviour behaviour in FindInScene<MonoBehaviour>(scene))
        {
            if (behaviour == null)
                continue;
            bool hud = behaviour is DeliveryHUD || behaviour is DeliverySessionHUD || behaviour is DeliveryGuidance;
            if (hud)
            {
                if (behaviour is DeliveryHUD && behaviour.GetComponent<Canvas>() != null)
                {
                    behaviour.gameObject.SetActive(false);
                    RecordPrefabOverride(behaviour.gameObject);
                }
                // Hide only UI objects referenced by the delivery display, preserving the player's camera and other HUDs.
                SerializedObject serialized = new SerializedObject(behaviour);
                SerializedProperty iterator = serialized.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                        continue;
                    UnityEngine.Object value = iterator.objectReferenceValue;
                    GameObject ui = value is TMP_Text text ? text.gameObject
                        : value is RectTransform rect ? rect.gameObject : null;
                    if (ui != null && ui.scene == scene)
                    {
                        ui.SetActive(false);
                        RecordPrefabOverride(ui);
                    }
                }
            }
            if (hud || behaviour is DeliverySession || behaviour is DeliveryOfferSelector
                || behaviour is DeliveryManager || behaviour is ManagerCinematic || behaviour is DeliveryPickupCinematic
                || behaviour is DeliveryNoclipFlightController)
            {
                behaviour.enabled = false;
                RecordPrefabOverride(behaviour);
            }
        }
    }

    public static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    private static SerializedProperty Property(SerializedObject settings, string name)
        => settings.FindProperty("<" + name + ">k__BackingField")
            ?? throw new InvalidOperationException("Propriete serialisee introuvable : " + name);

    private static void RecordPrefabOverride(UnityEngine.Object value)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(value))
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
}
