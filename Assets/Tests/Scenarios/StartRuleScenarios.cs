#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ScenarioRunner
{
    IEnumerator RaceAnyPigeon()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return UseStartRule(zone, MissionStartZone.StartRule.AnyPigeon, withRemote: false);

        var bots = new List<SpawnedBot>();
        yield return PlaceTwoPigeonsInDepartureZone(race, bots);
        PigeonController other = bots[0].Pigeon;

        Bot.PressInteract();
        yield return Expect("F du joueur seul : décompte", () => zone.Phase == MissionStartZone.StartPhase.CountingDown, 2f, zone.Describe);
        yield return Expect("Le joueur est gelé", () => Pigeon.IsMovementFrozen, 1f);
        yield return ExpectStays("Le bot, pas prêt, reste libre", () => !other.IsMovementFrozen, 1f, zone.Describe);
        yield return Expect("Cinématique de départ du joueur", () => race.DepartureCinematic.IsPlaying, zone.CountdownSeconds + 3f, zone.Describe);
        CancelIfPlaying(race.DepartureCinematic);
        yield return Expect("Course lancée sans attendre le bot", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 4f, zone.Describe);
        yield return Check("Le joueur court, pas le bot", zone.IsInCurrentStart(Pigeon) && !zone.IsInCurrentStart(other), zone.Describe());
        yield return Expect("Le joueur est libre de voler", () => !Pigeon.IsMovementFrozen, 1f);
        yield return Expect("La zone affiche le départ lancé", () => zone.Phase == MissionStartZone.StartPhase.Started, 1f, zone.Describe);

        bots[0].Controls.PressInteract();
        yield return ExpectStays("Le F du bot ne relance rien", () => race.Phase == PigeonCharacterRace.RacePhase.Racing
            && zone.Phase == MissionStartZone.StartPhase.Started && !other.WantsStart, 1.5f, zone.Describe);
    }

    IEnumerator RagdollIgnoredByZones()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return Check("Zone de départ en attente", zone.Phase == MissionStartZone.StartPhase.Waiting, zone.Describe());

        Vector3 center = zone.transform.position + Vector3.up * 0.5f;
        Quaternion rotation = Quaternion.Euler(0f, zone.transform.eulerAngles.y, 0f);
        yield return TeleportStunned(Pigeon, center, rotation, "Le pigeon tombe en ragdoll dans la zone de départ");
        yield return ExpectHolds("Ragdoll immobile dans la zone", () => Pigeon.IsStun && Pigeon.Speed <= zone.StopSpeedThreshold, 0.4f, 5f,
            () => $"ragdoll {Pigeon.IsStun}, vitesse {Pigeon.Speed:0.00}");
        yield return Check("Pas de prompt « F » en ragdoll", zone.Prompt == null || !PromptVisible(zone.Prompt));
        Bot.PressInteract();
        yield return ExpectStays("F en ragdoll : le pigeon n'est pas prêt", () => !Pigeon.WantsStart
            && zone.Phase == MissionStartZone.StartPhase.Waiting && Pigeon.IsStun, 1.5f, zone.Describe);

        yield return Teleport(Pigeon, center, rotation, "Le pigeon se repose dans la zone, debout");
        yield return ExpectLanded(Pigeon, 4f, zone.StopSpeedThreshold);
        yield return Expect("Debout : le prompt revient", () => zone.Prompt == null || PromptVisible(zone.Prompt), 1f);
        Bot.PressInteract();
        yield return Expect("Debout : F le rend prêt", () => Pigeon.WantsStart, 1f, zone.Describe);
        Bot.PressInteract();
        yield return Expect("F à nouveau : il annule", () => !Pigeon.WantsStart && zone.Phase == MissionStartZone.StartPhase.Waiting, 2f,
            zone.Describe);

        var stopObject = new GameObject("[Scenario] Stop zone");
        stopObject.transform.position = zone.transform.position;
        PigeonStopZone stop = stopObject.AddComponent<PigeonStopZone>();
        Defer(() => Destroy(stopObject));
        int stops = 0;
        stop.OnPlayerStopped.AddListener(_ => stops++);

        yield return TeleportStunned(Pigeon, center + Vector3.up * 2f, rotation, "Le pigeon tombe en ragdoll dans une PigeonStopZone");
        yield return ExpectHolds("Ragdoll immobile dans la zone d'arrêt", () => Pigeon.IsStun && Pigeon.Speed <= stop.StopSpeedThreshold, 0.4f, 5f,
            () => $"ragdoll {Pigeon.IsStun}, vitesse {Pigeon.Speed:0.00}");
        yield return ExpectStays("En ragdoll : la zone d'arrêt ne se déclenche pas", () => stops == 0 && Pigeon.IsStun, 1.5f,
            () => $"déclenchements {stops}");

        yield return Teleport(Pigeon, center, rotation, "Le pigeon se repose debout dans la zone d'arrêt");
        yield return ExpectLanded(Pigeon, 4f, stop.StopSpeedThreshold);
        yield return Expect("Debout : la zone d'arrêt se déclenche", () => stops >= 1, 2f, () => $"déclenchements {stops}");
    }

    IEnumerator MultiAnyStartedByRemote()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return UseStartRule(zone, MissionStartZone.StartRule.AnyPigeon, withRemote: true);
        yield return PlaceHostAndRemoteInDepartureZone(race);
        PigeonController remote = RemotePigeon;

        yield return RemoteDo("interact");
        yield return Expect("F du second joueur seul : décompte", () => zone.Phase == MissionStartZone.StartPhase.CountingDown, 2f,
            () => zone.Describe());
        yield return Expect("Le second joueur est gelé, l'hôte non", () => remote.IsMovementFrozen && !Pigeon.IsMovementFrozen, 1f,
            () => zone.Describe());
        yield return Expect("Le second joueur est gelé sur son écran", () => RemoteFlag(RemoteKeys.Frozen), 2f, RemoteState);
        yield return Expect("Fin du décompte : cinématique chez le second joueur", () => RemoteFlag(RemoteKeys.RaceDeparture(race)),
            zone.CountdownSeconds + 3f, () => $"{zone.Describe()} | {RemoteState()}");
        yield return Check("Pas de cinématique chez l'hôte", !race.DepartureCinematic.IsPlaying);
        yield return Expect("L'hôte voit « Race in progress »", () => HudContains(zone, "Race in progress"), 2f, () => HudState(zone));

        yield return RemoteDo("cancel-dialogs");
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 5f, () => $"{zone.Describe()} | {RemoteState()}");
        yield return Expect("Course lancée chez le second joueur", () => Remote(RemoteKeys.RacePhase(race)) == "Racing", 3f, RemoteState);
        yield return Check("Le second joueur court, pas l'hôte", zone.IsInCurrentStart(remote) && !zone.IsInCurrentStart(Pigeon), zone.Describe());
        yield return Expect("Le second joueur est libre sur son écran", () => RemoteIs(RemoteKeys.Frozen, "0"), 2f, RemoteState);
    }

    IEnumerator MultiAnyStartedByHost()
    {
        PigeonCharacterRace race = FindRace();
        yield return Check("Une course avec MissionStartZone existe", race != null);
        Defer(() => CleanupRace(race));
        MissionStartZone zone = race.DepartureZone;
        yield return UseStartRule(zone, MissionStartZone.StartRule.AnyPigeon, withRemote: true);
        yield return PlaceHostAndRemoteInDepartureZone(race);
        PigeonController remote = RemotePigeon;

        Bot.PressInteract();
        yield return Expect("F de l'hôte seul : décompte", () => zone.Phase == MissionStartZone.StartPhase.CountingDown, 2f, () => zone.Describe());
        yield return Expect("L'hôte est gelé, le second joueur non", () => Pigeon.IsMovementFrozen && !remote.IsMovementFrozen, 1f,
            () => zone.Describe());
        yield return Expect("Le décompte s'affiche chez le second joueur aussi", () => Remote(RemoteKeys.ZonePhase(zone)) == "CountingDown", 2f,
            RemoteState);
        yield return Check("Le cercle de départ reste affiché chez l'hôte pendant le décompte", MarkerShown(race));
        yield return Check("… et chez le second joueur, qui ne participe pas",
            race.DepartureMarker == null || RemoteFlag(RemoteKeys.RaceMarker(race)), RemoteState());
        yield return Expect("Cinématique de départ chez l'hôte", () => race.DepartureCinematic.IsPlaying, zone.CountdownSeconds + 3f,
            () => zone.Describe());
        yield return ExpectStays("Pas de cinématique chez le second joueur", () => RemoteIs(RemoteKeys.RaceDeparture(race), "0"), 1f, RemoteState);
        yield return Expect("Fin du décompte : le cercle disparaît aussi chez le second joueur",
            () => race.DepartureMarker == null || RemoteIs(RemoteKeys.RaceMarker(race), "0"), 2f, RemoteState);
        yield return Expect("Le second joueur voit « Race in progress »", () => RemoteHudContains(zone, "Race in progress"), 3f, RemoteState);

        CancelIfPlaying(race.DepartureCinematic);
        yield return Expect("Course lancée", () => race.Phase == PigeonCharacterRace.RacePhase.Racing, 5f, () => zone.Describe());
        yield return Check("L'hôte court, pas le second joueur", zone.IsInCurrentStart(Pigeon) && !zone.IsInCurrentStart(remote), zone.Describe());
        yield return Check("Le second joueur n'est pas gelé", !remote.IsMovementFrozen && RemoteIs(RemoteKeys.Frozen, "0"), RemoteState());
    }

    /// <summary>Change la règle de départ le temps du scénario, chez l'hôte et (multi) chez le second joueur.</summary>
    IEnumerator UseStartRule(MissionStartZone zone, MissionStartZone.StartRule rule, bool withRemote)
    {
        MissionStartZone.StartRule previous = zone.Rule;
        zone.SetRuleForTests(rule);
        Defer(() => zone.SetRuleForTests(previous));
        if (!withRemote)
            yield break;
        Defer(() => _link?.Send("start-rule", previous.ToString()));
        yield return RemoteDo("start-rule", rule.ToString());
    }

    /// <summary>Téléporte un pigeon et le met en ragdoll ; sans input, il y reste.</summary>
    IEnumerator TeleportStunned(PigeonController pigeon, Vector3 position, Quaternion rotation, string label)
    {
        PigeonController target = pigeon;
        PigeonController.RunInSimulation(_ =>
        {
            target.ForceTeleport(position, rotation);
            ref PigeonState s = ref target.SimState;
            s.Stunned = true;
            s.StunRemaining = 0.5f;
            s.StunRotation = Quaternion.identity;
        });
        yield return Expect(label, () => target.IsStun && (target.transform.position - position).sqrMagnitude < 9f, 2f,
            () => $"ragdoll {target.IsStun}, position {target.transform.position}, attendue {position}");
    }
}
#endif
