using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Petit point discret à la couleur du joueur au-dessus des autres pigeons : affiché seulement quand ils sont à l'écran,
/// assez loin et sans obstacle devant. Rien pour son propre pigeon. Ajouté par PigeonCooBeacon.
/// </summary>
[RequireComponent(typeof(PigeonController))]
public class PigeonPlayerDot : MonoBehaviour
{
    const float HeadHeight = 1.2f;
    const float DotSize = 14f;
    const float MaxAlpha = 0.75f;
    const float FadeSpeed = 6f;
    const float MinDistance = 6f;
    const float VisibilityCheckInterval = 0.15f;
    // Pigeons (Player), Ignore Raycast et UI ne cachent rien.
    const int ObstacleLayers = ~((1 << 2) | (1 << 3) | (1 << 5));

    static Sprite _sprite;

    PigeonController _pigeon;
    RectTransform _rect;
    Image _image;
    float _nextVisibilityCheck;
    bool _unobstructed;

    /// <summary>Tests : le point est affiché, et sa couleur.</summary>
    public bool IsShown => _image != null && _image.enabled && _image.color.a > 0.05f;
    public Color Color => _image != null ? _image.color : Color.clear;

    void Awake() => _pigeon = GetComponent<PigeonController>();

    void LateUpdate()
    {
        Vector3 anchor = transform.position + Vector3.up * HeadHeight;
        bool wanted = ShouldShow(anchor, out Vector3 screen);
        if (!wanted && _image == null)
            return;
        if (_image == null)
            CreateDot();

        if (wanted)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_rect.parent, screen, null, out Vector2 local);
            _rect.anchoredPosition = local;
        }
        Color color = _pigeon.PlayerColor;
        color.a = Mathf.MoveTowards(_image.color.a, wanted ? MaxAlpha : 0f, FadeSpeed * Time.unscaledDeltaTime);
        _image.color = color;
        _image.enabled = color.a > 0f;
    }

    bool ShouldShow(Vector3 anchor, out Vector3 screen)
    {
        screen = default;
        Camera view = Camera.main;
        if (view == null || GameSession.InMenu || _pigeon == PigeonController.Local)
            return false;

        screen = view.WorldToScreenPoint(anchor);
        if (screen.z < MinDistance || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height)
            return false;

        if (Time.unscaledTime >= _nextVisibilityCheck)
        {
            _nextVisibilityCheck = Time.unscaledTime + VisibilityCheckInterval;
            _unobstructed = !Physics.Linecast(view.transform.position, anchor, ObstacleLayers, QueryTriggerInteraction.Ignore);
        }
        return _unobstructed;
    }

    void CreateDot()
    {
        var go = new GameObject("Player Dot", typeof(RectTransform), typeof(Image));
        _rect = (RectTransform)go.transform;
        _rect.SetParent(CooIndicatorLayer.Get(), false);
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = new Vector2(DotSize, DotSize);
        _image = go.GetComponent<Image>();
        _image.sprite = GetSprite();
        _image.raycastTarget = false;
        Color color = _pigeon.PlayerColor;
        color.a = 0f;
        _image.color = color;
    }

    /// <summary>Disque aux bords adoucis, généré une fois pour tous les points.</summary>
    static Sprite GetSprite()
    {
        if (_sprite != null)
            return _sprite;
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance)));
            }
        }
        texture.Apply();
        _sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        return _sprite;
    }

    void OnDestroy()
    {
        if (_rect != null)
            Destroy(_rect.gameObject);
    }
}
