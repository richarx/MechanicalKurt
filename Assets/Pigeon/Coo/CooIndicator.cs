using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bulle « Crou crou ! » d'un autre joueur qui roucoule : au-dessus de lui s'il est à l'écran, sinon collée au bord
/// de l'écran dans sa direction, avec la distance. Suit le pigeon puis s'efface.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class CooIndicator : MonoBehaviour
{
    [SerializeField] Image _bubble;
    [SerializeField] TMP_Text _label;
    [SerializeField] TMP_Text _distance;
    [Tooltip("Pointe de la bulle (carré tourné à 45°, à moitié caché derrière elle) : vers le bas au-dessus du pigeon, vers lui quand la bulle est au bord de l'écran.")]
    [SerializeField] RectTransform _arrow;

    [Header("Réglages")]
    [SerializeField, Min(0.5f)] float _duration = 3f;
    [SerializeField, Min(0.05f)] float _fadeDuration = 0.6f;
    [Tooltip("Hauteur au-dessus du pigeon où la bulle s'accroche (m).")]
    [SerializeField] float _headHeight = 1.2f;
    [Tooltip("Écart entre la bulle et le bord de l'écran (unités du canvas).")]
    [SerializeField] float _edgeMargin = 30f;
    [Tooltip("Écart entre la pointe de la bulle et la tête du pigeon quand il est à l'écran.")]
    [SerializeField] float _arrowDistance = 20f;
    [SerializeField] float _popScale = 1.25f;

    RectTransform _rect;
    RectTransform _parent;
    CanvasGroup _group;
    Transform _target;
    float _shownAt = float.NegativeInfinity;

    /// <summary>Tests : bulle visible, au bord de l'écran ou non, et dernière distance affichée.</summary>
    public bool IsShowing => _group != null && _group.alpha > 0.01f && gameObject.activeSelf;
    public bool IsOnEdge { get; private set; }
    public Color Color => _bubble != null ? _bubble.color : Color.white;
    public string DistanceText => _distance != null ? _distance.text : "";
    /// <summary>Position de la bulle par rapport au centre de l'écran (unités du canvas), pour vérifier de quel côté elle apparaît.</summary>
    public Vector2 OffsetFromCenter => _rect != null ? _rect.anchoredPosition : Vector2.zero;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
    }

    public void Show(Transform target, Color color)
    {
        _target = target;
        _bubble.color = color;
        if (_arrow != null && _arrow.TryGetComponent(out Graphic arrowGraphic))
            arrowGraphic.color = color;
        _shownAt = Time.unscaledTime;
        gameObject.SetActive(true);
        Place();
    }

    public void Hide()
    {
        _shownAt = float.NegativeInfinity;
        _group.alpha = 0f;
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        float age = Time.unscaledTime - _shownAt;
        if (_target == null || age > _duration || GameSession.InMenu)
        {
            Hide();
            return;
        }
        _group.alpha = Mathf.Clamp01((_duration - age) / _fadeDuration);
        float pop = Mathf.Clamp01(age / 0.15f);
        _rect.localScale = Vector3.one * Mathf.Lerp(_popScale, 1f, pop * pop);
        Place();
    }

    void Place()
    {
        Camera camera = Camera.main;
        if (camera == null || _target == null)
            return;
        if (_parent == null)
            _parent = (RectTransform)_rect.parent;

        Vector3 world = _target.position + Vector3.up * _headHeight;
        Vector3 viewport = camera.WorldToViewportPoint(world);
        bool behind = viewport.z < 0f;
        Vector2 size = _parent.rect.size;
        Vector2 half = size * 0.5f;
        // Repère centré sur l'écran, en unités du canvas.
        Vector2 point = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
        if (behind)
            point = -point;

        Vector2 bubbleHalf = _bubble.rectTransform.rect.size * 0.5f;
        Vector2 inner = half - bubbleHalf - Vector2.one * _edgeMargin;
        IsOnEdge = behind || Mathf.Abs(point.x) > inner.x || Mathf.Abs(point.y) > inner.y;
        Vector2 direction;
        if (IsOnEdge)
        {
            if (point.sqrMagnitude < 0.01f)
                point = Vector2.down;
            direction = point.normalized;
            point *= Mathf.Min(inner.x / Mathf.Max(Mathf.Abs(point.x), 0.001f), inner.y / Mathf.Max(Mathf.Abs(point.y), 0.001f));
        }
        else
        {
            direction = Vector2.down;
            point.y += bubbleHalf.y + _arrowDistance;
        }
        _rect.anchoredPosition = point;
        if (_arrow != null)
        {
            // La pointe sort du bord de la bulle, du côté du pigeon.
            float toBorder = Mathf.Min(bubbleHalf.x / Mathf.Max(Mathf.Abs(direction.x), 0.001f), bubbleHalf.y / Mathf.Max(Mathf.Abs(direction.y), 0.001f));
            _arrow.anchoredPosition = direction * toBorder;
            _arrow.localRotation = Quaternion.FromToRotation(Vector3.down, direction) * Quaternion.Euler(0f, 0f, 45f);
        }

        Transform from = PigeonController.Local != null ? PigeonController.Local.transform : camera.transform;
        _distance.text = $"{Mathf.RoundToInt(Vector3.Distance(from.position, _target.position))} m";
    }
}
