using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Masque, chez le joueur local en course, les repères des autres missions (cercles de départ et d'arrivée,
/// zones des billboards) pour qu'il ne les confonde pas avec le parcours ; les rétablit ensuite.
/// </summary>
public static class MissionMarkers
{
    static readonly List<Renderer> _hidden = new List<Renderer>();
    static bool _isHidden;

    public static bool IsHidden => _isHidden;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _hidden.Clear();
        _isHidden = false;
    }

    /// <param name="keep">Repère à laisser tel quel (celui de la course elle-même).</param>
    public static void SetHidden(bool hidden, GameObject keep = null)
    {
        if (hidden == _isHidden)
            return;
        _isHidden = hidden;
        if (!hidden)
        {
            foreach (Renderer r in _hidden)
                if (r != null)
                    r.enabled = true;
            _hidden.Clear();
            return;
        }

        var roots = new List<Component>();
        roots.AddRange(Object.FindObjectsByType<DeliverySelectionCircle>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        roots.AddRange(Object.FindObjectsByType<MissionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        foreach (Component root in roots)
        {
            if (keep != null && root.transform.IsChildOf(keep.transform))
                continue;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                if (r.enabled)
                {
                    r.enabled = false;
                    _hidden.Add(r);
                }
        }
    }
}
