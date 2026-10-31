#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

public partial class ScenarioRunner
{
    IEnumerator PauseKeepsGameRunning()
    {
        PauseMenu menu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        yield return Check("Un PauseMenu existe", menu != null);
        PlayerControls player = Pigeon.GetComponent<PlayerControls>();
        yield return Check("Le pigeon du joueur a des PlayerControls", player != null);
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        Defer(() => menu.SetPaused(false));

        Pigeon.SetControlsOverride(null);
        Vector3 spot = Pigeon.transform.position + Vector3.up * 15f;
        yield return Teleport(Pigeon, spot, Quaternion.Euler(0f, Pigeon.transform.eulerAngles.y, 0f), "Le pigeon est en l'air");

        menu.SetPaused(true);
        yield return Expect("Menu pause ouvert : commandes en pause", () => player.IsPaused, 1f);
        yield return Check("Le temps n'est pas arrêté", Time.timeScale > 0.99f, $"timeScale {Time.timeScale}");
        float clockBefore = cycle != null ? cycle.Clock : 0f;

        yield return ExpectHolds("Le pigeon se met en vol stationnaire", () => Pigeon.IsHovering && !Pigeon.IsGrounded && Pigeon.Speed < 2f,
            1f, 5f, () => $"stationnaire {Pigeon.IsHovering}, au sol {Pigeon.IsGrounded}, vitesse {Pigeon.Speed:0.00}");
        float altitude = Pigeon.transform.position.y;
        yield return ExpectStays("Il reste à la même hauteur pendant la pause", () => Mathf.Abs(Pigeon.transform.position.y - altitude) < 1.5f
            && !Pigeon.IsGrounded, 2f, () => $"hauteur {Pigeon.transform.position.y:0.00} (début {altitude:0.00})");
        if (cycle != null)
            yield return Check("Le cycle jour/nuit avance pendant la pause", Mathf.Repeat(cycle.Clock - clockBefore, cycle.Duration) > 1f,
                $"avant {clockBefore:0.0} s, maintenant {cycle.Clock:0.0} s");

        menu.SetPaused(false);
        yield return Expect("Menu fermé : commandes rendues", () => !player.IsPaused, 2f);
        yield return Expect("Le pigeon quitte le vol stationnaire", () => !Pigeon.IsHovering, 1f);
    }

    IEnumerator MultiPauseAndDayNight()
    {
        PigeonController remote = RemotePigeon;
        PauseMenu menu = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        yield return Check("Un PauseMenu existe", menu != null);
        DayNightCycle cycle = FindAnyObjectByType<DayNightCycle>();
        yield return Check("Un DayNightCycle existe", cycle != null);

        float Gap()
        {
            if (!ScenarioLinkProtocol.TryParse(Remote(RemoteKeys.DayNightClock), out float seen))
                return float.PositiveInfinity;
            float gap = Mathf.Abs(seen - cycle.Clock);
            return Mathf.Min(gap, cycle.Duration - gap);
        }
        yield return Expect("Même heure du jour chez les deux joueurs", () => Gap() < 0.5f, 3f,
            () => $"hôte {cycle.Clock:0.00} s, second joueur {Remote(RemoteKeys.DayNightClock)} s");

        Defer(() => menu.SetPaused(false));
        menu.SetPaused(true);
        yield return Check("Pause de l'hôte : le temps n'est pas arrêté", Time.timeScale > 0.99f, $"timeScale {Time.timeScale}");
        Vector3 air = remote.transform.position + Vector3.up * 10f;
        yield return Teleport(remote, air, Quaternion.Euler(0f, remote.transform.eulerAngles.y, 0f), "Le second joueur est en l'air");
        Vector3 from = remote.transform.position;
        yield return Expect("Pendant la pause de l'hôte, le second joueur continue de bouger", () => Vector3.Distance(remote.transform.position, from) > 2f,
            3f, () => $"déplacement {Vector3.Distance(remote.transform.position, from):0.00} m");
        yield return Expect("L'heure avance toujours, au même rythme chez les deux", () => Gap() < 0.5f, 2f,
            () => $"hôte {cycle.Clock:0.00} s, second joueur {Remote(RemoteKeys.DayNightClock)} s");
        menu.SetPaused(false);

        Defer(() => _link?.Send("pause", "off"));
        yield return Teleport(remote, air, Quaternion.Euler(0f, remote.transform.eulerAngles.y, 0f), "Le second joueur repart en l'air");
        yield return RemoteDo("override", "off");
        yield return RemoteDo("pause", "on");
        yield return Expect("Le second joueur est en pause chez lui", () => RemoteFlag(RemoteKeys.Paused), 2f, RemoteState);
        yield return ExpectHolds("L'hôte le voit en vol stationnaire", () => remote.IsHovering && !remote.IsGrounded && remote.Speed < 2f, 1f, 5f,
            () => $"stationnaire {remote.IsHovering}, au sol {remote.IsGrounded}, vitesse {remote.Speed:0.00}");
        yield return RemoteDo("pause", "off");
        yield return RemoteDo("override", "on");
        yield return Expect("Fin de la pause chez le second joueur", () => RemoteIs(RemoteKeys.Paused, "0"), 2f, RemoteState);
    }
}
#endif
