#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Scénarios multi : l'hôte (pigeon piloté par Bot) et un VRAI second joueur, l'éditeur clone « Player 2 » de
// Multiplayer Play Mode. Ses actions passent par l'input de son pigeon, envoyé au serveur par le vrai réseau ;
// ce qu'il voit (HUD, cinématiques, gel, sons…) est lu dans l'instantané que renvoie son agent.
// Les téléportations restent faites par l'hôte, dans la simulation serveur, comme pour les bots.
public partial class ScenarioRunner
{
    public const string MultiRaceStartScenario = "Multi : départ de course à 2 joueurs";
    public const string MultiRaceWinScenario = "Multi : victoire, gel et course refaite";
    public const string MultiRaceHeightScenario = "Multi : échec de hauteur";
    public const string MultiLateJoinScenario = "Multi : arrivée en pleine course";
    public const string MultiTutoScenario = "Multi : tuto local au second joueur";
    public const string MultiEquipmentScenario = "Multi : équipement du second joueur";
    public const string MultiAudioScenario = "Multi : sons chez le second joueur";

    // ─────────────────────────────────────────────────────────────────────────
    //  M1. Départ de course : attente, décompte et annulation vus des deux côtés ; cinématique sur chaque écran ;
    //      la course attend la fin de celle du second joueur ; personnage à battre au même endroit chez les deux.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiRaceStart()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return PlaceHostAndRemoteInDepartureZone(race);
        PigeonController remote = RemotePigeon;

        // Le second joueur valide seul : il attend l'hôte, et le voit sur son écran.
        yield return RemoteDo("interact");
        yield return Expect("F du second joueur : il est prêt (vu par l'hôte)", () => remote.WantsStart, 2f, RemoteState);
        yield return ExpectStays("Un seul joueur prêt : pas de départ", () => zone.Phase == MissionStartZone.Waiting && remote.WantsStart,
            1.5f, () => zone.Describe());
        yield return Expect("Le second joueur voit qu'il attend l'autre joueur", () => RemoteHudContains(zone, "1/2 ready"), 2f, RemoteState);
        yield return Check("L'hôte, pas encore prêt, n'a pas de décompte à l'écran", zone.HudText == null, HudState(zone));

        // L'hôte valide : décompte chez les deux, gel, nombre de joueurs.
        Bot.PressInteract();
        yield return Expect("Les deux joueurs prêts : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f, () => zone.Describe());
        yield return Expect("Les deux pigeons sont gelés (hôte)", () => Pigeon.IsMovementFrozen && remote.IsMovementFrozen, 1f);
        yield return Expect("Le second joueur est gelé sur son écran", () => RemoteFlag("local/frozen"), 2f, RemoteState);
        yield return Expect("Décompte à l'écran de l'hôte : 2 joueurs", () => HudContains(zone, "Players: 2/2"), 1f, () => HudState(zone));
        yield return Expect("Décompte à l'écran du second joueur : 2 joueurs", () => RemoteHudContains(zone, "Players: 2/2"), 2f, RemoteState);

        // Le second joueur annule pendant le décompte.
        yield return RemoteDo("interact");
        yield return Expect("Annulation par le second joueur : retour en attente", () => zone.Phase == MissionStartZone.Waiting, 2f,
            () => zone.Describe());
        yield return Expect("Annulation : dégel (hôte)", () => !Pigeon.IsMovementFrozen && !remote.IsMovementFrozen, 1f);
        yield return Expect("Annulation : dégel chez le second joueur", () => RemoteIs("local/frozen", "0")
            && Remote(ZoneKey(zone, "phase")) == MissionStartZone.Waiting.ToString(CultureInfo.InvariantCulture), 2f, RemoteState);
        yield return Check("Annulation : pas de cinématique chez l'hôte", !race.DepartureCinematic.IsPlaying);
        yield return Check("Annulation : pas de cinématique chez le second joueur", RemoteIs(RaceKey(race, "departure"), "0"), RemoteState());

        // Relance : la cinématique ne démarre qu'à la fin du décompte, sur chaque écran.
        yield return RemoteDo("interact");
        yield return Expect("Relance : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f, () => zone.Describe());
        yield return Expect("Fin du décompte : cinématique chez l'hôte", () => race.DepartureCinematic.IsPlaying,
            zone.CountdownSeconds + 3f, () => zone.Describe());
        yield return Expect("Fin du décompte : cinématique chez le second joueur", () => RemoteFlag(RaceKey(race, "departure")), 3f, RemoteState);
        // État simulé (ce qui décide du jeu) : l'affichage du pigeon d'un client chez l'hôte peut basculer une image.
        yield return ExpectStays("Pendant les cinématiques, personne n'est libéré",
            () => zone.SimState.Phase == MissionStartZone.Cinematic && Pigeon.SimState.Frozen && remote.SimState.Frozen, 1f,
            () => $"{zone.Describe()} | vue : zone {zone.Phase}, gelés hôte {Pigeon.IsMovementFrozen}, second {remote.IsMovementFrozen}");

        // L'hôte finit la sienne : la course attend le second joueur.
        yield return PlayThroughDialogCinematic(race.DepartureCinematic, "dialogue de départ (hôte)");
        yield return ExpectStays("Le second joueur n'a pas fini : la course attend",
            () => race.Phase != PigeonCharacterRace.RacePhase.Racing && Pigeon.IsMovementFrozen, 1.5f, () => zone.Describe());
        yield return Expect("L'hôte voit qu'on attend l'autre joueur", () => HudContains(zone, "1/2"), 1f, () => HudState(zone));

        // Le second joueur lit son dialogue jusqu'au bout, en validant comme un joueur : fin signalée par son input.
        yield return RemoteDo("play-dialogs", "on");
        yield return Expect("Le second joueur a fini sa cinématique : course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing,
            40f, () => $"{zone.Describe()} | {RemoteState()}");
        yield return RemoteDo("play-dialogs", "off");
        yield return Expect("Course lancée sur l'écran du second joueur", () => Remote(RaceKey(race, "phase")) == "Racing", 3f, RemoteState);

        if (zone.StartPose != null)
        {
            yield return Check("Hôte téléporté sur la ligne de départ", DistanceXZ(Pigeon, zone.StartPose) <= zone.Radius + 1f,
                $"{DistanceXZ(Pigeon, zone.StartPose):0.0} m");
            yield return Check("Second joueur téléporté sur la ligne de départ", DistanceXZ(remote, zone.StartPose) <= zone.Radius + 1f,
                $"{DistanceXZ(remote, zone.StartPose):0.0} m");
        }
        yield return Expect("Les deux pigeons sont libres (hôte)", () => !Pigeon.IsMovementFrozen && !remote.IsMovementFrozen, 1f);
        yield return Expect("Le second joueur est libre sur son écran", () => RemoteIs("local/frozen", "0"), 2f, RemoteState);
        yield return ExpectCharacterInSync(race, "Le personnage à battre est au même endroit chez les deux joueurs");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  M2. Victoire du second joueur : gel des deux coureurs tant que chacun n'a pas fini la cinématique d'arrivée
    //      sur son écran, achievement chez les deux, puis course refaite.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiRaceWin()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;

        // Les deux repartent sans l'achievement (la course solo a pu le donner à l'hôte) : sinon le test ne prouve rien.
        // La vraie sauvegarde est restaurée à la fin des scénarios.
        if (race.CompletionAchievement != null)
        {
            AchievementManager.Instance?.ResetAll();
            yield return RemoteDo("reset-achievements");
            yield return Expect("Ni l'hôte ni le second joueur n'ont l'achievement de la course",
                () => !IsUnlocked(race.CompletionAchievement) && Remote("ach") != null && !RemoteHas("ach", race.CompletionAchievement.Id), 2f,
                () => $"hôte {IsUnlocked(race.CompletionAchievement)}, second joueur [{Remote("ach")}]");
        }

        yield return StartRaceWithRemote(race);
        PigeonController remote = RemotePigeon;

        Vector3 finish = race.DestinationStop.transform.position;
        yield return Teleport(remote, finish + Vector3.up * 0.5f, remote.transform.rotation, "Le second joueur se pose à l'arrivée");
        yield return Expect("Arrivée du second joueur : cinématique de fin", () => race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic,
            8f, () => $"course {race.Phase}, distance {DistanceToFinish(race, remote):0.0} m, vitesse {remote.Speed:0.00}");
        yield return Expect("Cinématique de fin chez l'hôte", () => race.ArrivalCinematic.IsPlaying, 3f);
        yield return Expect("Cinématique de fin chez le second joueur", () => RemoteFlag(RaceKey(race, "arrival")), 3f, RemoteState);
        yield return Expect("Les deux coureurs sont gelés (hôte)", () => Pigeon.IsMovementFrozen && remote.IsMovementFrozen, 1f);
        yield return Expect("Le second joueur est gelé sur son écran", () => RemoteFlag("local/frozen"), 2f, RemoteState);

        // Les deux essaient de bouger : rien ne doit bouger tant que les cinématiques ne sont pas finies.
        Bot.Flap = true;
        yield return RemoteDo("flap", "1");
        Vector3 remoteAt = remote.transform.position;
        string Frozen() => $"course {race.Phase}, gelés : hôte {Pigeon.IsMovementFrozen}, second {remote.IsMovementFrozen} "
            + $"(déplacé de {Vector3.Distance(remote.transform.position, remoteAt):0.00} m) | {RemoteState()}";
        yield return ExpectStays("Pendant la cinématique de fin, personne ne bouge",
            () => race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic && Pigeon.IsMovementFrozen && remote.IsMovementFrozen
                && RemoteFlag("local/frozen") && Vector3.Distance(remote.transform.position, remoteAt) < 0.2f, 6f, Frozen);
        Bot.Flap = false;
        yield return RemoteDo("flap", "0");

        CancelIfPlaying(race.ArrivalCinematic);
        yield return ExpectStays("L'hôte a fini, le second joueur non : toujours gelés",
            () => race.Phase == PigeonCharacterRace.RacePhase.ArrivalCinematic && Pigeon.IsMovementFrozen && remote.IsMovementFrozen, 1.5f, Frozen);

        // Chez le second joueur, la fin de cette cinématique attend le retour de sa caméra de jeu, qui expire au bout de
        // 10 s (CameraTimeoutSeconds) quand son pigeon a été téléporté jusqu'à l'arrivée : délai large.
        yield return RemoteDo("cancel-dialogs");
        yield return Expect("Tout le monde a fini : la course revient au départ", () => race.Phase == PigeonCharacterRace.RacePhase.Ready
            && zone.Phase == MissionStartZone.Waiting, 15f, () => $"course {race.Phase}, zone {zone.Phase} | {RemoteState()}");
        yield return Expect("Dégel (hôte)", () => !Pigeon.IsMovementFrozen && !remote.IsMovementFrozen, 1f);
        yield return Expect("Dégel et course au repos chez le second joueur", () => RemoteIs("local/frozen", "0")
            && Remote(RaceKey(race, "phase")) == "Ready", 3f, RemoteState);
        if (race.CompletionAchievement != null)
        {
            yield return Check("Achievement de la course chez l'hôte", IsUnlocked(race.CompletionAchievement));
            yield return Expect("Achievement de la course chez le second joueur", () => RemoteHas("ach", race.CompletionAchievement.Id), 3f,
                () => $"achievements du second joueur [{Remote("ach")}]");
        }

        // On refait la course à deux.
        yield return PlaceHostAndRemoteInDepartureZone(race);
        Bot.PressInteract();
        yield return RemoteDo("interact");
        yield return Expect("La course peut être refaite : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f, () => zone.Describe());
        Bot.PressInteract();
        yield return Expect("Annulation du nouveau départ", () => zone.Phase == MissionStartZone.Waiting, 2f, () => zone.Describe());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  M3. Le second joueur dépasse MaximumHeight : échec pour les deux, message sur les deux écrans.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiRaceHeight()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return StartRaceWithRemote(race);
        PigeonController remote = RemotePigeon;
        yield return Wait("Laisser la course tourner", 1f);

        yield return Teleport(remote, AboveMaximumHeightAwayFromFinish(race), remote.transform.rotation, "Le second joueur passe au-dessus de MaximumHeight");
        yield return Expect("La course échoue pour les deux", () => race.Phase == PigeonCharacterRace.RacePhase.Ready, 3f, () => $"course {race.Phase}");
        yield return Expect("La zone de départ est rouverte", () => zone.Phase == MissionStartZone.Waiting, 1f, () => zone.Describe());
        yield return Expect("Message d'échec chez l'hôte", () => race.FailureMessage != null && race.FailureMessage.Group != null
            && race.FailureMessage.Group.alpha > 0.05f, 2f);
        yield return Expect("Message d'échec et course au repos chez le second joueur", () => Remote(RaceKey(race, "phase")) == "Ready"
            && RemoteNumber(RaceKey(race, "failure")) > 0.05f, 3f, () => $"message {Remote(RaceKey(race, "failure"))} | {RemoteState()}");
        yield return Check("Personne n'est gelé (hôte)", !Pigeon.IsMovementFrozen && !remote.IsMovementFrozen);
        yield return Expect("Le second joueur n'est pas gelé", () => RemoteIs("local/frozen", "0"), 2f, RemoteState);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  M4. Le second joueur rejoint une course déjà lancée : il voit le personnage au bon endroit, « Race in
    //      progress » dans la zone de départ, et ne peut ni relancer, ni faire échouer, ni gagner la course.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiLateJoin()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return Check("Course au repos", race.Phase == PigeonCharacterRace.RacePhase.Ready && zone.Phase == MissionStartZone.Waiting,
            $"course {race.Phase}, zone {zone.Phase}");

        // Le second joueur quitte la partie ; l'hôte lance la course seul.
        yield return RemoteDo("disconnect");
        yield return Expect("Le second joueur quitte la partie", () => !HasRemotePlayerPigeon() && Remote("net") == "offline", 10f, RemoteState);
        yield return Check("L'hôte est seul dans la partie", PigeonController.All.Count == 1, $"{PigeonController.All.Count} pigeons");

        Vector3 center = zone.transform.position + Vector3.up * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, zone.transform.eulerAngles.y, 0f);
        yield return Teleport(Pigeon, center, rotation, "L'hôte se pose dans la zone de départ");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        Bot.PressInteract();
        yield return Expect("Seul joueur prêt : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f, () => zone.Describe());
        yield return Expect("Cinématique de départ de l'hôte", () => race.DepartureCinematic.IsPlaying, zone.CountdownSeconds + 3f, () => zone.Describe());
        CancelIfPlaying(race.DepartureCinematic);
        yield return Expect("Course lancée, l'hôte seul", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 4f, () => zone.Describe());

        // Le second joueur revient en pleine course.
        yield return RemoteDo("connect");
        yield return Expect("Le second joueur rejoint la partie en pleine course", () => RemoteReady, 30f, RemoteMissingReason);
        PigeonController late = RemotePigeon;
        yield return Expect("Il voit la course en cours", () => Remote(RaceKey(race, "phase")) == "Racing", 5f, RemoteState);
        yield return ExpectCharacterInSync(race, "Il voit le personnage à battre au même endroit que l'hôte");

        Vector3 side = zone.transform.right * Mathf.Min(0.8f, zone.Radius * 0.4f);
        yield return Teleport(late, center + side, rotation, "Le retardataire se pose dans la zone de départ");
        yield return ExpectLanded(late, 4f, zone.StopSpeedThreshold);
        yield return Expect("Il voit « Race in progress » à l'écran", () => RemoteHudContains(zone, "Race in progress"), 3f, RemoteState);
        yield return Check("Pas de prompt « F » pour lui", RemoteIs(ZoneKey(zone, "prompt"), "0"), RemoteState());
        yield return RemoteDo("interact");
        yield return ExpectStays("Son F ne relance pas la course", () => zone.Phase == MissionStartZone.Started
            && race.Phase == PigeonCharacterRace.RacePhase.Racing && !late.WantsStart, 1.5f, () => $"{zone.Describe()} | {RemoteState()}");
        yield return Check("Le retardataire n'est pas gelé", !late.IsMovementFrozen);

        yield return Teleport(late, AboveMaximumHeightAwayFromFinish(race), rotation, "Le retardataire passe au-dessus de MaximumHeight");
        yield return ExpectStays("Il ne fait pas échouer la course", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 1f,
            () => $"course {race.Phase}");

        Vector3 finish = race.DestinationStop.transform.position;
        yield return Teleport(late, finish + Vector3.up * 0.5f, rotation, "Le retardataire se pose à l'arrivée");
        yield return ExpectLanded(late, 4f, race.DestinationStop.StopSpeedThreshold);
        yield return ExpectStays("Il ne gagne pas la course à la place du coureur", () => race.Phase == PigeonCharacterRace.RacePhase.Racing,
            Mathf.Max(1f, race.DestinationStop.StopDuration + 0.5f), () => $"course {race.Phase}, distance à l'arrivée {DistanceToFinish(race, late):0.0} m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  M5. Le tuto du second joueur est local : cinématique, achievement et récompense chez lui seulement.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiTuto()
    {
        PigeonStopDialogCinematic tuto = null;
        foreach (PigeonStopDialogCinematic candidate in FindObjectsByType<PigeonStopDialogCinematic>(FindObjectsInactive.Exclude))
            if (candidate.StopZone != null)
                tuto = candidate;
        yield return Check("Le tuto est relié à une PigeonStopZone", tuto != null && tuto.Cinematic != null);

        PigeonController remote = RemotePigeon;
        PigeonStopZone zone = tuto.StopZone;
        string key = "tuto/" + tuto.name;
        yield return Check("Le tuto du second joueur n'est pas encore fait", Remote(key + "/consumed") == "0", RemoteState());
        Defer(() => { if (tuto.Cinematic.IsPlaying) tuto.Cinematic.CancelCinematic(); });

        byte hostEquipmentBefore = Pigeon.WornEquipment;
        PlayerControls hostControls = Pigeon.GetComponent<PlayerControls>();
        if (tuto.GivesEquipmentReward)
            yield return Check($"Le second joueur ne porte pas encore la récompense ({tuto.RewardEquipment})",
                (remote.WornEquipment & PigeonEquipment.Bit(tuto.RewardEquipment)) == 0, $"état {Convert.ToString(remote.WornEquipment, 2)}");

        // Le cercle de l'hôte doit rester sourd au pigeon de l'autre joueur. S'il a déjà servi (tuto solo), on le
        // réactive le temps du test pour que la vérification puisse échouer, puis on le remet comme avant.
        bool hostZoneBefore = zone.enabled;
        bool hostZoneFired = false;
        UnityEngine.Events.UnityAction<PigeonController> onHostStopped = _ => hostZoneFired = true;
        zone.OnPlayerStopped.AddListener(onHostStopped);
        zone.enabled = true;
        Defer(() =>
        {
            zone.OnPlayerStopped.RemoveListener(onHostStopped);
            zone.enabled = hostZoneBefore;
        });

        yield return Teleport(remote, zone.transform.position + Vector3.up * 0.5f, zone.transform.rotation, "Le second joueur se pose dans le cercle du tuto");
        yield return ExpectLanded(remote, 4f, zone.StopSpeedThreshold);
        yield return Expect("Sa cinématique du tuto démarre chez lui", () => RemoteFlag(key + "/playing"), 5f, RemoteState);
        yield return Check("Rien chez l'hôte : pas de cinématique", !tuto.Cinematic.IsPlaying && (hostControls == null || !hostControls.AreControlsBlocked));
        yield return Check("Le cercle de l'hôte ne réagit pas au pigeon de l'autre joueur", !hostZoneFired && zone.enabled,
            $"déclenché {hostZoneFired}, zone active {zone.enabled}");
        yield return Expect("Le tuto est consommé pour le second joueur", () => RemoteFlag(key + "/consumed"), 2f, RemoteState);
        if (tuto.CompletionAchievement != null)
            yield return Expect("Achievement du tuto chez le second joueur", () => RemoteHas("ach", tuto.CompletionAchievement.Id), 2f,
                () => $"achievements du second joueur [{Remote("ach")}]");

        yield return RemoteDo("cancel-dialogs");
        yield return Expect("Fin de sa cinématique", () => RemoteIs(key + "/playing", "0"), 4f, RemoteState);
        if (tuto.GivesEquipmentReward)
        {
            byte bit = PigeonEquipment.Bit(tuto.RewardEquipment);
            yield return Expect($"Récompense ({tuto.RewardEquipment}) portée par le second joueur, vue par l'hôte",
                () => (remote.WornEquipment & bit) != 0, 6f, () => $"état {Convert.ToString(remote.WornEquipment, 2)}");
            yield return Expect("… et sur son propre écran", () => (RemoteByte("local/equipment") & bit) != 0, 3f, RemoteState);
            yield return Check("L'hôte n'a rien reçu", Pigeon.WornEquipment == hostEquipmentBefore,
                $"hôte {Convert.ToString(Pigeon.WornEquipment, 2)}, avant {Convert.ToString(hostEquipmentBefore, 2)}");
        }
        yield return Expect("Commandes rendues au second joueur", () => RemoteIs("local/blocked", "0"), 8f, RemoteState);

        // Ressortir puis revenir : le tuto ne se rejoue pas pour lui.
        yield return Teleport(remote, zone.transform.position + zone.transform.forward * (zone.Radius + 4f) + Vector3.up * 2f,
            zone.transform.rotation, "Il ressort du cercle");
        yield return Teleport(remote, zone.transform.position + Vector3.up * 0.5f, zone.transform.rotation, "Il revient dans le cercle");
        yield return ExpectLanded(remote, 4f, zone.StopSpeedThreshold);
        yield return ExpectStays("Le tuto ne se rejoue pas pour lui", () => RemoteIs(key + "/playing", "0"), 2f, RemoteState);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  M6. Le second joueur ramasse un équipement : cinématique chez lui, état répliqué chez l'hôte.
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiEquipment()
    {
        PigeonController remote = RemotePigeon;
        PigeonEquipment remoteEquipment = remote.GetComponent<PigeonEquipment>();
        yield return Check("Le pigeon du second joueur a un PigeonEquipment", remoteEquipment != null);

        // Chaque joueur a ses propres objets à ramasser : on prend un objet encore présent chez lui, et de préférence
        // aussi chez l'hôte, d'un type que l'hôte ne porte pas (sinon « rien ne change chez l'hôte » ne prouverait rien).
        bool found = TryPickRemoteLoot(remote.WornEquipment, out PigeonEquipment.Equipment kind, out Vector3 spot, out bool hostCanDetect);
        yield return Check("Un objet à ramasser existe chez le second joueur", found, $"objets [{Remote("loots")}]");
        if (!hostCanDetect)
            Debug.LogWarning($"[Scénario] {kind} : l'hôte l'a déjà ramassé ou le porte, « rien ne change chez l'hôte » est moins probant.");
        byte bit = PigeonEquipment.Bit(kind);
        byte hostBefore = Pigeon.WornEquipment;
        PlayerControls hostControls = Pigeon.GetComponent<PlayerControls>();
        int lootsBefore = RemoteList("loots").Count;
        int hostLootsBefore = HostLootsAt(kind, spot);

        yield return HoldRemoteStillAt(remote, spot, LootableRadius(kind, spot), 0.15f, $"objet {kind}");
        yield return Expect("Il ramasse l'objet (il disparaît chez lui)", () => RemoteList("loots").Count < lootsBefore, 4f,
            () => $"objets [{Remote("loots")}]");
        yield return RemoteDo("flap", "0");
        yield return RemoteDo("brake", "0");
        yield return Expect("Cinématique d'équipement chez lui (commandes bloquées)", () => RemoteFlag("local/blocked"), 2f, RemoteState);
        yield return Check("Pas de cinématique chez l'hôte", hostControls == null || !hostControls.AreControlsBlocked);
        yield return Expect($"{kind} porté par le second joueur, vu par l'hôte (état répliqué)", () => (remote.WornEquipment & bit) != 0, 6f,
            () => $"état {Convert.ToString(remote.WornEquipment, 2)}");
        yield return Expect($"{kind} visible sur son pigeon chez l'hôte", () => remoteEquipment.IsWorn(kind), 2f);
        yield return Expect("… et sur son propre écran", () => (RemoteByte("local/equipment") & bit) != 0, 3f, RemoteState);
        yield return Check("L'équipement de l'hôte n'a pas changé", Pigeon.WornEquipment == hostBefore,
            $"hôte {Convert.ToString(Pigeon.WornEquipment, 2)}, avant {Convert.ToString(hostBefore, 2)}");
        yield return Check("L'objet de l'hôte est toujours là pour lui", HostLootsAt(kind, spot) == hostLootsBefore,
            $"{HostLootsAt(kind, spot)} objet(s), avant {hostLootsBefore}");
        yield return Expect("Fin de sa cinématique : commandes rendues", () => RemoteIs("local/blocked", "0"), 8f, RemoteState);
    }

    static int HostLootsAt(PigeonEquipment.Equipment kind, Vector3 spot)
    {
        int count = 0;
        foreach (LootableEquipment loot in FindObjectsByType<LootableEquipment>(FindObjectsInactive.Exclude))
            if (loot.Equipment == kind && Vector3.Distance(loot.transform.position, spot) < 0.5f)
                count++;
        return count;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  M7. Sons chez le second joueur : les siens en 2D, ceux de l'hôte en 3D (et l'inverse chez l'hôte).
    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator MultiAudio()
    {
        yield return Check("SFXManager présent", SFXManager.Instance != null);
        PigeonController remote = RemotePigeon;
        // Côte à côte sur un sol connu (le point d'apparition est en l'air) : les deux doivent pouvoir décoller du sol.
        PigeonCharacterRace race = FindRace();
        yield return Check("Un sol connu pour se poser (zone de départ de la course)", race != null);
        Transform ground = race.DepartureZone.transform;
        Quaternion rotation = Quaternion.Euler(0f, ground.eulerAngles.y, 0f);
        Vector3 side = ground.right * Mathf.Min(0.8f, race.DepartureZone.Radius * 0.4f);
        yield return Teleport(Pigeon, ground.position + Vector3.up * 0.5f - side, rotation, "L'hôte se pose");
        yield return Teleport(remote, ground.position + Vector3.up * 0.5f + side, rotation, "Le second joueur se pose à côté");
        yield return ExpectLanded(remote, 4f);
        yield return ExpectLanded(Pigeon, 4f);

        // Les deux décollent : de vrais sons de décollage, déclenchés par la simulation de chaque côté. Seuls les sons
        // ponctuels comptent (les boucles de vol existent dès l'apparition). Chaque liste est prise dès qu'elle
        // apparaît : un son ponctuel disparaît à la fin de son clip.
        List<string> remoteOwn = null, remoteOther = null, hostOwn = null, hostOther = null;
        yield return Check("Aucun son ponctuel avant le décollage", OneShotBlends(Pigeon).Count == 0 && OneShotBlends(remote).Count == 0,
            $"hôte [{string.Join(ScenarioLinkProtocol.ListSeparator, OneShotBlends(Pigeon))}], second [{string.Join(ScenarioLinkProtocol.ListSeparator, OneShotBlends(remote))}]");
        Bot.Flap = true;
        Defer(() => Bot.Flap = false);
        yield return RemoteDo("flap", "1");
        yield return Expect("Les sons de décollage sont joués des deux côtés", () =>
        {
            remoteOwn ??= NonEmpty(RemoteList("audio/local"));
            remoteOther ??= NonEmpty(RemoteList("audio/others"));
            hostOwn ??= NonEmpty(OneShotBlends(Pigeon));
            hostOther ??= NonEmpty(OneShotBlends(remote));
            return remoteOwn != null && remoteOther != null && hostOwn != null && hostOther != null;
        }, 4f, () => $"second joueur : siens {remoteOwn != null}, de l'hôte {remoteOther != null} ; hôte : siens {hostOwn != null}, du second {hostOther != null}");

        yield return Check("Chez le second joueur, ses propres sons sont en 2D", AllBlends(remoteOwn, 0f), string.Join(ScenarioLinkProtocol.ListSeparator, remoteOwn));
        yield return Check("Chez le second joueur, les sons de l'hôte sont en 3D", AllBlends(remoteOther, 1f), string.Join(ScenarioLinkProtocol.ListSeparator, remoteOther));
        yield return Check("Chez l'hôte, ses propres sons sont en 2D", AllBlends(hostOwn, 0f), string.Join(ScenarioLinkProtocol.ListSeparator, hostOwn));
        yield return Check("Chez l'hôte, les sons du second joueur sont en 3D", AllBlends(hostOther, 1f), string.Join(ScenarioLinkProtocol.ListSeparator, hostOther));

        Bot.Flap = false;
        yield return RemoteDo("flap", "0");
    }

    static List<string> NonEmpty(List<string> items) => items.Count > 0 ? items : null;

    // Même format que l'instantané du second joueur : « clip=spatialBlend » des sons ponctuels sous ce pigeon.
    static List<string> OneShotBlends(PigeonController pigeon)
    {
        var items = new List<string>();
        foreach (AudioSource source in pigeon.GetComponentsInChildren<AudioSource>())
            if (!source.loop)
                items.Add($"{(source.clip != null ? source.clip.name : "?")}={ScenarioLinkProtocol.Format(source.spatialBlend)}");
        return items;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Outils des scénarios multi
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Commande au second joueur ; l'étape échoue s'il ne l'exécute pas.</summary>
    IEnumerator RemoteDo(string verb, string argument = "")
    {
        yield return RemoteCommand(verb, argument);
        yield return Check($"Second joueur : {verb}{(argument.Length > 0 ? " " + argument : "")}", _lastRemoteOk, _lastRemoteDetail);
    }

    // Instantané trop vieux (clone figé, lien perdu) : rien n'est cru, dans un sens comme dans l'autre.
    const float RemoteSnapshotMaxAge = 1f;

    string Remote(string key) => _link != null && _link.Connected && _link.LastSnapshotTime >= 0f
        && Time.realtimeSinceStartup - _link.LastSnapshotTime <= RemoteSnapshotMaxAge ? _link.Get(key) : null;

    bool RemoteFlag(string key) => Remote(key) == "1";

    /// <summary>Valeur explicite (et fraîche) : un instantané absent ne fait jamais passer un test « négatif ».</summary>
    bool RemoteIs(string key, string value) => Remote(key) == value;

    float RemoteNumber(string key) => ScenarioLinkProtocol.TryParse(Remote(key), out float value) ? value : float.NaN;

    int RemoteByte(string key) => int.TryParse(Remote(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

    List<string> RemoteList(string key)
    {
        var items = new List<string>();
        string value = Remote(key);
        if (!string.IsNullOrEmpty(value))
            foreach (string item in value.Split(ScenarioLinkProtocol.ListSeparator[0]))
                if (item.Length > 0)
                    items.Add(item);
        return items;
    }

    bool RemoteHas(string key, string item) => RemoteList(key).Contains(item);

    static string ZoneKey(MissionStartZone zone, string field) => $"zone/{zone.name}/{field}";

    static string RaceKey(PigeonCharacterRace race, string field) => $"race/{race.name}/{field}";

    bool RemoteHudContains(MissionStartZone zone, string part)
    {
        string hud = Remote(ZoneKey(zone, "hud"));
        return hud != null && hud.Contains(part);
    }

    // « clip=0|clip=1 » : toutes les sources ont ce spatialBlend.
    static bool AllBlends(List<string> sources, float expected)
    {
        foreach (string source in sources)
        {
            int separator = source.LastIndexOf('=');
            if (separator < 0 || !ScenarioLinkProtocol.TryParse(source.Substring(separator + 1), out float blend) || Mathf.Abs(blend - expected) > 0.001f)
                return false;
        }
        return sources.Count > 0;
    }

    // Objet présent chez le second joueur, d'un type qu'il ne porte pas encore. De préférence un objet encore là chez
    // l'hôte, d'un type que l'hôte ne porte pas (hostCanDetect). Ordre stable d'un lancement à l'autre.
    bool TryPickRemoteLoot(byte worn, out PigeonEquipment.Equipment kind, out Vector3 spot, out bool hostCanDetect)
    {
        kind = default;
        spot = default;
        hostCanDetect = false;
        bool found = false;
        foreach (string item in RemoteList("loots"))
        {
            int at = item.IndexOf('@');
            if (at <= 0 || !Enum.TryParse(item.Substring(0, at), out PigeonEquipment.Equipment candidate)
                || !ScenarioLinkProtocol.TryParse(item.Substring(at + 1), out Vector3 position)
                || (worn & PigeonEquipment.Bit(candidate)) != 0)
                continue;
            bool detectable = HostLootsAt(candidate, position) > 0 && (Pigeon.WornEquipment & PigeonEquipment.Bit(candidate)) == 0;
            bool better = !found
                || (detectable && !hostCanDetect)
                || (detectable == hostCanDetect && (string.CompareOrdinal(candidate.ToString(), kind.ToString()) < 0
                    || (candidate == kind && position.x < spot.x)));
            if (!better)
                continue;
            kind = candidate;
            spot = position;
            hostCanDetect = detectable;
            found = true;
        }
        return found;
    }

    // Rayon de ramassage de l'objet du même type au même endroit chez l'hôte (2 m par défaut s'il l'a déjà ramassé).
    static float LootableRadius(PigeonEquipment.Equipment kind, Vector3 spot)
    {
        foreach (LootableEquipment loot in FindObjectsByType<LootableEquipment>(FindObjectsInactive.Include))
            if (loot.Equipment == kind && Vector3.Distance(loot.transform.position, spot) < 0.5f)
                return loot.Radius;
        return 2f;
    }

    // Comme HoldStillAt, pour le second joueur : posé sur place, ou en vol stationnaire (battements + frein envoyés
    // par son input) s'il n'y a rien dessous.
    IEnumerator HoldRemoteStillAt(PigeonController remote, Vector3 spot, float radius, float stopSpeed, string label)
    {
        yield return RemoteDo("flap", "0");
        yield return RemoteDo("brake", "0");
        float yaw = remote.transform.eulerAngles.y;
        yield return Teleport(remote, spot + Vector3.up * 0.5f, Quaternion.Euler(0f, yaw, 0f), $"Second joueur placé : {label}");

        float deadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < deadline && !remote.IsGrounded)
            yield return null;
        bool groundUnderSpot = remote.IsGrounded && Vector3.Distance(remote.transform.position, spot) <= radius + 0.5f;
        if (groundUnderSpot)
            yield return ExpectLanded(remote, 4f, stopSpeed);
        else
        {
            yield return Teleport(remote, spot, Quaternion.Euler(-45f, yaw, 0f), $"Rien dessous : vol stationnaire du second joueur ({label})");
            yield return RemoteDo("flap", "1");
            yield return RemoteDo("brake", "1");
            yield return ExpectStill(remote, 4f, stopSpeed);
        }
        yield return Check($"Second joueur dans le rayon : {label}", Vector3.Distance(remote.transform.position, spot) <= radius,
            $"distance {Vector3.Distance(remote.transform.position, spot):0.00} / rayon {radius}, {(groundUnderSpot ? "posé" : "en vol stationnaire")}");
    }

    // L'hôte et le second joueur posés côte à côte dans la zone de départ, personne de prêt.
    IEnumerator PlaceHostAndRemoteInDepartureZone(PigeonCharacterRace race)
    {
        MissionStartZone zone = race.DepartureZone;
        yield return Check("Course au repos", race.Phase == PigeonCharacterRace.RacePhase.Ready && zone.Phase == MissionStartZone.Waiting,
            $"course {race.Phase}, zone {zone.Phase}");
        PigeonController remote = RemotePigeon;
        yield return Check("Second joueur en jeu", remote != null, RemoteMissingReason());
        yield return Check("Deux joueurs dans la partie, aucun bot", PigeonController.All.Count == 2, $"{PigeonController.All.Count} pigeons");

        Vector3 center = zone.transform.position + Vector3.up * 0.5f;
        Vector3 side = zone.transform.right * Mathf.Min(0.8f, zone.Radius * 0.4f);
        Quaternion rotation = Quaternion.Euler(0f, zone.transform.eulerAngles.y, 0f);
        yield return Teleport(Pigeon, center - side, rotation, "Hôte placé dans la zone de départ");
        yield return Teleport(remote, center + side, rotation, "Second joueur placé dans la zone de départ");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        yield return ExpectLanded(remote, 4f, zone.StopSpeedThreshold);
        yield return Expect("Le prompt s'affiche chez l'hôte", () => zone.Prompt == null || PromptVisible(zone.Prompt), 1f);
        yield return Expect("Le prompt s'affiche chez le second joueur", () => zone.Prompt == null || RemoteFlag(ZoneKey(zone, "prompt")), 2f, RemoteState);
        yield return Check("Personne n'est encore prêt", !Pigeon.WantsStart && !remote.WantsStart && Remote("local/ready") == "0", RemoteState());
    }

    // Départ à deux le plus court possible : validations, cinématiques interrompues de chaque côté, course lancée.
    IEnumerator StartRaceWithRemote(PigeonCharacterRace race)
    {
        MissionStartZone zone = race.DepartureZone;
        yield return PlaceHostAndRemoteInDepartureZone(race);
        Bot.PressInteract();
        yield return RemoteDo("interact");
        yield return Expect("Les deux joueurs prêts : décompte", () => zone.Phase == MissionStartZone.CountingDown, 2f, () => zone.Describe());
        yield return Expect("Cinématique de départ chez l'hôte", () => race.DepartureCinematic.IsPlaying, zone.CountdownSeconds + 3f, () => zone.Describe());
        yield return Expect("Cinématique de départ chez le second joueur", () => RemoteFlag(RaceKey(race, "departure")), 3f, RemoteState);
        CancelIfPlaying(race.DepartureCinematic);
        yield return RemoteDo("cancel-dialogs");
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 5f, () => $"{zone.Describe()} | {RemoteState()}");
        yield return Expect("Course lancée chez le second joueur", () => Remote(RaceKey(race, "phase")) == "Racing", 3f, RemoteState);
    }

    // Le personnage est placé d'après l'horloge répliquée de la course : il doit être au même endroit chez les deux.
    // Tolérance : son déplacement pendant le retard de l'instantané (latence + 10 Hz), plus une marge.
    IEnumerator ExpectCharacterInSync(PigeonCharacterRace race, string step)
    {
        Vector3 before = race.Character.position;
        yield return Wait("Mesure de la vitesse du personnage", 1f);
        float speed = Vector3.Distance(race.Character.position, before);
        float tolerance = speed * 0.6f + 1.5f;
        float Gap() => ScenarioLinkProtocol.TryParse(Remote(RaceKey(race, "character")), out Vector3 seen)
            ? Vector3.Distance(seen, race.Character.position) : float.PositiveInfinity;
        yield return Check("Le personnage avance chez l'hôte", speed > 0.05f, $"{speed:0.00} m/s");
        yield return Expect(step, () => Gap() <= tolerance, 3f,
            () => $"écart {Gap():0.00} m (tolérance {tolerance:0.00} m, vitesse {speed:0.00} m/s), chez l'hôte {race.Character.position}, "
                + $"chez le second joueur {Remote(RaceKey(race, "character"))}");
    }
}
#endif
