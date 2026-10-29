using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Objet qui transporte le pigeon posé dessus (ballon de plage, voitures). Seuls les colliders sous un MovingSupport
// servent de support.
// Le pigeon garde dans son état prédit l'Id du support et sa position dans le repère du support : l'Id est donc
// déduit du chemin dans la hiérarchie (identique sur chaque machine pour un objet de scène) plutôt que d'une
// référence d'objet, qu'un état réseau ne peut pas porter.
[DisallowMultipleComponent]
public class MovingSupport : MonoBehaviour
{
    [SerializeField, Tooltip("Facultatif : transform lissé servant à l'affichage du pigeon posé dessus. Vide = ce transform.")]
    Transform _viewTransform;

    static readonly Dictionary<int, MovingSupport> _byId = new Dictionary<int, MovingSupport>();

    /// <summary>Identifiant stable (jamais 0) tant que le support est actif.</summary>
    public int Id { get; private set; }

    public Transform ViewTransform => _viewTransform != null ? _viewTransform : transform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _byId.Clear();

    /// <summary>Le support actif portant cet Id (faux s'il est inconnu, désactivé ou détruit).</summary>
    public static bool TryGet(int id, out MovingSupport support)
    {
        if (id != 0 && _byId.TryGetValue(id, out support) && support != null && support.isActiveAndEnabled)
            return true;
        support = null;
        return false;
    }

    void OnEnable()
    {
        int id = ComputeId();
        // Deux objets au même chemin (doublons de nom et d'index impossibles en pratique) : on décale.
        while (_byId.TryGetValue(id, out MovingSupport other) && other != null && other != this)
        {
            Debug.LogWarning($"MovingSupport : Id déjà pris par « {other.name} », décalé.", this);
            id = id == int.MaxValue ? 1 : id + 1;
        }
        Id = id;
        _byId[id] = this;
    }

    void OnDisable()
    {
        if (_byId.TryGetValue(Id, out MovingSupport registered) && registered == this)
            _byId.Remove(Id);
    }

    // FNV-1a sur « scène/parent#index/enfant#index… » : stable d'une exécution et d'une machine à l'autre
    // (contrairement à string.GetHashCode).
    int ComputeId()
    {
        var path = new StringBuilder(gameObject.scene.name);
        var chain = new List<Transform>();
        for (Transform t = transform; t != null; t = t.parent)
            chain.Add(t);
        for (int i = chain.Count - 1; i >= 0; i--)
            path.Append('/').Append(chain[i].name).Append('#').Append(chain[i].GetSiblingIndex());

        unchecked
        {
            uint hash = 2166136261;
            string text = path.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619;
            }
            int id = (int)(hash & 0x7FFFFFFF);
            return id == 0 ? 1 : id;
        }
    }
}
