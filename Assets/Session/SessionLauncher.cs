using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu principal : charge la ville derrière le menu sans démarrer le réseau, puis crée ou rejoint une partie par
/// son code (salle du relais PurrTransport). La ville lancée seule, sans ce menu, démarre comme avant (UDP local).
/// </summary>
public class SessionLauncher : MonoBehaviour
{
    [Tooltip("Scène de jeu, chargée par-dessus le menu.")]
    [SerializeField] string _gameScene = "MainCityScene";
    [Tooltip("Objets propres au menu (caméra du toit, interface, pigeons d'ambiance), désactivés à l'entrée en jeu.")]
    [SerializeField] GameObject[] _menuOnly = Array.Empty<GameObject>();
    [Tooltip("Délai maximal pour ouvrir ou rejoindre une partie.")]
    [SerializeField, Min(1f)] float _connectTimeout = 20f;

    const int CreateAttempts = 3;

    static readonly MethodInfo RoomExistsMethod = typeof(PurrTransportUtils)
        .GetMethod("RoomExistsAsync", BindingFlags.NonPublic | BindingFlags.Static);

    /// <summary>Tests : parties en UDP local au lieu du relais (code affiché mais non utilisé pour se connecter).</summary>
    public static bool UseLocalNetwork { get; set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void PageListenClose(string objectName, string method);
#endif

    NetworkManager _network;
    bool _inGame;

    public bool GameSceneLoaded { get; private set; }
    public string LoadError { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => UseLocalNetwork = false;

    IEnumerator Start()
    {
        GameSession.InMenu = true;
        GameSession.End();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        PageListenClose(gameObject.name, nameof(OnPageClosing));
#endif

        NetworkManager.DisableFlags();
        SceneManager.sceneLoaded += OnSceneLoaded;
        AsyncOperation load = SceneManager.LoadSceneAsync(_gameScene, LoadSceneMode.Additive);
        if (load == null)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            NetworkManager.EnableFlags();
            LoadError = $"Scène « {_gameScene} » introuvable (Build Profiles).";
            yield break;
        }
        while (!load.isDone)
            yield return null;
        SceneManager.SetActiveScene(SceneManager.GetSceneByName(_gameScene));
        yield return null;
        NetworkManager.EnableFlags();

        _network = NetworkManager.main;
        if (_network == null)
        {
            LoadError = "Pas de NetworkManager dans la scène de jeu.";
            yield break;
        }
        _network.onClientConnectionState += HandleClientState;
        HideGameInterfaces();
        GameSceneLoaded = true;
    }

    readonly List<Canvas> _hiddenCanvases = new List<Canvas>();

    /// <summary>Appelé avant la première image de la ville : sur le web, le chargement dure assez pour voir ses interfaces.</summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != _gameScene)
            return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        HideGameInterfaces();
    }

    /// <summary>Web : l'onglet se ferme. Sans fermeture propre, le relais garde la salle de l'hôte plusieurs minutes.</summary>
    public void OnPageClosing() => Leave();

    /// <summary>Les interfaces de la ville (tuto, HUD…) restent cachées tant que le menu est affiché.</summary>
    void HideGameInterfaces()
    {
        foreach (GameObject root in SceneManager.GetSceneByName(_gameScene).GetRootGameObjects())
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas && canvas.enabled)
                {
                    canvas.enabled = false;
                    _hiddenCanvases.Add(canvas);
                }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_network != null)
            _network.onClientConnectionState -= HandleClientState;
    }

    /// <summary>Ouvre une nouvelle partie et la rejoint en hôte ; onDone(null) en cas de succès, sinon le message d'erreur.</summary>
    public IEnumerator Create(Action<string> onDone)
    {
        string code = null;
        for (int attempt = 0; attempt < CreateAttempts && code == null; attempt++)
        {
            string candidate = SessionCode.Generate();
            bool? exists = null;
            yield return CheckRoom(candidate, result => exists = result);
            if (exists != true)
                code = candidate;
        }
        if (code == null)
        {
            onDone("Impossible de trouver un code libre, réessaie.");
            yield break;
        }

        UseTransport(code);
        _network.StartHost();
        bool ok = false;
        yield return WaitFor(() => _network.serverState == ConnectionState.Connected && _network.clientState == ConnectionState.Connected,
            () => _network.serverState == ConnectionState.Disconnected, result => ok = result);
        if (!ok)
        {
            Leave();
            onDone("Impossible d'ouvrir la partie (connexion au serveur).");
            yield break;
        }
        GameSession.Begin(code, host: true);
        onDone(null);
    }

    /// <summary>Rejoint la partie de ce code ; onDone(null) en cas de succès, sinon le message d'erreur.</summary>
    public IEnumerator Join(string code, Action<string> onDone)
    {
        if (!SessionCode.IsValid(code))
        {
            onDone($"Le code fait {SessionCode.Length} caractères (lettres et chiffres).");
            yield break;
        }
        bool? exists = null;
        yield return CheckRoom(code, result => exists = result);
        if (exists == false)
        {
            onDone("Aucune partie avec ce code.");
            yield break;
        }

        UseTransport(code);
        _network.StartClient();
        bool ok = false;
        yield return WaitFor(() => _network.clientState == ConnectionState.Connected && PigeonController.Local != null,
            () => _network.clientState == ConnectionState.Disconnected, result => ok = result);
        if (!ok)
        {
            Leave();
            onDone(exists == null ? "Connexion impossible : vérifie le code et ta connexion." : "La partie ne répond pas : l'hôte l'a peut-être quittée.");
            yield break;
        }
        GameSession.Begin(code, host: false);
        onDone(null);
    }

    /// <summary>Quitte la partie ouverte depuis le menu (retour sur l'écran de création).</summary>
    public void Leave()
    {
        GameSession.End();
        if (_network == null)
            return;
        if (_network.clientState != ConnectionState.Disconnected)
            _network.StopClient();
        if (_network.serverState != ConnectionState.Disconnected)
            _network.StopServer();
    }

    /// <summary>Quitte le menu : la caméra passe au pigeon du joueur et ses commandes reprennent.</summary>
    public void EnterGame()
    {
        _inGame = true;
        GameSession.InMenu = false;
        foreach (GameObject menuObject in _menuOnly)
            if (menuObject != null)
                menuObject.SetActive(false);
        foreach (Canvas canvas in _hiddenCanvases)
            if (canvas != null)
                canvas.enabled = true;
        _hiddenCanvases.Clear();
        SetLocalControlsPaused(false);
    }

    void Update()
    {
        if (GameSession.InMenu)
            SetLocalControlsPaused(true);
    }

    static void SetLocalControlsPaused(bool paused)
    {
        PigeonController local = PigeonController.Local;
        if (local != null && local.TryGetComponent(out PlayerControls controls) && controls.IsPaused != paused)
            controls.SetPaused(paused);
    }

    void HandleClientState(ConnectionState state)
    {
        if (!_inGame || state != ConnectionState.Disconnected)
            return;
        _inGame = false;
        GameSession.PendingMessage = GameSession.IsHost ? "La partie a été fermée." : "La partie est terminée : l'hôte est parti ou la connexion a été perdue.";
        GameSession.End();
        SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
    }

    void UseTransport(string code)
    {
        if (UseLocalNetwork)
            return;
        if (!_network.TryGetComponent(out PurrTransport relay))
            relay = _network.gameObject.AddComponent<PurrTransport>();
        relay.roomName = code;
        if (_network.transport != relay)
            _network.transport = relay;
    }

    /// <summary>La salle existe-t-elle sur le relais ? null si on ne peut pas le savoir.</summary>
    IEnumerator CheckRoom(string code, Action<bool?> onResult)
    {
        if (UseLocalNetwork || RoomExistsMethod == null)
        {
            onResult(UseLocalNetwork ? false : (bool?)null);
            yield break;
        }
        PurrTransport relay = _network.GetComponent<PurrTransport>();
        string server = relay != null ? relay.masterServer : "https://purrtransport.purrservers.com/";
        string key = relay != null ? relay.projectKey : PurrServicesProjectLink.projectKey;
        Task<bool> task;
        try
        {
            task = (Task<bool>)RoomExistsMethod.Invoke(null, new object[] { server, code, key });
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            onResult(null);
            yield break;
        }
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            yield return null;
        onResult(task.IsCompleted && !task.IsFaulted ? task.Result : (bool?)null);
    }

    IEnumerator WaitFor(Func<bool> success, Func<bool> failure, Action<bool> onResult)
    {
        float deadline = Time.realtimeSinceStartup + _connectTimeout;
        float earliestFailure = Time.realtimeSinceStartup + 0.5f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (success())
            {
                onResult(true);
                yield break;
            }
            if (Time.realtimeSinceStartup > earliestFailure && failure())
                break;
            yield return null;
        }
        onResult(false);
    }
}
