using UnityEngine;

/// <summary>
/// Quand ce pigeon roucoule, les autres joueurs voient une bulle à sa couleur (au-dessus de lui ou au bord de
/// l'écran) : de quoi se retrouver. Rien ne s'affiche pour son propre pigeon.
/// </summary>
[RequireComponent(typeof(PigeonController))]
public class PigeonCooBeacon : MonoBehaviour
{
    [SerializeField] CooIndicator _indicatorPrefab;

    PigeonController _pigeon;
    CooIndicator _indicator;

    /// <summary>Bulle de ce pigeon, créée à son premier roucoulement vu d'un autre joueur (null avant).</summary>
    public CooIndicator Indicator => _indicator;

    void Awake()
    {
        _pigeon = GetComponent<PigeonController>();
        _pigeon.OnCoo.AddListener(OnCoo);
    }

    void OnDestroy()
    {
        if (_pigeon != null)
            _pigeon.OnCoo.RemoveListener(OnCoo);
        if (_indicator != null)
            Destroy(_indicator.gameObject);
    }

    void OnCoo()
    {
        if (_pigeon == PigeonController.Local || _indicatorPrefab == null)
            return;
        if (_indicator == null)
            _indicator = Instantiate(_indicatorPrefab, CooIndicatorLayer.Get());
        _indicator.Show(transform, _pigeon.PlayerColor);
    }
}

/// <summary>Canvas des bulles de roucoulement, créé à la première bulle.</summary>
static class CooIndicatorLayer
{
    static RectTransform _layer;

    public static RectTransform Get()
    {
        if (_layer != null)
            return _layer;
        var go = new GameObject("[Roucoulements]", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _layer = (RectTransform)go.transform;
        return _layer;
    }
}
