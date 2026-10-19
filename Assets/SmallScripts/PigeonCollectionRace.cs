using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PigeonCollectionRace : MonoBehaviour
{
    public enum CollectionPhase { Ready, Collecting, Returning, ArrivalCinematic, Completed }

    [field: SerializeField, BoxGroup("Départ")]
    public PigeonStopZone DepartureStop { get; private set; }

    [field: SerializeField, BoxGroup("Départ")]
    public GameObject DepartureMarker { get; private set; }

    [field: SerializeField, BoxGroup("Départ"), Tooltip("Facultative. Si renseignée, le lancement attend sa fin normale.")]
    public DialogCinematic DepartureCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Collecte"), Required]
    public PigeonController Player { get; private set; }

    [field: SerializeField, BoxGroup("Collecte"), Required]
    public PigeonCollectionZone[] CollectionZones { get; private set; } = new PigeonCollectionZone[0];

    [field: SerializeField, BoxGroup("Compteur"), Required]
    public GameObject CounterParent { get; private set; }

    [field: SerializeField, BoxGroup("Compteur"), Required]
    public TMP_Text CounterText { get; private set; }

    [field: SerializeField, BoxGroup("Compteur"), Required]
    public Image CounterIcon { get; private set; }

    [field: SerializeField, BoxGroup("Compteur"), Required, PreviewField]
    public Sprite CollectionSprite { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public PigeonStopZone DestinationStop { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée")]
    public GameObject DestinationMarker { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Required]
    public DeliveryCompassArrow Arrow { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Tooltip("Facultatif, distinct de Destination Stop : s'il est actif quand la flèche apparaît, désactive ce PigeonStop jusqu'à la fin de la mission.")]
    public PigeonStopZone StopToDisableDuringReturn { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Tooltip("Facultative : jouée automatiquement à l'arrivée.")]
    public DialogCinematic ArrivalCinematic { get; private set; }

    [field: SerializeField, BoxGroup("Arrivée"), Tooltip("Facultatif : attribué après la cinématique finale, ou immédiatement si elle est absente.")]
    public AchievementData CompletionAchievement { get; private set; }

    [field: SerializeField, BoxGroup("Événements")]
    public UnityEvent OnCompleted { get; private set; } = new UnityEvent();

    [ShowInInspector, ReadOnly]
    public CollectionPhase Phase { get; private set; }

    [ShowInInspector, ReadOnly]
    public int CollectedCount { get; private set; }

    private Vector3 _previousPosition;
    private PigeonStopZone _suspendedStop;

    private void OnEnable()
    {
        HideCollection();
        SetStopActive(DepartureStop, DepartureMarker, Phase == CollectionPhase.Ready);
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.AddListener(HandleArrivalEnded);
    }

    // À brancher sur OnCinematicEnded de la cinématique de départ.
    public void StartCollection()
    {
        if (!isActiveAndEnabled || Phase != CollectionPhase.Ready || Time.timeScale <= 0f)
            return;
        if (DepartureCinematic != null && (DepartureCinematic.IsPlaying || !DepartureCinematic.CompletedNormally))
            return;
        if (!HasValidSetup())
        {
            Debug.LogWarning("PigeonCollectionRace : assigner le pigeon, des zones de collecte distinctes, le compteur (parent, TMP, Image et sprite), l'arrivée et la flèche. Garder le contrôleur, les zones de départ/arrivée et les cinématiques sur des objets actifs séparés des visuels.", this);
            return;
        }

        Phase = CollectionPhase.Collecting;
        CollectedCount = 0;
        _previousPosition = Player.transform.position;
        SetStopActive(DepartureStop, DepartureMarker, false);
        SetStopActive(DestinationStop, DestinationMarker, false);
        foreach (PigeonCollectionZone zone in CollectionZones)
            zone.SetAvailable(true);
        CounterIcon.sprite = CollectionSprite;
        CounterIcon.preserveAspect = true;
        RefreshCounter();
        CounterParent.SetActive(true);
    }

    private void LateUpdate()
    {
        if (Phase != CollectionPhase.Collecting || Player == null)
            return;

        Vector3 currentPosition = Player.transform.position;
        if (Time.deltaTime > 0f && Player.isActiveAndEnabled)
        {
            foreach (PigeonCollectionZone zone in CollectionZones)
            {
                if (zone != null && zone.TryCollect(_previousPosition, currentPosition))
                    CollectedCount++;
            }
            RefreshCounter();
            if (CollectedCount == CollectionZones.Length)
                ActivateDestination();
        }
        _previousPosition = currentPosition;
    }

    private void ActivateDestination()
    {
        Phase = CollectionPhase.Returning;
        if (_suspendedStop == null && StopToDisableDuringReturn != null
            && StopToDisableDuringReturn != DestinationStop && StopToDisableDuringReturn.isActiveAndEnabled)
        {
            _suspendedStop = StopToDisableDuringReturn;
            _suspendedStop.enabled = false;
        }
        SetStopActive(DestinationStop, DestinationMarker, true);
        Arrow.SetTarget(DestinationStop.transform, Player.transform);
        Arrow.SetArrowVisible(true);
    }

    // À brancher sur OnPlayerStopped de la zone d'arrivée.
    public void CompleteCollection()
    {
        if (!isActiveAndEnabled || Phase != CollectionPhase.Returning || Time.timeScale <= 0f
            || DestinationStop == null || !DestinationStop.isActiveAndEnabled)
            return;

        HideCollection();
        if (ArrivalCinematic == null)
        {
            FinishCollection();
            return;
        }

        Phase = CollectionPhase.ArrivalCinematic;
        ArrivalCinematic.PlayCinematic();
        if (!ArrivalCinematic.IsPlaying && Phase == CollectionPhase.ArrivalCinematic)
            RestoreDestination();
    }

    private void HandleArrivalEnded()
    {
        if (Phase != CollectionPhase.ArrivalCinematic)
            return;
        if (ArrivalCinematic.CompletedNormally)
            FinishCollection();
        else
            RestoreDestination();
    }

    private void RestoreDestination()
    {
        ActivateDestination();
        CounterParent.SetActive(true);
    }

    private void FinishCollection()
    {
        Phase = CollectionPhase.Completed;
        RestoreSuspendedStop();
        if (CompletionAchievement != null)
        {
            if (AchievementManager.Instance != null)
                AchievementManager.Instance.Unlock(CompletionAchievement);
            else
                Debug.LogWarning("PigeonCollectionRace : collecte terminée, mais aucun AchievementManager actif.", this);
        }
        OnCompleted.Invoke();
    }

    private void RestoreSuspendedStop()
    {
        if (_suspendedStop != null)
            _suspendedStop.enabled = true;
        _suspendedStop = null;
    }

    private void RefreshCounter() => CounterText.SetText("{0}/{1}", (float)CollectedCount, (float)CollectionZones.Length);

    private void HideCollection()
    {
        foreach (PigeonCollectionZone zone in CollectionZones)
            if (zone != null)
                zone.SetAvailable(false);
        SetStopActive(DestinationStop, DestinationMarker, false);
        if (CounterParent != null)
            CounterParent.SetActive(false);
        if (Arrow != null && DestinationStop != null)
            Arrow.ClearTarget(DestinationStop.transform);
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
        if (Player == null || !Player.isActiveAndEnabled || CollectionZones == null || CollectionZones.Length == 0
            || CounterParent == null || CounterText == null || CounterIcon == null || CollectionSprite == null
            || DestinationStop == null || DestinationStop == DepartureStop || !DestinationStop.gameObject.activeInHierarchy
            || Arrow == null || !Arrow.isActiveAndEnabled
            || (ArrivalCinematic != null && (!ArrivalCinematic.isActiveAndEnabled || ArrivalCinematic == DepartureCinematic)))
            return false;

        var uniqueZones = new HashSet<PigeonCollectionZone>();
        foreach (PigeonCollectionZone zone in CollectionZones)
            if (zone == null || !zone.enabled || !uniqueZones.Add(zone)
                || zone.Radius <= 0f || float.IsNaN(zone.Radius) || float.IsInfinity(zone.Radius))
                return false;
        return true;
    }

    private void OnDisable()
    {
        if (ArrivalCinematic != null)
            ArrivalCinematic.OnCinematicEnded.RemoveListener(HandleArrivalEnded);
        bool cancelArrival = Phase == CollectionPhase.ArrivalCinematic;
        if (Phase != CollectionPhase.Completed)
        {
            Phase = CollectionPhase.Ready;
            CollectedCount = 0;
        }
        HideCollection();
        SetStopActive(DepartureStop, DepartureMarker, Phase == CollectionPhase.Ready);
        RestoreSuspendedStop();
        if (cancelArrival && ArrivalCinematic != null)
            ArrivalCinematic.CancelCinematic();
    }
}
