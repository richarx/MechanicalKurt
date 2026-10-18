using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class PigeonStopZone : MonoBehaviour
{
    [SerializeField] PigeonController _pigeon;

    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 2f;

    [field: SerializeField, MinValue(0f), SuffixLabel("unités/s", true)]
    [field: Tooltip("Vitesse maximale pour considérer le pigeon à l'arrêt.")]
    public float StopSpeedThreshold { get; private set; } = 0.15f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Durée d'arrêt requise avant de déclencher l'événement. Mettre 0 pour un déclenchement immédiat.")]
    public float StopDuration { get; private set; } = 0.25f;

    [field: SerializeField]
    public UnityEvent OnPlayerStopped { get; private set; } = new UnityEvent();

    private float _stoppedTime;
    private float _nextPigeonSearch;
    private bool _hasTriggered;

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f)
            return;

        if (_pigeon == null && Time.time >= _nextPigeonSearch)
        {
            _pigeon = FindAnyObjectByType<PigeonController>();
            _nextPigeonSearch = Time.time + 1f;
        }

        // Evaluate after the player has moved in Update, using its actual speed.
        bool stoppedInside = _pigeon != null && _pigeon.isActiveAndEnabled &&
                             (_pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius &&
                             _pigeon.Speed <= StopSpeedThreshold;

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
            OnPlayerStopped?.Invoke();
        }
    }

    private void OnDisable()
    {
        _stoppedTime = 0f;
        _hasTriggered = false;
        _nextPigeonSearch = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Radius);
        Gizmos.color = previousColor;
    }
}
