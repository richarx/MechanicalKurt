using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeliverySelectionSetup
{
    private const string _menuPath = "Tools/Delivery/Create Selection Test";
    private const string _repairMenuPath = "Tools/Delivery/Repair Selected Selection Test";
    private const string _exampleFolder = "Assets/Delivery/Examples/";
    private const string _materialPath = _exampleFolder + "SelectionCircle.mat";

    [MenuItem(_menuPath)]
    private static void CreateExample()
    {
        DeliveryData letter = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "Lettre.asset");
        DeliveryData parcel = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "PetitColis.asset");
        DeliveryData telegram = AssetDatabase.LoadAssetAtPath<DeliveryData>(_exampleFolder + "Telegramme.asset");
        Material material = GetOrCreateMaterial();
        if (letter == null || parcel == null || telegram == null || material == null)
        {
            Debug.LogError("Selection setup needs the three DeliveryData examples and a URP Unlit shader.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Delivery Selection Test");
        try
        {
            GameObject root = CreateObject("Delivery - Selection Test", null);
            DeliverySession session = Undo.AddComponent<DeliverySession>(root);
            SerializedObject serializedSession = new SerializedObject(session);
            serializedSession.FindProperty("_durationSeconds").floatValue = 60f;
            serializedSession.ApplyModifiedProperties();
            GameObject pointsRoot = CreateObject("Points and Offers", root.transform);
            DeliveryPoint post = CreatePoint("Post Office", new Vector3(0f, 0f, 0f), pointsRoot.transform);
            DeliveryPoint market = CreatePoint("Market", new Vector3(12f, 0f, 0f), pointsRoot.transform);
            DeliveryPoint workshop = CreatePoint("Workshop", new Vector3(6f, 0f, 10f), pointsRoot.transform);
            DeliveryOffer[] offers =
            {
                CreateOffer("Letter - Post Office to Market", letter, post, market, pointsRoot.transform),
                CreateOffer("Small Parcel - Workshop to Post Office", parcel, workshop, post, pointsRoot.transform),
                CreateOffer("Telegram - Market to Workshop", telegram, market, workshop, pointsRoot.transform)
            };
            GameObject player = CreateObject("Player Placeholder", root.transform);
            player.transform.position = new Vector3(0f, 0f, 0f);
            DeliveryOfferSelector selector = Undo.AddComponent<DeliveryOfferSelector>(root);
            SerializedObject serializedSelector = new SerializedObject(selector);
            serializedSelector.FindProperty("_session").objectReferenceValue = session;
            serializedSelector.FindProperty("_player").objectReferenceValue = player.transform;
            serializedSelector.FindProperty("_circleMaterial").objectReferenceValue = material;
            SerializedProperty offerList = serializedSelector.FindProperty("_offers");
            offerList.arraySize = offers.Length;
            for (int i = 0; i < offers.Length; i++) offerList.GetArrayElementAtIndex(i).objectReferenceValue = offers[i];
            serializedSelector.ApplyModifiedProperties();

            CharacterController characterController = Undo.AddComponent<CharacterController>(player);
            characterController.detectCollisions = false;
            characterController.enabled = false;

            DeliveryNoclipFlightController flightController = Undo.AddComponent<DeliveryNoclipFlightController>(player);
            SerializedObject serializedFlightController = new SerializedObject(flightController);
            serializedFlightController.FindProperty("_characterController").objectReferenceValue = characterController;
            serializedFlightController.FindProperty("_offerSelector").objectReferenceValue = selector;
            serializedFlightController.FindProperty("_session").objectReferenceValue = session;
            serializedFlightController.FindProperty("_movementCamera").objectReferenceValue = Camera.main;
            serializedFlightController.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    [MenuItem(_menuPath, true)]
    private static bool CanCreateExample() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null
        && EditorSceneManager.GetActiveScene().IsValid() && EditorSceneManager.GetActiveScene().isLoaded;

    [MenuItem(_repairMenuPath)]
    private static void RepairSelectedExample()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("Select the existing Delivery test root or one of its children first.");
            return;
        }

        DeliverySession session = FindSessionInAncestors(selected.transform);
        if (session == null) session = selected.GetComponentInChildren<DeliverySession>(true);
        GameObject root = session != null ? session.gameObject : selected.transform.root.gameObject;
        if (session == null) session = root.GetComponent<DeliverySession>();
        DeliveryOfferSelector selector = root.GetComponent<DeliveryOfferSelector>();
        Transform playerTransform = null;
        if (selector != null)
            playerTransform = new SerializedObject(selector).FindProperty("_player").objectReferenceValue as Transform;
        if (playerTransform == null)
            playerTransform = FindChild(root.transform, "Player Placeholder");
        if (playerTransform == null)
            playerTransform = FindChildWithFlightController(root.transform);
        GameObject player = playerTransform != null ? playerTransform.gameObject : null;
        DeliveryOffer[] offers = root.GetComponentsInChildren<DeliveryOffer>(true);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
        if (material == null) material = GetOrCreateMaterial();
        if (session == null || player == null || offers.Length == 0 || material == null)
        {
            Debug.LogError("Repair needs an existing test root with DeliverySession, an assigned player (or Player Placeholder), at least one offer, and the URP selection material.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair Delivery Selection Test");
        try
        {
            if (selector == null) selector = Undo.AddComponent<DeliveryOfferSelector>(root);
            SerializedObject serializedSelector = new SerializedObject(selector);
            serializedSelector.FindProperty("_session").objectReferenceValue = session;
            SerializedProperty playerReference = serializedSelector.FindProperty("_player");
            if (playerReference.objectReferenceValue == null) playerReference.objectReferenceValue = player.transform;
            SerializedProperty circleMaterial = serializedSelector.FindProperty("_circleMaterial");
            if (circleMaterial.objectReferenceValue == null) circleMaterial.objectReferenceValue = material;
            SerializedProperty offerList = serializedSelector.FindProperty("_offers");
            if (offerList.arraySize == 0)
            {
                offerList.arraySize = offers.Length;
                for (int i = 0; i < offers.Length; i++) offerList.GetArrayElementAtIndex(i).objectReferenceValue = offers[i];
            }
            else
            {
                int nextOffer = 0;
                for (int i = 0; i < offerList.arraySize; i++)
                {
                    if (offerList.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                    while (nextOffer < offers.Length && IsOfferAssigned(offerList, offers[nextOffer])) nextOffer++;
                    if (nextOffer < offers.Length)
                        offerList.GetArrayElementAtIndex(i).objectReferenceValue = offers[nextOffer++];
                }
            }
            serializedSelector.ApplyModifiedProperties();

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController == null) characterController = Undo.AddComponent<CharacterController>(player);
            Undo.RecordObject(characterController, "Configure noclip CharacterController");
            characterController.detectCollisions = false;
            characterController.enabled = false;

            DeliveryNoclipFlightController flight = player.GetComponent<DeliveryNoclipFlightController>();
            if (flight == null) flight = Undo.AddComponent<DeliveryNoclipFlightController>(player);
            SerializedObject serializedFlight = new SerializedObject(flight);
            serializedFlight.FindProperty("_characterController").objectReferenceValue = characterController;
            serializedFlight.FindProperty("_offerSelector").objectReferenceValue = selector;
            serializedFlight.FindProperty("_session").objectReferenceValue = session;
            SerializedProperty camera = serializedFlight.FindProperty("_movementCamera");
            if (camera.objectReferenceValue == null) camera.objectReferenceValue = Camera.main;
            serializedFlight.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("Delivery test setup repaired. Start the session to accept an offer.", root);
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    [MenuItem(_repairMenuPath, true)]
    private static bool CanRepairSelectedExample() => !EditorApplication.isPlayingOrWillChangePlaymode
        && PrefabStageUtility.GetCurrentPrefabStage() == null
        && Selection.activeGameObject != null
        && EditorSceneManager.GetActiveScene().IsValid() && EditorSceneManager.GetActiveScene().isLoaded;

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

    private static Transform FindChildWithFlightController(Transform root)
    {
        if (root.GetComponent<DeliveryNoclipFlightController>() != null) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildWithFlightController(root.GetChild(i));
            if (found != null) return found;
        }
        return null;
    }

    private static bool IsOfferAssigned(SerializedProperty offerList, DeliveryOffer offer)
    {
        for (int i = 0; i < offerList.arraySize; i++)
            if (offerList.GetArrayElementAtIndex(i).objectReferenceValue == offer) return true;
        return false;
    }

    private static DeliverySession FindSessionInAncestors(Transform selected)
    {
        for (Transform current = selected; current != null; current = current.parent)
        {
            DeliverySession session = current.GetComponent<DeliverySession>();
            if (session != null) return session;
        }
        return null;
    }

    private static Material GetOrCreateMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        material = new Material(shader) { name = "Delivery Selection Circle" };
        AssetDatabase.CreateAsset(material, _materialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static GameObject CreateObject(string objectName, Transform parent)
    {
        GameObject created = new GameObject(objectName);
        Undo.RegisterCreatedObjectUndo(created, "Create " + objectName);
        if (parent != null) Undo.SetTransformParent(created.transform, parent, "Parent " + objectName);
        return created;
    }

    private static DeliveryPoint CreatePoint(string pointName, Vector3 position, Transform parent)
    {
        GameObject pointObject = CreateObject(pointName, parent);
        Undo.RecordObject(pointObject.transform, "Position " + pointName);
        pointObject.transform.position = position;
        DeliveryPoint point = Undo.AddComponent<DeliveryPoint>(pointObject);
        SerializedObject serialized = new SerializedObject(point);
        serialized.FindProperty("_displayName").stringValue = pointName;
        serialized.FindProperty("_radius").floatValue = 2f;
        serialized.ApplyModifiedProperties();
        return point;
    }

    private static DeliveryOffer CreateOffer(string offerName, DeliveryData data, DeliveryPoint departure,
        DeliveryPoint destination, Transform parent)
    {
        GameObject offerObject = CreateObject(offerName, parent);
        DeliveryOffer offer = Undo.AddComponent<DeliveryOffer>(offerObject);
        SerializedObject serialized = new SerializedObject(offer);
        serialized.FindProperty("_data").objectReferenceValue = data;
        serialized.FindProperty("_departure").objectReferenceValue = departure;
        serialized.FindProperty("_destination").objectReferenceValue = destination;
        serialized.ApplyModifiedProperties();
        return offer;
    }
}
