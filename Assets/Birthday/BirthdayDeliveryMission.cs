using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>Message spécial : départ partagé, livraison guidée et anniversaire partagé à l'arrivée.</summary>
public class BirthdayDeliveryMission : MonoBehaviour
{
    [SerializeField, BoxGroup("Départ")] private MissionStartZone _departureZone;
    [SerializeField, BoxGroup("Départ")] private DialogCinematic _departureCinematic;
    [SerializeField, BoxGroup("Départ")] private GameObject _departureMarker;
    [SerializeField, BoxGroup("Arrivée")] private MissionStartZone _arrivalZone;
    [SerializeField, BoxGroup("Arrivée")] private SpecialDanceCinematic _birthdayCinematic;
    [SerializeField, BoxGroup("Arrivée")] private GameObject _arrivalMarker;
    [SerializeField, BoxGroup("Guidage")] private DeliveryCompassArrow _compass;

    private bool _departureRequested;
    private bool _arrivalRequested;
    private bool _playingDeparture;
    private bool _playingBirthday;
    private int _departureCount;
    private int _arrivalCount;
    private bool _ownsCompass;

    public MissionStartZone DepartureZone => _departureZone;
    public MissionStartZone ArrivalZone => _arrivalZone;

    private void Awake()
    {
        if (_compass == null)
            _compass = FindAnyObjectByType<DeliveryCompassArrow>(FindObjectsInactive.Include);

        if (_departureZone == null || _arrivalZone == null || _departureZone == _arrivalZone
            || _departureCinematic == null || _birthdayCinematic == null || _compass == null
            || _departureCinematic.CinematicManager == null || _birthdayCinematic.CinematicManager == null
            || _departureCinematic.Dialogue == null || _birthdayCinematic.Dialogue == null
            || _arrivalZone.RequiredStartZone != _departureZone
            || _departureZone.Rule != MissionStartZone.StartRule.AllPigeons
            || _arrivalZone.Rule != MissionStartZone.StartRule.AllPigeons
            || _departureZone.CinematicTimeout <= 0f || _arrivalZone.CinematicTimeout <= 0f)
        {
            Debug.LogError("BirthdayDeliveryMission : assigner deux zones AllPigeons avec attente de cinématique, "
                + "les deux cinématiques et la boussole. L'arrivée doit dépendre de la zone de départ.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        _departureZone.OnCinematicLocal.AddListener(RequestDeparture);
        _arrivalZone.OnCinematicLocal.AddListener(RequestBirthday);
        _departureCinematic.OnCinematicEnded.AddListener(FinishDeparture);
        _birthdayCinematic.OnCinematicEnded.AddListener(FinishBirthday);
    }

    private void RequestDeparture()
    {
        _departureCount = PigeonController.Local.LastStartCountFor(_departureZone.id);
        _departureRequested = true;
    }

    private void RequestBirthday()
    {
        _arrivalCount = PigeonController.Local.LastStartCountFor(_arrivalZone.id);
        _arrivalRequested = true;
    }

    private void LateUpdate()
    {
        // Les événements vérifiés peuvent précéder la vue de la zone d'une image.
        // Attendre aussi qu'un éventuel autre dialogue rende les managers disponibles.
        if (_departureRequested && !_playingDeparture
            && _departureZone.Phase == MissionStartZone.StartPhase.Cinematic
            && CanPlay(_departureCinematic.CinematicManager))
        {
            _playingDeparture = true;
            _departureCinematic.PlayCinematic();
            if (!_departureCinematic.IsPlaying)
            {
                Debug.LogError("BirthdayDeliveryMission : lancement du briefing refusé.", this);
                FinishDeparture();
            }
        }

        if (_arrivalRequested && !_playingBirthday
            && _arrivalZone.Phase == MissionStartZone.StartPhase.Cinematic
            && CanPlay(_birthdayCinematic.CinematicManager))
        {
            _playingBirthday = true;
            _birthdayCinematic.PlayCinematic();
            if (!_birthdayCinematic.IsPlaying)
            {
                Debug.LogError("BirthdayDeliveryMission : lancement de l'anniversaire refusé.", this);
                FinishBirthday();
            }
        }

        if (_departureZone.Phase == MissionStartZone.StartPhase.Started)
        {
            _departureRequested = false;
            if (_playingDeparture)
                _departureCinematic.CancelCinematic();
        }
        if (_arrivalZone.Phase == MissionStartZone.StartPhase.Started)
        {
            _arrivalRequested = false;
            if (_playingBirthday)
                _birthdayCinematic.CancelCinematic();
        }

        SetMarker(_departureMarker, _departureZone.Phase is MissionStartZone.StartPhase.Waiting
            or MissionStartZone.StartPhase.CountingDown);
        bool delivering = _departureZone.Phase == MissionStartZone.StartPhase.Started
            && _arrivalZone.Phase is MissionStartZone.StartPhase.Waiting or MissionStartZone.StartPhase.CountingDown;
        SetMarker(_arrivalMarker, delivering);

        PigeonController local = PigeonController.Local;
        bool guide = delivering && local != null && !local.IsInMultiplayerActivity;
        if (guide && !_ownsCompass)
        {
            _compass.SetTarget(_arrivalZone.transform, local.transform);
            _ownsCompass = true;
        }
        else if (!guide)
            ClearCompass();
    }

    private static bool CanPlay(ManagerCinematic manager)
        => manager.isActiveAndEnabled && !manager.IsPlaying
        && DialogManager.instance != null && !DialogManager.instance.IsPlaying;

    private void FinishDeparture()
    {
        if (!_playingDeparture)
            return;
        _playingDeparture = _departureRequested = false;
        if (PigeonController.Local != null)
            PigeonController.Local.AcknowledgeCinematic(_departureZone.id, _departureCount);
    }

    private void FinishBirthday()
    {
        if (!_playingBirthday)
            return;
        _playingBirthday = _arrivalRequested = false;
        if (PigeonController.Local != null)
            PigeonController.Local.AcknowledgeCinematic(_arrivalZone.id, _arrivalCount);
    }

    private static void SetMarker(GameObject marker, bool visible)
    {
        if (marker != null && marker.activeSelf != visible)
            marker.SetActive(visible);
    }

    private void ClearCompass()
    {
        if (_ownsCompass && _compass != null)
            _compass.ClearTarget(_arrivalZone.transform);
        _ownsCompass = false;
    }

    private void OnDisable()
    {
        if (_playingDeparture)
        {
            _departureCinematic.CancelCinematic();
            FinishDeparture();
        }
        if (_playingBirthday)
        {
            _birthdayCinematic.CancelCinematic();
            FinishBirthday();
        }
        _departureRequested = _arrivalRequested = false;
        if (_departureZone != null)
            _departureZone.OnCinematicLocal.RemoveListener(RequestDeparture);
        if (_arrivalZone != null)
            _arrivalZone.OnCinematicLocal.RemoveListener(RequestBirthday);
        if (_departureCinematic != null)
            _departureCinematic.OnCinematicEnded.RemoveListener(FinishDeparture);
        if (_birthdayCinematic != null)
            _birthdayCinematic.OnCinematicEnded.RemoveListener(FinishBirthday);
        ClearCompass();
        SetMarker(_departureMarker, false);
        SetMarker(_arrivalMarker, false);
    }
}
