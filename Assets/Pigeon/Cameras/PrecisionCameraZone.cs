using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zone où le joueur passe en caméra de précision sans appuyer sur rien. Sa forme est l'union des colliders de cet objet
/// et de ses enfants (Box, Sphere, Capsule ou Mesh convexe), passés en trigger au lancement pour ne pas gêner le vol :
/// dupliquer ou déplacer un enfant suffit à l'adapter (donut, égout…). Seule la caméra du joueur local est concernée.
/// </summary>
public class PrecisionCameraZone : MonoBehaviour
{
    static readonly List<PrecisionCameraZone> Zones = new List<PrecisionCameraZone>();

    [SerializeField, Tooltip("Couleur des colliders dans la vue Scene, pour les placer.")]
    Color _gizmoColor = new Color(0.2f, 1f, 0.4f, 0.25f);

    Collider[] _colliders = new Collider[0];

    public int ColliderCount => _colliders.Length;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Zones.Clear();

    void OnEnable()
    {
        _colliders = GetComponentsInChildren<Collider>();
        // Une case oubliée ne doit pas laisser un mur invisible dans la zone.
        foreach (Collider part in _colliders)
            part.isTrigger = true;
        Zones.Add(this);
    }

    void OnDisable() => Zones.Remove(this);

    /// <summary>Une zone active force la caméra de précision à cet endroit.</summary>
    public static bool Forces(Vector3 position, bool grounded)
    {
        // Au sol, la caméra de précision empêche de bien se diriger en marchant.
        if (grounded)
            return false;
        for (int i = 0; i < Zones.Count; i++)
            if (Zones[i].Contains(position))
                return true;
        return false;
    }

    public bool Contains(Vector3 position)
    {
        foreach (Collider zone in _colliders)
        {
            if (zone != null && zone.enabled && zone.bounds.Contains(position)
                && (zone.ClosestPoint(position) - position).sqrMagnitude < 0.0001f)
                return true;
        }
        return false;
    }

    void OnDrawGizmos()
    {
        foreach (BoxCollider box in GetComponentsInChildren<BoxCollider>())
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        foreach (SphereCollider sphere in GetComponentsInChildren<SphereCollider>())
        {
            Gizmos.matrix = sphere.transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
