using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.ProBuilder.MeshOperations;

public class CameraSwapper : MonoBehaviour
{
    private enum CameraType
    {
        Orbital,
        UnderWater,
        Precision
    }

    [SerializeField] private Water waterPlane;
    [SerializeField] private CinemachineCamera orbitalCamera;
    [SerializeField] private CinemachineCamera underWaterCamera;
    [SerializeField] private CinemachineCamera precisionCamera;

    private PigeonController pigeonController;
    private PlayerControls controls;

    private CameraType currentCameraType;

    private void Start()
    {
        pigeonController = GetComponent<PigeonController>();
        controls = GetComponent<PlayerControls>();
    }

    // Update is called once per frame
    private void LateUpdate()
    {
        CameraType newCameraType = ComputeCameraType();

        if (newCameraType != currentCameraType)
            SetNewCameraType(newCameraType);
    }

    private CameraType ComputeCameraType()
    {
        if (waterPlane != null && pigeonController.transform.position.y < waterPlane.transform.position.y)
            return CameraType.UnderWater;
        else if (controls.PrecisionCamera)
            return CameraType.Precision;
        else
            return CameraType.Orbital;
    }

    private void SetNewCameraType(CameraType newCameraType)
    {
        currentCameraType = newCameraType;

        orbitalCamera.Priority = newCameraType == CameraType.Orbital ? 10 : 9;
        underWaterCamera.Priority = newCameraType == CameraType.UnderWater ? 10 : 9;
        precisionCamera.Priority = newCameraType == CameraType.Precision ? 10 : 9;
    }
}
