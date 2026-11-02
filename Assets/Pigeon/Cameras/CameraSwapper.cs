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
        Precision,
        Hover
    }

    [SerializeField] private Water waterPlane;
    [SerializeField] private CinemachineCamera orbitalCamera;
    [SerializeField] private CinemachineCamera underWaterCamera;
    [SerializeField] private CinemachineCamera precisionCamera;
    [SerializeField] private CinemachineCamera hoverCamera;

    private PigeonController pigeonController;
    private PlayerControls controls;

    private CameraType currentCameraType;

    private float lastMovementTimestamp;

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
        if (pigeonController.Controls.Movement.magnitude > 0.001)
            lastMovementTimestamp = Time.time;

        if (waterPlane != null && pigeonController.transform.position.y < waterPlane.transform.position.y)
            return CameraType.UnderWater;
        if (pigeonController.CurrentPigeonState == PigeonController.PigeonState.Braking && Time.time - lastMovementTimestamp <= 0.5f)
            return CameraType.Hover;
        else if (controls.PrecisionCamera && pigeonController.CurrentPigeonState != PigeonController.PigeonState.Grounded)
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
        hoverCamera.Priority = newCameraType == CameraType.Hover ? 10 : 9;
    }
}
