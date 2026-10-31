#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

public partial class ScenarioRunner
{
    IEnumerator RunDeliveryMission()
    {
        PigeonDeliveryRace delivery = FindAnyObjectByType<PigeonDeliveryRace>();
        yield return Check("Une PigeonDeliveryRace active existe", delivery != null);
        yield return Check("Livraison au repos", delivery.Phase == PigeonDeliveryRace.DeliveryPhase.Ready, $"phase {delivery.Phase}");
        yield return Check("Achievement de livraison pas encore débloqué", !IsUnlocked(delivery.CompletionAchievement));
        Defer(() => CancelIfPlaying(delivery.DepartureCinematic));
        Defer(() => CancelIfPlaying(delivery.ArrivalCinematic));

        yield return LandOnStopZone(delivery.DepartureStop, "départ de la livraison");
        yield return Expect("La cinématique de départ démarre", () => delivery.DepartureCinematic.IsPlaying, 3f,
            () => $"phase {delivery.Phase}");
        yield return Check("La livraison attend la fin du dialogue", delivery.Phase == PigeonDeliveryRace.DeliveryPhase.DepartureCinematic,
            $"phase {delivery.Phase}");
        yield return PlayThroughDialogCinematic(delivery.DepartureCinematic, "dialogue de départ");
        yield return Expect("La course démarre (chrono affiché)", () => delivery.Phase == PigeonDeliveryRace.DeliveryPhase.Racing
            && delivery.TimerParent != null && delivery.TimerParent.activeInHierarchy, 2f, () => $"phase {delivery.Phase}");

        yield return LandOnStopZone(delivery.DestinationStop, "arrivée de la livraison");
        yield return Expect("La cinématique d'arrivée démarre", () => delivery.ArrivalCinematic.IsPlaying, 3f,
            () => $"phase {delivery.Phase}, reste {delivery.RemainingSeconds:0.0} s");
        yield return PlayThroughDialogCinematic(delivery.ArrivalCinematic, "dialogue d'arrivée");
        yield return Expect("Livraison terminée", () => delivery.Phase == PigeonDeliveryRace.DeliveryPhase.Completed, 2f,
            () => $"phase {delivery.Phase}");
        yield return Check("Achievement de livraison débloqué", IsUnlocked(delivery.CompletionAchievement));
    }

    IEnumerator RunCollectionMission()
    {
        PigeonCollectionRace collection = FindAnyObjectByType<PigeonCollectionRace>();
        yield return Check("Une PigeonCollectionRace active existe", collection != null);
        yield return Check("Collecte au repos", collection.Phase == PigeonCollectionRace.CollectionPhase.Ready, $"phase {collection.Phase}");
        yield return Check("Achievement de collecte pas encore débloqué", !IsUnlocked(collection.CompletionAchievement));
        Defer(() => CancelIfPlaying(collection.DepartureCinematic));
        Defer(() => CancelIfPlaying(collection.ArrivalCinematic));

        yield return LandOnStopZone(collection.DepartureStop, "départ de la collecte");
        yield return Expect("La cinématique de départ démarre", () => collection.DepartureCinematic != null
            && collection.DepartureCinematic.IsPlaying, 3f);
        yield return PlayThroughDialogCinematic(collection.DepartureCinematic, "dialogue de départ");
        yield return Expect("La collecte démarre", () => collection.Phase == PigeonCollectionRace.CollectionPhase.Collecting, 2f,
            () => $"phase {collection.Phase}");

        int index = 0;
        foreach (PigeonCollectionZone zone in collection.CollectionZones)
        {
            index++;
            int expected = index;
            yield return Check($"Objet {index} disponible", zone.IsAvailable, zone.name);
            yield return Teleport(Pigeon, zone.transform.position, Pigeon.transform.rotation, $"Passage sur l'objet {index} ({zone.name})");
            yield return Expect($"Objet {index} ramassé", () => collection.CollectedCount >= expected, 1.5f,
                () => $"{collection.CollectedCount}/{collection.CollectionZones.Length}, disponible {zone.IsAvailable}");
        }
        yield return Expect("Tous les objets ramassés : retour vers l'arrivée", () => collection.Phase == PigeonCollectionRace.CollectionPhase.Returning, 2f,
            () => $"phase {collection.Phase}");

        yield return LandOnStopZone(collection.DestinationStop, "arrivée de la collecte");
        yield return Expect("La cinématique d'arrivée démarre", () => collection.ArrivalCinematic != null
            && collection.ArrivalCinematic.IsPlaying, 3f, () => $"phase {collection.Phase}");
        yield return PlayThroughDialogCinematic(collection.ArrivalCinematic, "dialogue d'arrivée");
        yield return Expect("Collecte terminée", () => collection.Phase == PigeonCollectionRace.CollectionPhase.Completed, 2f,
            () => $"phase {collection.Phase}");
        yield return Check("Achievement de collecte débloqué", IsUnlocked(collection.CompletionAchievement));
    }

    IEnumerator RunBillboardMission()
    {
        BillboardMission mission = FindAnyObjectByType<BillboardMission>();
        yield return Check("Une BillboardMission active existe", mission != null);
        yield return Check("Mission au repos", !mission.IsRunning && mission.CompletedCount == 0);

        PigeonStopZone start = mission.GetComponentInChildren<PigeonStopZone>(true);
        DialogCinematic cinematic = start != null ? start.GetComponent<DialogCinematic>() : null;
        yield return Check("Zone et cinématique de départ trouvées sous la mission", start != null && cinematic != null);
        Defer(() => CancelIfPlaying(cinematic));

        int completed = 0;
        void CountCompleted() => completed++;
        mission.OnCompleted.AddListener(CountCompleted);
        Defer(() => mission.OnCompleted.RemoveListener(CountCompleted));

        yield return LandOnStopZone(start, "départ des billboards");
        yield return Expect("La cinématique de départ démarre", () => cinematic.IsPlaying, 3f);
        yield return PlayThroughDialogCinematic(cinematic, "dialogue de départ");
        yield return Expect("La mission démarre (zones actives)", () => mission.IsRunning, 2f);

        for (int i = 0; i < mission.Zones.Count; i++)
        {
            MissionZone zone = mission.Zones[i];
            int expected = i + 1;
            yield return Check($"Zone {expected} active", zone != null && zone.gameObject.activeInHierarchy, zone != null ? zone.name : "null");

            Quaternion noseUp = Quaternion.Euler(-45f, Pigeon.transform.eulerAngles.y, 0f);
            yield return Teleport(Pigeon, zone.transform.position, noseUp, $"Placement sur la zone {expected} ({zone.name})");
            Bot.Flap = true;
            Bot.Brake = true;
            yield return ExpectStill(Pigeon, 4f, zone.StopSpeedThreshold);
            yield return Expect($"Zone {expected} : le prompt s'affiche", () => zone.Prompt != null && zone.IsPromptDisplayed
                    && zone.Prompt.gameObject.activeInHierarchy && zone.Prompt.alpha > 0.99f, 1.5f,
                () => $"distance {Vector3.Distance(Pigeon.transform.position, zone.transform.position):0.00} / rayon {zone.Radius}, "
                    + $"vitesse {Pigeon.Speed:0.00}, au sol {Pigeon.IsGrounded}, alpha {(zone.Prompt != null ? zone.Prompt.alpha : 0f):0.00}");
            Bot.PressInteract();
            yield return Expect($"Zone {expected} validée", () => mission.CompletedCount >= expected, 1.5f,
                () => $"{mission.CompletedCount}/{mission.TotalCount}");
            yield return Expect($"Zone {expected} : fin du plan photo", () => !zone.gameObject.activeSelf, 6f);
            yield return Check($"Zone {expected} : commandes rendues après la photo",
                Pigeon.GetComponent<PlayerControls>() == null || !Pigeon.GetComponent<PlayerControls>().AreControlsBlocked);
            Bot.Flap = false;
            Bot.Brake = false;
        }
        yield return Expect("Mission billboards terminée", () => !mission.IsRunning && completed == 1, 2f,
            () => $"{mission.CompletedCount}/{mission.TotalCount}, OnCompleted {completed}");

        MissionStartZone startZone = null;
        foreach (MissionStartZone candidate in FindObjectsByType<MissionStartZone>(FindObjectsInactive.Exclude))
            if (candidate.Prompt != null && (startZone == null || string.CompareOrdinal(candidate.name, startZone.name) < 0))
                startZone = candidate;
        if (startZone != null)
        {
            yield return HoldStillAt(startZone.transform.position, startZone.Radius, startZone.StopSpeedThreshold, $"zone de départ « {startZone.name} »");
            yield return Expect("Prompt partagé visible et opaque dans une zone de départ", () => PromptVisible(startZone.Prompt), 1.5f,
                () => $"actif {startZone.Prompt.activeInHierarchy}, alpha {PromptAlpha(startZone.Prompt):0.00}");
        }
    }

    IEnumerator RunPrayerMission()
    {
        RoarCinematic roar = FindAnyObjectByType<RoarCinematic>();
        yield return Check("Une RoarCinematic active existe", roar != null);
        PigeonStopZone stop = FindStopZoneCalling(roar);
        yield return Check("Une PigeonStopZone déclenche la cinématique", stop != null);
        yield return Check("Achievement de prière pas encore débloqué", !IsUnlocked(roar.CompletionAchievement));
        Defer(() => { if (roar.IsPlaying) roar.CancelCinematic(); });

        yield return LandOnStopZone(stop, "statue de la prière");
        yield return Expect("La cinématique Roar démarre", () => roar.IsPlaying, 3f);
        float duration = roar.CameraToRoarDelay + roar.RoarToDieDelay + roar.DieToRestoreDelay;
        yield return Expect("La cinématique Roar se termine", () => !roar.IsPlaying, duration + 4f);
        yield return Check("Achievement de prière débloqué", IsUnlocked(roar.CompletionAchievement));
    }

    IEnumerator LandOnStopZone(PigeonStopZone stop, string label)
    {
        yield return Check($"Zone « {label} » prête", stop != null && stop.isActiveAndEnabled,
            stop == null ? "absente" : $"{stop.name} active {stop.gameObject.activeInHierarchy}, composant {stop.enabled}");
        yield return HoldStillAt(stop.transform.position, stop.Radius, stop.StopSpeedThreshold, label);
    }

    IEnumerator HoldStillAt(Vector3 spot, float radius, float stopSpeed, string label)
    {
        Bot.Flap = Bot.Brake = false;
        float yaw = Pigeon.transform.eulerAngles.y;
        yield return Teleport(Pigeon, spot + Vector3.up * 0.5f, Quaternion.Euler(0f, yaw, 0f), $"Placement : {label}");

        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < deadline && !Pigeon.IsGrounded)
            yield return null;
        bool groundUnderZone = Pigeon.IsGrounded && Vector3.Distance(Pigeon.transform.position, spot) <= radius + 0.5f;

        if (groundUnderZone)
            yield return ExpectLanded(Pigeon, 4f, stopSpeed);
        else
        {
            yield return Teleport(Pigeon, spot, Quaternion.Euler(-45f, yaw, 0f), $"Rien sous la zone : vol stationnaire ({label})");
            Bot.Flap = Bot.Brake = true;
            yield return ExpectStill(Pigeon, 4f, stopSpeed);
        }
        yield return Check($"Dans le rayon : {label}", Vector3.Distance(Pigeon.transform.position, spot) <= radius,
            $"distance {Vector3.Distance(Pigeon.transform.position, spot):0.00} / rayon {radius}, {(groundUnderZone ? "posé" : "en vol stationnaire")}");
    }

    IEnumerator PlayThroughDialogCinematic(DialogCinematic cinematic, string label)
    {
        float deadline = Time.realtimeSinceStartup + 60f;
        float nextConfirm = 0f;
        _currentStep = $"Lecture du {label}";
        while (cinematic.IsPlaying && Time.realtimeSinceStartup < deadline)
        {
            DialogManager dialogs = DialogManager.instance;
            if (dialogs != null && dialogs.IsPlaying && dialogs.CurrentSequence == cinematic.Dialogue
                && Time.realtimeSinceStartup >= nextConfirm)
            {
                dialogs.RequestConfirm();
                nextConfirm = Time.realtimeSinceStartup + 0.3f;
            }
            yield return null;
        }
        yield return Check($"Fin normale du {label}", !cinematic.IsPlaying && cinematic.CompletedNormally,
            cinematic.IsPlaying ? "toujours en cours après 60 s" : cinematic.CompletedNormally ? "terminée" : "interrompue");
    }

    static void CancelIfPlaying(DialogCinematic cinematic)
    {
        if (cinematic != null && cinematic.IsPlaying)
            cinematic.CancelCinematic();
    }

    static bool IsUnlocked(AchievementData achievement)
        => achievement != null && AchievementManager.Instance != null && AchievementManager.Instance.IsUnlocked(achievement);

    static PigeonStopZone FindStopZoneCalling(Object target)
    {
        foreach (PigeonStopZone stop in FindObjectsByType<PigeonStopZone>(FindObjectsInactive.Include))
            for (int i = 0; i < stop.OnPlayerStopped.GetPersistentEventCount(); i++)
                if (stop.OnPlayerStopped.GetPersistentTarget(i) == target)
                    return stop;
        return null;
    }
}
#endif
