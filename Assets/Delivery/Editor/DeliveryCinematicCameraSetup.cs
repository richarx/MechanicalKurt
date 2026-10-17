using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliveryCinematicCameraSetup
{
    private const string MenuPath = "Tools/Delivery/Repair Cinematic Camera Setup";

    [MenuItem(MenuPath)]
    private static void Repair()
    {
        ManagerCinematic manager = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<ManagerCinematic>() : null;
        if (manager == null)
        {
            ManagerCinematic[] managers = Object.FindObjectsByType<ManagerCinematic>();
            if (managers.Length == 1) manager = managers[0];
        }
        if (manager == null)
        {
            Debug.LogWarning("Select the object holding ManagerCinematic, then run camera repair again.");
            return;
        }

        SerializedObject settings = new SerializedObject(manager);
        CinemachineBrain oldBrain = settings.FindProperty("_brain").objectReferenceValue as CinemachineBrain;
        Camera renderCamera = oldBrain != null ? oldBrain.GetComponent<Camera>() : null;
        if (renderCamera == null) renderCamera = Camera.main;
        CinemachineVirtualCameraBase gameplay = settings.FindProperty("_gameplayCamera").objectReferenceValue as CinemachineVirtualCameraBase;
        if (renderCamera == null || renderCamera.gameObject.scene != manager.gameObject.scene)
        {
            Debug.LogWarning("A MainCamera-tagged rendering Camera is required in the manager's scene.", manager);
            return;
        }
        if (gameplay == null || (gameplay.gameObject == renderCamera.gameObject && !(gameplay is CinemachineCamera)))
        {
            Debug.LogWarning("Assign a gameplay CinemachineCamera to the manager before repairing.", manager);
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Cinematic Camera Setup");
        try
        {
            Undo.RecordObject(manager, "Connect repaired cinematic cameras");
            bool splitGameplay = gameplay.gameObject == renderCamera.gameObject;
            if (splitGameplay)
            {
                GameObject gameplayObject = new GameObject("Gameplay Cinemachine Camera");
                Undo.RegisterCreatedObjectUndo(gameplayObject, "Create separate gameplay camera");
                Undo.SetTransformParent(gameplayObject.transform, renderCamera.transform.parent, "Place gameplay camera");
                gameplayObject.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
                CinemachineCamera separateCamera = Undo.AddComponent<CinemachineCamera>(gameplayObject);
                EditorUtility.CopySerialized(gameplay, separateCamera);
                Undo.DestroyObjectImmediate(gameplay);
                gameplay = separateCamera;
            }

            CinemachineBrain brain = renderCamera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = Undo.AddComponent<CinemachineBrain>(renderCamera.gameObject);
                if (oldBrain != null) EditorUtility.CopySerialized(oldBrain, brain);
            }
            if (oldBrain != null && oldBrain != brain) Undo.DestroyObjectImmediate(oldBrain);
            Undo.RecordObject(brain, "Enable rendering camera Brain");
            brain.enabled = true;

            settings.Update();
            settings.FindProperty("_brain").objectReferenceValue = brain;
            settings.FindProperty("_gameplayCamera").objectReferenceValue = gameplay;
            if (splitGameplay)
                settings.FindProperty("_prototypeViewSource").objectReferenceValue = renderCamera;
            settings.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeGameObject = manager.gameObject;
            Debug.Log("Cinematic cameras repaired. Brain is on the rendering camera; gameplay uses a separate Cinemachine camera. Prototype mouse look is preserved. Scene not saved; one Undo reverts the repair.", manager);
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    [MenuItem(MenuPath, true)]
    private static bool CanRepair()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && PrefabStageUtility.GetCurrentPrefabStage() == null;
    }
}
