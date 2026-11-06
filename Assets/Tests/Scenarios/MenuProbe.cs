#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Parcours du menu principal en Play (lancé par ScenarioMenu.RunMenuProbeFromCommandLine) : chargement de la ville,
/// « Créer » en réseau local, « Continuer », pause. Captures et rapport dans Logs/MenuProbe*.
/// </summary>
public class MenuProbe : MonoBehaviour
{
    public const string RunKey = "MenuProbe.Run";
    const string ReportPath = "Logs/MenuProbe.txt";

    readonly StringBuilder _report = new StringBuilder();
    int _failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!UnityEditor.SessionState.GetBool(RunKey, false))
            return;
        UnityEditor.SessionState.EraseBool(RunKey);
        UnityEditor.SessionState.SetBool(ScenarioRunner.ActiveKey, true);
        var go = new GameObject("[MenuProbe]");
        DontDestroyOnLoad(go);
        go.AddComponent<MenuProbe>();
    }

    void OnEnable() => Application.logMessageReceived += RecordError;
    void OnDisable() => Application.logMessageReceived -= RecordError;

    void RecordError(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;
        _failures++;
        _report.AppendLine($"FAIL erreur : {message.Split('\n')[0]}");
    }

    IEnumerator Start()
    {
        Debug.Log($"[MenuProbe] démarrage ({Mode})");
        SessionLauncher.UseLocalNetwork = Mode == "local";
        MainMenuUI ui = null;
        SessionLauncher launcher = null;
        yield return Until(() => (ui = FindAnyObjectByType<MainMenuUI>()) != null && (launcher = FindAnyObjectByType<SessionLauncher>()) != null, 10f);
        Check("Menu présent", ui != null && launcher != null);
        if (ui == null || launcher == null)
        {
            Finish();
            yield break;
        }

        yield return Until(() => launcher.GameSceneLoaded || launcher.LoadError != null, 90f);
        Check("Ville chargée derrière le menu", launcher.GameSceneLoaded, launcher.LoadError);
        Check("Réseau pas démarré", PurrNet.NetworkManager.main != null && !PurrNet.NetworkManager.main.isServer && !PurrNet.NetworkManager.main.isClient);
        if (Mode == "host")
        {
            yield return RelayHost(ui, launcher);
            Finish();
            yield break;
        }
        if (Mode == "join")
        {
            yield return RelayJoin(ui);
            Finish();
            yield break;
        }
        yield return Until(() => ui.IsChoiceShown, 5f);
        Check("Écran de choix affiché", ui.IsChoiceShown);
        Check("Dans le menu : le compteur de la musique n'a pas démarré", MusicPlayer.instance == null || !MusicPlayer.instance.HasJoinedGame);
        yield return new WaitForSecondsRealtime(3f);
        DescribeCanvases("choix");
        Capture("choice");
        foreach (AmbientPigeon pigeon in FindObjectsByType<AmbientPigeon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OrderBy(p => p.name).Take(3))
            CloseUp(pigeon.transform, pigeon.name.Replace(' ', '_'));
        int seen = 0;
        foreach (AmbientPigeon pigeon in FindObjectsByType<AmbientPigeon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            DescribeOnScreen(pigeon, out bool visible);
            if (visible) seen++;
        }
        Check("Pigeons d'ambiance bien visibles depuis la caméra du menu (au moins 2)", seen >= 2, $"{seen} visible(s)");

        // Chaque pigeon doit vivre : changer d'activité et se déplacer pendant qu'on regarde le menu.
        var ambient = FindObjectsByType<AmbientPigeon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).OrderBy(p => p.name).ToList();
        var activities = ambient.ToDictionary(p => p, p => new HashSet<string> { p.Activity });
        var travelled = ambient.ToDictionary(p => p, p => 0f);
        var last = ambient.ToDictionary(p => p, p => p.transform.position);
        float observeEnd = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < observeEnd)
        {
            yield return null;
            foreach (AmbientPigeon p in ambient)
            {
                activities[p].Add(p.Activity);
                travelled[p] += Vector3.Distance(last[p], p.transform.position);
                last[p] = p.transform.position;
            }
        }
        foreach (AmbientPigeon p in ambient)
            Check($"{p.name} bouge pendant 20 s", activities[p].Count >= 2 && (travelled[p] > 0.1f || activities[p].Contains("Turn")),
                $"{string.Join(", ", activities[p])} ; {travelled[p]:0.0} m parcourus");

        ui.OnCreate();
        yield return Until(() => ui.IsCreateShown, 25f);
        Check("Partie créée : code affiché", ui.IsCreateShown && SessionCode.IsValid(ui.ShownCode), $"code « {ui.ShownCode} »");
        yield return Until(() => PigeonController.Local != null, 10f);
        PigeonController local = PigeonController.Local;
        Check("Pigeon du joueur apparu", local != null);
        yield return new WaitForSecondsRealtime(1f);
        Check("Pendant l'écran du code, commandes en pause", local != null && local.GetComponent<PlayerControls>().IsPaused);
        Check("Écran du code : le compteur de la musique n'a toujours pas démarré", MusicPlayer.instance == null || !MusicPlayer.instance.HasJoinedGame);
        Check("Curseur libre dans le menu", Cursor.lockState == CursorLockMode.None, $"{Cursor.lockState}");
        DescribeCanvases("création");
        Capture("create");

        launcher.EnterGame();
        yield return new WaitForSecondsRealtime(2.5f);
        Check("Entrée en jeu : menu quitté, commandes rendues", !GameSession.InMenu && local != null && !local.GetComponent<PlayerControls>().IsPaused);
        DescribeCanvases("jeu");
        Capture("game");
        if (PigeonController.Local != null)
            CloseUp(PigeonController.Local.transform, "player_pigeon");

        PauseMenu pause = FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        Check("Menu pause présent", pause != null);
        if (pause != null)
        {
            pause.SetPaused(true);
            yield return new WaitForSecondsRealtime(1.5f);
            string codeLine = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(t => t.text).FirstOrDefault(t => t.StartsWith("Game code"));
            Check("Le code est affiché dans la pause", codeLine != null && codeLine.Contains(GameSession.Code ?? "??"), codeLine);
            Capture("pause");
            pause.SetPaused(false);
        }
        Finish();
    }

    /// <summary>« local » (UDP, une seule instance), « host » ou « join » (vrai relais, deux instances) : -menuProbe host|join.</summary>
    static string Mode
    {
        get
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-menuProbe");
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "local";
        }
    }

    static string CodeFile => Path.Combine(Path.GetTempPath(), "pigeon_menuprobe_code.txt");

    IEnumerator RelayHost(MainMenuUI ui, SessionLauncher launcher)
    {
        if (File.Exists(CodeFile))
            File.Delete(CodeFile);
        ui.OnCreate();
        yield return Until(() => ui.IsCreateShown || ui.IsChoiceShown, 40f);
        Check("Relais : partie créée", ui.IsCreateShown && SessionCode.IsValid(GameSession.Code), $"code « {GameSession.Code} », transport {PurrNet.NetworkManager.main.transport.GetType().Name}");
        if (!ui.IsCreateShown)
            yield break;
        File.WriteAllText(CodeFile, GameSession.Code);
        launcher.EnterGame();
        yield return Until(() => PigeonController.All.Count >= 2, 180f);
        Check("Relais : le second joueur a rejoint avec le code", PigeonController.All.Count >= 2, $"{PigeonController.All.Count} pigeon(s)");
        yield return new WaitForSecondsRealtime(6f);
        Check("Relais : il est toujours là", PigeonController.All.Count >= 2, $"{PigeonController.All.Count} pigeon(s)");
        Capture("relay_host");
    }

    IEnumerator RelayJoin(MainMenuUI ui)
    {
        yield return Until(() => File.Exists(CodeFile), 180f);
        string code = File.Exists(CodeFile) ? File.ReadAllText(CodeFile).Trim() : null;
        Check("Relais : code de l'hôte reçu", SessionCode.IsValid(code), code);
        if (!SessionCode.IsValid(code))
            yield break;
        ui.JoinWithCode("Rejoins-moi : https://exemple.test/jeu?code=" + code.ToLowerInvariant());
        yield return Until(() => !GameSession.InMenu || (ui.IsJoinShown && !string.IsNullOrEmpty(ui.JoinError)), 40f);
        Check("Relais : partie rejointe depuis un lien collé", !GameSession.InMenu && GameSession.Code == code, ui.JoinError);
        yield return Until(() => PigeonController.All.Count >= 2, 10f);
        Check("Relais : l'hôte est visible", PigeonController.All.Count >= 2, $"{PigeonController.All.Count} pigeon(s)");

        MainMenuUI menu = null;
        yield return Until(() => GameSession.InMenu && (menu = FindAnyObjectByType<MainMenuUI>()) != null && menu.IsChoiceShown, 60f);
        Check("Départ de l'hôte : retour au menu avec un message", menu != null && menu.IsChoiceShown && !string.IsNullOrEmpty(menu.ChoiceMessage),
            menu != null ? menu.ChoiceMessage : "pas de menu");
    }

    IEnumerator Until(System.Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < end && !condition())
            yield return null;
    }

    void Check(string step, bool ok, string detail = null)
    {
        if (!ok)
            _failures++;
        _report.AppendLine($"{(ok ? "OK  " : "FAIL")} {step}{(detail != null ? " — " + detail : "")}");
        Debug.Log($"[MenuProbe] {(ok ? "OK" : "FAIL")} {step}");
    }

    void DescribeCanvases(string label)
    {
        foreach (AmbientPigeon pigeon in FindObjectsByType<AmbientPigeon>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            _report.AppendLine($"     {pigeon.name} : {DescribeOnScreen(pigeon, out _)}");
        _report.Append($"     canvas visibles ({label}) :");
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (canvas.isRootCanvas && canvas.enabled)
            {
                string texts = string.Join(" / ", canvas.GetComponentsInChildren<TMP_Text>()
                    .Where(t => !string.IsNullOrWhiteSpace(t.text)).Take(3).Select(t => t.text.Replace('\n', ' ')));
                _report.Append($" [{canvas.name} : {texts}]");
            }
        _report.AppendLine();
    }

    /// <summary>
    /// Pigeon d'ambiance réellement visible : dans l'image, assez grand (25 px de haut sur 720), et aucun collider
    /// entre la caméra et sa tête.
    /// </summary>
    static string DescribeOnScreen(AmbientPigeon pigeon, out bool visible)
    {
        visible = false;
        Camera camera = Camera.main;
        if (camera == null)
            return "pas de caméra";
        Renderer body = pigeon.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
        if (body == null)
            return "pas de corps";
        Vector3 viewport = camera.WorldToViewportPoint(body.bounds.center);
        Vector3 top = camera.WorldToViewportPoint(body.bounds.center + Vector3.up * body.bounds.extents.y);
        Vector3 bottom = camera.WorldToViewportPoint(body.bounds.center - Vector3.up * body.bounds.extents.y);
        float pixels = Mathf.Abs(top.y - bottom.y) * 720f;
        Vector3 head = body.bounds.center + Vector3.up * body.bounds.extents.y * 0.8f;
        bool clear = !Physics.Linecast(camera.transform.position, head, ~(1 << pigeon.gameObject.layer), QueryTriggerInteraction.Ignore);
        bool inFrame = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
        visible = inFrame && clear && pixels >= 25f;
        return $"écran ({viewport.x:0.00}, {viewport.y:0.00}) à {viewport.z:0.0} m, {pixels:0} px de haut, {(clear ? "dégagé" : "caché")}";
    }

    /// <summary>Gros plan d'un pigeon (de face, de trois quarts) : Logs/MenuProbe_closeup_*.png.</summary>
    static void CloseUp(Transform pigeon, string label)
    {
        Renderer body = pigeon.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
        if (body == null)
            return;
        Bounds bounds = body.bounds;
        var go = new GameObject("[MenuProbe] Close-up camera");
        var camera = go.AddComponent<Camera>();
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.01f;
        Vector3 direction = (pigeon.forward + pigeon.right * 0.7f + Vector3.up * 0.4f).normalized;
        float distance = bounds.extents.magnitude * 3.2f;
        go.transform.position = bounds.center + direction * distance;
        go.transform.LookAt(bounds.center);
        var target = new RenderTexture(640, 640, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
        image.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes($"Logs/MenuProbe_closeup_{label}.png", image.EncodeToPNG());
        camera.targetTexture = null;
        Object.Destroy(target);
        Object.Destroy(image);
        Object.Destroy(go);
    }

    void Capture(string label)
    {
        Debug.Log($"[MenuProbe] capture {label}");
        Camera camera = Camera.main;
        if (camera == null)
        {
            _report.AppendLine($"     pas de caméra pour la capture « {label} »");
            return;
        }
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
        foreach (Canvas canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.05f;
        }
        Canvas.ForceUpdateCanvases();
        var target = new RenderTexture(1280, 720, 24);
        RenderTexture previous = camera.targetTexture;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        image.Apply();
        File.WriteAllBytes($"Logs/MenuProbe_{label}.png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = previous;
        Destroy(target);
        Destroy(image);
        foreach (Canvas canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
        }
    }

    void Finish()
    {
        _report.Insert(0, $"Sonde du menu — {_failures} échec(s)\n");
        File.WriteAllText(ReportPath, _report.ToString());
        Debug.Log("[MenuProbe]\n" + _report);
        UnityEditor.EditorApplication.Exit(_failures > 0 ? 1 : 0);
    }
}
#endif
