#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public partial class ScenarioRunner
{
    /// <summary>Repère horizontal de la caméra : devant et à droite de ce que voit le joueur.</summary>
    static void CameraFrame(out Vector3 forward, out Vector3 right)
    {
        forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
        right = Vector3.Cross(Vector3.up, forward);
    }

    /// <summary>Point dans le champ de la caméra : (vx, vy) en coordonnées d'écran 0-1, à cette distance.</summary>
    static Vector3 InView(float vx, float vy, float distance) => Camera.main.ViewportPointToRay(new Vector3(vx, vy, 0f)).GetPoint(distance);

    static string Describe(CooIndicator indicator) => indicator == null ? "pas de bulle"
        : $"visible {indicator.IsShowing}, au bord {indicator.IsOnEdge}, décalage {indicator.OffsetFromCenter}, distance « {indicator.DistanceText} », couleur #{ColorUtility.ToHtmlStringRGB(indicator.Color)}";

    /// <summary>Photo de la vue du joueur, bulles comprises (leur canvas passe un instant devant la caméra).</summary>
    static void CaptureWithBubbles(string label)
    {
        Canvas canvas = FindAnyObjectByType<CooIndicator>()?.GetComponentInParent<Canvas>();
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

    IEnumerator SeeOtherPigeons()
    {
        CameraFrame(out Vector3 forward, out Vector3 _);
        TrailRenderer ownTrail = Pigeon.GetComponentInChildren<TrailRenderer>();
        float ownWidth = ownTrail.widthMultiplier;
        Color ownStart = ownTrail.startColor;

        var bots = new List<SpawnedBot>();
        yield return SpawnBot(InView(0.5f, 0.6f, 15f), Quaternion.LookRotation(forward), bots);
        PigeonController other = bots[0].Pigeon;
        bots[0].Controls.Brake = true;
        PigeonPlayerDot dot = other.GetComponent<PigeonPlayerDot>();
        yield return Check("L'autre pigeon a son point", dot != null);
        yield return Expect("Point affiché à sa couleur", () => dot.IsShown && ColorsMatch(dot.Color, other.PlayerColor), 2f,
            () => $"affiché {dot.IsShown}, point #{ColorUtility.ToHtmlStringRGB(dot.Color)}, joueur #{ColorUtility.ToHtmlStringRGB(other.PlayerColor)}");
        yield return Check("Point discret", dot.Color.a <= 0.8f, $"opacité {dot.Color.a:0.00}");

        PigeonPlayerDot ownDot = Pigeon.GetComponent<PigeonPlayerDot>();
        yield return Check("Pas de point sur son propre pigeon", ownDot == null || !ownDot.IsShown);

        TrailRenderer otherTrail = other.GetComponentInChildren<TrailRenderer>();
        yield return Expect("Traînée de l'autre à sa couleur, plus large et plus longue",
            () => ColorsMatch(otherTrail.startColor, other.PlayerColor) && otherTrail.widthMultiplier > ownWidth * 2f && otherTrail.time > ownTrail.time * 2f, 1f,
            () => $"couleur #{ColorUtility.ToHtmlStringRGB(otherTrail.startColor)}, largeur {otherTrail.widthMultiplier:0.00} / {ownWidth:0.00}, durée {otherTrail.time:0.00}");
        yield return Check("Sa propre traînée ne change pas", ownTrail.widthMultiplier == ownWidth && ownTrail.startColor == ownStart,
            $"largeur {ownTrail.widthMultiplier:0.00}, couleur #{ColorUtility.ToHtmlStringRGB(ownTrail.startColor)}");

        // Un mur entre la caméra et l'autre pigeon cache le point.
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 otherPosition = other.transform.position + Vector3.up * 1.2f;
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Defer(() => Destroy(wall));
        wall.name = "[Scenario] Wall";
        wall.transform.position = Vector3.Lerp(cameraPosition, otherPosition, 0.5f);
        wall.transform.localScale = new Vector3(6f, 6f, 0.5f);
        wall.transform.rotation = Quaternion.LookRotation(otherPosition - cameraPosition);
        yield return Expect("Un obstacle devant : le point disparaît", () => !dot.IsShown, 1.5f, () => $"opacité {dot.Color.a:0.00}");
        Destroy(wall);
        yield return Expect("Plus d'obstacle : le point revient", () => dot.IsShown, 1.5f, () => $"opacité {dot.Color.a:0.00}");
    }

    static bool ColorsMatch(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    IEnumerator CooBubbles()
    {
        CameraFrame(out Vector3 forward, out Vector3 right);
        var bots = new List<SpawnedBot>();
        yield return SpawnBot(InView(0.65f, 0.6f, 10f), Quaternion.LookRotation(forward), bots);
        PigeonController other = bots[0].Pigeon;
        BotControls otherControls = bots[0].Controls;
        otherControls.Brake = true;
        PigeonCooBeacon beacon = other.GetComponent<PigeonCooBeacon>();
        yield return Check("Le pigeon a sa balise de roucoulement", beacon != null);
        int otherCoos = 0;
        other.OnCoo.AddListener(() => otherCoos++);

        otherControls.PressCoo();
        yield return Expect("L'autre pigeon roucoule (événement simulé)", () => otherCoos > 0, 1f);
        yield return Expect("Sa bulle apparaît au-dessus de lui", () => beacon.Indicator != null && beacon.Indicator.IsShowing && !beacon.Indicator.IsOnEdge
            && beacon.Indicator.OffsetFromCenter.y > -100f, 1f, () => $"{Describe(beacon.Indicator)} | caméra « {Camera.main.name} » en {Camera.main.transform.position} "
                + $"vers {Camera.main.transform.forward}, bot en {other.transform.position} → viewport {Camera.main.WorldToViewportPoint(other.transform.position)}, "
                + $"canvas {((RectTransform)beacon.Indicator.transform.parent).rect.size}, joueur en {Pigeon.transform.position}");
        yield return new WaitForSecondsRealtime(0.3f);
        CaptureWithBubbles("coo_onscreen");
        yield return Check("À la couleur de son joueur", beacon.Indicator.Color == other.PlayerColor,
            $"bulle #{ColorUtility.ToHtmlStringRGB(beacon.Indicator.Color)}, joueur #{ColorUtility.ToHtmlStringRGB(other.PlayerColor)}");
        float distance = Vector3.Distance(Pigeon.transform.position, other.transform.position);
        yield return Check("Avec la distance", int.TryParse(beacon.Indicator.DistanceText.Replace(" m", ""), out int shown) && Mathf.Abs(shown - distance) <= 2f,
            $"« {beacon.Indicator.DistanceText} » pour {distance:0.0} m");
        yield return Expect("Elle s'efface après quelques secondes", () => !beacon.Indicator.IsShowing, 5f, () => Describe(beacon.Indicator));

        yield return Teleport(other, Pigeon.transform.position + right * 40f - forward * 30f + Vector3.up * 3f, Quaternion.LookRotation(forward),
            "L'autre pigeon passe derrière, à droite");
        for (int i = 0; i < 3; i++)
        {
            otherControls.PressCoo();
            yield return new WaitForSecondsRealtime(0.3f);
        }
        yield return Expect("Hors de l'écran : bulle au bord droit, tournée vers lui", () => beacon.Indicator.IsShowing && beacon.Indicator.IsOnEdge
            && beacon.Indicator.OffsetFromCenter.x > 0f, 1f, () => Describe(beacon.Indicator));
        CaptureWithBubbles("coo_edge");
        yield return Check("Roucouler plusieurs fois renouvelle la même bulle", FindObjectsByType<CooIndicator>(FindObjectsSortMode.None).Length == 1,
            $"{FindObjectsByType<CooIndicator>(FindObjectsSortMode.None).Length} bulle(s)");

        PigeonCooBeacon own = Pigeon.GetComponent<PigeonCooBeacon>();
        int ownCoos = 0;
        UnityAction count = () => ownCoos++;
        Pigeon.OnCoo.AddListener(count);
        Defer(() => Pigeon.OnCoo.RemoveListener(count));
        var cooClips = (List<AudioClip>)typeof(PigeonSFX).GetField("coohClips", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(Pigeon.GetComponent<PigeonSFX>());
        Bot.PressCoo();
        yield return Expect("Le joueur roucoule", () => ownCoos > 0, 1f);
        yield return Expect("… et entend son propre roucoulement (son 2D)", () =>
        {
            foreach (AudioSource source in Pigeon.GetComponentsInChildren<AudioSource>())
                if (source.isPlaying && cooClips.Contains(source.clip) && source.spatialBlend < 0.5f && source.volume > 0.5f)
                    return true;
            return false;
        }, 1f, () => string.Join(", ", System.Linq.Enumerable.Select(Pigeon.GetComponentsInChildren<AudioSource>(),
            s => $"{(s.clip != null ? s.clip.name : "-")} joue {s.isPlaying} 3D {s.spatialBlend} vol {s.volume} pitch {s.pitch:0.00}")));
        yield return ExpectStays("Pas de bulle pour son propre pigeon", () => own.Indicator == null || !own.Indicator.IsShowing, 1f,
            () => Describe(own.Indicator));
    }

    IEnumerator MultiCoo()
    {
        PigeonController remote = RemotePigeon;
        CameraFrame(out Vector3 forward, out Vector3 _);
        yield return Teleport(remote, InView(0.65f, 0.6f, 10f), Quaternion.LookRotation(forward),
            "Le second joueur est devant l'hôte");
        Defer(() => _link?.Send("brake", "0"));
        yield return RemoteDo("brake", "1");
        PigeonCooBeacon beacon = remote.GetComponent<PigeonCooBeacon>();

        PigeonPlayerDot dot = remote.GetComponent<PigeonPlayerDot>();
        TrailRenderer trail = remote.GetComponentInChildren<TrailRenderer>();
        yield return Expect("L'hôte voit le point et la traînée du second joueur à sa couleur",
            () => dot.IsShown && ColorsMatch(dot.Color, remote.PlayerColor) && ColorsMatch(trail.startColor, remote.PlayerColor), 2f,
            () => $"point {dot.IsShown} #{ColorUtility.ToHtmlStringRGB(dot.Color)}, traînée #{ColorUtility.ToHtmlStringRGB(trail.startColor)}, joueur #{ColorUtility.ToHtmlStringRGB(remote.PlayerColor)}");

        yield return RemoteDo("coo");
        yield return Expect("L'hôte voit la bulle du second joueur", () => beacon.Indicator != null && beacon.Indicator.IsShowing, 2f,
            () => Describe(beacon.Indicator));
        yield return Check("À la couleur du second joueur, différente de celle de l'hôte",
            beacon.Indicator.Color == remote.PlayerColor && remote.PlayerColor != Pigeon.PlayerColor,
            $"bulle #{ColorUtility.ToHtmlStringRGB(beacon.Indicator.Color)}, second joueur #{ColorUtility.ToHtmlStringRGB(remote.PlayerColor)}, hôte #{ColorUtility.ToHtmlStringRGB(Pigeon.PlayerColor)}");
        yield return ExpectStays("Le second joueur ne voit pas de bulle pour lui-même", () => !(Remote(RemoteKeys.CooBubbles) ?? "").Contains("moi:"), 1f,
            RemoteState);

        string hostColor = ColorUtility.ToHtmlStringRGB(Pigeon.PlayerColor);
        Bot.PressCoo();
        yield return Expect("Le second joueur voit la bulle de l'hôte, à sa couleur", () => RemoteIs(RemoteKeys.CooBubbles, hostColor), 2f,
            () => $"attendu {hostColor} | {RemoteState()}");
        yield return Check("L'hôte ne voit pas de bulle pour lui-même",
            !Pigeon.TryGetComponent(out PigeonCooBeacon own) || own.Indicator == null || !own.Indicator.IsShowing);
    }
}
#endif
