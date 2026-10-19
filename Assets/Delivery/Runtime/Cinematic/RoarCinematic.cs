using System;
using System.Collections;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class RoarCinematic : MonoBehaviour
{
    private static readonly int RoarHash = Animator.StringToHash("Roar");
    private static readonly int DieHash = Animator.StringToHash("Die");

    [SerializeField] private GameObject _oldGameObject;

    [field: SerializeField, Required]
    public Animator ActorAnimator { get; private set; }

    [field: SerializeField]
    public Animator[] VictimAnimators { get; private set; } = Array.Empty<Animator>();

    [field: SerializeField, Required]
    [field: Tooltip("Camera Cinemachine active a afficher au declenchement. Retour automatique a la camera precedente apres Die et le delai de retour.")]
    public CinemachineCamera CinematicCamera { get; private set; }

    [field: SerializeField, MinValue(1)]
    public int CameraPriority { get; private set; } = 100;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Delai entre le changement de camera et le trigger Roar. Inclure la duree du blend souhaitee.")]
    public float CameraToRoarDelay { get; private set; } = 1f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    public float RoarToDieDelay { get; private set; } = 1f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Secondes reelles laissees aux animations Die avant de revenir a la camera precedente.")]
    public float DieToRestoreDelay { get; private set; } = 1f;

    [ShowInInspector, ReadOnly] public bool IsPlaying { get; private set; }

    private Coroutine _routine;
    private Coroutine _restoreRoutine;
    private CinemachineCamera _overriddenCamera;
    private CinemachineVirtualCameraBase _returnCamera;
    private PrioritySettings _previousPriority;

    // Conserve le nom pour les UnityEvents deja branches.
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled || IsPlaying)
            return;

        if (ActorAnimator == null)
        {
            Debug.LogWarning("RoarCinematic : assigner l'Animator principal.", this);
            return;
        }

        CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (CinematicCamera == null || !CinematicCamera.isActiveAndEnabled
            || brain == null || !brain.isActiveAndEnabled || !brain.IsValidChannel(CinematicCamera))
        {
            Debug.LogWarning("RoarCinematic : assigner une camera Cinemachine active, avec un Brain compatible sur la Main Camera.", this);
            return;
        }

        ActorAnimator.gameObject.SetActive(true);
        if (_oldGameObject != null)
            _oldGameObject.SetActive(false);

        if (!HasTrigger(ActorAnimator, RoarHash))
        {
            Debug.LogWarning("RoarCinematic : assigner un Animator actif avec le trigger Roar.", this);
            return;
        }

        IsPlaying = true;
        if (_restoreRoutine != null)
        {
            StopCoroutine(_restoreRoutine);
            _restoreRoutine = null;
        }
        if (_overriddenCamera != CinematicCamera)
        {
            RestoreCameraImmediately();
            _overriddenCamera = CinematicCamera;
            _previousPriority = CinematicCamera.Priority;
            _returnCamera = brain.ActiveVirtualCamera as CinemachineVirtualCameraBase;
        }
        int livePriority = brain.ActiveVirtualCamera is CinemachineVirtualCameraBase liveCamera
            ? liveCamera.Priority.Value : 0;
        CinematicCamera.Priority = Mathf.Max(CameraPriority,
            livePriority < int.MaxValue ? livePriority + 1 : int.MaxValue);
        CinematicCamera.Prioritize();
        _routine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        // Laisser au Brain une frame pour prendre en compte le changement de priorite.
        yield return null;
        yield return new WaitForSeconds(Mathf.Max(0f, CameraToRoarDelay));

        if (!HasTrigger(ActorAnimator, RoarHash))
        {
            CancelCinematic();
            yield break;
        }
        ActorAnimator.ResetTrigger(RoarHash);
        ActorAnimator.SetTrigger(RoarHash);

        yield return new WaitForSeconds(Mathf.Max(0f, RoarToDieDelay));

        if (VictimAnimators != null)
        {
            foreach (Animator victim in VictimAnimators)
            {
                if (victim == null)
                    continue;
                if (!HasTrigger(victim, DieHash))
                {
                    Debug.LogWarning("RoarCinematic : cette victime doit avoir un Animator actif avec le trigger Die.", victim);
                    continue;
                }
                victim.ResetTrigger(DieHash);
                victim.SetTrigger(DieHash);
            }
        }

        yield return RestoreCameraAfterDelay();

        IsPlaying = false;
        _routine = null;
    }

    public void CancelCinematic()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        if (_restoreRoutine != null)
            StopCoroutine(_restoreRoutine);
        _routine = null;
        _restoreRoutine = null;
        IsPlaying = false;
        RestoreCameraImmediately();
    }

    // Un appel externe ne doit pas court-circuiter l'attente apres Die.
    public void RestoreCamera()
    {
        if (!isActiveAndEnabled || IsPlaying || _restoreRoutine != null || _overriddenCamera == null)
            return;
        _restoreRoutine = StartCoroutine(RestoreCameraAfterDelay());
    }

    private IEnumerator RestoreCameraAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, DieToRestoreDelay));
        RestoreCameraImmediately();
        _restoreRoutine = null;
    }

    private void RestoreCameraImmediately()
    {
        if (_overriddenCamera != null)
        {
            _overriddenCamera.Priority = _previousPriority;
            _overriddenCamera.Prioritize();
            if (_returnCamera != null && _returnCamera != _overriddenCamera && _returnCamera.isActiveAndEnabled)
                _returnCamera.Prioritize();
        }
        _overriddenCamera = null;
        _returnCamera = null;
    }

    private void OnDisable() => CancelCinematic();

    private static bool HasTrigger(Animator animator, int hash)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        return false;
    }
}
