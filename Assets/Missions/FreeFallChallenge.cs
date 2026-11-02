using System.Collections.Generic;
using System.Text;
using PurrNet.Prediction;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public struct FreeFallState : IPredictedData<FreeFallState>
{
    public FreeFallChallenge.ActivityPhase Phase;
    public int StartCount;
    public byte Participants;
    public float Elapsed;
    public float ResultsRemaining;
    public float BestHeight;
    public int RecordStartCount;

    public void Dispose() { }
}

/// <summary>
/// Chute libre multijoueur : chaque pigeon termine sa tentative ; le dernier appui valide obtient la première place.
/// Les égalités du même tick sont conservées.
/// Le StartPose place les participants en altitude, sans vitesse initiale ; seule la gravité agit pendant la chute.
/// Le record est la plus petite distance positive au sol d'un essai terminé sans contact avec le sol ou l'eau.
/// </summary>
public class FreeFallChallenge : PredictedIdentity<FreeFallState>
{
    public enum ActivityPhase : byte { Idle, Falling, Results }
    public enum LossReason : byte { None, Flap, Movement, Ground, Respawn, Timeout }

    [SerializeField, BoxGroup("Départ")] private MissionStartZone _departureZone;
    [SerializeField, BoxGroup("Départ")] private GameObject _departureMarker;
    [SerializeField, BoxGroup("Chute")] private LayerMask _groundLayers = 273; // Default, Water, PigeonOnly.
    [SerializeField, BoxGroup("Chute")] private float _groundProbeDistance = 1000f;
    [SerializeField, BoxGroup("Chute")] private float _movementDeadZone = 0.15f;
    [SerializeField, BoxGroup("Chute")] private float _maximumDuration = 120f;
    [SerializeField, BoxGroup("Résultats")] private float _resultsTimeout = 90f;
    [SerializeField, BoxGroup("Résultats")] private string _recordKey;
    [SerializeField, BoxGroup("Présentation")] private TMP_FontAsset _font;

    private bool _validSetup;
    private FreeFallState _viewState;
    private RaceHud _hud;
    private float _savedThisSession = -1f;
    private readonly List<PigeonController> _viewParticipants = new List<PigeonController>();

    public MissionStartZone DepartureZone => _departureZone;
    public ActivityPhase Phase => _viewState.Phase;
    public int StartCount => _viewState.StartCount;
    public float BestHeight => _viewState.BestHeight;
    public string HudText => _hud != null ? _hud.RaceText : null;
    public string ResultsText => _hud != null ? _hud.ResultsText : null;
    public string RecordKey => string.IsNullOrWhiteSpace(_recordKey)
        ? $"FreeFall.Best.{gameObject.scene.name}.{name}" : _recordKey;

    protected override void LateAwake()
    {
        _validSetup = _departureZone != null && _departureZone.StartPose != null
            && _departureZone.StartPose.position.y > _departureZone.transform.position.y
            && _departureZone.CinematicTimeout == 0f
            && _groundLayers.value != 0 && (_groundLayers.value & (1 << 3)) == 0
            && IsPositiveFinite(_groundProbeDistance) && IsPositiveFinite(_maximumDuration)
            && IsPositiveFinite(_resultsTimeout) && IsPositiveFinite(_movementDeadZone) && _movementDeadZone < 1f;
        if (!_validSetup)
            Debug.LogError("FreeFallChallenge : assigner une MissionStartZone sans cinématique, avec son StartPose en hauteur ; "
                + "exclure le layer Player des sols, régler des durées et une portée positives, et une zone morte entre 0 et 1.", this);
    }

    protected override FreeFallState GetInitialState() => new FreeFallState
    {
        BestHeight = isServer ? PlayerPrefs.GetFloat(RecordKey, 0f) : 0f
    };

    protected override void GetUnityState(ref FreeFallState state) { }
    protected override void SetUnityState(FreeFallState state) { }

    protected override void Simulate(ref FreeFallState state, float delta)
    {
        if (!_validSetup)
            return;
        switch (state.Phase)
        {
            case ActivityPhase.Idle:
                ref MissionStartState start = ref _departureZone.SimState;
                if (start.Phase == MissionStartZone.StartPhase.Started && start.StartCount != state.StartCount)
                    Begin(ref state, start.StartCount);
                break;
            case ActivityPhase.Falling:
                SimulateFalling(ref state, delta);
                break;
            case ActivityPhase.Results:
                SimulateResults(ref state, delta);
                break;
        }
    }

    private void Begin(ref FreeFallState state, int startCount)
    {
        state.Phase = ActivityPhase.Falling;
        state.StartCount = startCount;
        state.Elapsed = 0f;
        int participants = 0;
        foreach (PigeonController pigeon in PigeonController.All)
        {
            if (!_departureZone.IsInCurrentStart(pigeon))
                continue;
            ref PigeonState ps = ref pigeon.SimState;
            ps.FreeFall = new FreeFallProgress
            {
                Activity = id, StartCount = startCount, Active = true,
                MovementDeadZone = _movementDeadZone, CurrentHeight = -1f
            };
            pigeon.ResetFreeFallMotion();
            UpdateHeight(ref ps.FreeFall, ps.Position);
            participants++;
        }
        state.Participants = (byte)Mathf.Min(participants, byte.MaxValue);
        if (participants == 0)
            Reset(ref state);
    }

    private void SimulateFalling(ref FreeFallState state, float delta)
    {
        state.Elapsed += delta;
        int present = 0, remaining = 0;
        foreach (PigeonController pigeon in PigeonController.All)
        {
            ref PigeonState ps = ref pigeon.SimState;
            ref FreeFallProgress progress = ref ps.FreeFall;
            if (!progress.IsIn(id, state.StartCount))
                continue;
            present++;
            if (progress.Finished)
                continue;

            if (progress.Loss == LossReason.None && ps.Grounded)
                progress.Loss = LossReason.Ground;
            else if (progress.Loss == LossReason.None && state.Elapsed >= _maximumDuration)
            {
                progress.Loss = LossReason.Timeout;
                progress.InputPosition = ps.Position;
            }

            if (progress.Loss != LossReason.Ground)
                UpdateHeight(ref progress, progress.Loss == LossReason.None ? ps.Position : progress.InputPosition);
            if (progress.Loss == LossReason.None)
                remaining++;
        }

        if (present == 0)
        {
            Reset(ref state);
            return;
        }

        // Deuxième passage : les éliminations du même tick partagent une place, quel que soit l'ordre des pigeons.
        foreach (PigeonController pigeon in PigeonController.All)
        {
            ref FreeFallProgress progress = ref pigeon.SimState.FreeFall;
            if (!progress.IsIn(id, state.StartCount) || progress.Finished || progress.Loss == LossReason.None)
                continue;
            progress.Finished = true;
            progress.FinishTime = state.Elapsed;
            RecordAttempt(ref state, progress);
        }

        // Le dernier participant continue lui aussi : sa hauteur reste mesurée jusqu'à sa propre fin de tentative.
        if (remaining > 0)
            return;
        RankParticipants(state.StartCount);
        state.Phase = ActivityPhase.Results;
        state.ResultsRemaining = _resultsTimeout;
    }

    private void RankParticipants(int startCount)
    {
        foreach (PigeonController pigeon in PigeonController.All)
        {
            ref FreeFallProgress progress = ref pigeon.SimState.FreeFall;
            if (!progress.IsIn(id, startCount))
                continue;
            int place = 1;
            foreach (PigeonController other in PigeonController.All)
            {
                ref FreeFallProgress candidate = ref other.SimState.FreeFall;
                if (candidate.IsIn(id, startCount) && CompareResults(candidate, progress) < 0)
                    place++;
            }
            progress.Place = (byte)Mathf.Min(place, byte.MaxValue);
        }
    }

    private static int CompareResults(in FreeFallProgress a, in FreeFallProgress b)
    {
        bool aValid = HasValidResult(a), bValid = HasValidResult(b);
        if (aValid != bValid)
            return aValid ? -1 : 1;
        // À validité égale, le dernier à terminer passe devant ; même tick = ex æquo.
        return b.FinishTime.CompareTo(a.FinishTime);
    }

    private static bool HasValidResult(in FreeFallProgress progress) => progress.Finished
        && progress.Loss is LossReason.Flap or LossReason.Movement && HasValidHeight(progress);

    private void UpdateHeight(ref FreeFallProgress progress, Vector3 position)
    {
        if (!Physics.Raycast(position + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit,
                _groundProbeDistance, _groundLayers, QueryTriggerInteraction.Ignore))
        {
            progress.CurrentHeight = -1f;
            return;
        }
        float height = Mathf.Max(0f, hit.distance - 0.05f);
        progress.CurrentHeight = height;
        if (height <= 0f)
            return;
        if (!progress.HasHeight || height < progress.MinimumHeight)
            progress.MinimumHeight = height;
        progress.HasHeight = true;
    }

    private static void RecordAttempt(ref FreeFallState state, in FreeFallProgress progress)
    {
        if (!HasValidHeight(progress))
            return;
        if (state.BestHeight > 0f && progress.MinimumHeight >= state.BestHeight)
            return;
        state.BestHeight = progress.MinimumHeight;
        state.RecordStartCount = state.StartCount;
    }

    private void SimulateResults(ref FreeFallState state, float delta)
    {
        state.ResultsRemaining -= delta;
        int waiting = 0;
        foreach (PigeonController pigeon in PigeonController.All)
        {
            ref PigeonState ps = ref pigeon.SimState;
            if (!ps.FreeFall.IsIn(id, state.StartCount))
                continue;
            if (ps.Mission.InteractPressed && !ps.FreeFall.Dismissed)
            {
                ps.FreeFall.Dismissed = true;
                ps.FreeFall.Active = false;
                ps.Mission.InteractPressed = false;
            }
            if (!ps.FreeFall.Dismissed)
                waiting++;
        }
        if (waiting == 0 || state.ResultsRemaining <= 0f)
            Reset(ref state);
    }

    private void Reset(ref FreeFallState state)
    {
        foreach (PigeonController pigeon in PigeonController.All)
        {
            ref FreeFallProgress progress = ref pigeon.SimState.FreeFall;
            if (progress.IsIn(id, state.StartCount))
                progress.Active = false;
        }
        state.Phase = ActivityPhase.Idle;
        _departureZone.ResetToWaiting();
    }

    public static LossReason GetInputLoss(in PigeonInput input, float movementDeadZone)
    {
        if (input.Respawn)
            return LossReason.Respawn;
        if (input.Flap)
            return LossReason.Flap;
        float threshold = movementDeadZone * movementDeadZone;
        return input.Movement.sqrMagnitude > threshold || input.RelativeMovement.sqrMagnitude > threshold
            || input.Brake || input.Dive || input.BarrelLeft || input.BarrelRight
            ? LossReason.Movement : LossReason.None;
    }

    protected override void UpdateView(FreeFallState view, FreeFallState? verified)
    {
        _viewState = view;
        // Les PlayerPrefs ne doivent jamais être écrits par une prédiction qui pourrait être annulée.
        if (isServer && verified.HasValue)
            SaveRecord(verified.Value.BestHeight);
        if (_departureMarker != null)
        {
            bool show = view.Phase == ActivityPhase.Idle && _departureZone != null
                && _departureZone.Phase is MissionStartZone.StartPhase.Waiting or MissionStartZone.StartPhase.CountingDown;
            if (_departureMarker.activeSelf != show)
                _departureMarker.SetActive(show);
        }

        PigeonController local = PigeonController.Local;
        bool participating = local != null && local.FreeFallProgress.IsIn(id, view.StartCount);
        if (participating && local.FreeFallProgress.Active)
            MissionMarkers.SetHidden(true, _departureMarker);
        else if (local == null || !local.IsInMultiplayerActivity)
            MissionMarkers.SetHidden(false);
        if (view.Phase == ActivityPhase.Idle || !participating || !local.FreeFallProgress.Active)
        {
            if (_hud != null)
                _hud.HideAll();
            return;
        }

        if (_hud == null)
            _hud = RaceHud.Create(_font);
        _viewParticipants.Clear();
        bool caughtUp = true;
        foreach (PigeonController pigeon in PigeonController.All)
        {
            if (!pigeon.FreeFallProgress.IsIn(id, view.StartCount))
                continue;
            _viewParticipants.Add(pigeon);
            caughtUp &= pigeon.FreeFallProgress.Finished;
        }
        if (view.Phase == ActivityPhase.Falling)
        {
            _hud.HideResults();
            _hud.ShowRace(BuildFallingText(local.FreeFallProgress));
        }
        else
        {
            _hud.HideRace();
            if (caughtUp)
                _hud.ShowResults(BuildResultsText(local));
        }
    }

    private string BuildFallingText(in FreeFallProgress progress)
    {
        bool invalid = progress.Finished && !HasValidHeight(progress);
        string height = !invalid && progress.CurrentHeight >= 0f ? $"{progress.CurrentHeight:0.00} m" : "--";
        string status = progress.Finished ? $"Out: {ReasonText(progress.Loss)}\nWaiting for the others" : "Don't move or flap!";
        string heightLabel = progress.Finished ? "Your attempt" : "Height";
        return $"<size=120%>Free fall</size>\n{heightLabel}: {height}\n<size=65%>{status}</size>"
            + (invalid ? "\n<size=60%>Invalid attempt</size>"
                : progress.HasHeight ? $"\n<size=60%>Your lowest: {progress.MinimumHeight:0.00} m</size>" : "")
            + (_viewState.BestHeight > 0f ? $"\n<size=60%>Record: {_viewState.BestHeight:0.00} m</size>" : "");
    }

    private string BuildResultsText(PigeonController local)
    {
        _viewParticipants.Sort((a, b) => CompareResults(a.FreeFallProgress, b.FreeFallProgress));
        var text = new StringBuilder("<size=130%>Free fall results</size>\n\n");
        bool hasWinner = false;
        foreach (PigeonController pigeon in _viewParticipants)
        {
            FreeFallProgress progress = pigeon.FreeFallProgress;
            string player = pigeon == local ? "You" : pigeon.owner.HasValue ? $"Player {pigeon.owner.Value.id.value}" : "Bot";
            bool valid = HasValidResult(progress);
            string result = ReasonText(progress.Loss);
            if (!valid)
            {
                if (progress.Loss is LossReason.Flap or LossReason.Movement)
                    result = "No height measured";
                result = "DQ: " + result;
            }
            if (progress.Place == 1 && valid)
            {
                result = $"Winner! ({result})";
                hasWinner = true;
            }
            string height = HasValidHeight(progress) ? $"{progress.MinimumHeight:0.00} m" : "--";
            string color = ColorUtility.ToHtmlStringRGB(pigeon.PlayerColor);
            text.Append($"<size=70%>{progress.Place}. <color=#{color}>{player}</color> - {result} - {height}</size>\n");
        }
        if (!hasWinner)
            text.Append("\n<size=80%>No valid attempt: no winner</size>\n");
        if (_viewState.BestHeight > 0f)
            text.Append(_viewState.RecordStartCount == _viewState.StartCount
                ? $"\n<color=#FFD040>New record! {_viewState.BestHeight:0.00} m</color>\n"
                : $"\n<size=80%>Lowest record: {_viewState.BestHeight:0.00} m</size>\n");
        text.Append("\n<size=70%>Press F / Y to leave</size>");
        return text.ToString();
    }

    private void SaveRecord(float height)
    {
        if (!IsPositiveFinite(height) || Mathf.Approximately(height, _savedThisSession))
            return;
        _savedThisSession = height;
        float saved = PlayerPrefs.GetFloat(RecordKey, 0f);
        if (saved > 0f && saved <= height)
            return;
        PlayerPrefs.SetFloat(RecordKey, height);
        PlayerPrefs.Save();
    }

    private static string ReasonText(LossReason reason) => reason switch
    {
        LossReason.Flap => "Flapped",
        LossReason.Movement => "Moved",
        LossReason.Ground => "Hit the ground / water",
        LossReason.Respawn => "Respawned",
        LossReason.Timeout => "Time limit",
        _ => ""
    };

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool HasValidHeight(in FreeFallProgress progress) => progress.HasHeight
        && IsPositiveFinite(progress.MinimumHeight)
        && progress.Loss is not (LossReason.Ground or LossReason.Respawn or LossReason.Timeout);

    protected override void Destroyed()
    {
        if (PigeonController.Local == null || !PigeonController.Local.IsInMultiplayerActivity)
            MissionMarkers.SetHidden(false);
        if (_hud != null)
            Destroy(_hud.gameObject);
    }
}
