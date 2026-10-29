using Unity.Cinemachine;
using UnityEngine;

// Choisit la caméra du joueur : sous l'eau, précision (bouton maintenu en vol) ou orbitale.
// Multijoueur : ne touche aux priorités QUE pour le pigeon du joueur local. Cinemachine est global : un pigeon
// distant ou un bot qui monterait sa caméra à 10 volerait la vue (à priorité égale, la plus récente gagne).
public class CameraSwapper : MonoBehaviour
{
    public enum CameraType
    {
        Orbital,
        UnderWater,
        Precision
    }

    [SerializeField, Tooltip("Facultatif : retrouvé dans la scène si vide (le pigeon est un prefab réseau).")]
    private Water waterPlane;
    [SerializeField] private CinemachineCamera orbitalCamera;
    [SerializeField] private CinemachineCamera underWaterCamera;
    [SerializeField] private CinemachineCamera precisionCamera;

    private PigeonController pigeonController;
    private CameraType currentCameraType;
    private bool applied;

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
        // Les inputs passent par le pigeon : joueur ou bot des scénarios.
        IPigeonControls controls = pigeonController.Controls;
        if (waterPlane != null && pigeonController.transform.position.y < waterPlane.transform.position.y)
            return CameraType.UnderWater;
        else if (controls != null && controls.PrecisionCamera && !pigeonController.IsGrounded)
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
    }

    private CinemachineCamera CameraOf(CameraType type)
    {
        switch (type)
        {
            case CameraType.UnderWater: return underWaterCamera;
            case CameraType.Precision: return precisionCamera;
            default: return orbitalCamera;
        }
    }
}
