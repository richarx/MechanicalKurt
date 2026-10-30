using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class PigeonStopDialogCinematic : MonoBehaviour
{
    [field: SerializeField, Required]
    [field: Tooltip("Zone locale : se poser dans le cercle lance le tuto pour le pigeon du joueur local. Brancher PlayCinematic sur son OnPlayerStopped.")]
    public PigeonStopZone StopZone { get; private set; }

    [field: SerializeField, Required]
    public DialogCinematic Cinematic { get; private set; }

    [field: SerializeField, Required]
    [field: Tooltip("Achievement validé dès que le lancement de la cinématique est accepté.")]
    public AchievementData CompletionAchievement { get; private set; }

    [SerializeField] private GameObject _cylinder;

    [Header("Récompense")]
    [SerializeField, Tooltip("Donne un équipement au pigeon du joueur à la fin de la cinématique (autrefois un appel ToggleBackPack sur le pigeon de la scène, qui n'existe plus).")]
    private bool _giveEquipmentReward = true;
    [SerializeField, ShowIf(nameof(_giveEquipmentReward))]
    private PigeonEquipment.Equipment _rewardEquipment = PigeonEquipment.Equipment.Backpack;

    public GameObject Cylinder => _cylinder;
    public bool GivesEquipmentReward => _giveEquipmentReward;
    public PigeonEquipment.Equipment RewardEquipment => _rewardEquipment;

    private bool _consumed;
    private bool _rewardGiven;

    private void OnEnable()
    {
        if (Cinematic != null)
            Cinematic.OnCinematicEnded.AddListener(HandleCinematicEnded);
    }

    private void OnDisable()
    {
        if (Cinematic != null)
            Cinematic.OnCinematicEnded.RemoveListener(HandleCinematicEnded);
    }

    private void HandleCinematicEnded()
    {
        if (!_consumed || _rewardGiven || !_giveEquipmentReward)
            return;
        PigeonController local = PigeonController.Local;
        PigeonEquipment equipment = local != null ? local.GetComponentInChildren<PigeonEquipment>() : null;
        if (equipment == null)
        {
            Debug.LogWarning("PigeonStopDialogCinematic : pas de pigeon local avec PigeonEquipment pour la récompense.", this);
            return;
        }
        _rewardGiven = true;
        equipment.UnlockItem(_rewardEquipment);
    }

    // Brancher sur PigeonStopZone.OnPlayerStopped.
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled || _consumed)
            return;

        if (StopZone == null || Cinematic == null)
        {
            Debug.LogWarning("PigeonStopDialogCinematic : assigner la zone d'arrêt et le DialogCinematic.", this);
            return;
        }

        if (!StopZone.isActiveAndEnabled || Cinematic.IsPlaying)
            return;

        Cinematic.PlayCinematic();

        // Ne consommer la zone que si le lancement a été accepté.
        // Désactiver uniquement le composant pour laisser la cinématique active.
        if (Cinematic.IsPlaying)
        {
            _consumed = true;
            StopZone.enabled = false;
            if (_cylinder != null)
                _cylinder.SetActive(false);

            if (CompletionAchievement == null || AchievementManager.Instance == null)
            {
                Debug.LogWarning("PigeonStopDialogCinematic : assigner l'achievement de fin de tuto et prévoir un AchievementManager dans la scène.", this);
                return;
            }

            AchievementManager manager = AchievementManager.Instance;
            manager.Unlock(CompletionAchievement);

            // L'achievement fait avancer le tuto ; le rafraîchissement masque
            // immédiatement son message de fin pour laisser place au dialogue.
            foreach (AchievementTutorial tutorial in FindObjectsByType<AchievementTutorial>(FindObjectsInactive.Include))
            {
                if (tutorial.gameObject.scene != gameObject.scene)
                    continue;
                if (tutorial.Manager != null && tutorial.Manager != manager)
                    continue;
                tutorial.RefreshProgress();
            }
        }
    }

    private void Reset()
    {
        StopZone = GetComponent<PigeonStopZone>();
        Cinematic = GetComponent<DialogCinematic>();
    }
}
