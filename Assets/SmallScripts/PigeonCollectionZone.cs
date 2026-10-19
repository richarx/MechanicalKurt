using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class PigeonCollectionZone : MonoBehaviour
{
    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 2f;

    [ShowInInspector, ReadOnly]
    public bool IsAvailable { get; private set; }

    // L'objet à ramasser et son repère visuel doivent être enfants de cette zone.
    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        gameObject.SetActive(available);
    }

    public bool TryCollect(Vector3 previousPosition, Vector3 currentPosition)
    {
        if (!IsAvailable || !isActiveAndEnabled)
            return false;

        // Teste aussi le trajet entre deux frames pour ne pas rater un passage rapide.
        Vector3 movement = currentPosition - previousPosition;
        float progress = movement.sqrMagnitude > 0f
            ? Mathf.Clamp01(Vector3.Dot(transform.position - previousPosition, movement) / movement.sqrMagnitude)
            : 0f;
        Vector3 closestPosition = previousPosition + movement * progress;
        if ((closestPosition - transform.position).sqrMagnitude > Radius * Radius)
            return false;

        SetAvailable(false);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, Radius);
        Gizmos.color = previousColor;
    }
}
