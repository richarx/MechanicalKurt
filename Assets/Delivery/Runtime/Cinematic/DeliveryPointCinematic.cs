using UnityEngine;

// Keep this on the DeliveryPoint, outside the selection visual that hides on pickup.
[DisallowMultipleComponent]
[RequireComponent(typeof(DeliveryPoint))]
public sealed class DeliveryPointCinematic : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private string _sendOffTrigger = "SendOff";
    [SerializeField, Min(0f), Tooltip("How long to keep the shot after triggering the animation, including its transition.")]
    private float _sendOffSeconds = 1f;

    public float SendOffSeconds => _sendOffSeconds;

    public void PlaySendOff()
    {
        if (_animator == null) return;
        int hash = Animator.StringToHash(_sendOffTrigger);
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.nameHash != hash || parameter.type != AnimatorControllerParameterType.Trigger) continue;
            _animator.SetTrigger(hash);
            return;
        }
        Debug.LogWarning("Assign a SendOff trigger matching the NPC Animator parameter.", this);
    }
}
