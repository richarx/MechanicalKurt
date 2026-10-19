using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class PigeonStopDialogCinematic : MonoBehaviour
{
    [field: SerializeField, Required]
    public PigeonStopZone StopZone { get; private set; }

    [field: SerializeField, Required]
    public DialogCinematic Cinematic { get; private set; }

    [field: SerializeField, Required]
    [field: Tooltip("Achievement validé dès que le lancement de la cinématique est accepté.")]
    public AchievementData CompletionAchievement { get; private set; }

    [SerializeField] private GameObject _cylinder;

    // Brancher sur PigeonStopZone.OnPlayerStopped.
    public void PlayCinematic()
    {
        if (!isActiveAndEnabled)
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
