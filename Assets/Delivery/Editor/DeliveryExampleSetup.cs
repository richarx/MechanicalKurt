using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliveryExampleSetup
{
    private const string _menuPath = "Tools/Delivery/Create Points and Offers Test";
    private const string _exampleFolder = "Assets/Delivery/Examples/";

    [MenuItem(_menuPath)]
    private static void CreateExample()
    {
        DeliveryData letter = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "Lettre.asset");
        DeliveryData parcel = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "PetitColis.asset");
        DeliveryData telegram = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "Telegramme.asset");

        if (letter == null || parcel == null || telegram == null)
        {
            Debug.LogError("Delivery setup requires the three example assets in " + _exampleFolder);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        const string undoName = "Create Delivery Points and Offers";
        Undo.SetCurrentGroupName(undoName);

        try
        {
            GameObject root = CreateObject("Delivery - Points and Offers Test", null);
            GameObject points = CreateObject("Points", root.transform);
            GameObject offers = CreateObject("Simultaneous Offers", root.transform);

            DeliveryPoint post = CreatePoint("Post Office", new Vector3(0f, 0f, 0f), points.transform);
            DeliveryPoint market = CreatePoint("Market", new Vector3(12f, 0f, 0f), points.transform);
            DeliveryPoint workshop = CreatePoint("Workshop", new Vector3(6f, 0f, 10f), points.transform);

            CreateOffer("Letter - Post Office to Market", letter, post, market, offers.transform);
            CreateOffer("Small Parcel - Workshop to Post Office", parcel, workshop, post, offers.transform);
            CreateOffer("Telegram - Market to Workshop", telegram, market, workshop, offers.transform);

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    [MenuItem(_menuPath, true)]
    private static bool CanCreateExample()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode
            && PrefabStageUtility.GetCurrentPrefabStage() == null
            && EditorSceneManager.GetActiveScene().IsValid()
            && EditorSceneManager.GetActiveScene().isLoaded;
    }

    private static GameObject CreateObject(string objectName, Transform parent)
    {
        GameObject createdObject = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(createdObject, "Create " + objectName);

        if (parent != null)
            Undo.SetTransformParent(createdObject.transform, parent, "Parent " + objectName);

        return createdObject;
    }

    private static DeliveryPoint CreatePoint(string pointName, Vector3 position, Transform parent)
    {
        GameObject pointObject = CreateObject(pointName, parent);
        Undo.RecordObject(pointObject.transform, "Position " + pointName);
        pointObject.transform.position = position;
        DeliveryPoint point = Undo.AddComponent<DeliveryPoint>(pointObject);
        SerializedObject serializedPoint = new SerializedObject(point);
        serializedPoint.FindProperty("_displayName").stringValue = pointName;
        serializedPoint.FindProperty("_radius").floatValue = 2f;
        serializedPoint.ApplyModifiedProperties();
        return point;
    }

    private static void CreateOffer(string offerName, DeliveryData data, DeliveryPoint departure,
        DeliveryPoint destination, Transform parent)
    {
        GameObject offerObject = CreateObject(offerName, parent);
        DeliveryOffer offer = Undo.AddComponent<DeliveryOffer>(offerObject);
        SerializedObject serializedOffer = new SerializedObject(offer);
        serializedOffer.FindProperty("_data").objectReferenceValue = data;
        serializedOffer.FindProperty("_departure").objectReferenceValue = departure;
        serializedOffer.FindProperty("_destination").objectReferenceValue = destination;
        serializedOffer.ApplyModifiedProperties();
    }
}
