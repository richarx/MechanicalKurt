using PurrNet.Prediction;

/// <summary>Participation d'un pigeon aux départs de MissionStartZone et aux cinématiques partagées (partie de PigeonState).</summary>
public struct PigeonMissionState
{
    /// <summary>Interact appuyé pendant ce tick.</summary>
    public bool InteractPressed;
    public bool HasReadyZone;
    public PredictedComponentID ReadyZone;
    /// <summary>Gel posé par FreezingZone pendant son décompte : c'est elle qui le lève.</summary>
    public bool FrozenByZone;
    public PredictedComponentID FreezingZone;
    public PredictedComponentID LastStartZone;
    public int LastStartCount;
    /// <summary>Dernière cinématique partagée finie (zone ou course, numéro de départ).</summary>
    public PredictedComponentID CinematicAckZone;
    public int CinematicAckCount;

    public bool IsReadyFor(PredictedComponentID zone) => HasReadyZone && ReadyZone.Equals(zone);

    public void SetReady(PredictedComponentID zone, bool ready)
    {
        HasReadyZone = ready;
        if (ready)
            ReadyZone = zone;
    }

    public bool IsInStart(PredictedComponentID zone, int count) => LastStartCount == count && LastStartZone.Equals(zone);

    /// <summary>Numéro du dernier départ de cette zone auquel le pigeon a participé, 0 = aucun.</summary>
    public int StartCountFor(PredictedComponentID zone) => LastStartZone.Equals(zone) ? LastStartCount : 0;

    public void MarkStarted(PredictedComponentID zone, int count)
    {
        HasReadyZone = false;
        LastStartZone = zone;
        LastStartCount = count;
    }

    public bool HasFinishedCinematic(PredictedComponentID owner, int count)
        => CinematicAckCount == count && CinematicAckZone.Equals(owner);

    public bool IsFrozenBy(PredictedComponentID zone) => FrozenByZone && FreezingZone.Equals(zone);
}

public partial class PigeonController
{
    PredictedComponentID _cinematicAckZone;
    int _cinematicAckCount;

    /// <summary>Le joueur a validé une zone de départ (lu depuis la vue).</summary>
    public bool WantsStart => _viewState.Mission.HasReadyZone;

    /// <summary>Le joueur a validé cette zone de départ (lu depuis la vue).</summary>
    public bool IsReadyFor(PredictedComponentID zone) => _viewState.Mission.IsReadyFor(zone);

    /// <summary>Vrai si ce pigeon a participé au départ numéro <paramref name="count"/> de cette zone.</summary>
    public bool WasStartedBy(PredictedComponentID zone, int count)
        => _viewState.Mission.IsInStart(zone, count) || currentState.Mission.IsInStart(zone, count);

    /// <summary>Numéro du dernier départ de cette zone auquel ce pigeon a participé (0 = aucun). État simulé d'abord : la vue peut retarder d'un tick.</summary>
    public int LastStartCountFor(PredictedComponentID zone)
    {
        int simulated = currentState.Mission.StartCountFor(zone);
        return simulated != 0 ? simulated : _viewState.Mission.StartCountFor(zone);
    }

    /// <summary>Ce pigeon a fini la cinématique du départ numéro <paramref name="count"/> (vue, ou demande en cours).</summary>
    public bool HasFinishedCinematic(PredictedComponentID owner, int count)
        => _viewState.Mission.HasFinishedCinematic(owner, count)
        || (_cinematicAckCount == count && _cinematicAckZone.Equals(owner));

    /// <summary>
    /// Signale la fin d'une cinématique partagée : départ (owner = la MissionStartZone) ou arrivée d'une course
    /// (owner = la course). Passe par l'input : l'orchestrateur avance quand tous les participants l'ont signalée.
    /// Un seul accusé à la fois (le dernier remplace le précédent). Ignorée si ce pigeon n'est pas contrôlé ici.
    /// </summary>
    public void AcknowledgeCinematic(PredictedComponentID owner, int count)
    {
        if (!isController)
            return;
        _cinematicAckZone = owner;
        _cinematicAckCount = count;
    }

    /// <summary>Gèle le pigeon pour le décompte de cette zone ; seule elle pourra le libérer (ReleaseFrom).</summary>
    [SimulationOnly]
    public void FreezeFor(PredictedComponentID zone)
    {
        ForceFreeze(true);
        ref PigeonMissionState mission = ref currentState.Mission;
        mission.FrozenByZone = true;
        mission.FreezingZone = zone;
    }

    [SimulationOnly]
    public void ReleaseFrom(PredictedComponentID zone)
    {
        ref PigeonMissionState mission = ref currentState.Mission;
        if (!mission.IsFrozenBy(zone))
            return;
        mission.FrozenByZone = false;
        ForceFreeze(false);
    }

    void WriteMissionInput(ref PigeonInput input)
    {
        input.CinematicAckZone = _cinematicAckZone;
        input.CinematicAckCount = _cinematicAckCount;
    }

    static void ApplyMissionInput(in PigeonInput input, ref PigeonMissionState mission)
    {
        mission.InteractPressed = input.Interact;
        if (input.CinematicAckCount == 0)
            return;
        mission.CinematicAckZone = input.CinematicAckZone;
        mission.CinematicAckCount = input.CinematicAckCount;
    }
}
