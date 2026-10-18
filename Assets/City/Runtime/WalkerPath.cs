using UnityEngine;

[DisallowMultipleComponent]
public class WalkerPath : CityPath
{
    public Vector3 GetPosition(int index) => GetPointPosition(index);

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

    protected override void Reset()
    {
        base.Reset();
        SetGizmoColor(new Color(0.3f, 0.9f, 0.5f, 1f));
    }
}
