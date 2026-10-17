using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeliverySelectionCircle : MonoBehaviour
{
    [SerializeField] private LineRenderer _line;
    [SerializeField, Min(12)] private int _segments = 64;
    [SerializeField, Min(0.01f)] private float _lineWidth = 0.12f;
    [SerializeField, Min(0f)] private float _verticalOffset = 0.12f;

    private DeliveryPoint _point;
    private float _configuredRadius = -1f;
    private Vector3 _configuredPosition;
    private Material _runtimeMaterial;

    public void Configure(DeliveryPoint point, Material material, Color color)
    {
        _point = point;
        if (_point == null || material == null || material.shader == null)
            return;

        if (_line == null) _line = GetComponent<LineRenderer>();
        if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
        _line.useWorldSpace = true;
        _line.alignment = LineAlignment.View;
        _line.loop = true;
        _line.positionCount = Mathf.Max(12, _segments);
        _line.widthMultiplier = _lineWidth;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;

        ReleaseRuntimeMaterial();
        _runtimeMaterial = new Material(material) { name = material.name + " (Selection Ring)" };
        if (_runtimeMaterial.HasProperty("_Cull")) _runtimeMaterial.SetFloat("_Cull", 0f);
        _line.sharedMaterial = _runtimeMaterial;
        SetColor(color);
        RefreshGeometry();
    }

    private void LateUpdate()
    {
        RefreshGeometry();
    }

    public void SetProgress(float progress, Color idleColor, Color activeColor)
    {
        SetColor(Color.Lerp(idleColor, activeColor, Mathf.Clamp01(progress)));
    }

    private void SetColor(Color color)
    {
        if (_line != null)
        {
            _line.startColor = Color.white;
            _line.endColor = Color.white;
        }
        if (_runtimeMaterial == null) return;
        if (_runtimeMaterial.HasProperty("_BaseColor")) _runtimeMaterial.SetColor("_BaseColor", color);
        if (_runtimeMaterial.HasProperty("_Color")) _runtimeMaterial.SetColor("_Color", color);
    }

    private void RefreshGeometry()
    {
        if (_point == null || _line == null || _line.positionCount < 3)
            return;

        if (!_point.IsValid)
            return;
        float radius = _point.Radius;
        Vector3 pointPosition = _point.Position;
        if (Mathf.Approximately(radius, _configuredRadius) && pointPosition == _configuredPosition)
            return;

        int segments = _line.positionCount;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            _line.SetPosition(i, pointPosition + Vector3.up * _verticalOffset + offset);
        }
        _configuredRadius = radius;
        _configuredPosition = pointPosition;
    }

    private void OnDestroy()
    {
        ReleaseRuntimeMaterial();
    }

    private void ReleaseRuntimeMaterial()
    {
        if (_runtimeMaterial == null) return;
        if (Application.isPlaying) Destroy(_runtimeMaterial);
        else DestroyImmediate(_runtimeMaterial);
        _runtimeMaterial = null;
    }
}
