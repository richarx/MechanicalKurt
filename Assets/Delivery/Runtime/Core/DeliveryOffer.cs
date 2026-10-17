using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeliveryOffer : MonoBehaviour
{
    [InfoBox("$ValidationMessage", InfoMessageType.Error, nameof(HasConfigurationError))]
    [SerializeField, Required] private DeliveryData _data;
    [SerializeField, Required] private DeliveryPoint _departure;
    [SerializeField, Required] private DeliveryPoint _destination;

    public DeliveryData Data => _data;
    public DeliveryPoint Departure => _departure;
    public DeliveryPoint Destination => _destination;

    [ShowInInspector, ReadOnly]
    public string Label => _data != null ? _data.Label : string.Empty;

    [ShowInInspector, ReadOnly]
    public bool IsValid => string.IsNullOrEmpty(ValidationMessage);

    // Availability describes scene/data validity. Selection and delivery progress
    // belong to the selector/manager, so renewing never disables shared points.
    [ShowInInspector, ReadOnly]
    public bool IsAvailable => isActiveAndEnabled && IsValid
        && _departure.isActiveAndEnabled && _destination.isActiveAndEnabled;

    public string ValidationMessage
    {
        get
        {
            if (_data == null)
                return "Assign a DeliveryData asset to the offer.";

            if (!_data.IsValid)
                return "Invalid data: " + _data.ValidationMessage;

            if (_departure == null)
                return "Assign a departure point to the offer.";

            if (_destination == null)
                return "Assign a destination point to the offer.";

            if (_departure == _destination)
                return "Departure and destination must be different points.";

            if (!_departure.IsValid)
                return "Invalid departure: " + _departure.ValidationMessage;

            if (!_destination.IsValid)
                return "Invalid destination: " + _destination.ValidationMessage;

            return string.Empty;
        }
    }

    private bool HasConfigurationError => !IsValid;
}
