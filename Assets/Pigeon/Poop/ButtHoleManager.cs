using UnityEngine;
using UnityEngine.Events;

public class ButtHoleManager : MonoBehaviour
{
    [SerializeField] Transform _buttHole;
    [SerializeField] GameObject _poopPrefab;
    [SerializeField] float _cooldownMax;

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
            // Lâchée comme une bombe : elle part à la vitesse du pigeon, puis seule la gravité agit.
            // La vitesse de la simulation : celle du CharacterController dépend de la durée de l'image et s'emballe entre deux ticks.
            poopRb.linearVelocity = _pigeon.Velocity;
            _cooldown = _cooldownMax;
            OnShit?.Invoke();
        }
        else
        {
            _cooldown -= Time.deltaTime;
        }
    }
}
