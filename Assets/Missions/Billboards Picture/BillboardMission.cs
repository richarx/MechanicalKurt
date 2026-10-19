using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class BillboardMission : MonoBehaviour
{
    [field: SerializeField]
    [field: Tooltip("Zones à valider, dans n'importe quel ordre. Elles sont désactivées jusqu'à Launch().")]
    public List<MissionZone> Zones { get; private set; } = new List<MissionZone>();

    [field: SerializeField]
    [field: Tooltip("(zones validées, total)")]
    public UnityEvent<int, int> OnProgress { get; private set; } = new UnityEvent<int, int>();

    [field: SerializeField]
    public UnityEvent OnCompleted { get; private set; } = new UnityEvent();

    private readonly HashSet<MissionZone> _completedZones = new HashSet<MissionZone>();

    [ShowInInspector, ReadOnly]
    public bool IsRunning { get; private set; }

    [ShowInInspector, ReadOnly]
    public int CompletedCount => _completedZones.Count;

    public int TotalCount => Zones.Count;

    private void Awake()
    {
        SetZonesActive(false);
    }

    [Button, EnableIf("@UnityEngine.Application.isPlaying")]
    public void Launch()
    {
        if (Zones.Count == 0)
        {
            Debug.LogWarning("BillboardMission : aucune zone assignée.", this);
            return;
        }

        _completedZones.Clear();
        IsRunning = true;
        SetZonesActive(true);
        OnProgress.Invoke(CompletedCount, TotalCount);
    }

    // Appelé par MissionZone quand le joueur valide la zone.
    public void ReportZoneCompleted(MissionZone zone)
    {
        if (!IsRunning || zone == null || !Zones.Contains(zone) || !_completedZones.Add(zone))
            return;

        OnProgress.Invoke(CompletedCount, TotalCount);

        if (CompletedCount >= TotalCount)
        {
            IsRunning = false;
            OnCompleted.Invoke();
        }
    }

    private void SetZonesActive(bool active)
    {
        foreach (MissionZone zone in Zones)
            if (zone != null)
                zone.gameObject.SetActive(active);
    }
}
