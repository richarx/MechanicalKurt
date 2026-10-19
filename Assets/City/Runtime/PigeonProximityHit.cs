using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PigeonProximityHit : MonoBehaviour
{
    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 2f;

    [field: SerializeField, MinValue(0f), SuffixLabel("unités/s", true)]
    [field: Tooltip("Vitesse minimale du pigeon à l'entrée dans le rayon. Mettre 0 pour ignorer la vitesse.")]
    public float MinimumSpeed { get; private set; } = 5f;

    [field: SerializeField, Tooltip("Trouvé automatiquement si laissé vide.")]
    public PigeonController Pigeon { get; private set; }

    [field: SerializeField, Required, Tooltip("Nom de l'état de hit dans l'Animator (ou chemin complet si nécessaire).")]
    public string HitStateName { get; private set; } = "Hit";

    [field: SerializeField, MinValue(0)]
    public int AnimationLayer { get; private set; }

    [field: SerializeField, Tooltip("Bassin animé dont le déplacement horizontal est transféré au personnage. Détecté automatiquement pour les personnages du projet.")]
    public Transform MotionRoot { get; private set; }

    private Animator _animator;
    private AmbientWalker _walker;
    private Coroutine _hitRoutine;
    private bool _wasInside;
    private bool _ownsWalkerPause;
    private float _nextPigeonSearch;
    private bool _trackingHitMotion;
    private bool _hitMotionEntered;
    private bool _hitMotionExiting;
    private Vector3 _motionReferencePosition;
    private Vector3 _previousMotionOffset;
    private float _lastMotionTime;
    private float _lastTransitionTime;

    [SerializeField] private List<AudioClip> hitAudioClips;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _walker = GetComponentInParent<AmbientWalker>();
        if (MotionRoot == null)
        {
            MotionRoot = _animator.isHuman
                ? _animator.GetBoneTransform(HumanBodyBones.Hips)
                : transform.Find("Armature/mixamorig:Hips");
        }
    }

    private void Start()
    {
        if (_animator.runtimeAnimatorController != null &&
            AnimationLayer >= 0 && AnimationLayer < _animator.layerCount &&
            !string.IsNullOrWhiteSpace(HitStateName))
        {
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == HitTrigger && parameter.type == AnimatorControllerParameterType.Trigger)
                    return;
            }
        }

        Debug.LogWarning("PigeonProximityHit : configurer un trigger 'Hit', un état de hit et une couche valides dans l'Animator.", this);
        enabled = false;
    }

    private void LateUpdate()
    {
        ApplyHitMotion();

        if (Pigeon == null && Time.time >= _nextPigeonSearch)
        {
            Pigeon = FindAnyObjectByType<PigeonController>();
            _nextPigeonSearch = Time.time + 1f;
        }

        bool inside = Pigeon != null && Pigeon.isActiveAndEnabled &&
                      (Pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius;

        if (inside && !_wasInside && Pigeon.Speed >= MinimumSpeed &&
            _hitRoutine == null && _animator.isActiveAndEnabled)
            _hitRoutine = StartCoroutine(PlayHit());

        _wasInside = inside;
    }

    private IEnumerator PlayHit()
    {
        // Pause without disabling the walker, preserving its route and waiting time.
        if (_walker != null && _walker.isActiveAndEnabled && !_walker.IsPaused)
        {
            _ownsWalkerPause = true;
            _walker.SetPaused(true);
        }

        BeginHitMotion();
        PlayHitSound();

        _animator.SetTrigger(HitTrigger);
        yield return null;

        // Allow the Animator to consume the trigger and complete the entry transition.
        float entryWait = 0f;
        while (_animator.isActiveAndEnabled &&
               !_animator.GetCurrentAnimatorStateInfo(AnimationLayer).IsName(HitStateName) &&
               entryWait < 2f)
        {
            yield return null;
            entryWait += Time.deltaTime;
        }

        if (_animator.isActiveAndEnabled &&
            !_animator.GetCurrentAnimatorStateInfo(AnimationLayer).IsName(HitStateName))
        {
            _trackingHitMotion = false;
            _animator.ResetTrigger(HitTrigger);
            Debug.LogWarning($"PigeonProximityHit : l'état '{HitStateName}' n'a pas démarré. Vérifier son nom et la transition avec le trigger 'Hit'.", this);
        }

        while (_animator.isActiveAndEnabled)
        {
            AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
            if (!state.IsName(HitStateName) ||
                (!_animator.IsInTransition(AnimationLayer) && state.normalizedTime >= 1f))
                break;

            yield return null;
        }

        ResumeWalker();
        _hitRoutine = null;
    }

    private void BeginHitMotion()
    {
        if (MotionRoot == null || MotionRoot == transform || !MotionRoot.IsChildOf(transform))
        {
            Debug.LogWarning("PigeonProximityHit : assigner le bassin animé dans Motion Root pour conserver le recul du hit.", this);
            return;
        }

        _motionReferencePosition = transform.InverseTransformPoint(MotionRoot.position);
        _previousMotionOffset = Vector3.zero;
        _lastMotionTime = float.NaN;
        _lastTransitionTime = float.NaN;
        _hitMotionEntered = false;
        _hitMotionExiting = false;
        _trackingHitMotion = true;
    }

    private void ApplyHitMotion()
    {
        if (!_trackingHitMotion || MotionRoot == null || !_animator.isActiveAndEnabled)
            return;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
        bool transitioning = _animator.IsInTransition(AnimationLayer);
        bool currentIsHit = state.IsName(HitStateName);
        bool nextIsHit = transitioning && _animator.GetNextAnimatorStateInfo(AnimationLayer).IsName(HitStateName);
        if (!currentIsHit && !nextIsHit)
        {
            if (_hitMotionEntered)
                _trackingHitMotion = false;
            return;
        }

        _hitMotionEntered = true;
        float transitionTime = transitioning ? _animator.GetAnimatorTransitionInfo(AnimationLayer).normalizedTime : -1f;
        // An unevaluated Animator still has the compensated pose from the last frame.
        if (state.normalizedTime == _lastMotionTime && transitionTime == _lastTransitionTime)
            return;

        _lastMotionTime = state.normalizedTime;
        _lastTransitionTime = transitionTime;

        if (currentIsHit && ((transitioning && !nextIsHit) || (!transitioning && state.normalizedTime >= 1f)))
            _hitMotionExiting = true;

        Vector3 localOffset = transform.InverseTransformPoint(MotionRoot.position) - _motionReferencePosition;
        Vector3 offset = Vector3.ProjectOnPlane(transform.TransformVector(localOffset), Vector3.up);

        // Preserve the recoil on the gameplay object, keeping its original ground height.
        // During the exit blend, discard the animated return to the walking pose.
        if (!_hitMotionExiting)
        {
            Transform character = _walker != null ? _walker.transform : transform;
            character.position += offset - _previousMotionOffset;
        }

        _previousMotionOffset = offset;
        MotionRoot.position -= offset;
    }

    private void OnDisable()
    {
        _trackingHitMotion = false;
        if (_hitRoutine != null)
        {
            StopCoroutine(_hitRoutine);
            _hitRoutine = null;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                _animator.ResetTrigger(HitTrigger);
        }

        ResumeWalker();
        _wasInside = false;
    }

    private void ResumeWalker()
    {
        if (_ownsWalkerPause && _walker != null)
            _walker.SetPaused(false);

        _ownsWalkerPause = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Radius);
    }

    private void PlayHitSound()
    {
        if (hitAudioClips == null || hitAudioClips.Count == 0) return;

        AudioClip clip = hitAudioClips[Random.Range(0, hitAudioClips.Count)];
        SFXManager.Instance.PlaySFX(clip);    
    }
}
