using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PigeonDeliveryRace : MonoBehaviour
{
    public enum DeliveryPhase { Ready, DepartureCinematic, Racing, ArrivalCinematic, Completed }

    [field: SerializeField, BoxGroup("Départ"), Required]
    public PigeonStopZone DepartureStop { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Required]
    public DialogCinematic DepartureCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Tooltip("Repère visuel facultatif, sans les scripts de cinématique ni le contrôleur de course.")]
    public GameObject DepartureMarker { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public PigeonStopZone DestinationStop { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public DialogCinematic ArrivalCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Tooltip("Repère visuel facultatif, sans les scripts de cinématique ni le contrôleur de course.")]
    public GameObject DestinationMarker { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public Transform Player { get; private set; }

    [field: SerializeField, BoxGroup("Course"), MinValue(0.01f), SuffixLabel("s", true)]
    public float DurationSeconds { get; private set; } = 60f;

    [field: SerializeField, BoxGroup("Course"), Required]
    public GameObject TimerParent { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public TMP_Text TimerText { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public DeliveryCompassArrow Arrow { get; private set; }

    [field: SerializeField, BoxGroup("Course"), Required]
    public AchievementData CompletionAchievement { get; private set; }

    [field: SerializeField, BoxGroup("Échec"), Tooltip("Message facultatif affiché uniquement quand le chrono expire. Le texte se règle sur son objet UI.")]
    public FadingMessage FailureMessage { get; private set; }

    [ShowInInspector, ReadOnly]
    public DeliveryPhase Phase { get; private set; }

    [ShowInInspector, ReadOnly]
    public float RemainingSeconds => Phase == DeliveryPhase.Racing
        ? (float)System.Math.Max(0d, _deadline - Time.timeAsDouble) : _remainingSeconds;

    // Les courses peuvent partager la même flèche et le même texte de chrono.
    private static PigeonDeliveryRace _activeDelivery;
    private double _deadline;
    private float _remainingSeconds;

    private void OnEnable()
    {
        SetStopActive(DepartureStop, DepartureMarker, Phase == DeliveryPhase.Ready);
        SetStopActive(DestinationStop, DestinationMarker, false);
        if (_activeDelivery == null && TimerText != null)
            TimerText.gameObject.SetActive(false);

        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicStarted.AddListener(HandleDepartureStarted);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.AddListener(HandleArrivalEnded);
    }

    // La zone de départ lance directement DialogCinematic.PlayCinematic.
    private void HandleDepartureStarted()
    {
        if (!isActiveAndEnabled || Phase != DeliveryPhase.Ready || _activeDelivery != null)
        {
            DepartureCinematic.CancelCinematic();
            return;
        }
        if (!HasValidSetup())
        {
            Debug.LogWarning("PigeonDeliveryRace : assigner deux zones et deux cinématiques distinctes, le joueur, le chrono TMP, la flèche et l'achievement ; durée finie et positive requise. Garder les objets des zones et cinématiques actifs.", this);
            DepartureCinematic.CancelCinematic();
            return;
        }

        _activeDelivery = this;
        Phase = DeliveryPhase.DepartureCinematic;
        _remainingSeconds = DurationSeconds;
        if (FailureMessage != null)
            FailureMessage.Hide();
        SetStopActive(DepartureStop, DepartureMarker, false);
    }

    // Brancher sur DialogCinematic.OnCinematicEnded de la cinématique de départ.
    public void StartDelivery()
    {
        if (!isActiveAndEnabled || Phase != DeliveryPhase.DepartureCinematic || _activeDelivery != this
            || DepartureCinematic.IsPlaying)
            return;
        if (!DepartureCinematic.CompletedNormally)
        {
            FailDelivery();
            return;
        }

        // OnCinematicEnded arrive après le retour de caméra et le déblocage des commandes.
        _deadline = Time.timeAsDouble + DurationSeconds;
        Phase = DeliveryPhase.Racing;
        SetStopActive(DestinationStop, DestinationMarker, true);
        Arrow.SetTarget(DestinationStop.transform, Player);
        Arrow.SetArrowVisible(true);
        TimerParent.SetActive(true);
        RefreshTimer();
        TimerText.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (Phase != DeliveryPhase.Racing)
            return;
        if (RemainingSeconds <= 0f)
        {
            FailDelivery(true);
            return;
        }
        RefreshTimer();
    }

    // Brancher sur le OnPlayerStopped de la zone d'arrivée.
    public void CompleteDelivery()
    {
        if (!isActiveAndEnabled || Phase != DeliveryPhase.Racing || !DestinationStop.isActiveAndEnabled
            || Time.timeScale <= 0f)
            return;


        _remainingSeconds = RemainingSeconds;
        Phase = DeliveryPhase.ArrivalCinematic;
        TimerParent.SetActive(false);
        HideRaceDisplay();
        ArrivalCinematic.PlayCinematic();
        if (!ArrivalCinematic.IsPlaying)
            FailDelivery();
    }

    private void HandleArrivalEnded()
    {
        if (Phase != DeliveryPhase.ArrivalCinematic)
            return;
        if (!ArrivalCinematic.CompletedNormally)
        {
            FailDelivery();
            return;
        }

        Phase = DeliveryPhase.Completed;
        _activeDelivery = null;
        if (AchievementManager.Instance != null)
            AchievementManager.Instance.Unlock(CompletionAchievement);
        else
            Debug.LogWarning("PigeonDeliveryRace : course terminée, mais aucun AchievementManager actif dans la scène.", this);
    }

    private void FailDelivery(bool timedOut = false)
    {
        _remainingSeconds = 0f;
        Phase = DeliveryPhase.Ready;
        HideRaceDisplay();
        if (_activeDelivery == this)
            _activeDelivery = null;
        SetStopActive(DepartureStop, DepartureMarker, true);
        if (timedOut && FailureMessage != null)
            FailureMessage.Show();
    }

    private void HideRaceDisplay()
    {
        SetStopActive(DestinationStop, DestinationMarker, false);
        TimerParent.SetActive(false);
        if (TimerText != null)
            TimerText.gameObject.SetActive(false);
        if (Arrow != null && DestinationStop != null)
        {
            Arrow.ClearTarget(DestinationStop.transform);
            Arrow.SetArrowVisible(false);
        }
    }

    private void RefreshTimer()
    {
        int seconds = Mathf.CeilToInt(RemainingSeconds);
        TimerText.SetText("{0:00}:{1:00}", (float)(seconds / 60), (float)(seconds % 60));
    }

    private static void SetStopActive(PigeonStopZone stop, GameObject marker, bool active)
    {
        if (stop != null)
            stop.enabled = active;
        if (marker != null)
            marker.SetActive(active);
    }

    private bool HasValidSetup()
    {
        return DepartureStop != null && DestinationStop != null && DepartureStop != DestinationStop
            && DepartureCinematic != null && ArrivalCinematic != null && DepartureCinematic != ArrivalCinematic
            && DepartureCinematic.isActiveAndEnabled && ArrivalCinematic.isActiveAndEnabled
            && DepartureStop.gameObject.activeInHierarchy && DestinationStop.gameObject.activeInHierarchy
            && Player != null && TimerText != null && Arrow != null && Arrow.isActiveAndEnabled
            && CompletionAchievement != null && !float.IsNaN(DurationSeconds)
            && !float.IsInfinity(DurationSeconds) && DurationSeconds > 0f;
    }

    private void OnDisable()
    {
        if (DepartureCinematic != null)
            DepartureCinematic.OnCinematicStarted.RemoveListener(HandleDepartureStarted);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.RemoveListener(HandleArrivalEnded);

        if (_activeDelivery != this)
            return;

        DeliveryPhase previousPhase = Phase;
        FailDelivery();
        if (previousPhase == DeliveryPhase.DepartureCinematic)
            DepartureCinematic.CancelCinematic();
        else if (previousPhase == DeliveryPhase.ArrivalCinematic)
            ArrivalCinematic.CancelCinematic();
    }
}
