using System;
using System.Collections;
using Sirenix.OdinInspector;
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

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    public float RoarToDieDelay { get; private set; } = 1f;

    [ShowInInspector, ReadOnly] public bool IsPlaying { get; private set; }

    private Coroutine _routine;

    // Conserve le nom pour les UnityEvents deja branches.
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled || IsPlaying)
            return;

        ActorAnimator.gameObject.SetActive(true);
        _oldGameObject.SetActive(false);

        if (!HasTrigger(ActorAnimator, RoarHash))
        {
            Debug.LogWarning("RoarCinematic : assigner un Animator actif avec le trigger Roar.", this);
            return;
        }

        IsPlaying = true;
        _routine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
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

        IsPlaying = false;
        _routine = null;
    }

    public void CancelCinematic()
    {
        if (_routine != null)
            StopCoroutine(_routine);
        _routine = null;
        IsPlaying = false;
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
