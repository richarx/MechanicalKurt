using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliveryExampleSetup
{
    private const string _menuPath = "Tools/Delivery/Créer les 3 lieux et 3 offres de test";
    private const string _exampleFolder = "Assets/Delivery/Examples/";

    [MenuItem(_menuPath)]
    private static void CreateExample()
    {
        DeliveryData letter = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "Lettre.asset");
        DeliveryData parcel = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "PetitColis.asset");
        DeliveryData telegram = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "Telegramme.asset");

        if (letter == null || parcel == null || telegram == null)
        {
            Debug.LogError("Setup Delivery : les trois assets exemple sont requis dans " + _exampleFolder);
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        const string undoName = "Créer les lieux et offres Delivery";
        Undo.SetCurrentGroupName(undoName);

        try
        {
            GameObject root = CreateObject("Delivery - Test étape 1", null);
            GameObject points = CreateObject("Lieux", root.transform);
            GameObject offers = CreateObject("Offres simultanées", root.transform);

            DeliveryPoint post = CreatePoint("Poste", new Vector3(0f, 0f, 0f), points.transform);
            DeliveryPoint market = CreatePoint("Marché", new Vector3(12f, 0f, 0f), points.transform);
            DeliveryPoint workshop = CreatePoint("Atelier", new Vector3(6f, 0f, 10f), points.transform);

            CreateOffer("Lettre - Poste vers Marché", letter, post, market, offers.transform);
            CreateOffer("Petit colis - Atelier vers Poste", parcel, workshop, post, offers.transform);
            CreateOffer("Télégramme - Marché vers Atelier", telegram, market, workshop, offers.transform);

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
        Undo.RegisterCreatedObjectUndo(createdObject, "Créer " + objectName);

        if (parent != null)
            Undo.SetTransformParent(createdObject.transform, parent, "Rattacher " + objectName);

        return createdObject;
    }

    private static DeliveryPoint CreatePoint(string pointName, Vector3 position, Transform parent)
    {
        GameObject pointObject = CreateObject(pointName, parent);
        Undo.RecordObject(pointObject.transform, "Positionner " + pointName);
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
