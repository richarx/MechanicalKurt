using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Choisit la caméra du joueur local (sous l'eau, précision, orbitale) ; ne touche pas aux autres pigeons.
/// </summary>
public class CameraSwapper : MonoBehaviour
{
    public enum CameraType
    {
        Orbital,
        UnderWater,
        Precision,
        Hover
    }

    [SerializeField, Tooltip("Facultatif : retrouvé dans la scène si vide (le pigeon est un prefab réseau).")]
    private Water waterPlane;
    [SerializeField] private CinemachineCamera orbitalCamera;
    [SerializeField] private CinemachineCamera underWaterCamera;
    [SerializeField] private CinemachineCamera precisionCamera;
    [SerializeField] private CinemachineCamera hoverCamera;

    private PigeonController pigeonController;
    private CameraType currentCameraType;
    private bool applied;
    private float lastMovementTimestamp;

    public CameraType Current => currentCameraType;
    public CinemachineCamera OrbitalCamera => orbitalCamera;
    public CinemachineCamera CurrentCamera => CameraOf(currentCameraType);

    private void Awake()
    {
        pigeonController = GetComponent<PigeonController>();
        if (waterPlane == null)
            waterPlane = FindAnyObjectByType<Water>();
    }

    private void LateUpdate()
    {
        if (pigeonController == null || !pigeonController.isOwner)
            return;

        CameraType newCameraType = ComputeCameraType();
        if (!applied || newCameraType != currentCameraType)
            SetNewCameraType(newCameraType);
    }

    /// <summary>Recalcule la caméra et coupe sans fondu (respawn, départ de course…).</summary>
    public void SnapNow()
    {
        if (pigeonController == null || !pigeonController.isOwner)
            return;
        SetNewCameraType(ComputeCameraType());
        CinemachineCamera camera = CurrentCamera;
        if (camera != null)
            CinemachineCore.FindPotentialTargetBrain(camera)?.ResetState();
    }

    private CameraType ComputeCameraType()
    {
        IPigeonControls controls = pigeonController.Controls;
        if (controls != null && controls.Movement.magnitude > 0.001f)
            lastMovementTimestamp = Time.time;

        PigeonController.MovementState state = pigeonController.CurrentPigeonState;
        if (waterPlane != null && pigeonController.transform.position.y < waterPlane.transform.position.y)
            return CameraType.UnderWater;
        if (state == PigeonController.MovementState.Braking && Time.time - lastMovementTimestamp <= 0.5f)
            return CameraType.Hover;
        else if (controls != null && controls.PrecisionCamera && state != PigeonController.MovementState.Grounded && state != PigeonController.MovementState.Dancing)
            return CameraType.Precision;
        else
            return CameraType.Orbital;
    }

    private void SetNewCameraType(CameraType newCameraType)
    {
        currentCameraType = newCameraType;
        applied = true;

        if (orbitalCamera != null)
            orbitalCamera.Priority = newCameraType == CameraType.Orbital ? 10 : 9;
        if (underWaterCamera != null)
            underWaterCamera.Priority = newCameraType == CameraType.UnderWater ? 10 : 9;
        if (precisionCamera != null)
            precisionCamera.Priority = newCameraType == CameraType.Precision ? 10 : 9;
        if (hoverCamera != null)
            hoverCamera.Priority = newCameraType == CameraType.Hover ? 10 : 9;
    }

    private CinemachineCamera CameraOf(CameraType type)
    {
        switch (type)
        {
            case CameraType.UnderWater: return underWaterCamera;
            case CameraType.Precision: return precisionCamera;
            case CameraType.Hover: return hoverCamera;
            default: return orbitalCamera;
        }
    }
}
