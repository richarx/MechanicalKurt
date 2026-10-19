using System.Collections;
using UnityEngine;

public class PigeonRagdollHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private PigeonController _pigeonController;
    [SerializeField] private Transform _hipsBone;
    [SerializeField] private Transform _visualRoot;

    [Header("Get Up Settings (Without Animation)")]
    [Tooltip("Si activé, redresse le pigeon en douceur sur quelques frames au lieu d'un pop sec.")]
    [SerializeField] private bool _smoothPop = true;
    [SerializeField] private float _smoothDuration = 0.3f;
    [SerializeField] private LayerMask _groundLayer = ~0;

    private Rigidbody[] _ragdollRigidbodies;
    private Collider[] _ragdollColliders;
    private bool _isRagdoll = false;

    public bool IsRagdoll => _isRagdoll;

    void Awake()
    {

        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        _ragdollColliders = GetComponentsInChildren<Collider>();


        SetRagdollState(false);
    }


    public void SetRagdollState(bool active)
    {
        _isRagdoll = active;


        if (_animator != null) _animator.enabled = !active;


        foreach (var rb in _ragdollRigidbodies)
        {
            if (rb.gameObject == gameObject) continue;

            rb.isKinematic = !active;
            rb.detectCollisions = active;
        }

        foreach (var col in _ragdollColliders)
        {
            if (col.gameObject == gameObject) continue;
            col.enabled = active;
        }
    }


    public void AlignAndGetUp()
    {
        if (!_isRagdoll) return;

        StartCoroutine(RoutineAlignAndGetUp());
    }

    private IEnumerator RoutineAlignAndGetUp()
    {

        Vector3 hipsPosition = _hipsBone.position;
        Quaternion hipsRotation = _hipsBone.rotation;


        Vector3 targetPosition = hipsPosition;
        if (Physics.Raycast(hipsPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, _groundLayer))
        {
            targetPosition.y = hit.point.y;
        }


        Vector3 projectedForward = Vector3.ProjectOnPlane(hipsRotation * Vector3.forward, Vector3.up).normalized;
        if (projectedForward == Vector3.zero) projectedForward = transform.forward;
        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, Vector3.up);

        if (!_smoothPop)
        {

            transform.position = targetPosition;
            transform.rotation = targetRotation;

            if (_visualRoot != null)
                _visualRoot.localRotation = Quaternion.identity;

            SetRagdollState(false);
        }
        else
        {

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;


            SetRagdollState(false);

            float elapsedTime = 0f;
            while (elapsedTime < _smoothDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / _smoothDuration);


                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                if (_visualRoot != null)
                {
                    _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, Quaternion.identity, t);
                }

                yield return null;
            }

            transform.position = targetPosition;
            transform.rotation = targetRotation;
            if (_visualRoot != null) _visualRoot.localRotation = Quaternion.identity;
        }
    }
}