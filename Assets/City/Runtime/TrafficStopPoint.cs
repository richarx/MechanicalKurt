using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("")] // Legacy: use the point type on TrafficPath / WalkerPath.
public class TrafficStopPoint : MonoBehaviour
{
    [field: SerializeField, BoxGroup("Arret"), MinValue(0f), SuffixLabel("s", true)]
    public float MinWaitDuration { get; private set; } = 2f;

    [field: SerializeField, BoxGroup("Arret"), MinValue(nameof(MinWaitDuration)), SuffixLabel("s", true)]
    public float MaxWaitDuration { get; private set; } = 5f;

    [field: SerializeField, BoxGroup("Arret"), MinValue(0f), SuffixLabel("s", true)]
    public float Cooldown { get; private set; } = 10f;

    private bool _isInCooldown = false;


    public float RollWaitDuration()
    {
        if (_isInCooldown)
            return 0f;

        float toWait = Random.Range(MinWaitDuration, MaxWaitDuration);
        WaitCooldown().Forget();
        return toWait;
    }

    private async UniTaskVoid WaitCooldown()
    {
        _isInCooldown = true;
        try
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, Cooldown)),
                cancellationToken: destroyCancellationToken);
        }
        finally
        {
            _isInCooldown = false;
        }
    }

    private void OnDrawGizmos()
    {
        if (!isActiveAndEnabled)
            return;

        Color previousColor = Gizmos.color;
        if (_isInCooldown)
            Gizmos.color = Color.green;
        else
            Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.6f);
        Gizmos.color = previousColor;
    }
}
