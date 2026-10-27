using UnityEngine;

// Ragdoll purement VISUEL : la position et la rotation du pigeon viennent du state prédit (PigeonController).
// - Les hips sont cinématiques : ils suivent le _visualRoot (qui reçoit StunRotation depuis le contrôleur).
// - Les autres os sont dynamiques et ballottent autour des hips via leurs joints.
// - Le CharacterController reste actif pendant le stun : on ignore les collisions avec les os du ragdoll.
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

        // Les hips suivent la hiérarchie (donc le visuel piloté par l'état) au lieu d'être simulés.
        if (active && _hipsBone != null)
            _hipsBone.isKinematic = true;
    }

    // Plus de repositionnement depuis les hips : la position vient de l'état simulé.
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

    // Compatibilité avec l'ancien nom.
    public void AlignAndGetUp() => EndRagdoll();
}