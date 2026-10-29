using PurrNet;
using UnityEngine;

// Les voitures (colliders sur le layer PigeonOnly, MovingSupport) sont simulées localement par chaque client, au
// hasard et à la frame : elles ne sont pas au même endroit d'une machine à l'autre. Tant que le trafic n'est pas
// synchronisé, elles ne doivent donc pas entrer dans la simulation prédite des pigeons en multijoueur, sinon un client
// serait corrigé sans arrêt (posé sur SA voiture, percuté par celles de l'hôte).
// Règle, identique sur chaque machine : voitures solides (obstacle, support, stun) quand on joue seul, traversables
// dès qu'un autre joueur est connecté. Les bots des scénarios ne sont pas des joueurs.
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

    // Réglage global de la physique : le rendre tel qu'au lancement (l'éditeur le garderait après le Play).
    void OnDestroy()
    {
        if (_carLayer >= 0 && _pigeonLayer >= 0)
            Physics.IgnoreLayerCollision(_carLayer, _pigeonLayer, _initialIgnore);
        CarsAreGhosts = false;
    }
}
