using UnityEngine;

/// <summary>
/// Pigeon local sorti des limites : demande son retour au point d'apparition (input.Respawn).
/// </summary>
[DisallowMultipleComponent]
public class PositionFailSafe : MonoBehaviour
{
    [SerializeField] float _minDeep = -10f;
    [SerializeField] float _maxLinearDist = 450f;
    [Tooltip("Délai avant une nouvelle demande, le temps que la simulation applique la précédente.")]
    [SerializeField, Min(0f)] float _retriggerDelay = 1f;

    PigeonController _pigeonController;
    float _nextAllowedTime;

    void Awake()
    {
        _pigeonController = GetComponent<PigeonController>();
    }

    void Update()
    {
        if (_pigeonController == null || PigeonController.Local != _pigeonController)
            return;

        if (Time.unscaledTime < _nextAllowedTime)
            return;

        Vector3 position = _pigeonController.transform.position;
        if (Mathf.Abs(position.x) > _maxLinearDist || Mathf.Abs(position.z) > _maxLinearDist || position.y < _minDeep)
        {
            _pigeonController.RequestRespawn();
            _nextAllowedTime = Time.unscaledTime + _retriggerDelay;
        }
    }
}