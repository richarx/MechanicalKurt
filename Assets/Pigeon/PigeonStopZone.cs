using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Zone locale : surveille le pigeon du joueur local. Pour un départ commun à plusieurs joueurs, utiliser MissionStartZone.
/// </summary>
public class PigeonStopZone : MonoBehaviour
{
    [SerializeField, Tooltip("Facultatif : forcer un pigeon précis. Vide = pigeon du joueur local.")]
    PigeonController _pigeon;

    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 2f;

    [field: SerializeField, MinValue(0f), SuffixLabel("unités/s", true)]
    [field: Tooltip("Vitesse maximale pour considérer le pigeon à l'arrêt.")]
    public float StopSpeedThreshold { get; private set; } = 0.15f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Durée d'arrêt requise avant de déclencher l'événement. Mettre 0 pour un déclenchement immédiat.")]
    public float StopDuration { get; private set; } = 0.25f;

    [field: SerializeField]
    [field: Tooltip("Reçoit le pigeon qui s'est arrêté. Les méthodes sans paramètre déjà branchées restent valides.")]
    public UnityEvent<PigeonController> OnPlayerStopped { get; private set; } = new UnityEvent<PigeonController>();

    private float _stoppedTime;
    private bool _hasTriggered;

    public PigeonController Pigeon => _pigeon != null ? _pigeon : PigeonController.Local;

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f)
            return;

        PigeonController pigeon = Pigeon;

        // Evaluate after the player has moved in Update, using its actual speed.
        bool stoppedInside = pigeon != null && pigeon.isActiveAndEnabled && !pigeon.IsStun && !pigeon.IsInTimedRace &&
                             (pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius &&
                             pigeon.Speed <= StopSpeedThreshold;

        if (!stoppedInside)
        {
            _stoppedTime = 0f;
            _hasTriggered = false;
            return;
        }

        if (_hasTriggered)
            return;

        _stoppedTime += Time.deltaTime;
        if (_stoppedTime >= StopDuration)
        {
            _hasTriggered = true;
            OnPlayerStopped?.Invoke(pigeon);
        }
    }

    private void OnDisable()
    {
        _stoppedTime = 0f;
        _hasTriggered = false;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Radius);
        Gizmos.color = previousColor;
    }
}
