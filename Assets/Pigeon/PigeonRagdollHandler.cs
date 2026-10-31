using UnityEngine;

/// <summary>
/// Ragdoll purement visuel : la position du pigeon vient de son état prédit.
/// </summary>
public class PigeonRagdollHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private Rigidbody _hipsBone;
    [SerializeField] private Transform _visualRoot;

    private Rigidbody[] _ragdollRigidbodies;
    private Collider[] _ragdollColliders;
    private bool _isRagdoll = false;

    public bool IsRagdoll => _isRagdoll;

    void Awake()
    {
        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        _ragdollColliders = GetComponentsInChildren<Collider>();

        if (_characterController != null)
        {
            foreach (var col in _ragdollColliders)
            {
                if (col.gameObject == gameObject) continue;
                Physics.IgnoreCollision(_characterController, col, true);
            }
        }

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

        if (active && _hipsBone != null)
            _hipsBone.isKinematic = true;
    }

    public void EndRagdoll()
    {
        if (!_isRagdoll) return;

        SetRagdollState(false);

        if (_visualRoot != null)
            _visualRoot.localRotation = Quaternion.identity;

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    public void AlignAndGetUp() => EndRagdoll();
}