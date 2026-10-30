using PurrNet;
using UnityEngine;

/// <summary>
/// Voitures solides en solo, traversables dÃ¨s qu'un autre joueur est connectÃ© : le trafic n'est pas synchronisÃ©
/// et ne doit pas entrer dans la simulation prÃ©dite des pigeons.
/// </summary>
public class TrafficCollisionGate : MonoBehaviour
{
    const string CarLayerName = "PigeonOnly";
    const string PigeonLayerName = "Player";

    int _carLayer = -1;
    int _pigeonLayer = -1;
    bool _initialIgnore;
    bool _applied;
    bool _ignoring;

    /// <summary>Vrai quand les voitures sont traversables (partie à plusieurs joueurs).</summary>
    public static bool CarsAreGhosts { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TrafficCollisionGate>() != null)
            return;
        var go = new GameObject("[TrafficCollisionGate]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<TrafficCollisionGate>();
    }

    void Awake()
    {
        _carLayer = LayerMask.NameToLayer(CarLayerName);
        _pigeonLayer = LayerMask.NameToLayer(PigeonLayerName);
        if (_carLayer >= 0 && _pigeonLayer >= 0)
            _initialIgnore = Physics.GetIgnoreLayerCollision(_carLayer, _pigeonLayer);
        CarsAreGhosts = false;
    }

    void Update()
    {
        if (_carLayer < 0 || _pigeonLayer < 0)
            return;
        NetworkManager network = NetworkManager.main;
        bool multiplayer = network != null && network.playerCount > 1;
        bool ignore = _initialIgnore || multiplayer;
        if (_applied && ignore == _ignoring)
            return;
        _applied = true;
        _ignoring = ignore;
        CarsAreGhosts = multiplayer;
        Physics.IgnoreLayerCollision(_carLayer, _pigeonLayer, ignore);
    }

    void OnDestroy()
    {
        if (_carLayer >= 0 && _pigeonLayer >= 0)
            Physics.IgnoreLayerCollision(_carLayer, _pigeonLayer, _initialIgnore);
        CarsAreGhosts = false;
    }
}
