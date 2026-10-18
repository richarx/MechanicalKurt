using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
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

    private Animator _animator;
    private AmbientWalker _walker;
    private Coroutine _hitRoutine;
    private bool _wasInside;
    private bool _ownsWalkerPause;
    private float _nextPigeonSearch;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _walker = GetComponentInParent<AmbientWalker>();
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

    private void OnDisable()
    {
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
}
