using System;
using System.Text.Json.Serialization;
using UnityEngine;

public class PositionFailSafe : MonoBehaviour
{
    [SerializeField] float _minDeep = -10f;
    [SerializeField] float _maxLinearDist = 450f;
    PigeonController _pigeonController;
    Vector3 _initialPos;
    Quaternion _initialQuat;

    void Start()
    {
        _pigeonController = GetComponent<PigeonController>();
        _initialPos = _pigeonController.transform.position;
        _initialQuat = _pigeonController.transform.rotation;
    }

    void Update()
    {
        Vector3 position = _pigeonController.transform.position;
        if (Mathf.Abs(position.x) > _maxLinearDist || Mathf.Abs(position.z) > _maxLinearDist || position.y < _minDeep)
        {
            _pigeonController.Teleport(_initialPos, _initialQuat);
        }
    }
}
