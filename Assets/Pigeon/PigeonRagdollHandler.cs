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
    private Transform[] _bones;
    private Vector3[] _restPositions;
    private Quaternion[] _restRotations;
    private Vector3[] _restScales;
    private bool _isRagdoll = false;

    public bool IsRagdoll => _isRagdoll;

    void Awake()
    {
        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        _ragdollColliders = GetComponentsInChildren<Collider>();
        RecordRestPose();

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
        RestoreRestPose();

        if (_visualRoot != null)
            _visualRoot.localRotation = Quaternion.identity;

        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }
    }

    public void AlignAndGetUp() => EndRagdoll();

    /// <summary>
    /// Les articulations étirent les os pendant le ragdoll, et l'animation ne réécrit pas leurs positions :
    /// sans cette pose de repos, Rebind reprendrait la pose déformée.
    /// </summary>
    void RecordRestPose()
    {
        Transform root = _animator != null ? _animator.transform : transform;
        _bones = root.GetComponentsInChildren<Transform>(true);
        _restPositions = new Vector3[_bones.Length];
        _restRotations = new Quaternion[_bones.Length];
        _restScales = new Vector3[_bones.Length];
        for (int i = 0; i < _bones.Length; i++)
        {
            _bones[i].GetLocalPositionAndRotation(out _restPositions[i], out _restRotations[i]);
            _restScales[i] = _bones[i].localScale;
        }
    }

    void RestoreRestPose()
    {
        for (int i = 0; i < _bones.Length; i++)
        {
            Transform bone = _bones[i];
            if (bone == null || bone == _visualRoot || bone == transform)
                continue;
            bone.SetLocalPositionAndRotation(_restPositions[i], _restRotations[i]);
            bone.localScale = _restScales[i];
        }
    }
}