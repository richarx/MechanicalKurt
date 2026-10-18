using UnityEngine;

[DisallowMultipleComponent]
public sealed class WalkerPath : MonoBehaviour
{
    [SerializeField] private Color _gizmoColor = new Color(0.3f, 0.9f, 0.5f, 1f);

    // Direct children define the loop in their Hierarchy order.
    public int PointCount => transform.childCount;

    public Vector3 GetPosition(int index)
    {
        return transform.GetChild(index).position;
    }

    public int GetClosestPointIndex(Vector3 position)
    {
        int closestIndex = 0;
        float closestDistance = float.PositiveInfinity;
        for (int i = 0; i < PointCount; i++)
        {
            float distance = (GetPosition(i) - position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = _gizmoColor;
        for (int i = 0; i < PointCount; i++)
        {
            Vector3 position = GetPosition(i);
            Gizmos.DrawWireSphere(position, 0.15f);
            if (PointCount > 1)
                Gizmos.DrawLine(position, GetPosition((i + 1) % PointCount));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 0.25f, $"{i + 1}");
#endif
        }
        Gizmos.color = previousColor;
    }
}
