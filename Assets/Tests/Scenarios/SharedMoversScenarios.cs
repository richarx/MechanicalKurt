#if UNITY_EDITOR
using System.Collections;
using System.Globalization;
using PurrNet.Prediction;
using UnityEngine;

public partial class ScenarioRunner
{
    public static string DescribeVector(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "{0:0.000},{1:0.000},{2:0.000}", v.x, v.y, v.z);

    static Vector3 ParseVector(string text)
    {
        string[] parts = text.Split(',');
        return new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
            float.Parse(parts[1], CultureInfo.InvariantCulture), float.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    IEnumerator ZeppelinIsSmooth()
    {
        MoveZeppelin zeppelin = FindAnyObjectByType<MoveZeppelin>();
        yield return Check("Un dirigeable dans la scène", zeppelin != null);
        MovingSupport support = zeppelin.GetComponentInChildren<MovingSupport>();
        Renderer shown = zeppelin.View.GetComponentInChildren<Renderer>();
        yield return Check("Son affichage est séparé de son collider", shown != null && shown.enabled
            && support.ViewTransform != support.transform && support.ViewTransform.IsChildOf(zeppelin.View));
        int visibleRenderers = 0;
        foreach (Renderer renderer in zeppelin.GetComponentsInChildren<Renderer>())
            if (renderer.enabled && !renderer.transform.IsChildOf(zeppelin.View))
                visibleRenderers++;
        yield return Check("Le maillage n'est pas affiché en double", visibleRenderers == 0, $"{visibleRenderers} rendu(s) hors du pivot");

        Vector3 lastShown = shown.transform.position, lastBody = zeppelin.transform.position;
        int frames = 0, moved = 0, bodyMoves = 0;
        float biggestStep = 0f, maxGap = 0f, travelled = 0f;
        float start = Time.realtimeSinceStartup, end = start + 2f;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            frames++;
            float step = Vector3.Distance(shown.transform.position, lastShown);
            if (step > 1e-4f) moved++;
            if ((zeppelin.transform.position - lastBody).sqrMagnitude > 1e-8f) bodyMoves++;
            biggestStep = Mathf.Max(biggestStep, step);
            travelled += step;
            maxGap = Mathf.Max(maxGap, Vector3.Distance(zeppelin.View.position, zeppelin.transform.position));
            lastShown = shown.transform.position;
            lastBody = zeppelin.transform.position;
        }
        float fps = frames / Mathf.Max(0.01f, Time.realtimeSinceStartup - start);
        string detail = $"{moved}/{frames} images ({fps:0} i/s), collider {bodyMoves}, plus grand pas {biggestStep:0.000} m pour {travelled:0.0} m";
        yield return Check("Il avance", travelled > 1f, detail);
        yield return Check("L'affichage bouge à chaque image", moved >= frames * 0.9f, detail);
        if (fps > 30f)
            yield return Check("Le collider, lui, ne bouge qu'aux ticks", bodyMoves < moved * 0.9f, detail);
        yield return Check("L'affichage reste près du collider", maxGap < 1.5f, $"écart max {maxGap:0.00} m");

        Collider hull = zeppelin.GetComponentInChildren<Collider>();
        // Le pigeon plane et le dirigeable file : on le place devant, sur la route du sommet de la coque.
        Vector3 before = hull.bounds.center;
        yield return new WaitForSecondsRealtime(0.25f);
        Vector3 hullVelocity = (hull.bounds.center - before) / 0.25f;
        Vector3 top = hull.bounds.center + hullVelocity * 0.5f;
        bool hasTop = hull.Raycast(new Ray(hull.bounds.center + Vector3.up * 30f, Vector3.down), out RaycastHit topHit, 60f);
        yield return Check("La coque a un dessus au centre", hasTop, $"coque {hull.bounds.size}");
        top.y = topHit.point.y + 0.6f;
        yield return Teleport(Pigeon, top, Quaternion.identity, "Le pigeon au-dessus du dirigeable");
        var trace = new System.Text.StringBuilder();
        float nextTrace = 0f, traceStart = Time.realtimeSinceStartup;
        bool Landed()
        {
            float t = Time.realtimeSinceStartup - traceStart;
            if (t >= nextTrace && trace.Length < 900)
            {
                nextTrace = t + 0.25f;
                Vector3 rel = Pigeon.transform.position - hull.bounds.center;
                trace.Append($" [{t:0.00}s rel({rel.x:0.0},{rel.y:0.0},{rel.z:0.0}) {Pigeon.CurrentPigeonState} sup {Pigeon.SupportId} sol {Pigeon.IsGrounded}]");
            }
            return Pigeon.SupportId == support.Id;
        }
        yield return Expect("Il se pose dessus", Landed, 4f,
            () => $"attendu {support.Id}, coque {hull.bounds.size}, couche {LayerMask.LayerToName(hull.gameObject.layer)}, trace :{trace}");
        yield return new WaitForSecondsRealtime(0.4f);
        float biggestShake = 0f, drift = 0f;
        int shakeFrames = 0;
        Vector3 anchor = support.ViewTransform.InverseTransformPoint(Pigeon.transform.position), lastLocal = anchor;
        end = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < end && Pigeon.SupportId == support.Id)
        {
            yield return null;
            Vector3 local = support.ViewTransform.InverseTransformPoint(Pigeon.transform.position);
            biggestShake = Mathf.Max(biggestShake, Vector3.Distance(local, lastLocal));
            drift = Mathf.Max(drift, Vector3.Distance(local, anchor));
            lastLocal = local;
            shakeFrames++;
        }
        yield return Check("Posé, il ne tremble pas par rapport au dirigeable affiché", shakeFrames > 10 && biggestShake < 0.05f,
            $"plus grand saut d'une image {biggestShake:0.000} m sur {shakeFrames} images, dérive {drift:0.000} m");
    }

    IEnumerator MultiSharedZeppelinAndBall()
    {
        MoveZeppelin zeppelin = FindAnyObjectByType<MoveZeppelin>();
        yield return Check("Un dirigeable dans la scène", zeppelin != null);
        yield return Expect("Le second joueur publie son dirigeable", () => !string.IsNullOrEmpty(Remote(RemoteKeys.ZeppelinPosition)), 3f, RemoteState);
        float worstZeppelin = 0f;
        float end = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            string[] parts = Remote(RemoteKeys.ZeppelinPosition).Split(';');
            // Comparé à la même heure : chaque machine a son décalage réseau.
            double clock = double.Parse(parts[0], CultureInfo.InvariantCulture);
            worstZeppelin = Mathf.Max(worstZeppelin, (float)System.Math.Abs(clock - CityClock.Now) * 7.5f < 4f
                ? Vector3.Distance(ParseVector(parts[1]), zeppelin.transform.position) - (float)System.Math.Abs(clock - CityClock.Now) * 7.5f
                : 0f);
        }
        yield return Check("Même dirigeable chez les deux joueurs", worstZeppelin < 0.5f, $"écart max, décalage réseau retiré : {worstZeppelin:0.00} m");

        BallRolling ball = FindAnyObjectByType<BallRolling>();
        yield return Check("Un ballon de plage", ball != null);
        Rigidbody body = ball.GetComponent<Rigidbody>();
        PredictedRigidbody predicted = ball.GetComponent<PredictedRigidbody>();
        MovingSupport support = ball.GetComponent<MovingSupport>();
        yield return Check("Le ballon est synchronisé par le réseau", predicted != null
            && predicted.predictionPolicy == PredictionPolicy.SoftCorrection && ball.GetComponent<LocalPhysics>() == null);
        yield return Check("Son maillage est sous le pivot lissé", ball.GetComponentInChildren<Renderer>().transform.IsChildOf(support.ViewTransform)
            && support.ViewTransform != ball.transform);

        yield return Expect("Le second joueur publie son ballon", () => !string.IsNullOrEmpty(Remote(RemoteKeys.BallPosition)), 3f, RemoteState);
        yield return Expect("Même position au départ", () => Vector3.Distance(ParseVector(Remote(RemoteKeys.BallPosition)), body.position) < 0.3f, 5f,
            () => $"hôte {body.position}, second joueur {Remote(RemoteKeys.BallPosition)}");

        Vector3 start = body.position;
        predicted.linearVelocity = new Vector3(3f, 0f, 0f);
        yield return Expect("L'hôte pousse le ballon : il bouge chez le second joueur", () => Vector3.Distance(ParseVector(Remote(RemoteKeys.BallPosition)), start) > 0.5f, 4f,
            () => $"départ {start}, hôte {body.position}, second joueur {Remote(RemoteKeys.BallPosition)}");
        yield return ExpectHolds("Les deux ballons restent ensemble pendant qu'il roule",
            () => Vector3.Distance(ParseVector(Remote(RemoteKeys.BallPosition)), body.position) < 0.5f, 2f, 6f,
            () => $"hôte {body.position} à {body.linearVelocity.magnitude:0.0} m/s, second joueur {Remote(RemoteKeys.BallPosition)}");
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        // Le second joueur fait rouler le ballon : l'hôte doit le voir, puisqu'il fait référence.
        Collider collider = ball.GetComponent<Collider>();
        Vector3 top = collider.bounds.center + Vector3.up * (collider.bounds.extents.y + 0.15f);
        PigeonController remotePigeon = RemotePigeon;
        yield return Teleport(remotePigeon, top, Quaternion.identity, "Le second joueur sur le ballon");
        string supportId = support.Id.ToString(CultureInfo.InvariantCulture);
        yield return Expect("Il se pose dessus, sur son écran", () => RemoteIs(RemoteKeys.SupportId, supportId), 4f, RemoteState);
        Vector3 before = body.position;
        Quaternion rotationBefore = body.rotation;
        yield return RemoteDo("walk", "1");
        Defer(() => StartCoroutine(RemoteDo("walk", "0")));
        float angle = 0f, moved = 0f;
        end = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            angle = Mathf.Max(angle, Quaternion.Angle(rotationBefore, body.rotation));
            moved = Mathf.Max(moved, Vector3.Distance(before, body.position));
        }
        yield return RemoteDo("walk", "0");
        yield return Check("Le second joueur marche dessus : le ballon roule chez l'hôte", angle > 10f || moved > 0.3f, $"{angle:0} °, {moved:0.00} m");
        yield return Expect("Toujours au même endroit chez les deux", () => Vector3.Distance(ParseVector(Remote(RemoteKeys.BallPosition)), body.position) < 0.5f, 8f,
            () => $"hôte {body.position}, second joueur {Remote(RemoteKeys.BallPosition)}");
    }
}
#endif
