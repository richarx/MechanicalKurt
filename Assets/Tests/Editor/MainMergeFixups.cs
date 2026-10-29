using System.Collections.Generic;
using System.Text;
using PurrNet;
using PurrNet.Prediction;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// Corrections faites après la fusion de main dans la branche multijoueur, là où le YAML seul ne suffit pas.
// Idempotent : chaque étape vérifie l'état avant d'agir. Menu Tools > Fusion main > Appliquer les corrections,
// ou en ligne de commande : -executeMethod MainMergeFixups.Run (quitte Unity ensuite si -quit).
public static class MainMergeFixups
{
    const string PlayerPath = "Assets/Pigeon/Player.prefab";
    const string FreeLookPath = "Assets/Pigeon/Cameras/FreeLook Camera.prefab";
    const string SunglassesModelGuid = "073d5ba040f7f0449826abbc918c4e39";
    const string SunglassesLootGuid = "660cd4db44a3a234aa63575fdcaf4417";
    const string BeachGuid = "d5421b66b51e59e4bb8e187029cdae3b";

    static readonly StringBuilder Report = new StringBuilder();

    [MenuItem("Tools/Fusion main/Appliquer les corrections")]
    public static void Run()
    {
        Report.Clear();
        FixFreeLookPrefab();
        FixPlayerPrefab();
        FixSunglassesLoot();
        FixBeach();
        AssetDatabase.SaveAssets();
        Debug.Log("[Fusion main] Corrections :\n" + Report);
        System.IO.File.WriteAllText("Logs/MainMergeFixups.txt", Report.ToString());
    }

    static void Log(string line) => Report.AppendLine(line);

    // La FreeLook de main garde un composant WaterCamera dont le script a été supprimé par main.
    static void FixFreeLookPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(FreeLookPath);
        try
        {
            int removed = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            Log($"FreeLook Camera.prefab : {removed} script(s) manquant(s) retiré(s)");
            if (removed > 0)
                PrefabUtility.SaveAsPrefabAsset(root, FreeLookPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void FixPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        bool dirty = false;
        try
        {
            var swapper = root.GetComponent<CameraSwapper>();
            var swapperSo = swapper != null ? new SerializedObject(swapper) : null;
            CinemachineCamera orbital = swapperSo?.FindProperty("orbitalCamera").objectReferenceValue as CinemachineCamera;
            CinemachineCamera underwater = swapperSo?.FindProperty("underWaterCamera").objectReferenceValue as CinemachineCamera;
            CinemachineCamera precision = swapperSo?.FindProperty("precisionCamera").objectReferenceValue as CinemachineCamera;
            Log($"Player : caméras orbitale={Name(orbital)}, sous l'eau={Name(underwater)}, précision={Name(precision)}");

            // 1. Contrôles : la caméra orbitale (l'ancienne référence visait un objet local devenu prefab imbriqué).
            var controls = root.GetComponent<PlayerControls>();
            if (controls != null && orbital != null)
            {
                var so = new SerializedObject(controls);
                SerializedProperty camera = so.FindProperty("_camera");
                var follow = orbital.GetComponent<CinemachineOrbitalFollow>();
                if (camera.objectReferenceValue != follow)
                {
                    camera.objectReferenceValue = follow;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    Log("Player : PlayerControls._camera -> OrbitalFollow de la FreeLook");
                }
            }

            // 2. Orientation de départ de la FreeLook : 0 (choix de la branche, pigeon créé à une rotation quelconque).
            var orbitalFollow = orbital != null ? orbital.GetComponent<CinemachineOrbitalFollow>() : null;
            if (orbitalFollow != null)
            {
                var so = new SerializedObject(orbitalFollow);
                SerializedProperty value = so.FindProperty("HorizontalAxis.Value");
                SerializedProperty center = so.FindProperty("HorizontalAxis.Center");
                if (value != null && center != null && (value.floatValue != 0f || center.floatValue != 0f))
                {
                    value.floatValue = 0f;
                    center.floatValue = 0f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    dirty = true;
                    Log("Player : FreeLook HorizontalAxis Value/Center -> 0");
                }
            }

            // 3. Caméras désactivées sur les pigeons distants (NetworkOwnershipToggle) : FreeLook, sous l'eau, précision.
            var toggle = root.GetComponent<NetworkOwnershipToggle>();
            if (toggle != null)
            {
                var so = new SerializedObject(toggle);
                SerializedProperty list = so.FindProperty("_gameObjects");
                var wanted = new List<GameObject>();
                foreach (CinemachineCamera cam in new[] { orbital, underwater, precision })
                    if (cam != null)
                        wanted.Add(cam.gameObject);

                var existing = new HashSet<GameObject>();
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty target = list.GetArrayElementAtIndex(i).FindPropertyRelative("target");
                    if (target.objectReferenceValue == null)
                    {
                        list.DeleteArrayElementAtIndex(i);
                        dirty = true;
                        Log("Player : NetworkOwnershipToggle, entrée vide retirée");
                    }
                    else
                        existing.Add(target.objectReferenceValue as GameObject);
                }
                foreach (GameObject go in wanted)
                {
                    if (existing.Contains(go))
                        continue;
                    int index = list.arraySize;
                    list.InsertArrayElementAtIndex(index);
                    SerializedProperty entry = list.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("target").objectReferenceValue = go;
                    entry.FindPropertyRelative("activeAsOwner").boolValue = true;
                    dirty = true;
                    Log($"Player : NetworkOwnershipToggle += {go.name} (actif seulement pour le propriétaire)");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 4. Lunettes : main les avait ajoutées sur le pigeon de la scène, qui n'existe plus. Dans le prefab réseau,
            //    à côté du chapeau, désactivées, et reliées à PigeonEquipment.sunglasses.
            var equipment = root.GetComponent<PigeonEquipment>();
            if (equipment != null)
            {
                var so = new SerializedObject(equipment);
                SerializedProperty sunglasses = so.FindProperty("sunglasses");
                GameObject hat = so.FindProperty("hat").objectReferenceValue as GameObject;
                if (sunglasses.objectReferenceValue == null && hat != null)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(SunglassesModelGuid));
                    if (model == null)
                        Log("Player : ERREUR modèle Sleek Sunglasses introuvable");
                    else
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, hat.transform.parent);
                        instance.name = "Sleek Sunglasses";
                        instance.transform.SetSiblingIndex(hat.transform.GetSiblingIndex() + 1);
                        instance.transform.localPosition = new Vector3(0.09009894f, 0.000023067f, -0.26996192f);
                        instance.transform.localRotation = new Quaternion(0.8595047f, -0.08018711f, 0.16020417f, 0.47870287f);
                        instance.transform.localScale = new Vector3(4f, 4f, 4f);
                        instance.SetActive(false);
                        sunglasses.objectReferenceValue = instance;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                        Log($"Player : lunettes ajoutées sous « {hat.transform.parent.name} » et reliées à PigeonEquipment");
                    }
                }
            }

            // 5. Bulles sous l'eau : la scène les référençait sur le pigeon de scène ; PigeonSFX les expose désormais.
            var sfx = root.GetComponent<PigeonSFX>();
            if (sfx != null)
            {
                var so = new SerializedObject(sfx);
                SerializedProperty bubbles = so.FindProperty("underwaterBubbles");
                if (bubbles != null && bubbles.objectReferenceValue == null)
                {
                    ParticleSystem found = null;
                    foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
                        if (ps.name.StartsWith("FX_Bubbles"))
                        {
                            found = ps;
                            break;
                        }
                    if (found != null)
                    {
                        bubbles.objectReferenceValue = found;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                        Log($"Player : PigeonSFX.underwaterBubbles -> {found.name}");
                    }
                    else
                        Log("Player : ERREUR particules de bulles introuvables");
                }
            }

            int missing = 0;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            Log($"Player : scripts manquants restants = {missing}");

            if (dirty)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Le loot de lunettes de main n'a pas de _equipment (valeur par défaut : chapeau) et appelle encore ToggleSunglasses.
    static void FixSunglassesLoot()
    {
        string path = AssetDatabase.GUIDToAssetPath(SunglassesLootGuid);
        if (string.IsNullOrEmpty(path))
        {
            Log("Loot lunettes : prefab introuvable");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var loot = root.GetComponentInChildren<LootableEquipment>(true);
            if (loot == null)
            {
                Log("Loot lunettes : pas de LootableEquipment");
                return;
            }
            var so = new SerializedObject(loot);
            bool dirty = false;
            SerializedProperty kind = so.FindProperty("_equipment");
            if (kind.enumValueIndex != (int)PigeonEquipment.Equipment.Sunglasses)
            {
                kind.enumValueIndex = (int)PigeonEquipment.Equipment.Sunglasses;
                dirty = true;
            }
            SerializedProperty calls = so.FindProperty("<OnPlayerStopped>k__BackingField.m_PersistentCalls.m_Calls");
            if (calls != null && calls.arraySize > 0)
            {
                calls.ClearArray();
                dirty = true;
            }
            if (dirty)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            Log($"Loot lunettes : _equipment = {loot.Equipment}, appels persistants vidés");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Ballon local à chaque client : LocalPhysics le fige pendant les rollbacks de la prédiction.
    static void FixBeach()
    {
        string path = AssetDatabase.GUIDToAssetPath(BeachGuid);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ball = root.GetComponentInChildren<BallRolling>(true);
            if (ball == null)
            {
                Log("Plage : pas de BallRolling");
                return;
            }
            if (ball.GetComponent<LocalPhysics>() == null)
            {
                ball.gameObject.AddComponent<LocalPhysics>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Log($"Plage : LocalPhysics ajouté sur « {ball.name} »");
            }
            else
                Log("Plage : LocalPhysics déjà présent");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string Name(Object o) => o != null ? o.name : "AUCUNE";
}
