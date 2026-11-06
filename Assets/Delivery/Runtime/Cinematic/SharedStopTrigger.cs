using PurrNet.Prediction;
using UnityEngine;

public struct SharedStopTriggerState : IPredictedData<SharedStopTriggerState>
{
    public int PlayCount;
    public float StoppedTime;
    public float Cooldown;
    /// <summary>Déjà déclenché par le pigeon encore posé : il faut que la zone se vide pour recommencer.</summary>
    public bool Triggered;

    public void Dispose() { }
}

/// <summary>
/// Statue commune à tous les joueurs : un pigeon qui se pose dans la zone (PigeonStopZone, pour sa position et ses réglages)
/// la déclenche pour tout le monde, et chacun reçoit le succès. La caméra de la cinématique ne bouge que pour ce joueur.
/// Ce composant reste hors de l'ancienne statue, qui est désactivée au réveil avec sa zone : c'est ce qui rend le réveil unique.
/// </summary>
public class SharedStopTrigger : PredictedIdentity<SharedStopTriggerState>
{
    [SerializeField] RoarCinematic _roar;
    [SerializeField] PigeonStopZone _zone;

    PredictedEvent<PredictedComponentID> _playedEvent;
    bool _lateJoinChecked;

    public PigeonStopZone Zone => _zone;
    public RoarCinematic Roar => _roar;

    protected override void LateAwake()
    {
        _playedEvent = new PredictedEvent<PredictedComponentID>(predictionManager, this);
        _playedEvent.AddListener(HandlePlayed);
    }

    protected override SharedStopTriggerState GetInitialState() => default;
    protected override void GetUnityState(ref SharedStopTriggerState s) { }
    protected override void SetUnityState(SharedStopTriggerState s) { }

    protected override void Simulate(ref SharedStopTriggerState s, float dt)
    {
        s.Cooldown = Mathf.Max(0f, s.Cooldown - dt);

        PigeonController pigeon = _zone != null && _zone.gameObject.activeInHierarchy ? FindStoppedPigeon() : null;
        if (pigeon == null)
        {
            s.StoppedTime = 0f;
            s.Triggered = false;
            return;
        }
        if (s.Triggered)
            return;

        s.StoppedTime += dt;
        if (s.StoppedTime < Zone.StopDuration || s.Cooldown > 0f)
            return;

        s.Triggered = true;
        s.PlayCount++;
        s.Cooldown = _roar != null ? _roar.TotalDuration : 0f;
        _playedEvent.Invoke(pigeon.id);
    }

    PigeonController FindStoppedPigeon()
    {
        float radius = Zone.Radius;
        Vector3 center = Zone.transform.position;
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            if (!ps.Stunned && !ps.Race.Active && !ps.FreeFall.Active
                && (ps.Position - center).sqrMagnitude <= radius * radius
                && ps.LastVelocity.magnitude <= Zone.StopSpeedThreshold)
                return pigeons[i];
        }
        return null;
    }

    void HandlePlayed(PredictedComponentID triggeredBy)
    {
        _lateJoinChecked = true;
        PigeonController local = PigeonController.Local;
        if (_roar != null)
            _roar.PlayShared(local != null && local.id.Equals(triggeredBy));
    }

    protected override void UpdateView(SharedStopTriggerState view, SharedStopTriggerState? verified)
    {
        // Arrivé après coup : la statue a déjà été réveillée, sans rejouer la cinématique ni donner le succès.
        if (_lateJoinChecked || !verified.HasValue)
            return;
        _lateJoinChecked = true;
        if (verified.Value.PlayCount > 0 && _roar != null)
            _roar.ShowAftermath();
    }
}
