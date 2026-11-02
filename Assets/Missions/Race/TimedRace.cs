using System.Collections.Generic;
using System.Text;
using PurrNet.Prediction;
using TMPro;
using UnityEngine;

public struct TimedRaceState : IPredictedData<TimedRaceState>
{
    public TimedRace.RacePhase Phase;
    /// <summary>Numéro du départ de la zone que cette course suit (identifie ses coureurs).</summary>
    public int StartCount;
    public float Elapsed;
    /// <summary>Temps laissé aux autres après la première arrivée (0 tant que personne n'est arrivé).</summary>
    public float Grace;
    public float ResultsRemaining;
    /// <summary>Coureurs arrivés : la place du prochain.</summary>
    public byte Finished;
    /// <summary>Meilleur temps de cette course (0 = aucun), gardé par l'hôte d'une session à l'autre.</summary>
    public float BestTime;
    /// <summary>Départ pendant lequel le record a été battu (affiche « New record! »).</summary>
    public int RecordStartCount;

    public void Dispose() { }
}

/// <summary>Progression d'un pigeon dans la dernière course chronométrée courue (partie de PigeonState).</summary>
public struct RaceProgress
{
    public PredictedComponentID Race;
    public int StartCount;
    /// <summary>Indice du prochain anneau à passer ; égal au nombre d'anneaux une fois arrivé.</summary>
    public byte Next;
    /// <summary>Place à l'arrivée, 0 = pas (encore) arrivé.</summary>
    public byte Place;
    public float FinishTime;
    /// <summary>A quitté l'écran des résultats (Interact).</summary>
    public bool Dismissed;
    /// <summary>En course, du départ jusqu'à la sortie du classement : les autres missions l'ignorent.</summary>
    public bool Active;
    /// <summary>Position en fin du tick précédent : le passage d'un anneau se teste sur le segment parcouru.</summary>
    public Vector3 PreviousPosition;

    public bool IsIn(PredictedComponentID race, int startCount) => startCount > 0 && StartCount == startCount && Race.Equals(race);
}

/// <summary>
/// Course chronométrée entre joueurs, prédite. Organisation : l'objet de la course porte ce composant ; un enfant
/// « Départ » porte la MissionStartZone (règle AnyPigeon, sans cinématique) et son cercle rouge ; les enfants de
/// « Points de passage » sont les anneaux (RaceCheckpoint) dans l'ordre, le dernier étant l'arrivée. Pour une nouvelle
/// course : dupliquer l'objet, déplacer le départ et les anneaux (le tracé s'affiche dans la vue Scene).
/// À la première arrivée, les autres ont <see cref="GraceSeconds"/> pour finir ; puis chacun voit le classement et le
/// quitte avec Interact.
/// </summary>
public class TimedRace : PredictedIdentity<TimedRaceState>
{
    public enum RacePhase : byte { Idle, Racing, Results }

    [Header("Départ")]
    [SerializeField] MissionStartZone _departureZone;
    [Tooltip("Cercle de départ, affiché quand la course attend des joueurs.")]
    [SerializeField] GameObject _departureMarker;

    [Header("Parcours")]
    [Tooltip("Parent des anneaux : ses enfants RaceCheckpoint, dans l'ordre de la hiérarchie, forment le parcours ; le dernier est l'arrivée.")]
    [SerializeField] Transform _checkpointsRoot;
    [Tooltip("Nombre d'anneaux suivants visibles (en transparence décroissante) après le prochain.")]
    [SerializeField, Range(0, 6)] int _upcomingVisible = 3;
    [Tooltip("Opacité du premier anneau suivant ; chaque anneau plus loin l'est moins.")]
    [SerializeField, Range(0f, 1f)] float _upcomingAlpha = 0.45f;

    [Header("Fin")]
    [Tooltip("Temps laissé aux autres coureurs après la première arrivée.")]
    [SerializeField, Min(1f)] float _graceSeconds = 30f;
    [Tooltip("Durée maximale de l'écran des résultats, si un joueur ne le quitte pas.")]
    [SerializeField, Min(5f)] float _resultsTimeout = 90f;
    [Tooltip("Au-delà, un déplacement en un tick (téléportation, réapparition) ne compte pas comme passage d'anneau.")]
    [SerializeField, Min(1f)] float _maxStepPerTick = 10f;

    [Tooltip("Clé du record enregistré chez l'hôte. Vide : nom de la scène et de la course (à garder unique en dupliquant).")]
    [SerializeField] string _recordKey;

    [Header("Présentation")]
    [Tooltip("Boussole qui montre le prochain anneau au coureur local. Vide : celle de la scène.")]
    [SerializeField] DeliveryCompassArrow _compass;
    [SerializeField] Color _compassTop = new Color(1f, 0.25f, 0.2f);
    [SerializeField] Color _compassBottom = new Color(0.65f, 0.05f, 0.05f);
    [SerializeField] TMP_FontAsset _font;
    [SerializeField] AudioClip _checkpointClip;
    [SerializeField] AudioClip _finishClip;

    RaceCheckpoint[] _checkpoints = new RaceCheckpoint[0];
    TimedRaceState _viewState;
    RaceHud _hud;
    int _lastLocalNext = -1;
    int _lastLocalStart;
    readonly List<PigeonController> _viewRacers = new List<PigeonController>();

    public MissionStartZone DepartureZone => _departureZone;
    public IReadOnlyList<RaceCheckpoint> Checkpoints => _checkpoints;
    public RacePhase Phase => _viewState.Phase;
    public int StartCount => _viewState.StartCount;
    public float GraceSeconds => _graceSeconds;
    public float GraceRemaining => _viewState.Grace;
    /// <summary>Textes affichés au joueur local (null si masqués), pour les tests.</summary>
    public string HudText => _hud != null ? _hud.RaceText : null;
    public string ResultsText => _hud != null ? _hud.ResultsText : null;

#if UNITY_EDITOR
    /// <summary>Scénarios : raccourcit l'attente après la première arrivée. À faire sur chaque machine.</summary>
    public void SetGraceForTests(float seconds) => _graceSeconds = seconds;
#endif

    protected override void LateAwake()
    {
        CollectCheckpoints();
        foreach (RaceCheckpoint checkpoint in _checkpoints)
            checkpoint.Hide();
    }

    void CollectCheckpoints()
    {
        var list = new List<RaceCheckpoint>();
        if (_checkpointsRoot != null)
            foreach (Transform child in _checkpointsRoot)
                if (child.TryGetComponent(out RaceCheckpoint checkpoint))
                    list.Add(checkpoint);
        _checkpoints = list.ToArray();
    }

    protected override void Destroyed()
    {
        MissionMarkers.SetHidden(false);
        if (_hud != null)
            Destroy(_hud.gameObject);
    }

    // L'hôte part de son record enregistré ; les clients le reçoivent avec l'état de la course.
    protected override TimedRaceState GetInitialState() => new TimedRaceState
    {
        Phase = RacePhase.Idle,
        BestTime = isServer ? SavedBestTime : 0f
    };

    /// <summary>Clé du record dans les PlayerPrefs de l'hôte : propre à chaque course de la scène.</summary>
    public string RecordKey => string.IsNullOrEmpty(_recordKey) ? $"TimedRace.Best.{gameObject.scene.name}.{name}" : _recordKey;

    public float SavedBestTime
    {
        get => PlayerPrefs.GetFloat(RecordKey, 0f);
        set
        {
            if (value <= 0f)
                PlayerPrefs.DeleteKey(RecordKey);
            else
                PlayerPrefs.SetFloat(RecordKey, value);
            PlayerPrefs.Save();
        }
    }

    public float BestTime => _viewState.BestTime;

#if UNITY_EDITOR
    /// <summary>Scénarios (hôte) : remplace le record de la session, sans toucher à celui enregistré.</summary>
    [SimulationOnly]
    public void SetBestTimeForTests(float seconds)
    {
        currentState.BestTime = seconds;
        currentState.RecordStartCount = 0;
        _savedThisSession = seconds;
    }
#endif

    protected override void GetUnityState(ref TimedRaceState s) { }
    protected override void SetUnityState(TimedRaceState s) { }

    protected override TimedRaceState Interpolate(TimedRaceState from, TimedRaceState to, float t)
    {
        TimedRaceState result = to;
        if (from.Phase == to.Phase && from.StartCount == to.StartCount)
        {
            result.Elapsed = Mathf.Lerp(from.Elapsed, to.Elapsed, t);
            if (from.Finished > 0)
                result.Grace = Mathf.Lerp(from.Grace, to.Grace, t);
        }
        return result;
    }

    protected override void Simulate(ref TimedRaceState s, float dt)
    {
        if (_departureZone == null || _checkpoints.Length == 0)
            return;
        switch (s.Phase)
        {
            case RacePhase.Idle:
                ref MissionStartState zone = ref _departureZone.SimState;
                if (zone.Phase == MissionStartZone.StartPhase.Started && zone.StartCount != s.StartCount)
                    Begin(ref s, zone.StartCount);
                break;
            case RacePhase.Racing:
                SimulateRacing(ref s, dt);
                break;
            case RacePhase.Results:
                SimulateResults(ref s, dt);
                break;
        }
    }

    void Begin(ref TimedRaceState s, int startCount)
    {
        s.Phase = RacePhase.Racing;
        s.StartCount = startCount;
        s.Elapsed = 0f;
        s.Grace = 0f;
        s.Finished = 0;
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            if (!_departureZone.IsInCurrentStart(pigeons[i]))
                continue;
            ref PigeonState ps = ref pigeons[i].SimState;
            ps.Race = new RaceProgress { Race = id, StartCount = startCount, PreviousPosition = ps.Position, Active = true };
        }
    }

    void SimulateRacing(ref TimedRaceState s, float dt)
    {
        s.Elapsed += dt;
        int racers = 0, finished = 0;
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            if (!ps.Race.IsIn(id, s.StartCount))
                continue;
            racers++;
            ref RaceProgress race = ref ps.Race;
            if (race.Place == 0 && race.Next < _checkpoints.Length
                && (ps.Position - race.PreviousPosition).sqrMagnitude <= _maxStepPerTick * _maxStepPerTick
                && _checkpoints[race.Next].Crossed(race.PreviousPosition, ps.Position))
            {
                race.Next++;
                if (race.Next >= _checkpoints.Length)
                {
                    s.Finished++;
                    race.Place = s.Finished;
                    race.FinishTime = s.Elapsed;
                    if (s.BestTime <= 0f || s.Elapsed < s.BestTime)
                    {
                        s.BestTime = s.Elapsed;
                        s.RecordStartCount = s.StartCount;
                    }
                    if (s.Finished == 1)
                        s.Grace = _graceSeconds;
                }
            }
            race.PreviousPosition = ps.Position;
            if (race.Place > 0)
                finished++;
        }

        if (racers == 0)
        {
            Reset(ref s);
            return;
        }
        if (s.Finished > 0)
            s.Grace = Mathf.Max(0f, s.Grace - dt);
        if (finished == racers || (s.Finished > 0 && s.Grace <= 0f))
        {
            s.Phase = RacePhase.Results;
            s.ResultsRemaining = _resultsTimeout;
        }
    }

    void SimulateResults(ref TimedRaceState s, float dt)
    {
        s.ResultsRemaining -= dt;
        int racers = 0, dismissed = 0;
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            if (!ps.Race.IsIn(id, s.StartCount))
                continue;
            racers++;
            if (ps.Mission.InteractPressed && !ps.Race.Dismissed)
            {
                ps.Race.Dismissed = true;
                ps.Race.Active = false;
                // Consommé : ce même appui ne doit pas rendre le pigeon prêt dans la zone qui rouvre (il y est peut-être).
                ps.Mission.InteractPressed = false;
            }
            if (ps.Race.Dismissed)
                dismissed++;
        }
        if (racers == 0 || dismissed == racers || s.ResultsRemaining <= 0f)
            Reset(ref s);
    }

    void Reset(ref TimedRaceState s)
    {
        var pigeons = PigeonController.All;
        for (int i = 0; i < pigeons.Count; i++)
        {
            ref PigeonState ps = ref pigeons[i].SimState;
            if (ps.Race.IsIn(id, s.StartCount))
                ps.Race.Active = false;
        }
        s.Phase = RacePhase.Idle;
        s.Elapsed = 0f;
        s.Grace = 0f;
        _departureZone.ResetToWaiting();
    }

    // ---------- Présentation ----------

    float _savedThisSession = -1f;

    protected override void UpdateView(TimedRaceState view, TimedRaceState? verified)
    {
        _viewState = view;
        SaveRecord(view.BestTime);
        PigeonController local = PigeonController.Local;
        bool localRacer = local != null && view.Phase != RacePhase.Idle && local.RaceProgress.IsIn(id, view.StartCount);

        if (_departureMarker != null)
        {
            bool showMarker = view.Phase == RacePhase.Idle && _departureZone != null
                && _departureZone.Phase is MissionStartZone.StartPhase.Waiting or MissionStartZone.StartPhase.CountingDown;
            if (_departureMarker.activeSelf != showMarker)
                _departureMarker.SetActive(showMarker);
        }

        RaceProgress progress = localRacer ? local.RaceProgress : default;
        if (local != null && local.RaceProgress.IsIn(id, view.StartCount))
            MissionMarkers.SetHidden(local.IsInTimedRace, _departureMarker);
        else if (local == null || !local.IsInTimedRace)
            MissionMarkers.SetHidden(false);
        int next = localRacer && view.Phase == RacePhase.Racing && progress.Place == 0 ? progress.Next : -1;
        UpdateCheckpoints(next);
        UpdateCompass(next >= 0 && next < _checkpoints.Length ? _checkpoints[next].transform : null, local);
        PlayLocalFeedback(localRacer ? view.StartCount : 0, progress);

        if (!localRacer || (view.Phase == RacePhase.Results && progress.Dismissed))
        {
            if (_hud != null)
                _hud.HideAll();
            return;
        }
        if (_hud == null)
            _hud = RaceHud.Create(_font);
        CollectViewRacers(view.StartCount);
        if (view.Phase == RacePhase.Racing)
        {
            _hud.ShowRace(BuildRaceText(view, local, progress));
            _hud.HideResults();
        }
        else if (!ViewsCaughtUp())
        {
            // La course peut passer aux résultats une image avant que la vue d'un pigeon n'ait son arrivée.
            _hud.HideRace();
        }
        else
        {
            _hud.HideRace();
            _hud.ShowResults(BuildResultsText(local));
        }
    }

    /// <summary>L'hôte enregistre un nouveau record, s'il bat celui déjà enregistré.</summary>
    void SaveRecord(float best)
    {
        if (!isServer || best <= 0f || Mathf.Approximately(best, _savedThisSession))
            return;
        _savedThisSession = best;
        float saved = SavedBestTime;
        if (saved <= 0f || best < saved)
            SavedBestTime = best;
    }

    /// <summary>Le prochain anneau est opaque, les suivants de plus en plus transparents ; -1 masque tout.</summary>
    void UpdateCheckpoints(int next)
    {
        for (int i = 0; i < _checkpoints.Length; i++)
        {
            int ahead = i - next;
            if (next < 0 || ahead < 0 || ahead > _upcomingVisible)
                _checkpoints[i].Hide();
            else
                _checkpoints[i].Show(ahead == 0, _upcomingAlpha * Mathf.Pow(0.55f, ahead - 1));
        }
    }

    Transform _compassTarget;

    /// <summary>La boussole (en rouge) pointe le prochain anneau du coureur local ; rendue à la fin de sa course.</summary>
    void UpdateCompass(Transform target, PigeonController local)
    {
        if (target == _compassTarget)
            return;
        if (_compass == null)
            _compass = FindAnyObjectByType<DeliveryCompassArrow>();
        if (_compass == null)
            return;
        if (_compassTarget != null)
            _compass.ClearTarget(_compassTarget);
        _compassTarget = target;
        if (target == null)
            return;
        _compass.SetTarget(target, local.transform);
        _compass.SetColors(_compassTop, _compassBottom);
    }

    /// <summary>Boussole utilisée par la course (tests).</summary>
    public DeliveryCompassArrow Compass => _compass;

    void PlayLocalFeedback(int startCount, in RaceProgress progress)
    {
        if (startCount == 0)
        {
            _lastLocalStart = 0;
            return;
        }
        if (startCount != _lastLocalStart)
        {
            _lastLocalStart = startCount;
            _lastLocalNext = progress.Next;
            return;
        }
        if (progress.Next <= _lastLocalNext)
            return;
        _lastLocalNext = progress.Next;
        AudioClip clip = progress.Next >= _checkpoints.Length ? _finishClip : _checkpointClip;
        if (clip != null && SFXManager.Instance != null)
            SFXManager.Instance.PlaySFX(clip);
    }

    void CollectViewRacers(int startCount)
    {
        _viewRacers.Clear();
        foreach (PigeonController pigeon in PigeonController.All)
            if (pigeon.RaceProgress.IsIn(id, startCount))
                _viewRacers.Add(pigeon);
    }

    /// <summary>Les vues des coureurs montrent toutes les arrivées comptées par la course.</summary>
    bool ViewsCaughtUp()
    {
        int finished = 0;
        foreach (PigeonController racer in _viewRacers)
            if (racer.RaceProgress.Place > 0)
                finished++;
        return finished >= _viewState.Finished;
    }

    /// <summary>Classement : arrivés par place, puis par anneaux passés, puis par distance au prochain anneau.</summary>
    int Compare(PigeonController a, PigeonController b)
    {
        RaceProgress pa = a.RaceProgress, pb = b.RaceProgress;
        if (pa.Place > 0 || pb.Place > 0)
            return pa.Place == 0 ? 1 : pb.Place == 0 ? -1 : pa.Place.CompareTo(pb.Place);
        if (pa.Next != pb.Next)
            return pb.Next.CompareTo(pa.Next);
        return DistanceToNext(a).CompareTo(DistanceToNext(b));
    }

    float DistanceToNext(PigeonController pigeon)
    {
        int next = pigeon.RaceProgress.Next;
        return next < _checkpoints.Length ? Vector3.Distance(pigeon.transform.position, _checkpoints[next].transform.position) : 0f;
    }

    string BuildRaceText(in TimedRaceState view, PigeonController local, in RaceProgress progress)
    {
        _viewRacers.Sort(Compare);
        int place = _viewRacers.IndexOf(local) + 1;
        var sb = new StringBuilder();
        if (progress.Place > 0)
        {
            sb.Append($"<size=130%>Finished!</size>\n{Ordinal(progress.Place)} - {FormatTime(progress.FinishTime)}");
            if (_viewRacers.Count > 1)
                sb.Append($"\n<size=70%>Waiting for the other racers: {Mathf.CeilToInt(view.Grace)} s</size>");
            return sb.ToString();
        }
        sb.Append($"<size=140%>{FormatTime(view.Elapsed)}</size>\n");
        sb.Append($"<size=120%>{Ordinal(place)}</size><size=80%> / {_viewRacers.Count}</size>\n");
        sb.Append($"<size=70%>Ring {Mathf.Min(progress.Next + 1, _checkpoints.Length)} / {_checkpoints.Length}</size>");
        if (view.BestTime > 0f)
            sb.Append($"\n<size=60%>Best {FormatTime(view.BestTime)}</size>");
        if (view.Finished > 0)
            sb.Append($"\n<size=70%><color=#FF5050>{Mathf.CeilToInt(view.Grace)} s left!</color></size>");
        return sb.ToString();
    }

    string BuildResultsText(PigeonController local)
    {
        _viewRacers.Sort(Compare);
        var sb = new StringBuilder("<size=130%>Results</size>\n\n");
        for (int i = 0; i < _viewRacers.Count; i++)
        {
            PigeonController racer = _viewRacers[i];
            RaceProgress p = racer.RaceProgress;
            string name = racer == local ? "You" : racer.owner.HasValue ? $"Player {racer.owner.Value.id.value}" : "Bot";
            string color = ColorUtility.ToHtmlStringRGB(racer.PlayerColor);
            string time = p.Place > 0 ? FormatTime(p.FinishTime) : "DNF";
            sb.Append($"<align=left><pos=8%>{(p.Place > 0 ? Ordinal(p.Place) : "-")}<pos=30%><color=#{color}>{name}</color><pos=70%>{time}</align>\n");
        }
        if (_viewState.BestTime > 0f)
            sb.Append(_viewState.RecordStartCount == _viewState.StartCount
                ? $"\n<color=#FFD040>New record! {FormatTime(_viewState.BestTime)}</color>\n"
                : $"\n<size=80%>Best time: {FormatTime(_viewState.BestTime)}</size>\n");
        sb.Append("\n<size=70%>Press F / Y to leave</size>");
        return sb.ToString();
    }

    public static string FormatTime(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        int minutes = (int)(seconds / 60f);
        return minutes + ":" + (seconds - minutes * 60f).ToString("00.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    static string Ordinal(int place) => place switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{place}th"
    };

    void OnDrawGizmos()
    {
        if (_checkpointsRoot == null)
            return;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Vector3 previous = _departureZone != null && _departureZone.StartPose != null
            ? _departureZone.StartPose.position
            : _departureZone != null ? _departureZone.transform.position : transform.position;
        foreach (Transform child in _checkpointsRoot)
        {
            Gizmos.DrawLine(previous, child.position);
            previous = child.position;
        }
    }
}
