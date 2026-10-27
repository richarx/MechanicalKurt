using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class PigeonStopDialogCinematic : MonoBehaviour
{
    [field: SerializeField, Required]
    [field: Tooltip("Zone de départ multijoueur. Brancher PlayCinematic sur son OnStartedLocal (pas OnStarted).")]
    public MissionStartZone StartZone { get; private set; }

    [field: SerializeField, Required]
    public DialogCinematic Cinematic { get; private set; }

    [field: SerializeField, Required]
    [field: Tooltip("Achievement validé dès que le lancement de la cinématique est accepté.")]
    public AchievementData CompletionAchievement { get; private set; }

    [SerializeField] private GameObject _cylinder;

    public GameObject Cylinder => _cylinder;

    // Consommation locale : la zone reste utilisable par les autres joueurs.
    private bool _consumed;

    // Brancher sur MissionStartZone.OnStartedLocal.
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled || _consumed)
            return;

        if (StartZone == null || Cinematic == null)
        {
            Debug.LogWarning("PigeonStopDialogCinematic : assigner la zone de départ et le DialogCinematic.", this);
            return;
        }

        if (Cinematic.IsPlaying)
            return;

        Cinematic.PlayCinematic();

        // Ne consommer la zone que si le lancement a été accepté.
        if (Cinematic.IsPlaying)
        {
            _consumed = true;
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
        StartZone = GetComponent<MissionStartZone>();
        Cinematic = GetComponent<DialogCinematic>();
    }
}
