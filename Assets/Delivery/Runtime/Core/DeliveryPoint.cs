using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeliveryPoint : MonoBehaviour
{
    [InfoBox("$ValidationMessage", InfoMessageType.Error, nameof(HasConfigurationError))]
    [SerializeField, Required] private string _displayName;
    [SerializeField, MinValue(0.01f), SuffixLabel("world units", true)] private float _radius = 2f;
    [SerializeField] private Color _gizmoColor = new Color(0.2f, 0.85f, 1f, 1f);

    [field:SerializeField] public  CinemachineCamera CameraCinematic {get; private set;}

    public string DisplayName => _displayName;
    public float Radius => _radius;

    [ShowInInspector, ReadOnly]
    public Vector3 Position => transform.position;

    [ShowInInspector, ReadOnly]
    public bool IsValid => string.IsNullOrEmpty(ValidationMessage);

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_displayName))
                return "Enter a point name.";

            if (float.IsNaN(_radius) || float.IsInfinity(_radius) || _radius <= 0f)
                return "Point radius must be a finite number greater than zero.";

            return string.Empty;
        }
    }

    private bool HasConfigurationError => !IsValid;

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (float.IsNaN(_radius) || float.IsInfinity(_radius) || _radius <= 0f)
            return;

        Color previousColor = UnityEditor.Handles.color;
        UnityEditor.Handles.color = _gizmoColor;
        UnityEditor.Handles.DrawWireDisc(Position, transform.up, _radius);
        UnityEditor.Handles.Label(Position, string.IsNullOrWhiteSpace(_displayName) ? name : _displayName);
        UnityEditor.Handles.color = previousColor;
#endif
    }
}
