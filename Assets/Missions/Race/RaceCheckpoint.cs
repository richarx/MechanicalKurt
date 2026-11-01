using DG.Tweening;
using UnityEngine;

/// <summary>
/// Anneau de passage d'une course chronométrée (TimedRace) : enfant de son objet « Points de passage », dans l'ordre
/// du parcours, le dernier étant l'arrivée. Le passage est détecté par la simulation de la course (segment parcouru
/// pendant un tick qui traverse le disque de l'anneau, dans un sens ou dans l'autre) ; ce composant ne fait que l'affichage.
/// </summary>
public class RaceCheckpoint : MonoBehaviour
{
    [Tooltip("Rayon du disque à traverser, dans le plan de l'anneau (perpendiculaire à son axe Z bleu).")]
    [SerializeField, Min(0.5f)] float _radius = 5f;
    [SerializeField] Transform _visual;
    [Tooltip("Matériau du prochain anneau à passer (opaque).")]
    [SerializeField] Material _nextMaterial;
    [Tooltip("Matériau transparent des anneaux suivants ; leur opacité baisse avec leur rang.")]
    [SerializeField] Material _upcomingMaterial;
    [SerializeField] float _pulseScale = 0.12f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    Renderer[] _renderers;
    MaterialPropertyBlock _block;
    bool _shown = true;
    bool _isNext;
    float _alpha = -1f;
    Tween _pulse;
    Vector3 _visualScale = Vector3.one;

    public float Radius => _radius;
    /// <summary>Affiché, et opacité (1 = prochain anneau).</summary>
    public bool IsShown => _shown;
    public bool IsNext => _shown && _isNext;
    public float Alpha => _shown ? _alpha : 0f;

    // Pas d'Awake : un anneau inactif dans la scène n'en reçoit qu'à sa première activation, en plein Show.
    void Init()
    {
        if (_renderers != null)
            return;
        _renderers = GetComponentsInChildren<Renderer>(true);
        _block = new MaterialPropertyBlock();
        if (_visual != null)
            _visualScale = _visual.localScale;
    }

    /// <summary>Le segment <paramref name="from"/> → <paramref name="to"/> traverse le disque de l'anneau.</summary>
    public bool Crossed(Vector3 from, Vector3 to)
    {
        Vector3 center = transform.position;
        Vector3 normal = transform.forward;
        float a = Vector3.Dot(from - center, normal);
        float b = Vector3.Dot(to - center, normal);
        if ((a > 0f && b > 0f) || (a < 0f && b < 0f) || Mathf.Approximately(a, b))
            return false;
        Vector3 hit = Vector3.Lerp(from, to, a / (a - b));
        return (hit - center).sqrMagnitude <= _radius * _radius;
    }

    public void Hide()
    {
        Init();
        if (!_shown)
            return;
        _shown = false;
        _pulse?.Kill();
        gameObject.SetActive(false);
    }

    /// <summary>Prochain anneau : opaque et pulsant. Sinon transparent, d'opacité <paramref name="alpha"/>.</summary>
    public void Show(bool next, float alpha)
    {
        Init();
        alpha = next ? 1f : alpha;
        if (_shown && _isNext == next && Mathf.Approximately(_alpha, alpha))
            return;
        bool lookChanged = !_shown || _isNext != next;
        _shown = true;
        _isNext = next;
        _alpha = alpha;
        gameObject.SetActive(true);

        Material material = next ? _nextMaterial : _upcomingMaterial;
        foreach (Renderer r in _renderers)
        {
            if (material != null)
                r.sharedMaterial = material;
            r.GetPropertyBlock(_block);
            if (material != null && material.HasProperty(BaseColorId))
            {
                Color color = material.GetColor(BaseColorId);
                color.a *= alpha;
                _block.SetColor(BaseColorId, color);
            }
            if (material != null && material.HasProperty(EmissionColorId))
                _block.SetColor(EmissionColorId, material.GetColor(EmissionColorId) * alpha);
            r.SetPropertyBlock(_block);
        }

        if (!lookChanged || _visual == null)
            return;
        _pulse?.Kill();
        _visual.localScale = _visualScale;
        if (next)
            _pulse = _visual.DOScale(_visualScale * (1f + _pulseScale), 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
    }

    void OnDestroy() => _pulse?.Kill();

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Gizmos.DrawLine(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * _radius, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * _radius);
        }
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 2f);
    }
}
