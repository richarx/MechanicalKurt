using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PigeonFanSetup
{
    [MenuItem("GameObject/Gameplay/Pigeon Fan", false, 10)]
    private static void CreateFan(MenuCommand command)
    {
        GameObject root = new GameObject("Pigeon Fan");
        GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
        if (root.transform.parent == null && SceneView.lastActiveSceneView != null)
            root.transform.position = SceneView.lastActiveSceneView.pivot;

        PigeonFan fan = root.AddComponent<PigeonFan>();
        // Placeholder geometry: replace these children with the team's model as needed.
        CreatePrimitive("Base", PrimitiveType.Cylinder, root.transform,
            new Vector3(0f, -0.2f, 0f), new Vector3(5f, 0.2f, 5f), true);

        GameObject rotor = new GameObject("Rotor");
        rotor.transform.SetParent(root.transform, false);
        rotor.transform.localPosition = Vector3.up * 0.1f;
        CreatePrimitive("Blade X", PrimitiveType.Cube, rotor.transform,
            Vector3.zero, new Vector3(4.5f, 0.1f, 0.4f), false);
        CreatePrimitive("Blade Z", PrimitiveType.Cube, rotor.transform,
            Vector3.zero, new Vector3(0.4f, 0.1f, 4.5f), false);
        CreatePrimitive("Hub", PrimitiveType.Cylinder, rotor.transform,
            Vector3.zero, new Vector3(0.6f, 0.2f, 0.6f), false);

        SerializedObject serializedFan = new SerializedObject(fan);
        serializedFan.FindProperty("<Rotor>k__BackingField").objectReferenceValue = rotor.transform;
        serializedFan.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(root, "Créer un ventilateur");
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    [MenuItem("GameObject/Gameplay/Pigeon Fan", true)]
    private static bool CanCreateFan()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void CreatePrimitive(string objectName, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, bool solid)
    {
        GameObject visual = GameObject.CreatePrimitive(type);
        visual.name = objectName;
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = position;
        visual.transform.localScale = scale;
        if (!solid)
            Object.DestroyImmediate(visual.GetComponent<Collider>());
    }
}
