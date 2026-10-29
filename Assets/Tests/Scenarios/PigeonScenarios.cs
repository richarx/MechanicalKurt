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
    public const string RaceLockedScenario = "Course : une seule à la fois";
    public const string RaceWinScenario = "Course : victoire et course refaite";

    public static readonly string[] ScenarioNames =
    {
        RingScenario, TutoScenario, RaceStartScenario, RaceHeightScenario, RaceLockedScenario, RaceWinScenario,
        DeliveryScenario, CollectionScenario, BillboardScenario, PrayerScenario,
        EquipmentScenario, WaterSpeedScenario,
        BeachBallScenario, MaxDepthScenario, CamerasScenario, SunglassesScenario, SewerRingsScenario, CarRideScenario,
        PigeonAudioScenario,
    };

    List<ScenarioDef> BuildScenarios() => new List<ScenarioDef>
    {
        new ScenarioDef { Name = RingScenario, Body = RingBoost },
        new ScenarioDef { Name = TutoScenario, Body = TutoStopZone },
        new ScenarioDef { Name = RaceStartScenario, Body = RaceStart },
        new ScenarioDef { Name = RaceHeightScenario, Body = RaceHeightFailure },
        new ScenarioDef { Name = RaceLockedScenario, Body = RaceLockedWhileRunning },
        new ScenarioDef { Name = RaceWinScenario, Body = RaceWinAndReplay },
        new ScenarioDef { Name = DeliveryScenario, Body = RunDeliveryMission },
        new ScenarioDef { Name = CollectionScenario, Body = RunCollectionMission },
        new ScenarioDef { Name = BillboardScenario, Body = RunBillboardMission },
        new ScenarioDef { Name = PrayerScenario, Body = RunPrayerMission },
        new ScenarioDef { Name = EquipmentScenario, Body = RunEquipmentPickup },
        new ScenarioDef { Name = WaterSpeedScenario, Body = RunWaterSpeedway },
        new ScenarioDef { Name = BeachBallScenario, Body = RunBeachBall },
        new ScenarioDef { Name = MaxDepthScenario, Body = RunMaxDepth },
        new ScenarioDef { Name = CamerasScenario, Body = RunCameras },
        new ScenarioDef { Name = SunglassesScenario, Body = RunSunglasses },
        new ScenarioDef { Name = SewerRingsScenario, Body = RunSewerRings },
        new ScenarioDef { Name = CarRideScenario, Body = RunCarRide },
        new ScenarioDef { Name = PigeonAudioScenario, Body = RunPigeonAudio },
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
    //  2. Tuto (zone locale) : se poser dans le cercle, sans F → cinématique + achievement + sac à dos,
    //     puis plus jamais de relance pour ce joueur.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator TutoStopZone()
    {
        PigeonStopDialogCinematic tuto = null;
        foreach (PigeonStopDialogCinematic candidate in FindObjectsByType<PigeonStopDialogCinematic>(FindObjectsInactive.Exclude))
            if (candidate.StopZone != null)
                tuto = candidate;
        yield return Check("Le tuto est relié à une PigeonStopZone", tuto != null && tuto.Cinematic != null);

        PigeonStopZone zone = tuto.StopZone;
        AchievementManager achievements = AchievementManager.Instance;
        string ZoneState() => $"zone active {zone.isActiveAndEnabled}, vitesse {Pigeon.Speed:0.00}, au sol {Pigeon.IsGrounded}, "
            + $"distance {Vector3.Distance(Pigeon.transform.position, zone.transform.position):0.00} / rayon {zone.Radius}, "
            + $"dialogue occupé {(DialogManager.instance != null && DialogManager.instance.IsPlaying)}";
        yield return Check("AchievementManager présent", achievements != null);
        yield return Check("L'achievement de fin n'est pas encore débloqué", !achievements.IsUnlocked(tuto.CompletionAchievement));
        Defer(() => { if (tuto.Cinematic.IsPlaying) tuto.Cinematic.CancelCinematic(); });

        yield return Teleport(Pigeon, zone.transform.position + Vector3.up * 0.5f, zone.transform.rotation, "Placement dans le cercle du tuto");
        yield return Expect("Posé dans le cercle, sans F : la cinématique du tuto démarre", () => tuto.Cinematic.IsPlaying, 5f, ZoneState);
        yield return Check("Aucun appui sur F n'a été fait", !Bot.HasPendingInteract && Bot.PredictedInteractConsumed == 0);
        yield return Check("L'achievement de fin est débloqué", achievements.IsUnlocked(tuto.CompletionAchievement));
        yield return Check("Le cylindre du tuto est masqué", tuto.Cylinder == null || !tuto.Cylinder.activeSelf);
        yield return Check("La zone du tuto est consommée pour ce joueur", !zone.enabled);

        tuto.Cinematic.CancelCinematic();
        yield return Expect("Fin de la cinématique", () => !tuto.Cinematic.IsPlaying, 5f);
        if (tuto.GivesEquipmentReward)
        {
            byte rewardBit = PigeonEquipment.Bit(tuto.RewardEquipment);
            yield return Expect($"Récompense de fin de tuto : {tuto.RewardEquipment} porté", () => (Pigeon.WornEquipment & rewardBit) != 0, 4f,
                () => $"état {System.Convert.ToString(Pigeon.WornEquipment, 2)}");
            PlayerControls playerControls = Pigeon.GetComponent<PlayerControls>();
            yield return Expect("Fin de la cinématique d'équipement : commandes rendues",
                () => playerControls == null || !playerControls.AreControlsBlocked, 6f);
        }
        yield return ExpectLanded(Pigeon, 3f, zone.StopSpeedThreshold);
        yield return ExpectStays("Rester dans le cercle ne relance pas le tuto", () => !tuto.Cinematic.IsPlaying, 2f, ZoneState);

        // Ressortir puis revenir : toujours rien, le tuto ne se joue qu'une fois par joueur.
        yield return Teleport(Pigeon, zone.transform.position + zone.transform.forward * (zone.Radius + 4f) + Vector3.up * 2f,
            zone.transform.rotation, "Sortie du cercle");
        yield return Teleport(Pigeon, zone.transform.position + Vector3.up * 0.5f, zone.transform.rotation, "Retour dans le cercle");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        yield return ExpectStays("Revenir dans le cercle ne relance pas le tuto", () => !tuto.Cinematic.IsPlaying, 2f, ZoneState);
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

        yield return Expect("Le joueur prêt voit l'attente des autres", () => HudContains(zone, "1/2"), 1f, () => HudState(zone));

        bots[0].Controls.PressInteract();
        yield return Expect("Deux pigeons prêts : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f,
            () => $"phase {zone.Phase}, prêts : joueur {Pigeon.WantsStart}, bot {other.WantsStart}");
        yield return Expect("Les deux pigeons sont gelés", () => Pigeon.IsMovementFrozen && other.IsMovementFrozen, 1f);
        yield return Expect("Décompte affiché avec le nombre de joueurs", () => HudContains(zone, "Players: 2/2"), 1f, () => HudState(zone));
        yield return ExpectStays("Pendant le décompte, pas de cinématique", () => !race.DepartureCinematic.IsPlaying,
            Mathf.Min(1f, zone.CountdownSeconds * 0.5f), () => HudState(zone));

        bots[0].Controls.PressInteract();
        yield return Expect("Annulation pendant le décompte : retour en attente", () => zone.Phase == MissionStartZone.Waiting, 2f,
            () => $"phase {zone.Phase}");
        yield return Expect("Annulation : les deux pigeons sont dégelés", () => !Pigeon.IsMovementFrozen && !other.IsMovementFrozen, 1f);
        yield return Check("Annulation : pas de cinématique", !race.DepartureCinematic.IsPlaying);

        // Le joueur est resté prêt : le bot revalide.
        bots[0].Controls.PressInteract();
        yield return Expect("Relance : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f,
            () => $"phase {zone.Phase}, prêts : joueur {Pigeon.WantsStart}, bot {other.WantsStart}");
        yield return Expect("Fin du décompte : la cinématique de départ démarre", () => race.DepartureCinematic.IsPlaying,
            zone.CountdownSeconds + 2f, () => $"zone {zone.Phase}, reste {zone.CountdownRemaining:0.0} s");
        yield return Expect("Phase cinématique", () => zone.Phase == MissionStartZone.Cinematic, 1f, zone.Describe);

        bots[0].Controls.PressInteract();
        yield return ExpectStays("Pendant la cinématique, on ne peut plus annuler ; personne n'est libéré",
            () => zone.Phase == MissionStartZone.Cinematic && Pigeon.IsMovementFrozen && other.IsMovementFrozen, 1f,
            () => $"zone {zone.Phase}, gelés : joueur {Pigeon.IsMovementFrozen}, bot {other.IsMovementFrozen}");

        yield return PlayThroughDialogCinematic(race.DepartureCinematic, "dialogue de départ de la course");
        yield return ExpectStays("Le bot n'a pas fini sa cinématique : la course attend", () => race.Phase != PigeonCharacterRace.RacePhase.Racing
            && Pigeon.IsMovementFrozen, 1.5f, () => $"course {race.Phase}, zone {zone.Phase}, {zone.ReadyCount}/{zone.TotalCount}");
        yield return Expect("Le joueur voit qu'on attend l'autre joueur", () => HudContains(zone, "1/2"), 1f, () => HudState(zone));

        zone.ReportCinematicFinished(other);
        yield return Expect("Tout le monde a fini la cinématique : course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing,
            2f, () => $"phase course {race.Phase}, zone {zone.Phase}, {zone.ReadyCount}/{zone.TotalCount}");
        yield return Expect("Le décompte est masqué", () => zone.HudText == null, 1f, () => HudState(zone));

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

    static bool HudContains(MissionStartZone zone, string part) => zone.HudText != null && zone.HudText.Contains(part);
    static string HudState(MissionStartZone zone) => $"zone {zone.Phase}, texte « {zone.HudText?.Replace('\n', ' ') ?? "masqué"} »";

    // Les deux participants finissent leur cinématique (bot signalé à la main), puis la course part.
    IEnumerator FinishDepartureCinematics(PigeonCharacterRace race, PigeonController other)
    {
        MissionStartZone zone = race.DepartureZone;
        yield return Expect("Cinématique de départ", () => race.DepartureCinematic.IsPlaying, zone.CountdownSeconds + 3f,
            () => $"zone {zone.Phase}");
        CancelIfPlaying(race.DepartureCinematic);
        zone.ReportCinematicFinished(other);
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
        yield return FinishDepartureCinematics(race, other);
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 3f,
            () => $"phase course {race.Phase} ; {zone.Describe()}");
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
    //  5. Course en cours : un pigeon arrivé après le départ ne peut pas relancer la course,
    //     ni la faire échouer, ni la gagner ; le personnage à battre avance avec l'horloge de la course.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator RaceLockedWhileRunning()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));

        var bots = new List<SpawnedBot>();
        yield return PlaceTwoPigeonsInDepartureZone(race, bots);
        MissionStartZone zone = race.DepartureZone;
        PigeonController racer = bots[0].Pigeon;

        Bot.PressInteract();
        bots[0].Controls.PressInteract();
        yield return Expect("Les deux pigeons valident", () => Pigeon.WantsStart && racer.WantsStart, 1f,
            () => $"joueur {Pigeon.WantsStart}, bot {racer.WantsStart}");
        yield return FinishDepartureCinematics(race, racer);
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 3f,
            () => $"phase course {race.Phase} ; {zone.Describe()}");
        Vector3 characterAtStart = race.Character.position;

        // Un troisième pigeon arrive pendant la course et se pose dans la zone de départ.
        Vector3 center = zone.transform.position + Vector3.up * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, zone.transform.eulerAngles.y, 0f);
        yield return SpawnBot(center, rotation, bots);
        PigeonController late = bots[1].Pigeon;
        yield return Check("Trois pigeons dans la partie", PigeonController.All.Count == 3, $"{PigeonController.All.Count} pigeons");
        yield return Teleport(late, center, rotation, "Le retardataire se pose dans la zone de départ");
        yield return ExpectLanded(late, 4f, zone.StopSpeedThreshold);
        bots[1].Controls.PressInteract();
        yield return ExpectStays("Pendant la course, la zone de départ ne relance rien",
            () => zone.Phase == MissionStartZone.Started && race.Phase == PigeonCharacterRace.RacePhase.Racing && !late.WantsStart, 1.5f,
            () => $"zone {zone.Phase}, course {race.Phase}, retardataire prêt {late.WantsStart} ; {zone.Describe()}");
        yield return Check("Le retardataire n'est ni gelé ni téléporté", !late.IsMovementFrozen
            && Vector3.Distance(late.transform.position, zone.transform.position) <= zone.Radius);

        yield return Teleport(late, AboveMaximumHeightAwayFromFinish(race), rotation, "Le retardataire passe au-dessus de MaximumHeight");
        yield return ExpectStays("Il ne fait pas échouer la course", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 1f,
            () => $"course {race.Phase}");

        Vector3 finish = race.DestinationStop.transform.position;
        yield return Teleport(late, finish + Vector3.up * 0.5f, rotation, "Le retardataire se pose à l'arrivée");
        yield return ExpectLanded(late, 4f, race.DestinationStop.StopSpeedThreshold);
        yield return ExpectStays("Il ne gagne pas la course à la place des coureurs",
            () => race.Phase == PigeonCharacterRace.RacePhase.Racing, Mathf.Max(1f, race.DestinationStop.StopDuration + 0.5f),
            () => $"course {race.Phase}, distance à l'arrivée {DistanceToFinish(race, late):0.0} m");

        yield return Check("Le personnage à battre avance sur son trajet",
            Vector3.Distance(race.Character.position, characterAtStart) > 0.5f,
            $"{Vector3.Distance(race.Character.position, characterAtStart):0.0} m parcourus");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  6. Course gagnée : les coureurs restent gelés tant que tous n'ont pas fini la cinématique d'arrivée,
    //     puis la course revient au départ et peut être refaite.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator RaceWinAndReplay()
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
        yield return FinishDepartureCinematics(race, other);
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 3f,
            () => $"phase course {race.Phase} ; {zone.Describe()}");

        Vector3 finish = race.DestinationStop.transform.position;
        yield return Teleport(Pigeon, finish + Vector3.up * 0.5f, Pigeon.transform.rotation, "Le joueur se pose à l'arrivée");
        yield return Expect("Arrivée : cinématique de fin", () => race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic
            && race.ArrivalCinematic.IsPlaying, 6f,
            () => $"course {race.Phase}, cinématique {race.ArrivalCinematic.IsPlaying}, distance {DistanceToFinish(race, Pigeon):0.0} m, vitesse {Pigeon.Speed:0.00}");
        string Frozen() => $"course {race.Phase}, cinématique {race.ArrivalCinematic.IsPlaying}, gelés : joueur {Pigeon.IsMovementFrozen}, bot {other.IsMovementFrozen}";
        yield return Expect("Les deux coureurs sont gelés", () => Pigeon.IsMovementFrozen && other.IsMovementFrozen, 1f, Frozen);

        // Plus long que l'ancienne durée fixe (5 s) : le gel tient tant que la cinématique n'est pas finie.
        Bot.PressInteract();
        yield return ExpectStays("Pendant la cinématique de fin, personne ne bouge",
            () => race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic && Pigeon.IsMovementFrozen && other.IsMovementFrozen,
            6f, Frozen);

        CancelIfPlaying(race.ArrivalCinematic);
        yield return ExpectStays("Le joueur a fini, le bot non : toujours gelés",
            () => race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic && Pigeon.IsMovementFrozen, 1f, Frozen);

        race.ReportArrivalCinematicFinished(other);
        yield return Expect("Tout le monde a fini : la course revient au départ", () => race.Phase == PigeonCharacterRace.RacePhase.Ready
            && zone.Phase == MissionStartZone.Waiting, 2f, () => $"course {race.Phase}, zone {zone.Phase}");
        yield return Expect("Les coureurs sont dégelés", () => !Pigeon.IsMovementFrozen && !other.IsMovementFrozen, 1f, Frozen);
        yield return Check("Le marqueur de départ est de retour", race.DepartureMarker == null || race.DepartureMarker.activeInHierarchy);
        if (race.CompletionAchievement != null && AchievementManager.Instance != null)
            yield return Check("L'achievement de la course est débloqué", AchievementManager.Instance.IsUnlocked(race.CompletionAchievement));

        // On refait la course.
        Vector3 center = zone.transform.position + Vector3.up * 0.5f;
        Vector3 side = zone.transform.right * Mathf.Min(0.8f, zone.Radius * 0.4f);
        Quaternion rotation = Quaternion.Euler(0f, zone.transform.eulerAngles.y, 0f);
        yield return Teleport(Pigeon, center - side, rotation, "Retour du joueur dans la zone de départ");
        yield return Teleport(other, center + side, rotation, "Retour du bot dans la zone de départ");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        yield return ExpectLanded(other, 4f, zone.StopSpeedThreshold);
        yield return Expect("Le prompt s'affiche de nouveau", () => zone.Prompt == null || PromptVisible(zone.Prompt), 1f);
        Bot.PressInteract();
        bots[0].Controls.PressInteract();
        yield return Expect("La course peut être refaite : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f,
            () => $"zone {zone.Phase}, prêts : joueur {Pigeon.WantsStart}, bot {other.WantsStart}");

        // Annulation, pour laisser la course au repos au scénario suivant.
        Bot.PressInteract();
        yield return Expect("Annulation du nouveau départ", () => zone.Phase == MissionStartZone.Waiting, 2f, () => $"zone {zone.Phase}");
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
        yield return Expect("Le prompt s'affiche pour le joueur", () => zone.Prompt == null || PromptVisible(zone.Prompt), 1f,
            () => $"prompt actif {zone.Prompt.activeInHierarchy}, alpha {PromptAlpha(zone.Prompt):0.00}");
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
        // Arrêté en pleine cinématique de départ : la libérer pour tous, sinon les pigeons restent gelés jusqu'au délai max.
        if (race.DepartureZone != null && race.DepartureZone.Phase == MissionStartZone.Cinematic)
            foreach (PigeonController pigeon in PigeonController.All)
                race.DepartureZone.ReportCinematicFinished(pigeon);
        if (race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic)
            foreach (PigeonController pigeon in PigeonController.All)
                race.ReportArrivalCinematicFinished(pigeon);

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

    // Le prompt est partagé avec les MissionZone (CanvasGroup en fondu) : actif ne suffit pas, il doit être opaque.
    static float PromptAlpha(GameObject prompt)
        => prompt != null && prompt.TryGetComponent(out CanvasGroup group) ? group.alpha : 1f;

    static bool PromptVisible(GameObject prompt) => prompt != null && prompt.activeInHierarchy && PromptAlpha(prompt) > 0.99f;

    static float DistanceXZ(PigeonController pigeon, Transform target)
    {
        Vector3 d = pigeon.transform.position - target.position;
        d.y = 0f;
        return d.magnitude;
    }
}
#endif
