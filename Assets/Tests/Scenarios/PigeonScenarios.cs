#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Les scénarios eux-mêmes. Chacun part du pigeon du joueur à son point d'apparition et nettoie derrière lui.
// Les cibles (anneau, tuto, course) sont cherchées dans la vraie scène : si un objet est déplacé ou
// mal configuré, c'est le scénario qui le signale.
public partial class ScenarioRunner
{
    public const string RingScenario = "Anneau de boost";
    public const string TutoScenario = "Tuto : zone de départ";
    public const string RaceStartScenario = "Course : départ à 2 pigeons";
    public const string RaceHeightScenario = "Course : échec de hauteur";

    public static readonly string[] ScenarioNames =
    {
        RingScenario, TutoScenario, RaceStartScenario, RaceHeightScenario,
        DeliveryScenario, CollectionScenario, BillboardScenario, PrayerScenario,
        EquipmentScenario,
    };

    List<ScenarioDef> BuildScenarios() => new List<ScenarioDef>
    {
        new ScenarioDef { Name = RingScenario, Body = RingBoost },
        new ScenarioDef { Name = TutoScenario, Body = TutoStartZone },
        new ScenarioDef { Name = RaceStartScenario, Body = RaceStart },
        new ScenarioDef { Name = RaceHeightScenario, Body = RaceHeightFailure },
        new ScenarioDef { Name = DeliveryScenario, Body = RunDeliveryMission },
        new ScenarioDef { Name = CollectionScenario, Body = RunCollectionMission },
        new ScenarioDef { Name = BillboardScenario, Body = RunBillboardMission },
        new ScenarioDef { Name = PrayerScenario, Body = RunPrayerMission },
        new ScenarioDef { Name = EquipmentScenario, Body = RunEquipmentPickup },
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  1. Anneau : traversée en vol plané, boost unique, puis nouveau boost à la traversée suivante.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator RingBoost()
    {
        // Toujours le même anneau d'un lancement à l'autre (FindAnyObjectByType n'a pas d'ordre garanti).
        BoostRing ring = null;
        foreach (BoostRing candidate in FindObjectsByType<BoostRing>(FindObjectsInactive.Exclude))
            if (ring == null || string.CompareOrdinal(candidate.name, ring.name) < 0
                || (candidate.name == ring.name && candidate.transform.position.x < ring.transform.position.x))
                ring = candidate;
        yield return Check("Un BoostRing actif existe dans la scène", ring != null, ring != null ? $"« {ring.name} » en {ring.transform.position}" : null);

        var box = ring.GetComponent<BoxCollider>();
        Vector3 center = box != null ? ring.transform.TransformPoint(box.center) : ring.transform.position;
        Vector3 normal = ring.transform.forward;

        int boosts = 0;
        void CountBoost() => boosts++;
        Pigeon.OnBoosted.AddListener(CountBoost);
        Defer(() => Pigeon.OnBoosted.RemoveListener(CountBoost));

        // Départ 5 m devant l'anneau, 3 m au-dessus, nez à -30° : le vol plané passe par le centre.
        Vector3 start = center - normal * 5f + Vector3.up * 3f;
        Quaternion glide = Quaternion.LookRotation(normal) * Quaternion.Euler(30f, 0f, 0f);

        yield return Teleport(Pigeon, start, glide, "Placement devant l'anneau");
        float speedBefore = Pigeon.Speed;
        yield return Expect("La traversée déclenche le boost", () => boosts >= 1, 4f,
            () => $"boosts {boosts}, vitesse {Pigeon.Speed:0.0}, distance au centre {Vector3.Distance(Pigeon.transform.position, center):0.0} m");
        yield return Expect($"Vitesse de sortie ≥ {ring.MinExitSpeed * 0.8f:0} (MinExitSpeed {ring.MinExitSpeed})",
            () => Pigeon.Speed >= ring.MinExitSpeed * 0.8f, 1f,
            () => $"vitesse {Pigeon.Speed:0.0} (avant {speedBefore:0.0})");
        yield return ExpectStays("Un seul boost par traversée", () => boosts == 1, 1f, () => $"boosts {boosts}");

        yield return Teleport(Pigeon, start, glide, "Retour devant l'anneau");
        yield return Expect("La traversée suivante redonne un boost", () => boosts >= 2, 4f,
            () => $"boosts {boosts}, étourdi {Pigeon.IsStun}, vitesse {Pigeon.Speed:0.0}, distance au centre {Vector3.Distance(Pigeon.transform.position, center):0.0} m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. Tuto : se poser dans la zone, prompt, F, cinématique + achievement, puis pas de relance locale.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator TutoStartZone()
    {
        PigeonStopDialogCinematic tuto = null;
        foreach (PigeonStopDialogCinematic candidate in FindObjectsByType<PigeonStopDialogCinematic>(FindObjectsInactive.Exclude))
            if (candidate.StartZone != null)
                tuto = candidate;
        yield return Check("Le tuto est relié à une MissionStartZone", tuto != null && tuto.Cinematic != null);

        MissionStartZone zone = tuto.StartZone;
        AchievementManager achievements = AchievementManager.Instance;

        int started = 0, startedLocal = 0, countdowns = 0;
        void OnStarted() => started++;
        void OnStartedLocal() => startedLocal++;
        void OnCountdown() => countdowns++;
        zone.OnStarted.AddListener(OnStarted);
        zone.OnStartedLocal.AddListener(OnStartedLocal);
        zone.OnCountdownStarted.AddListener(OnCountdown);
        Defer(() =>
        {
            zone.OnStarted.RemoveListener(OnStarted);
            zone.OnStartedLocal.RemoveListener(OnStartedLocal);
            zone.OnCountdownStarted.RemoveListener(OnCountdown);
        });
        string ZoneState() => $"phase {zone.Phase}, prêt {Pigeon.WantsStart}, gelé {Pigeon.IsMovementFrozen}, "
            + $"vitesse {Pigeon.Speed:0.00}, comptes à rebours {countdowns}, OnStarted {started}, OnStartedLocal {startedLocal}, "
            + $"dialogue occupé {(DialogManager.instance != null && DialogManager.instance.IsPlaying)}, "
            + $"appui en attente {Bot.HasPendingInteract}, appuis lus {Bot.PredictedInteractConsumed}";
        yield return Check("AchievementManager présent", achievements != null);
        yield return Check("L'achievement de fin n'est pas encore débloqué", !achievements.IsUnlocked(tuto.CompletionAchievement));
        Defer(() => { if (tuto.Cinematic.IsPlaying) tuto.Cinematic.CancelCinematic(); });

        yield return Teleport(Pigeon, zone.transform.position + Vector3.up * 0.5f, zone.transform.rotation, "Placement dans le cercle du tuto");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        yield return Check("Le pigeon est dans le rayon de la zone",
            Vector3.Distance(Pigeon.transform.position, zone.transform.position) <= zone.Radius,
            $"distance {Vector3.Distance(Pigeon.transform.position, zone.transform.position):0.00} / rayon {zone.Radius}");
        yield return Expect("Le prompt d'interaction s'affiche", () => zone.Prompt != null && zone.Prompt.activeInHierarchy, 1f,
            () => zone.Prompt == null ? "aucun prompt assigné" : zone.Prompt.activeInHierarchy ? null : "prompt inactif");
        yield return ExpectStays("Sans appui sur F, rien ne démarre", () => !tuto.Cinematic.IsPlaying, 1f, ZoneState);

        Bot.PressInteract();
        yield return Expect("F : le pigeon valide (prêt)", () => Pigeon.WantsStart || started > 0, 1f, ZoneState);
        yield return Expect("F : la zone lance le départ", () => started > 0, 2f, ZoneState);
        yield return Expect("Le départ est attribué au joueur local", () => startedLocal > 0, 1f, ZoneState);
        yield return Expect("F : la cinématique du tuto démarre", () => tuto.Cinematic.IsPlaying, 2f, ZoneState);
        yield return Check("L'achievement de fin est débloqué", achievements.IsUnlocked(tuto.CompletionAchievement));
        yield return Check("Le cylindre du tuto est masqué", tuto.Cylinder == null || !tuto.Cylinder.activeSelf);
        yield return Expect("Le pigeon n'est pas resté gelé", () => !Pigeon.IsMovementFrozen, 1f);
        yield return Expect("La zone s'est réarmée (attente)", () => zone.Phase == MissionStartZone.Waiting, 1f,
            () => $"phase {zone.Phase}");

        tuto.Cinematic.CancelCinematic();
        yield return Expect("Fin de la cinématique", () => !tuto.Cinematic.IsPlaying, 5f);
        yield return ExpectLanded(Pigeon, 3f, zone.StopSpeedThreshold);

        Bot.PressInteract();
        yield return ExpectStays("Un second appui ne relance pas la cinématique", () => !tuto.Cinematic.IsPlaying, 2f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. Course : un seul prêt → rien ; deux prêts → compte à rebours et gel ; annulation ;
    //     relance → téléportation sur la ligne de départ et course lancée.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator RaceStart()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));

        var bots = new List<SpawnedBot>();
        yield return PlaceTwoPigeonsInDepartureZone(race, bots);
        MissionStartZone zone = race.DepartureZone;
        PigeonController other = bots[0].Pigeon;

        Bot.PressInteract();
        yield return Expect("F : le joueur valide", () => Pigeon.WantsStart, 1f,
            () => $"appuis lus {Bot.PredictedInteractConsumed}, vitesse {Pigeon.Speed:0.00}");
        yield return ExpectStays("Un seul pigeon prêt : pas de départ, le joueur reste prêt",
            () => zone.Phase == MissionStartZone.Waiting && Pigeon.WantsStart, 2f,
            () => $"phase {zone.Phase}, joueur prêt {Pigeon.WantsStart}, vitesse {Pigeon.Speed:0.000}, au sol {Pigeon.IsGrounded}");

        bots[0].Controls.PressInteract();
        yield return Expect("Deux pigeons prêts : compte à rebours", () => zone.Phase == MissionStartZone.CountingDown, 2f,
            () => $"phase {zone.Phase}, prêts : joueur {Pigeon.WantsStart}, bot {other.WantsStart}");
        yield return Expect("Les deux pigeons sont gelés", () => Pigeon.IsMovementFrozen && other.IsMovementFrozen, 1f);
        yield return Expect("La cinématique de départ démarre", () => race.DepartureCinematic.IsPlaying, 2f);

        bots[0].Controls.PressInteract();
        yield return Expect("Annulation par un joueur : retour en attente", () => zone.Phase == MissionStartZone.Waiting, 2f,
            () => $"phase {zone.Phase}");
        yield return Expect("Annulation : les deux pigeons sont dégelés", () => !Pigeon.IsMovementFrozen && !other.IsMovementFrozen, 1f);
        yield return Expect("Annulation : la cinématique est coupée", () => !race.DepartureCinematic.IsPlaying, 5f);

        // Le joueur est resté prêt : le bot revalide.
        bots[0].Controls.PressInteract();
        yield return Expect("Relance : compte à rebours", () => zone.Phase == MissionStartZone.CountingDown, 2f,
            () => $"phase {zone.Phase}, prêts : joueur {Pigeon.WantsStart}, bot {other.WantsStart}");
        yield return Expect("Fin du compte à rebours : course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing,
            zone.CountdownSeconds + 3f, () => $"phase course {race.Phase}, zone {zone.Phase}, reste {zone.CountdownRemaining:0.0} s");

        if (zone.StartPose != null)
        {
            yield return Check("Joueur téléporté sur la ligne de départ", DistanceXZ(Pigeon, zone.StartPose) <= zone.Radius + 1f,
                $"{DistanceXZ(Pigeon, zone.StartPose):0.0} m");
            yield return Check("Bot téléporté sur la ligne de départ", DistanceXZ(other, zone.StartPose) <= zone.Radius + 1f,
                $"{DistanceXZ(other, zone.StartPose):0.0} m");
        }
        yield return Expect("Les deux pigeons sont libres de voler", () => !Pigeon.IsMovementFrozen && !other.IsMovementFrozen, 1f);
        yield return ExpectStays("La course tourne (pas d'arrivée immédiate)", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 2f,
            () => $"phase {race.Phase} ; distance à l'arrivée : joueur {DistanceToFinish(race, Pigeon):0.0} m, bot {DistanceToFinish(race, other):0.0} m, "
                + $"rayon d'arrivée {race.DestinationStop.Radius}, seuil de vitesse {race.DestinationStop.StopSpeedThreshold}, durée {race.DestinationStop.StopDuration}");
    }

    static float DistanceToFinish(PigeonCharacterRace race, PigeonController pigeon)
        => Vector3.Distance(pigeon.transform.position, race.DestinationStop.transform.position);

    // ─────────────────────────────────────────────────────────────────────────
    //  4. Course : un pigeon dépasse MaximumHeight → échec pour tous, message, zone réarmée.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator RaceHeightFailure()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));

        var bots = new List<SpawnedBot>();
        yield return PlaceTwoPigeonsInDepartureZone(race, bots);
        MissionStartZone zone = race.DepartureZone;
        PigeonController other = bots[0].Pigeon;

        Bot.PressInteract();
        bots[0].Controls.PressInteract();
        yield return Expect("Les deux pigeons valident", () => Pigeon.WantsStart && other.WantsStart, 1f,
            () => $"joueur {Pigeon.WantsStart}, bot {other.WantsStart}");
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, zone.CountdownSeconds + 4f,
            () => $"phase course {race.Phase}, zone {zone.Phase}");
        yield return Wait("Laisser la course tourner", 1f);

        Vector3 above = AboveMaximumHeightAwayFromFinish(race);
        yield return Check("Point de test hors de la zone d'arrivée",
            Vector3.Distance(above, race.DestinationStop.transform.position) > race.DestinationStop.Radius,
            $"{Vector3.Distance(above, race.DestinationStop.transform.position):0.0} m de l'arrivée");
        yield return Teleport(other, above, other.transform.rotation, "Le bot passe au-dessus de MaximumHeight");
        yield return Expect("La course échoue pour tout le monde", () => race.Phase == PigeonCharacterRace.RacePhase.Ready, 2f,
            () => $"phase {race.Phase}");
        yield return Expect("La zone de départ est réarmée", () => zone.Phase == MissionStartZone.Waiting, 1f,
            () => $"phase {zone.Phase}");
        yield return Expect("Le message d'échec s'affiche", () => race.FailureMessage != null
            && race.FailureMessage.Group != null && race.FailureMessage.Group.alpha > 0.05f, 2f);
        yield return Check("Personne n'est gelé", !Pigeon.IsMovementFrozen && !other.IsMovementFrozen);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Outils communs aux courses
    // ─────────────────────────────────────────────────────────────────────────
    PigeonCharacterRace FindRace()
    {
        foreach (PigeonCharacterRace race in FindObjectsByType<PigeonCharacterRace>(FindObjectsInactive.Exclude))
            if (race.DepartureZone != null)
                return race;
        return null;
    }

    IEnumerator PlaceTwoPigeonsInDepartureZone(PigeonCharacterRace race, List<SpawnedBot> bots)
    {
        MissionStartZone zone = race.DepartureZone;
        yield return Check("Course au repos", race.Phase == PigeonCharacterRace.RacePhase.Ready
            && zone.Phase == MissionStartZone.Waiting, $"course {race.Phase}, zone {zone.Phase}");

        Vector3 center = zone.transform.position + Vector3.up * 0.5f;
        Vector3 side = zone.transform.right * Mathf.Min(0.8f, zone.Radius * 0.4f);
        Quaternion rotation = Quaternion.Euler(0f, zone.transform.eulerAngles.y, 0f);

        yield return SpawnBot(center + side, rotation, bots);
        yield return Check("Exactement 2 pigeons dans la partie", PigeonController.All.Count == 2,
            $"{PigeonController.All.Count} pigeons");
        PigeonController other = bots[0].Pigeon;

        yield return Teleport(Pigeon, center - side, rotation, "Joueur placé dans la zone de départ");
        yield return Teleport(other, center + side, rotation, "Bot placé dans la zone de départ");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        yield return ExpectLanded(other, 4f, zone.StopSpeedThreshold);
        yield return Expect("Le prompt s'affiche pour le joueur", () => zone.Prompt == null || zone.Prompt.activeInHierarchy, 1f);
        yield return Check("Personne n'est encore prêt", !Pigeon.WantsStart && !other.WantsStart);
    }

    // Remet la course au repos : un pigeon trop haut fait échouer la course (même chemin que le jeu).
    void CleanupRace(PigeonCharacterRace race)
    {
        if (race == null)
            return;
        if (race.DepartureCinematic != null && race.DepartureCinematic.IsPlaying)
            race.DepartureCinematic.CancelCinematic();
        if (race.ArrivalCinematic != null && race.ArrivalCinematic.IsPlaying)
            race.ArrivalCinematic.CancelCinematic();

        if (race.Phase == PigeonCharacterRace.RacePhase.Racing)
        {
            Vector3 above = AboveMaximumHeightAwayFromFinish(race);
            PigeonController pigeon = Pigeon;
            PigeonController.RunInSimulation(_ => pigeon.ForceTeleport(above, pigeon.transform.rotation));
        }
        // Les validations (WantsStart) tombent d'elles-mêmes quand les pigeons quittent la zone au respawn.
    }

    // Au-dessus de MaximumHeight, à la verticale de la zone de départ, et assez loin de l'arrivée
    // (sinon la course est gagnée au lieu d'échouer : l'arrivée se mesure en 3D).
    static Vector3 AboveMaximumHeightAwayFromFinish(PigeonCharacterRace race)
    {
        Vector3 point = race.DepartureZone.transform.position;
        point.y = race.MaximumHeight + 3f;
        if (race.DestinationStop == null)
            return point;

        Vector3 finish = race.DestinationStop.transform.position;
        float minDistance = race.DestinationStop.Radius + 5f;
        Vector3 away = point - finish;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = Vector3.forward;
        while (Vector3.Distance(point, finish) < minDistance)
            point += away.normalized * minDistance;
        return point;
    }

    static float DistanceXZ(PigeonController pigeon, Transform target)
    {
        Vector3 d = pigeon.transform.position - target.position;
        d.y = 0f;
        return d.magnitude;
    }
}
#endif
