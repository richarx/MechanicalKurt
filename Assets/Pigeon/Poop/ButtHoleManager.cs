using System;
using UnityEngine;
using UnityEngine.Events;

public class ButtHoleManager : MonoBehaviour
{
    [SerializeField] Transform _buttHole;
    [SerializeField] GameObject _poopPrefab;
    [SerializeField] float _cooldownMax;

    [HideInInspector] public UnityEvent OnShit = new UnityEvent();

    IPigeonControls _pigeonControls;
    CharacterController _characterController;
    float _cooldown;

    void Start()
    {
        _pigeonControls = GetComponent<IPigeonControls>();
        _characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (_cooldown < 0 && _pigeonControls.ConsumeShit)
        {
            Rigidbody poopRb = Instantiate(_poopPrefab, _buttHole.position, _buttHole.rotation).GetComponent<Rigidbody>();
            poopRb.linearVelocity = _characterController.velocity;
            _cooldown = _cooldownMax;
            OnShit?.Invoke();
        }
        else
        {
            _cooldown -= Time.deltaTime;
        }
    }

}
