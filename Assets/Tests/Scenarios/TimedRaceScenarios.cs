#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ScenarioRunner
{
    /// <summary>Course de test, la même sur chaque machine et d'une exécution à l'autre (plus petit nom).</summary>
    public static TimedRace FindTimedRace()
    {
        TimedRace race = null;
        foreach (TimedRace candidate in FindObjectsByType<TimedRace>(FindObjectsInactive.Exclude))
            if (race == null || string.CompareOrdinal(candidate.name, race.name) < 0)
                race = candidate;
        return race;
    }

    /// <summary>Photo de la vue du joueur avec le HUD de course (son canvas passe un instant devant la caméra).</summary>
    static void CaptureWithRaceHud(string label)
    {
        Canvas canvas = FindAnyObjectByType<RaceHud>()?.GetComponent<Canvas>();
        Camera camera = Camera.main;
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;
            Canvas.ForceUpdateCanvases();
        }
        Capture(camera, label);
        if (canvas != null)
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    static string DescribeRace(TimedRace race) =>
        $"phase {race.Phase}, départ {race.StartCount}, zone {race.DepartureZone.Phase}, HUD « {race.HudText?.Replace('\n', ' ')} », résultats « {race.ResultsText?.Replace('\n', ' ')} »";

    /// <summary>Fait traverser l'anneau au pigeon : placé 5 m avant, lancé à travers à 25 m/s.</summary>
    IEnumerator FlyThrough(PigeonController pigeon, RaceCheckpoint checkpoint, string label, string captureLabel = null)
    {
        Vector3 forward = checkpoint.transform.forward;
        yield return Teleport(pigeon, checkpoint.transform.position - forward * (captureLabel != null ? 14f : 5f), Quaternion.LookRotation(forward), label);
        if (captureLabel != null)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            CaptureWithRaceHud(captureLabel);
        }
        PigeonController target = pigeon;
        PigeonController.RunInSimulation(_ => target.SimState.PendingImpulse += forward * 25f);
    }

    /// <summary>Pose le pigeon dans la zone de départ et attend qu'il soit immobile.</summary>
    IEnumerator LandInRaceZone(PigeonController pigeon, TimedRace race, Vector3 offset, string label)
    {
        Transform zone = race.DepartureZone.transform;
        yield return Teleport(pigeon, zone.position + offset + Vector3.up * 0.5f, Quaternion.Euler(0f, zone.eulerAngles.y, 0f), label);
        yield return ExpectLanded(pigeon, 4f, race.DepartureZone.StopSpeedThreshold);
    }

    IEnumerator TimedRaceSolo()
    {
        TimedRace race = FindTimedRace();
        yield return Check("Une course chronométrée existe", race != null && race.Checkpoints.Count >= 3,
            race == null ? "aucune" : $"{race.Checkpoints.Count} anneaux");
        MissionStartZone zone = race.DepartureZone;
        yield return Check("Départ : un seul joueur suffit, sans cinématique", zone.Rule == MissionStartZone.StartRule.AnyPigeon && zone.CinematicTimeout <= 0f);
        yield return Check("Le dernier anneau est l'arrivée (triple)", race.Checkpoints[race.Checkpoints.Count - 1].GetComponentsInChildren<Renderer>(true).Length >= 3,
            race.Checkpoints[race.Checkpoints.Count - 1].name);
        float previousGrace = race.GraceSeconds;
        int previousLaps = race.Laps;
        race.SetGraceForTests(4f);
        race.SetLapsForTests(1);
        Defer(() => { race.SetGraceForTests(previousGrace); race.SetLapsForTests(previousLaps); });
        // Record de session à 1 s, impossible à battre ici ; le record enregistré du joueur ne doit pas bouger.
        float savedRecord = race.SavedBestTime;
        Defer(() => race.SavedBestTime = savedRecord);
        PigeonController.RunInSimulation(_ => race.SetBestTimeForTests(1f));

        var bots = new List<SpawnedBot>();
        yield return SpawnBot(zone.transform.position + Vector3.up * 3f, Quaternion.identity, bots);
        SpawnedBot bot = bots[0];
        yield return LandInRaceZone(Pigeon, race, Vector3.zero, "Le joueur se pose dans le cercle de départ");
        yield return LandInRaceZone(bot.Pigeon, race, zone.transform.right * 1f, "Un bot se pose à côté");
        Popup popup = zone.Prompt.GetComponent<Popup>();
        yield return Expect("L'invite annonce une course", () => zone.Prompt.activeInHierarchy && popup.KeyboardText.Contains("race"), 2f,
            () => $"affichée {zone.Prompt.activeInHierarchy}, texte « {popup.KeyboardText} »");
        Capture(Camera.main, "timedrace_start");

        DeliverySelectionCircle circle = zone.GetComponentInChildren<DeliverySelectionCircle>(true);
        Renderer circleRenderer = circle != null ? circle.GetComponent<Renderer>() : null;
        float restBottom = circleRenderer != null ? circleRenderer.bounds.min.y : 0f;
        float highestBottom = float.MinValue, lowestBottom = float.MaxValue;

        Bot.PressInteract();
        bot.Controls.PressInteract();
        yield return Expect("Décompte puis départ (sans cinématique)", () =>
        {
            if (circleRenderer != null && zone.IsCircleHighlighted && circleRenderer.gameObject.activeInHierarchy)
            {
                highestBottom = Mathf.Max(highestBottom, circleRenderer.bounds.min.y);
                lowestBottom = Mathf.Min(lowestBottom, circleRenderer.bounds.min.y);
            }
            return race.Phase == TimedRace.RacePhase.Racing;
        }, zone.CountdownSeconds + 2f, () => DescribeRace(race));
        if (circleRenderer != null)
            yield return Check("Pendant le décompte, le cercle s'enfonce sans jamais sortir du sol",
                lowestBottom < restBottom - 0.1f && highestBottom <= restBottom + 0.01f,
                $"bas au repos {restBottom:0.00}, puis entre {lowestBottom:0.00} et {highestBottom:0.00}");
        yield return Expect("HUD : chrono, place sur 2 et meilleur temps", () => race.HudText != null && race.HudText.Contains("/ 2") && race.HudText.Contains("Ring 1")
            && race.HudText.Contains("Best 0:01.00"), 1f, () => DescribeRace(race));

        RaceCheckpoint first = race.Checkpoints[0];
        yield return Expect("Seul le prochain anneau est plein, les suivants transparents et de plus en plus", () =>
            first.IsNext && race.Checkpoints[1].IsShown && race.Checkpoints[1].Alpha < 1f
            && race.Checkpoints[2].Alpha < race.Checkpoints[1].Alpha && !race.Checkpoints[race.Checkpoints.Count - 1].IsShown, 1f,
            () => $"opacités {string.Join(", ", ListAlphas(race))}");
        yield return new WaitForSecondsRealtime(0.8f);
        CaptureWithRaceHud("timedrace_racing");
        DeliveryCompassArrow compass = race.Compass;
        yield return Check("La boussole montre le prochain anneau, en rouge", compass != null && compass.CurrentTarget == first.transform
            && compass.DisplayedTopColor.r > 0.8f && compass.DisplayedTopColor.g < 0.4f,
            compass == null ? "pas de boussole" : $"cible {compass.CurrentTarget?.name}, couleur {compass.DisplayedTopColor}");

        // En course, les autres missions ne se voient pas et ne réagissent pas.
        yield return Expect("Les cercles des autres missions sont masqués", () => MissionMarkers.IsHidden, 1f);
        MissionStartZone otherZone = null;
        foreach (MissionStartZone z in FindObjectsByType<MissionStartZone>(FindObjectsSortMode.None))
            if (z != zone)
                otherZone = z;
        if (otherZone != null)
        {
            yield return Teleport(Pigeon, otherZone.transform.position + Vector3.up * 0.5f, Quaternion.identity, "Le coureur passe dans la zone d'une autre mission");
            yield return ExpectLanded(Pigeon, 4f, otherZone.StopSpeedThreshold);
            yield return ExpectStays("Pas d'invite de l'autre mission", () => otherZone.Prompt == null || !otherZone.Prompt.activeInHierarchy
                || (otherZone.Prompt.GetComponent<Popup>()?.KeyboardText ?? "").Contains("race"), 1f);
            Bot.PressInteract();
            yield return ExpectStays("F ne la lance pas", () => !Pigeon.WantsStart && otherZone.Phase == MissionStartZone.StartPhase.Waiting, 1f,
                () => otherZone.Describe());
        }

        for (int i = 0; i < race.Checkpoints.Count; i++)
        {
            RaceCheckpoint checkpoint = race.Checkpoints[i];
            int expected = i + 1;
            yield return FlyThrough(Pigeon, checkpoint, $"Vers {checkpoint.name}", i == 1 ? "timedrace_rings" : null);
            yield return Expect($"{checkpoint.name} passé", () => Pigeon.RaceProgress.Next >= expected, 3f,
                () => $"prochain {Pigeon.RaceProgress.Next}, position {Pigeon.transform.position}, anneau {checkpoint.transform.position}");
            if (i == 0)
            {
                yield return Expect("En tête après le premier anneau, anneau 2 annoncé", () => race.HudText != null && race.HudText.Contains("1st")
                    && race.HudText.Contains("Ring 2"), 1f, () => DescribeRace(race));
                yield return Expect("La boussole passe au suivant", () => compass.CurrentTarget == race.Checkpoints[1].transform, 0.5f,
                    () => compass.CurrentTarget?.name);
            }
        }

        yield return Expect("Arrivé : place et temps, attente des autres", () => race.HudText != null && race.HudText.Contains("Finished")
            && race.HudText.Contains("1st") && race.HudText.Contains("Waiting"), 1f, () => DescribeRace(race));
        yield return Check("La boussole est rendue (plus de cible, couleurs d'origine)", compass.CurrentTarget == null);
        yield return ExpectHolds("Arrivé : il se met en vol stationnaire et s'arrête", () => Pigeon.IsHovering && Pigeon.Speed < 1f, 0.5f, 2f,
            () => $"état {Pigeon.CurrentPigeonState}, vitesse {Pigeon.Speed:0.0}");
        yield return Expect("Après le délai : classement final", () => race.Phase == TimedRace.RacePhase.Results && race.ResultsText != null, race.GraceSeconds + 2f,
            () => DescribeRace(race));
        yield return Check("Classement : le joueur 1er avec son temps, le bot pas arrivé", race.ResultsText.Contains("1st") && race.ResultsText.Contains("You")
            && race.ResultsText.Contains("DNF"), race.ResultsText.Replace('\n', ' '));
        yield return Check("Record pas battu : meilleur temps rappelé, rien d'enregistré", race.ResultsText.Contains("Best time: 0:01.00")
            && !race.ResultsText.Contains("New record") && Mathf.Approximately(race.SavedBestTime, savedRecord),
            $"enregistré {race.SavedBestTime:0.00} (avant {savedRecord:0.00}) | {race.ResultsText.Replace('\n', ' ')}");
        CaptureWithRaceHud("timedrace_results");

        Bot.PressInteract();
        yield return Expect("F : le joueur quitte le classement", () => race.ResultsText == null, 1f, () => DescribeRace(race));
        yield return Expect("Libre : plus en course, missions de nouveau visibles", () => !Pigeon.IsInTimedRace && !MissionMarkers.IsHidden, 1f);
        yield return Check("La course attend encore le bot", race.Phase == TimedRace.RacePhase.Results, DescribeRace(race));
        bot.Controls.PressInteract();
        yield return Expect("Tous partis : la course rouvre", () => race.Phase == TimedRace.RacePhase.Idle
            && zone.Phase == MissionStartZone.StartPhase.Waiting, 1f, () => DescribeRace(race));
    }

    static int RingsPassed(PigeonController pigeon, TimedRace race) =>
        pigeon.RaceProgress.Lap * race.Checkpoints.Count + pigeon.RaceProgress.Next;

    IEnumerator TimedRaceLapsAlone()
    {
        TimedRace race = FindTimedRace();
        yield return Check("Une course chronométrée existe", race != null && race.Checkpoints.Count >= 3);
        int count = race.Checkpoints.Count;
        int previousLaps = race.Laps;
        race.SetLapsForTests(2);
        Defer(() => race.SetLapsForTests(previousLaps));
        float savedRecord = race.SavedBestTime;
        Defer(() => race.SavedBestTime = savedRecord);
        PigeonController.RunInSimulation(_ => race.SetBestTimeForTests(1f));

        yield return LandInRaceZone(Pigeon, race, Vector3.zero, "Le joueur se pose seul dans le cercle de départ");
        Bot.PressInteract();
        yield return Expect("Départ", () => race.Phase == TimedRace.RacePhase.Racing, race.DepartureZone.CountdownSeconds + 2f, () => DescribeRace(race));
        yield return Expect("HUD : tour 1 sur 2, anneau 1", () => race.HudText != null && race.HudText.Contains("Lap 1 / 2") && race.HudText.Contains("Ring 1"), 1f,
            () => DescribeRace(race));
        yield return Check("Seul en course : pas de place affichée", !race.HudText.Contains("1st"), DescribeRace(race));
        CaptureWithRaceHud("timedrace_laps");

        for (int i = 0; i < count; i++)
        {
            int expected = i + 1;
            yield return FlyThrough(Pigeon, race.Checkpoints[i], $"Tour 1 : vers {race.Checkpoints[i].name}");
            yield return Expect($"Tour 1 : {race.Checkpoints[i].name} passé", () => RingsPassed(Pigeon, race) >= expected, 3f,
                () => $"tours {Pigeon.RaceProgress.Lap}, prochain {Pigeon.RaceProgress.Next}");
            if (i == count - 2)
                yield return Expect("Devant le dernier anneau du tour : le premier s'annonce déjà", () => race.Checkpoints[count - 1].IsNext && race.Checkpoints[0].IsShown, 1f,
                    () => $"opacités {string.Join(", ", ListAlphas(race))}");
        }
        yield return Check("Le dernier anneau ne termine pas la course", Pigeon.RaceProgress.Place == 0 && race.Phase == TimedRace.RacePhase.Racing, DescribeRace(race));
        yield return Expect("HUD : tour 2 sur 2, retour à l'anneau 1", () => race.HudText != null && race.HudText.Contains("Lap 2 / 2") && race.HudText.Contains("Ring 1"), 1f,
            () => DescribeRace(race));
        yield return Expect("Le premier anneau redevient le prochain", () => race.Checkpoints[0].IsNext, 1f, () => $"opacités {string.Join(", ", ListAlphas(race))}");

        for (int i = 0; i < count; i++)
        {
            int expected = count + i + 1;
            yield return FlyThrough(Pigeon, race.Checkpoints[i], $"Tour 2 : vers {race.Checkpoints[i].name}");
            yield return Expect($"Tour 2 : {race.Checkpoints[i].name} passé", () => RingsPassed(Pigeon, race) >= expected, 3f,
                () => $"tours {Pigeon.RaceProgress.Lap}, prochain {Pigeon.RaceProgress.Next}");
            if (i == count - 2)
                yield return Expect("Dernier tour : plus rien après l'arrivée", () => race.Checkpoints[count - 1].IsNext && !race.Checkpoints[0].IsShown, 1f,
                    () => $"opacités {string.Join(", ", ListAlphas(race))}");
        }
        yield return Expect("Arrivé au bout des deux tours : classement", () => race.Phase == TimedRace.RacePhase.Results && race.ResultsText != null, 2f, () => DescribeRace(race));
        yield return Check("Classement solo : son temps, sans place", race.ResultsText.Contains("You") && !race.ResultsText.Contains("1st"), race.ResultsText.Replace('\n', ' '));
        CaptureWithRaceHud("timedrace_laps_results");
        Bot.PressInteract();
        yield return Expect("La course rouvre", () => race.Phase == TimedRace.RacePhase.Idle, 2f, () => DescribeRace(race));
    }

    IEnumerator MultiTimedRaceLaps()
    {
        TimedRace race = FindTimedRace();
        yield return Check("Une course chronométrée existe", race != null);
        PigeonController remote = RemotePigeon;
        int count = race.Checkpoints.Count;
        int previousLaps = race.Laps;
        float previousGrace = race.GraceSeconds;
        string previous = $"{previousLaps};{previousGrace.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        race.SetLapsForTests(2);
        race.SetGraceForTests(3f);
        Defer(() => { race.SetLapsForTests(previousLaps); race.SetGraceForTests(previousGrace); StartCoroutine(RemoteDo("racesetup", previous)); });
        yield return RemoteDo("racesetup", "2;3");
        float savedRecord = race.SavedBestTime;
        Defer(() => race.SavedBestTime = savedRecord);
        PigeonController.RunInSimulation(_ => race.SetBestTimeForTests(1f));

        yield return LandInRaceZone(Pigeon, race, Vector3.zero, "L'hôte se pose dans le cercle");
        yield return LandInRaceZone(remote, race, race.DepartureZone.transform.right * 1f, "Le second joueur aussi");
        Bot.PressInteract();
        yield return RemoteDo("interact");
        yield return Expect("Départ des deux joueurs", () => race.Phase == TimedRace.RacePhase.Racing && remote.RaceProgress.IsIn(race.id, race.StartCount),
            race.DepartureZone.CountdownSeconds + 3f, () => DescribeRace(race));
        yield return Expect("Le second joueur voit le tour 1 sur 2 et sa place", () => (Remote(RemoteKeys.TimedRaceHud) ?? "").Contains("Lap 1 / 2")
            && (Remote(RemoteKeys.TimedRaceHud) ?? "").Contains("/ 2</size>"), 3f, RemoteState);

        for (int i = 0; i < count; i++)
        {
            int expected = i + 1;
            yield return FlyThrough(remote, race.Checkpoints[i], $"Second joueur, tour 1 : vers {race.Checkpoints[i].name}");
            yield return Expect($"Tour 1 : {race.Checkpoints[i].name} passé", () => RingsPassed(remote, race) >= expected, 3f,
                () => $"tours {remote.RaceProgress.Lap}, prochain {remote.RaceProgress.Next}");
        }
        yield return Expect("Il voit le tour 2 sur son écran", () => (Remote(RemoteKeys.TimedRaceHud) ?? "").Contains("Lap 2 / 2"), 3f, RemoteState);
        yield return Expect("L'hôte le voit devant lui : 2e", () => race.HudText != null && race.HudText.Contains("2nd") && race.HudText.Contains("Lap 1 / 2"), 2f, () => DescribeRace(race));

        for (int i = 0; i < count; i++)
        {
            int expected = count + i + 1;
            yield return FlyThrough(remote, race.Checkpoints[i], $"Second joueur, tour 2 : vers {race.Checkpoints[i].name}");
            yield return Expect($"Tour 2 : {race.Checkpoints[i].name} passé", () => RingsPassed(remote, race) >= expected, 3f,
                () => $"tours {remote.RaceProgress.Lap}, prochain {remote.RaceProgress.Next}");
        }
        yield return Expect("Second joueur arrivé, sur son écran", () => (Remote(RemoteKeys.TimedRaceHud) ?? "").Contains("Finished")
            || (Remote(RemoteKeys.TimedRaceResults) ?? "").Contains("1st"), 3f, RemoteState);
        yield return Expect("Après le délai : classement, l'hôte pas arrivé", () => race.Phase == TimedRace.RacePhase.Results && race.ResultsText != null
            && race.ResultsText.Contains("DNF"), race.GraceSeconds + 3f, () => DescribeRace(race));
        Bot.PressInteract();
        yield return RemoteDo("interact");
        yield return Expect("La course rouvre", () => race.Phase == TimedRace.RacePhase.Idle, 3f, () => DescribeRace(race));
    }

    static IEnumerable<string> ListAlphas(TimedRace race)
    {
        foreach (RaceCheckpoint checkpoint in race.Checkpoints)
            yield return checkpoint.IsShown ? checkpoint.Alpha.ToString("0.00") : "-";
    }

    IEnumerator MultiTimedRace()
    {
        TimedRace race = FindTimedRace();
        yield return Check("Une course chronométrée existe", race != null);
        MissionStartZone zone = race.DepartureZone;
        PigeonController remote = RemotePigeon;
        int previousLaps = race.Laps;
        string previousSetup = $"{previousLaps};{race.GraceSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        string oneLap = $"1;{race.GraceSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        race.SetLapsForTests(1);
        Defer(() => { race.SetLapsForTests(previousLaps); StartCoroutine(RemoteDo("racesetup", previousSetup)); });
        yield return RemoteDo("racesetup", oneLap);
        // Pas de record au départ : le premier arrivé en établit un, gardé par l'hôte (restauré après le test).
        float savedRecord = race.SavedBestTime;
        Defer(() => race.SavedBestTime = savedRecord);
        race.SavedBestTime = 0f;
        PigeonController.RunInSimulation(_ => race.SetBestTimeForTests(0f));
        yield return LandInRaceZone(Pigeon, race, Vector3.zero, "L'hôte se pose dans le cercle");
        yield return LandInRaceZone(remote, race, zone.transform.right * 1f, "Le second joueur aussi");

        Bot.PressInteract();
        yield return RemoteDo("interact");
        yield return Expect("Départ des deux joueurs", () => race.Phase == TimedRace.RacePhase.Racing
            && Pigeon.RaceProgress.IsIn(race.id, race.StartCount) && remote.RaceProgress.IsIn(race.id, race.StartCount), zone.CountdownSeconds + 3f,
            () => DescribeRace(race));
        yield return Expect("Le second joueur voit son chrono et sa place sur 2", () => (Remote(RemoteKeys.TimedRaceHud) ?? "").Contains("/ 2"), 3f, RemoteState);

        for (int i = 0; i < race.Checkpoints.Count; i++)
        {
            int expected = i + 1;
            yield return FlyThrough(remote, race.Checkpoints[i], $"Le second joueur vers {race.Checkpoints[i].name}");
            yield return Expect($"Second joueur : {race.Checkpoints[i].name} passé", () => remote.RaceProgress.Next >= expected, 3f,
                () => $"prochain {remote.RaceProgress.Next}");
        }
        yield return Expect("Second joueur arrivé 1er, sur son écran", () => (Remote(RemoteKeys.TimedRaceHud) ?? "").Contains("Finished"), 3f, RemoteState);
        yield return Expect("L'hôte voit le temps restant", () => race.HudText != null && race.HudText.Contains("left"), 2f, () => DescribeRace(race));

        for (int i = 0; i < race.Checkpoints.Count; i++)
        {
            int expected = i + 1;
            yield return FlyThrough(Pigeon, race.Checkpoints[i], $"L'hôte vers {race.Checkpoints[i].name}");
            yield return Expect($"Hôte : {race.Checkpoints[i].name} passé", () => Pigeon.RaceProgress.Next >= expected, 2f,
                () => $"prochain {Pigeon.RaceProgress.Next}");
        }
        yield return Expect("Tous arrivés : classement tout de suite", () => race.Phase == TimedRace.RacePhase.Results, 2f, () => DescribeRace(race));
        yield return Expect("Classement de l'hôte : 2e, le second joueur 1er", () => race.ResultsText != null && race.ResultsText.Contains("2nd")
            && race.ResultsText.IndexOf("Player", System.StringComparison.Ordinal) < race.ResultsText.IndexOf("You", System.StringComparison.Ordinal),
            0.5f, () => race.ResultsText?.Replace('\n', ' '));
        CaptureWithRaceHud("timedrace_multi_results");
        yield return Expect("Le second joueur voit le même classement", () => (Remote(RemoteKeys.TimedRaceResults) ?? "").Contains("1st"), 3f, RemoteState);
        string record = TimedRace.FormatTime(race.BestTime);
        yield return Check("Nouveau record : le temps du premier, affiché chez l'hôte", race.BestTime > 0f && race.ResultsText.Contains($"New record! {record}"),
            $"record {record} | {race.ResultsText.Replace('\n', ' ')}");
        yield return Expect("… et chez le second joueur", () => (Remote(RemoteKeys.TimedRaceResults) ?? "").Contains($"New record! {record}"), 3f, RemoteState);
        yield return Check("L'hôte l'a enregistré pour les prochaines parties", Mathf.Approximately(race.SavedBestTime, race.BestTime),
            $"enregistré {race.SavedBestTime:0.00}, record {race.BestTime:0.00}");

        Bot.PressInteract();
        yield return Expect("L'hôte quitte le classement", () => race.ResultsText == null, 1f, () => DescribeRace(race));
        yield return ExpectStays("… le second joueur le voit toujours", () => !string.IsNullOrEmpty(Remote(RemoteKeys.TimedRaceResults)), 1f, RemoteState);
        yield return RemoteDo("interact");
        yield return Expect("Les deux partis : la course rouvre", () => race.Phase == TimedRace.RacePhase.Idle, 3f, () => DescribeRace(race));
        yield return Expect("… et le classement a disparu chez le second joueur", () => string.IsNullOrEmpty(Remote(RemoteKeys.TimedRaceResults)), 3f, RemoteState);
    }
}
#endif
