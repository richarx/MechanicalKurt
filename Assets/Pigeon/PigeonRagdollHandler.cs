using UnityEngine;

public class PigeonRagdollHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private PigeonController _pigeonController;
    [SerializeField] private Transform _hipsBone;
    [SerializeField] private Transform _visualRoot;

    [Header("Get Up Settings")]
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

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        if (_visualRoot != null)
            _visualRoot.localRotation = Quaternion.identity;

        SetRagdollState(false);

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }
}
