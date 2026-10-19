using System.Collections;
using UnityEngine;

public class PigeonRagdollHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator _animator;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private PigeonController _pigeonController; // Ton contrôleur de vol/sol
    [SerializeField] private Transform _hipsBone;                 // Le bassin (Hips / Pelvis) du pigeon
    [SerializeField] private Transform _visualRoot;               // Optionnel : La racine visuelle du modèle (pour lisser la rotation)

    [Header("Get Up Settings (Without Animation)")]
    [Tooltip("Si activé, redresse le pigeon en douceur sur quelques frames au lieu d'un pop sec.")]
    [SerializeField] private bool _smoothPop = true;
    [SerializeField] private float _smoothDuration = 0.3f;        // Durée du redressement fluide
    [SerializeField] private LayerMask _groundLayer = ~0;         // Couches pour aligner au sol

    private Rigidbody[] _ragdollRigidbodies;
    private Collider[] _ragdollColliders;
    private bool _isRagdoll = false;

    public bool IsRagdoll => _isRagdoll;

    void Awake()
    {
        // Récupération de tous les Rigidbodies et Colliders enfants (les os du ragdoll)
        _ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        _ragdollColliders = GetComponentsInChildren<Collider>();

        // S'assure que le ragdoll est désactivé au lancement du jeu
        SetRagdollState(false);
    }

    /// <summary>
    /// Active ou désactive le mode Ragdoll (Physique pure vs Animator/Controller).
    /// </summary>
    public void SetRagdollState(bool active)
    {
        _isRagdoll = active;

        // 1. Gestion des composants principaux
        if (_animator != null) _animator.enabled = !active;
        if (_characterController != null) _characterController.enabled = !active;
        if (_pigeonController != null) _pigeonController.enabled = !active;

        // 2. Activation / Désactivation des os physiques
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

    /// <summary>
    /// Déclenche la procédure de réalignement du bassin et remet le pigeon debout.
    /// </summary>
    public void AlignAndGetUp()
    {
        if (!_isRagdoll) return;

        StartCoroutine(RoutineAlignAndGetUp());
    }

    private IEnumerator RoutineAlignAndGetUp()
    {
        // 1. Capture de la position et de l'orientation du bassin au sol
        Vector3 hipsPosition = _hipsBone.position;
        Quaternion hipsRotation = _hipsBone.rotation;

        // Raycast pour trouver le sol exact sous le bassin
        Vector3 targetPosition = hipsPosition;
        if (Physics.Raycast(hipsPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, _groundLayer))
        {
            targetPosition.y = hit.point.y;
        }

        // Calcul de la direction horizontale propre du regard
        Vector3 projectedForward = Vector3.ProjectOnPlane(hipsRotation * Vector3.forward, Vector3.up).normalized;
        if (projectedForward == Vector3.zero) projectedForward = transform.forward;
        Quaternion targetRotation = Quaternion.LookRotation(projectedForward, Vector3.up);

        if (!_smoothPop)
        {
            // -------------------------------------------------------------------
            // OPTION A : LE POP SEC (Instantané)
            // -------------------------------------------------------------------
            transform.position = targetPosition;
            transform.rotation = targetRotation;

            if (_visualRoot != null)
                _visualRoot.localRotation = Quaternion.identity;

            SetRagdollState(false);
        }
        else
        {
            // -------------------------------------------------------------------
            // OPTION B : LE REDRESSEMENT FLUIDE (Interpolation sur quelques frames)
            // -------------------------------------------------------------------
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            // On désactive d'abord le ragdoll pour figer les os dans leur position actuelle de fin de chute
            SetRagdollState(false);

            float elapsedTime = 0f;
            while (elapsedTime < _smoothDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / _smoothDuration);

                // Interpolation douce de la position et de la rotation vers le sol redressé
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                if (_visualRoot != null)
                {
                    _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, Quaternion.identity, t);
                }

                yield return null;
            }

            // S'assure d'atteindre la valeur finale exacte
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            if (_visualRoot != null) _visualRoot.localRotation = Quaternion.identity;
        }
    }
}