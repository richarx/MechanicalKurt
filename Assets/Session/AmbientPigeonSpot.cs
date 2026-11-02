using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Emplacement où un pigeon d'ambiance du menu peut se poser. Il y en a plus que de pigeons : chacun en prend un libre
/// au hasard dans son groupe (bord du nid, plateforme…) au lancement et après chaque vol.
/// </summary>
public class AmbientPigeonSpot : MonoBehaviour
{
    [SerializeField] string _group = "nest";
    [Tooltip("Rayon de marche autour de l'emplacement.")]
    [SerializeField, Min(0.1f)] float _groundRadius = 0.5f;
    [Tooltip("Écart de hauteur accepté pour marcher : petit sur un perchoir (bord du nid) pour ne pas en descendre.")]
    [SerializeField, Min(0.01f)] float _groundTolerance = 0.1f;

    static readonly List<AmbientPigeonSpot> _all = new List<AmbientPigeonSpot>();

    public string Group => _group;
    public float GroundRadius => _groundRadius;
    public float GroundTolerance => _groundTolerance;
    public AmbientPigeon Occupant { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all.Clear();

    void OnEnable() => _all.Add(this);

    void OnDisable()
    {
        _all.Remove(this);
        Occupant = null;
    }

    /// <summary>Un emplacement libre du groupe, autre que l'actuel si possible ; null s'il n'y en a aucun.</summary>
    public static AmbientPigeonSpot Take(string group, AmbientPigeon pigeon, AmbientPigeonSpot current)
    {
        var free = new List<AmbientPigeonSpot>();
        foreach (AmbientPigeonSpot spot in _all)
            if (spot.Group == group && spot != current && (spot.Occupant == null || spot.Occupant == pigeon))
                free.Add(spot);
        if (free.Count == 0)
            return current;
        AmbientPigeonSpot chosen = free[Random.Range(0, free.Count)];
        if (current != null && current.Occupant == pigeon)
            current.Occupant = null;
        chosen.Occupant = pigeon;
        return chosen;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = _group == "nest" ? Color.yellow : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
    }
}
