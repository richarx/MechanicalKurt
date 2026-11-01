#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public partial class ScenarioRunner
{
    /// <summary>« heure|distance de chaque voiture », dans l'ordre du trafic synchronisé (aussi envoyé par le second joueur).</summary>
    public static string DescribeTraffic(TrafficNetwork traffic)
    {
        var parts = new List<string> { traffic.currentState.Clock.ToString("0.000", CultureInfo.InvariantCulture) };
        foreach (AmbientCar car in traffic.Cars)
            parts.Add(car != null ? car.Distance.ToString("0.000", CultureInfo.InvariantCulture) : "?");
        return string.Join(ScenarioLinkProtocol.ListSeparator, parts);
    }

    static long TrafficTick(string description, out string[] distances)
    {
        string[] parts = description.Split(ScenarioLinkProtocol.ListSeparator[0]);
        distances = parts;
        return (long)Math.Round(double.Parse(parts[0], CultureInfo.InvariantCulture) * 1000d);
    }

    /// <summary>Les voitures avancent au rythme des ticks (20 par seconde) : leur affichage doit bouger à chaque image.</summary>
    IEnumerator TrafficIsSmooth()
    {
        TrafficNetwork traffic = FindAnyObjectByType<TrafficNetwork>();
        yield return Check("Le trafic est synchronisé", traffic != null && TrafficNetwork.IsRunning);
        AmbientCar car = null;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (car == null && Time.realtimeSinceStartup < deadline)
        {
            foreach (AmbientCar candidate in traffic.Cars)
                if (candidate != null && candidate.CurrentSpeed > 4f)
                    car = candidate;
            yield return null;
        }
        yield return Check("Une voiture roule", car != null);
        MovingSupport support = car.GetComponent<MovingSupport>();
        Renderer body = car.GetComponentInChildren<Renderer>();
        yield return Check("Son affichage est séparé de ses colliders", support.ViewTransform != car.transform
            && body.transform.IsChildOf(support.ViewTransform), $"vue « {support.ViewTransform.name} », rendu sous « {body.transform.parent.name} »");

        int frames = 0, moved = 0, rootMoves = 0;
        Vector3 last = body.transform.position, lastRoot = car.transform.position;
        float maxGap = 0f;
        float end = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < end && car.CurrentSpeed > 2f)
        {
            yield return null;
            frames++;
            if ((body.transform.position - last).sqrMagnitude > 1e-8f) moved++;
            if ((car.transform.position - lastRoot).sqrMagnitude > 1e-8f) rootMoves++;
            maxGap = Mathf.Max(maxGap, Vector3.Distance(support.ViewTransform.position, car.transform.position));
            last = body.transform.position;
            lastRoot = car.transform.position;
        }
        yield return Check("L'affichage bouge à chaque image, pas seulement aux ticks", frames >= 10 && moved >= frames * 0.9f,
            $"{moved}/{frames} images avec mouvement (colliders : {rootMoves}/{frames})");
        yield return Check("Il reste collé à la voiture simulée", maxGap < 1f, $"écart max {maxGap:0.00} m");
    }

    IEnumerator MultiTrafficShared()
    {
        TrafficNetwork traffic = FindAnyObjectByType<TrafficNetwork>();
        yield return Check("Le trafic est synchronisé", traffic != null && TrafficNetwork.IsRunning && traffic.Cars.Count > 0,
            traffic == null ? "pas de TrafficNetwork" : $"{traffic.Cars.Count} voitures");
        int carLayer = LayerMask.NameToLayer("PigeonOnly"), pigeonLayer = LayerMask.NameToLayer("Player");
        yield return Check("À deux joueurs, les voitures restent solides chez l'hôte", !TrafficCollisionGate.CarsAreGhosts
            && !Physics.GetIgnoreLayerCollision(carLayer, pigeonLayer));
        yield return Expect("… et chez le second joueur", () => RemoteIs(RemoteKeys.CarsGhosts, "0"), 2f, RemoteState);

        // Les deux machines n'ont pas la même heure au même instant : on compare leurs voitures à la même heure du trafic.
        var hostByTick = new Dictionary<long, string[]>();
        var pending = new List<string>();
        string lastRemote = null;
        int compared = 0;
        float worst = 0f;
        string worstText = "";
        float end = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < end)
        {
            hostByTick[TrafficTick(DescribeTraffic(traffic), out string[] host)] = host;
            string remote = Remote(RemoteKeys.TrafficCars);
            if (!string.IsNullOrEmpty(remote) && remote != lastRemote)
            {
                lastRemote = remote;
                pending.Add(remote);
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (!hostByTick.TryGetValue(TrafficTick(pending[i], out string[] client), out string[] same))
                    continue;
                pending.RemoveAt(i);
                compared++;
                for (int c = 1; c < client.Length && c < same.Length; c++)
                {
                    float gap = Mathf.Abs(float.Parse(client[c], CultureInfo.InvariantCulture) - float.Parse(same[c], CultureInfo.InvariantCulture));
                    if (gap > worst) { worst = gap; worstText = $"voiture {c - 1} à {client[0]} s : second joueur {client[c]}, hôte {same[c]}"; }
                }
            }
            yield return null;
        }
        yield return Check("Mêmes voitures aux mêmes endroits chez les deux joueurs", compared >= 5 && worst < 0.01f,
            $"{compared} instants comparés, plus grand écart {worst:0.000} m {worstText} | en attente {pending.Count}");

        AmbientCar car = null;
        foreach (AmbientCar candidate in traffic.Cars)
            if (candidate != null && candidate.CurrentSpeed > 3f && candidate.TryGetComponent(out MovingSupport _))
            {
                car = candidate;
                break;
            }
        yield return Check("Une voiture roule", car != null);
        MovingSupport support = car.GetComponent<MovingSupport>();
        Bounds roof = default;
        bool any = false;
        foreach (Collider c in car.GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!any) { roof = c.bounds; any = true; } else roof.Encapsulate(c.bounds);
        }

        PigeonController remotePigeon = RemotePigeon;
        yield return Teleport(remotePigeon, new Vector3(roof.center.x, roof.max.y + 0.15f, roof.center.z),
            Quaternion.LookRotation(car.transform.forward), $"Le second joueur sur le toit de « {car.name} »");
        string supportId = support.Id.ToString(CultureInfo.InvariantCulture);
        yield return Expect("Il tient sur la voiture, sur son écran", () => RemoteIs(RemoteKeys.SupportId, supportId), 3f,
            () => $"voiture {supportId} | {RemoteState()}");
        Vector3 start = car.transform.position;
        yield return ExpectHolds("La voiture l'emporte, chez lui comme chez l'hôte",
            () => RemoteIs(RemoteKeys.SupportId, supportId) && remotePigeon.SupportId == support.Id, 2f, 4f,
            () => $"support chez l'hôte {remotePigeon.SupportId}, voiture {supportId} | {RemoteState()}");
        yield return Check("La voiture a bien avancé pendant ce temps", Vector3.Distance(start, car.transform.position) > 1f,
            $"{Vector3.Distance(start, car.transform.position):0.0} m");
    }
}
#endif
