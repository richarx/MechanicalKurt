using Unity.Cinemachine;
using UnityEngine;

public class WaterCamera : MonoBehaviour
{
    PigeonController _pigeon;
    Water _water;
    CinemachineOrbitalFollow _orbitalFollow;

    void Start()
    {
        _pigeon = GetComponentInParent<PigeonController>();
        _water = FindAnyObjectByType<Water>();
        _orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
    }

    void LateUpdate()
    {
        if (_water != null && _pigeon.transform.position.y < _water.transform.position.y)
        {
            _orbitalFollow.VerticalAxis.Value = _orbitalFollow.VerticalAxis.Range.x;
        }
    }
}
