using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class CityPath : MonoBehaviour
{
    [field: SerializeField, DisableInPlayMode]
    [field: ListDrawerSettings(ShowIndexLabels = true, CustomAddFunction = nameof(CreateWaypoint))]
    public List<CityPathPoint> Waypoints { get; private set; } = new List<CityPathPoint>();

    [field: SerializeField, FormerlySerializedAs("_gizmoColor")]
    public Color GizmoColor { get; private set; } = new Color(1f, 0.65f, 0.15f, 1f);

    [field: SerializeField, FormerlySerializedAs("_showGizmos")]
    public bool ShowGizmos { get; private set; }

    [field: SerializeField, HideInInspector]
    public bool UsesStoredPoints { get; private set; }

    public int PointCount => Waypoints.Count;

    public CityPathPoint GetPoint(int index)
    {
        return Waypoints[index];
    }

    public Vector3 GetPointPosition(int index) => transform.TransformPoint(GetPoint(index).Position);

    protected CityPathPoint CreateWaypoint()
    {
        UsesStoredPoints = true;
        Vector3 position = Waypoints.Count > 0 ? Waypoints[Waypoints.Count - 1].Position + Vector3.forward * 2f : Vector3.zero;
        return new CityPathPoint(position);
    }

    public void AddPoint(Vector3 worldPosition)
    {
        UsesStoredPoints = true;
        Waypoints.Add(new CityPathPoint(transform.InverseTransformPoint(worldPosition)));
        InvalidatePath();
    }

    public void UpdatePointFromEditor(int index, Vector3 localPosition)
    {
        Waypoints[index].SetPosition(localPosition);
        InvalidatePath();
    }

    public void RemovePoint(int index)
    {
        Waypoints.RemoveAt(index);
        InvalidatePath();
    }


    protected virtual void OnEnable()
    {
        if (!Application.isPlaying)
            return;
        foreach (CityPathPoint point in Waypoints)
            point.ResetRuntimeState();
    }

    protected virtual void OnValidate() => InvalidatePath();
    public virtual void InvalidatePath() { }

    protected virtual void Reset()
    {
        UsesStoredPoints = true;
    }

    protected void SetGizmoColor(Color color) => GizmoColor = color;

    private void OnDrawGizmos()
    {
        if (ShowGizmos)
            DrawPathGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!ShowGizmos)
            DrawPathGizmos();
    }

    private void DrawPathGizmos()
    {
        Color previousColor = Gizmos.color;
        for (int i = 0; i < PointCount; i++)
        {
            CityPathPoint point = GetPoint(i);
            Vector3 position = transform.TransformPoint(point.Position);
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(position, 0.2f);
            if (PointCount > 1)
            {
                Vector3 next = GetPointPosition((i + 1) % PointCount);
                Gizmos.DrawLine(position, next);
                Vector3 direction = (next - position).normalized;
                Vector3 middle = (position + next) * 0.5f;
                Vector3 back = -direction * 0.7f;
                Vector3 side = Vector3.Cross(direction, Vector3.up) * 0.3f;
                Gizmos.DrawLine(middle, middle + back + side);
                Gizmos.DrawLine(middle, middle + back - side);
            }
            if (point.HasStop || point.HasTrafficLight)
            {
                Gizmos.color = point.HasTrafficLight
                    ? (point.IsStop ? Color.red : Color.green)
                    : (point.IsInCooldown ? Color.green : Color.red);
                Gizmos.DrawWireCube(position + Vector3.up * 0.6f, Vector3.one * 0.6f);
            }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(position + Vector3.up * 0.4f, $"{i}");
#endif
        }
        Gizmos.color = previousColor;
    }
}
