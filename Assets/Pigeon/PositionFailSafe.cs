using UnityEngine;

// Détecte qu'un pigeon est sorti des limites et demande son retour au point d'apparition.
// La détection reste locale (owner uniquement) ; la téléportation elle-même est faite par la simulation
// de PigeonController via input.Respawn, donc rejouable et sans position transmise par le client.
// Noms de champs conservés pour garder les valeurs déjà sérialisées dans le prefab.
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
        // Chaque client possède une copie de ce composant sur chaque pigeon : seul le pigeon local agit.
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