using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "DeliveryData", menuName = "Delivery/Delivery Data")]
public sealed class DeliveryData : ScriptableObject
{
    [InfoBox("$ValidationMessage", InfoMessageType.Error, nameof(HasConfigurationError))]
    [SerializeField, Required] private string _label;
    [SerializeField, Required, TextArea(2, 5)] private string _content;

    public string Label => _label;
    public string Content => _content;

    [ShowInInspector, ReadOnly]
    public bool IsValid => string.IsNullOrEmpty(ValidationMessage);

    public string ValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_label))
                return "Enter a delivery label.";

            if (string.IsNullOrWhiteSpace(_content))
                return "Enter the delivery content.";

            return string.Empty;
        }
    }

    private bool HasConfigurationError => !IsValid;
}
