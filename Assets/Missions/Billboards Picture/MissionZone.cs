using DG.Tweening;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MissionZone : MonoBehaviour
{
    [field: SerializeField, Required]
    public BillboardMission Mission { get; private set; }

    [field: SerializeField, Required]
    [field: Tooltip("Texte d'instruction partagé sur le canvas (« Appuyer sur Y / F »), aussi affiché par les MissionStartZone (SetActive) : on lui rend alpha et échelle à chaque masquage.")]
    public CanvasGroup Prompt { get; private set; }

    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 3f;

    [field: SerializeField, MinValue(0f), SuffixLabel("unités/s", true)]
    [field: Tooltip("Vitesse maximale pour considérer le pigeon à l'arrêt.")]
    public float StopSpeedThreshold { get; private set; } = 0.15f;

    [field: SerializeField]
    public UnityEvent OnValidated { get; private set; } = new UnityEvent();

    [field: SerializeField]
    public UnityEvent OnStartValidation { get; private set; } = new UnityEvent();

    private PigeonController _pigeon;
    private PlayerControls _blockableControls;
    private bool _isInside;

    private bool isPromptDisplayed;
    private bool isValidated;

    [SerializeField] private Image cameraFlashImage;
    [SerializeField] private AudioClip cameraAnticipationSFX;
    [SerializeField] private AudioClip cameraSFX;
    [SerializeField] private AudioClip successBillboardSFX;
    [SerializeField] private CinemachineCamera camera;
    [SerializeField] private float cameraSwapDuration = 3f;

    private const float FlashDelay = 1.5f;

    /// <summary>Le prompt est affiché pour cette zone (lu par les scénarios).</summary>
    public bool IsPromptDisplayed => isPromptDisplayed;

    private void OnEnable()
    {
        isValidated = false;
        PigeonController.LocalChanged += BindPigeon;
        BindPigeon(PigeonController.Local);
    }

    private void BindPigeon(PigeonController pigeon)
    {
        _pigeon = pigeon;
        _blockableControls = pigeon != null ? pigeon.GetComponentInChildren<PlayerControls>() : null;
    }

    private void LateUpdate()
    {
        if (isValidated)
            return;

        IPigeonControls controls = _pigeon != null ? _pigeon.Controls : null;

        // Évalué après le déplacement du pigeon dans Update, comme PigeonStopZone.
        bool inside = _pigeon != null && controls != null && _pigeon.isActiveAndEnabled && !_pigeon.IsStun &&
                      (_pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius &&
                      _pigeon.Speed <= StopSpeedThreshold;

        if (inside != _isInside)
        {
            _isInside = inside;
            // Le prompt est partagé : ne le toucher qu'à l'entrée / la sortie de cette zone.
            SetPromptState(inside);
            // Jeter un appui fait hors zone pour ne pas valider dès l'entrée.
            if (inside)
                _ = controls.ConsumeInteract;
            return;
        }

        if (_isInside && controls.ConsumeInteract)
            Validate();
    }

    private void Validate()
    {
        isValidated = true;
        PlayerControls blocked = _blockableControls;

        Sequence sequence = DOTween.Sequence().SetLink(gameObject)
            .AppendCallback(() =>
            {
                PunchAndHidePrompt();
                Mission.ReportZoneCompleted(this);
                if (blocked != null)
                    blocked.SetControlsBlocked(true);
                PlaySFX(cameraAnticipationSFX);
                OnStartValidation.Invoke();
            })
            .AppendInterval(FlashDelay)
            .AppendCallback(ResetCameraFlash)
            .AppendCallback(() => PlaySFX(cameraSFX));
        if (cameraFlashImage != null)
            sequence.Join(cameraFlashImage.DOFade(0.0f, 0.2f));
        sequence
            .AppendInterval(Mathf.Max(0.0f, cameraSwapDuration - FlashDelay))
            .AppendCallback(() =>
            {
                if (cameraFlashImage != null)
                    cameraFlashImage.gameObject.SetActive(false);
                if (blocked != null)
                    blocked.SetControlsBlocked(false);

                OnValidated.Invoke();
                gameObject.SetActive(false);
            });
    }

    private void ResetCameraFlash()
    {
        if (cameraFlashImage == null)
            return;
        cameraFlashImage.gameObject.SetActive(true);
        Color flashColor = cameraFlashImage.color;
        flashColor.a = 0.25f;
        cameraFlashImage.color = flashColor;
    }

    private static void PlaySFX(AudioClip clip)
    {
        if (clip != null && SFXManager.Instance != null)
            SFXManager.Instance.PlaySFX(clip);
    }

    private void OnDisable()
    {
        PigeonController.LocalChanged -= BindPigeon;
        HidePrompt();
        _isInside = false;
    }

    private void SetPromptState(bool state)
    {
        if (Prompt == null)
            return;

        if (state)
            DisplayPrompt();
        else
            HidePrompt();
    }

    private void DisplayPrompt()
    {
        if (isPromptDisplayed)
            return;

        isPromptDisplayed = true;

        DOTween.Kill(Prompt);
        Prompt.transform.localScale = Vector3.one;
        if (!Prompt.gameObject.activeSelf)
            Prompt.alpha = 0f;
        DOTween.Sequence().SetTarget(Prompt)
            .AppendCallback(() => Prompt.gameObject.SetActive(true))
            .Append(Prompt.DOFade(1.0f, 0.3f));
    }

    private void HidePrompt()
    {
        if (!isPromptDisplayed || Prompt == null)
            return;

        isPromptDisplayed = false;

        DOTween.Kill(Prompt);
        DOTween.Sequence().SetTarget(Prompt)
            .Append(Prompt.DOFade(0.0f, 0.3f))
            .AppendCallback(DeactivatePrompt);
    }

    private void PunchAndHidePrompt()
    {
        _isInside = false;

        if (!isPromptDisplayed || Prompt == null)
            return;

        isPromptDisplayed = false;

        DOTween.Kill(Prompt);
        DOTween.Sequence().SetTarget(Prompt)
            .Append(Prompt.GetComponent<RectTransform>().DOPunchScale(Vector3.one * 0.5f, 0.5f))
            .Append(Prompt.DOFade(0.0f, 0.3f))
            .AppendCallback(DeactivatePrompt);
    }

    private void DeactivatePrompt()
    {
        Prompt.gameObject.SetActive(false);
        Prompt.alpha = 1f;
        Prompt.transform.localScale = Vector3.one;
    }

    private void OnDrawGizmosSelected()
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, Radius);
        Gizmos.color = previousColor;
    }

    public void PlayValidatedBillboardSFX()
    {
        float newPitch = 0.95f + Mission.CompletedCount * -0.05f;
        SFXManager.Instance.PlaySFX(successBillboardSFX, pitch: newPitch);
    }
}
