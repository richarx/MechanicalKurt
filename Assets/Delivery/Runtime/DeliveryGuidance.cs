using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class DeliveryGuidance : MonoBehaviour
{
    [SerializeField] private DeliveryManager _manager;
    [SerializeField] private Camera _camera;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _marker;
    [SerializeField] private TMP_Text _distanceText;
    [SerializeField] private RectTransform _arrow;
    [SerializeField, Min(0f)] private float _screenMargin = 48f;
    [SerializeField] private Material _destinationMaterial;
    [SerializeField] private Color _destinationColor = new Color(1f, 0.78f, 0.16f, 1f);

    private DeliveryPoint _ringTarget;
    private DeliverySelectionCircle _ring;
    private string _lastDistance;

    private void OnEnable() => RefreshRing(null);

    private void OnDisable()
    {
        SetVisible(false);
        ClearRing();
    }

    private void LateUpdate()
    {
        if (_manager == null || _manager.Session == null || !_manager.Session.isActiveAndEnabled
            || _manager.Session.State != DeliverySessionState.Running)
        {
            SetVisible(false);
            RefreshRing(null);
            return;
        }

        DeliveryPoint target = null;
        if (_manager.State == DeliveryState.Carrying || _manager.State == DeliveryState.DroppingOff)
        {
            DeliveryOffer offer = _manager.CurrentOffer;
            if (offer != null) target = offer.Destination;
        }
        RefreshRing(target);
        if (target == null || _marker == null)
        {
            SetVisible(false);
            return;
        }

        Camera cameraToUse = _camera != null ? _camera : Camera.main;
        Canvas canvasToUse = _canvas != null ? _canvas : _marker.GetComponentInParent<Canvas>();
        if (cameraToUse == null || canvasToUse == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 screen = cameraToUse.WorldToScreenPoint(target.Position);
        Vector2 direction = new Vector2(screen.x - Screen.width * 0.5f, screen.y - Screen.height * 0.5f);
        if (!IsFinite(screen.x) || !IsFinite(screen.y) || !IsFinite(screen.z) || Mathf.Abs(screen.z) < 0.01f)
            direction = Vector2.up;
        else if (screen.z < 0f) direction = -direction;
        if (!IsFinite(direction.x) || !IsFinite(direction.y) || direction.sqrMagnitude < 0.001f) direction = Vector2.up;
        Vector2 clamped = ClampScreenPoint(screen, direction, canvasToUse.scaleFactor);
        RectTransform parentRect = _marker.parent as RectTransform;
        Camera uiCamera = canvasToUse.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvasToUse.worldCamera;
        if (parentRect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, clamped, uiCamera, out Vector2 local))
        {
            SetVisible(false);
            return;
        }
        _marker.position = parentRect.TransformPoint(local);
        if (_arrow != null) _arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        if (_distanceText != null && _manager.Player != null)
        {
            string distance = $"{Vector3.Distance(_manager.Player.position, target.Position):0} m";
            if (_lastDistance != distance) _distanceText.text = _lastDistance = distance;
        }
        SetVisible(true);
    }

    private Vector2 ClampScreenPoint(Vector3 projected, Vector2 direction, float canvasScale)
    {
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);
        float scale = Mathf.Max(0.01f, canvasScale);
        float margin = Mathf.Max(0f, _screenMargin) * scale;
        float left = Mathf.Clamp(margin, 0f, screenWidth * 0.5f);
        float right = screenWidth - left;
        float bottom = Mathf.Clamp(margin, 0f, screenHeight * 0.5f);
        float top = screenHeight - bottom;
        if (_marker != null)
        {
            // Canvas scale is applied once; lossyScale already includes it in overlay mode.
            Vector2 half = Vector2.Scale(_marker.rect.size * 0.5f, new Vector2(
                Mathf.Abs(_marker.localScale.x) * scale, Mathf.Abs(_marker.localScale.y) * scale));
            left = Mathf.Min(screenWidth * 0.5f, left + half.x);
            right = Mathf.Max(screenWidth * 0.5f, right - half.x);
            bottom = Mathf.Min(screenHeight * 0.5f, bottom + half.y);
            top = Mathf.Max(screenHeight * 0.5f, top - half.y);
        }
        if (IsFinite(projected.x) && IsFinite(projected.y) && projected.z > 0f
            && projected.x >= left && projected.x <= right && projected.y >= bottom && projected.y <= top)
            return new Vector2(projected.x, projected.y);

        Vector2 center = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);
        Vector2 ray = direction.normalized;
        if (!IsFinite(ray.x) || !IsFinite(ray.y) || ray.sqrMagnitude < 0.001f) ray = Vector2.up;
        float tx = ray.x > 0f ? (right - center.x) / ray.x : ray.x < 0f ? (left - center.x) / ray.x : float.PositiveInfinity;
        float ty = ray.y > 0f ? (top - center.y) / ray.y : ray.y < 0f ? (bottom - center.y) / ray.y : float.PositiveInfinity;
        float t = Mathf.Max(0f, Mathf.Min(tx > 0f ? tx : float.PositiveInfinity, ty > 0f ? ty : float.PositiveInfinity));
        if (!IsFinite(t)) return center;
        return center + ray * t;
    }

    private void RefreshRing(DeliveryPoint target)
    {
        if (target == null)
        {
            if (_ring != null || _ringTarget != null) ClearRing();
            return;
        }
        if (_ringTarget == target) return;
        ClearRing();
        _ringTarget = target;
        if (_ringTarget == null || _destinationMaterial == null) return;
        GameObject ringObject = new GameObject("Delivery Destination Ring");
        ringObject.transform.SetParent(transform, true);
        _ring = ringObject.AddComponent<DeliverySelectionCircle>();
        _ring.Configure(_ringTarget, _destinationMaterial, _destinationColor);
    }

    private void ClearRing()
    {
        if (_ring != null)
        {
            GameObject ringObject = _ring.gameObject;
            ringObject.SetActive(false);
            if (Application.isPlaying) Destroy(ringObject); else DestroyImmediate(ringObject);
        }
        _ring = null;
        _ringTarget = null;
        _lastDistance = null;
    }

    private void SetVisible(bool visible)
    {
        if (_marker != null && _marker.gameObject.activeSelf != visible) _marker.gameObject.SetActive(visible);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
