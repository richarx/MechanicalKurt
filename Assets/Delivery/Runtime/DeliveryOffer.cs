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

    // La disponibilité ne représente pas une progression : toutes les offres valides
    // et actives peuvent être consultées simultanément à cette étape.
    [ShowInInspector, ReadOnly]
    public bool IsAvailable => isActiveAndEnabled && IsValid
        && _departure.isActiveAndEnabled && _destination.isActiveAndEnabled;

    public string ValidationMessage
    {
        get
        {
            if (_data == null)
                return "Affecter un asset DeliveryData à l'offre.";

            if (!_data.IsValid)
                return "Données invalides : " + _data.ValidationMessage;

            if (_departure == null)
                return "Affecter un lieu de départ à l'offre.";

            if (_destination == null)
                return "Affecter un lieu de destination à l'offre.";

            if (_departure == _destination)
                return "Le départ et la destination doivent être deux lieux différents.";

            if (!_departure.IsValid)
                return "Départ invalide : " + _departure.ValidationMessage;

            if (!_destination.IsValid)
                return "Destination invalide : " + _destination.ValidationMessage;

            return string.Empty;
        }
    }

    private bool HasConfigurationError => !IsValid;
}
