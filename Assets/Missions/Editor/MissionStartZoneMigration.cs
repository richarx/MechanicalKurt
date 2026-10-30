using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Migration ponctuelle des courses solo vers MissionStartZone, sur les scÃ¨nes ouvertes. Ã€ supprimer une fois les scÃ¨nes migrÃ©es.
/// </summary>
public static class MissionStartZoneMigration
{
    const string DeparturePoseName = "TrueStart";

    [MenuItem("Tools/Missions/Migrer les courses vers MissionStartZone")]
    static void Migrate()
    {
        int migrated = 0;
        foreach (PigeonCharacterRace race in Object.FindObjectsByType<PigeonCharacterRace>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (race.DepartureZone != null || race.DepartureCinematic == null)
                continue;

            GameObject host = race.DepartureCinematic.gameObject;
            PigeonStopZone oldStop = host.GetComponent<PigeonStopZone>();

            MissionStartZone zone = Undo.AddComponent<MissionStartZone>(host);
            var zoneSo = new SerializedObject(zone);
            if (oldStop != null)
            {
                zoneSo.FindProperty("_radius").floatValue = oldStop.Radius;
                zoneSo.FindProperty("_stopSpeedThreshold").floatValue = oldStop.StopSpeedThreshold;
            }
            GameObject pose = GameObject.Find(DeparturePoseName);
            if (pose != null)
                zoneSo.FindProperty("_startPose").objectReferenceValue = pose.transform;
            zoneSo.ApplyModifiedProperties();

            var raceSo = new SerializedObject(race);
            raceSo.FindProperty("<DepartureZone>k__BackingField").objectReferenceValue = zone;
            raceSo.ApplyModifiedProperties();

            if (oldStop != null)
                Undo.DestroyObjectImmediate(oldStop);

            Undo.RecordObject(race.DepartureCinematic, "Migrer la course");
            RemoveCalls(race.DepartureCinematic.OnCinematicEnded, race, "StartRace");

            if (race.DestinationStop != null)
            {
                Undo.RecordObject(race.DestinationStop, "Migrer la course");
                RemoveCalls(race.DestinationStop.OnPlayerStopped, race, "CompleteRace");
            }

            EditorSceneManager.MarkSceneDirty(race.gameObject.scene);
            migrated++;
            Debug.Log($"MissionStartZone ajoutée sur « {host.name} » pour « {race.name} »"
                + (pose != null ? "" : $" — aucun objet « {DeparturePoseName} » : régler StartPose à la main."), zone);
        }

        Debug.Log($"Migration MissionStartZone : {migrated} course(s) migrée(s). Vérifier le compte à rebours (durée de la cinématique de départ) puis sauvegarder la scène.");
    }

    static void RemoveCalls(UnityEventBase evt, Object target, string methodName)
    {
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object callTarget = evt.GetPersistentTarget(i);
            if (callTarget == null || (callTarget == target && evt.GetPersistentMethodName(i) == methodName))
                UnityEventTools.RemovePersistentListener(evt, i);
        }
    }
}
