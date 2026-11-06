using UnityEngine;
using UnityEngine.Events;

public class ButtHoleManager : MonoBehaviour
{
    [SerializeField] Transform _buttHole;
    [SerializeField] GameObject _poopPrefab;
    [SerializeField] float _cooldownMax;
    [SerializeField, Range(0f, 1f), Tooltip("Part de la vitesse du pigeon gardée par la fiente : assez faible pour qu'elle tombe sous lui au lieu de partir devant.")]
    float _inheritedVelocity = 0.25f;
    [SerializeField, Min(0f), Tooltip("Vitesse vers le bas donnée à la fiente quand elle part.")]
    float _dropSpeed = 2f;

    [HideInInspector] public UnityEvent OnShit = new UnityEvent();

    PigeonController _pigeon;
    float _cooldown;

    void Start()
    {
        _pigeon = GetComponent<PigeonController>();
    }

    void Update()
    {
        IPigeonControls controls = _pigeon.Controls;
        bool paused = PauseMenu.instance != null && PauseMenu.instance.IsPaused;
        if (_cooldown < 0 && !paused && controls != null && controls.ConsumeShit)
        {
            Rigidbody poopRb = Instantiate(_poopPrefab, _buttHole.position, _buttHole.rotation).GetComponent<Rigidbody>();
            // La vitesse de la simulation : celle du CharacterController dépend de la durée de l'image et s'emballe entre deux ticks.
            poopRb.linearVelocity = _pigeon.Velocity * _inheritedVelocity + Vector3.down * _dropSpeed;
            _cooldown = _cooldownMax;
            OnShit?.Invoke();
        }
        else
        {
            _cooldown -= Time.deltaTime;
        }
    }
}
