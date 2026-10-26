using DG.Tweening;
using DG.Tweening.Core.Easing;
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
    [field: Tooltip("Texte d'instruction partagé sur le canvas (« Appuyer sur Y / F »).")]
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
    private PlayerControls _controls;
    private float _nextPigeonSearch;
    private bool _isInside;

    private Sequence promptAnimationSequence;
    private bool isPromptDisplayed;
    private bool isValidated;

    [SerializeField] private Image cameraFlashImage;
    [SerializeField] private AudioClip cameraAnticipationSFX;
    [SerializeField] private AudioClip cameraSFX;
    [SerializeField] private AudioClip successBillboardSFX;
    [SerializeField] private CinemachineCamera camera;
    [SerializeField] private float cameraSwapDuration = 3f;

    private void LateUpdate()
    {
        if (isValidated)
            return;

        if (_pigeon == null && Time.time >= _nextPigeonSearch)
        {
            _pigeon = FindAnyObjectByType<PigeonController>();
            _controls = _pigeon != null ? _pigeon.GetComponentInChildren<PlayerControls>() : null;
            if (_controls == null)
                _controls = FindAnyObjectByType<PlayerControls>();
            _nextPigeonSearch = Time.time + 1f;
        }

        // Évalué après le déplacement du pigeon dans Update, comme PigeonStopZone.
        bool inside = _pigeon != null && _controls != null && _pigeon.isActiveAndEnabled &&
                      (_pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius &&
                      _pigeon.Speed <= StopSpeedThreshold;

        if (inside != _isInside)
        {
            _isInside = inside;
            // Le prompt est partagé : ne le toucher qu'à l'entrée / la sortie de cette zone.
            SetPromptState(inside);
            // Jeter un appui fait hors zone pour ne pas valider dès l'entrée.
            if (inside)
                _ = _controls.ConsumeInteract;
            return;
        }

        if (_isInside && _controls.ConsumeInteract)
            Validate();
    }

    private void Validate()
    {
        Sequence sequence = DOTween.Sequence()
            .AppendCallback(() =>
            {
                isValidated = true;
                PunchAndHidePrompt();
                Mission.ReportZoneCompleted(this);
                _controls.SetControlsBlocked(true);
                SFXManager.Instance.PlaySFX(cameraAnticipationSFX);
                OnStartValidation.Invoke();
            })
            .AppendInterval(1.5f)
            .AppendCallback(() => ResetCameraFlash())
            .AppendCallback(() => SFXManager.Instance.PlaySFX(cameraSFX))
            .Join(cameraFlashImage.DOFade(0.0f, 0.2f))
            .AppendInterval(Mathf.Min(0.0f, cameraSwapDuration - 1.5f))
            .AppendCallback(() =>
            {
                cameraFlashImage.gameObject.SetActive(false);
                _controls.SetControlsBlocked(false);

                OnValidated.Invoke();
                gameObject.SetActive(false);
            });
    }

    private void ResetCameraFlash()
    {
        cameraFlashImage.gameObject.SetActive(true);
        Color flashColor = cameraFlashImage.color;
        flashColor.a = 0.25f;
        cameraFlashImage.color = flashColor;
    }

    private void OnDisable()
    {
        HidePrompt();
        _nextPigeonSearch = 0f;
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

        promptAnimationSequence?.Kill();

        promptAnimationSequence = DOTween.Sequence()
            .AppendCallback(() => Prompt.gameObject.SetActive(true))
            .Append(Prompt.DOFade(1.0f, 0.3f));
    }

    private void HidePrompt()
    {
        if (!isPromptDisplayed)
            return;

        isPromptDisplayed = false;

        promptAnimationSequence?.Kill();

        promptAnimationSequence = DOTween.Sequence()
            .Append(Prompt.DOFade(0.0f, 0.3f))
            .AppendCallback(() => Prompt.gameObject.SetActive(false));
    }

    private void PunchAndHidePrompt()
    {
        if (Prompt == null)
            return;

        if (!isPromptDisplayed)
            return;

        isPromptDisplayed = false;

        promptAnimationSequence?.Kill();

        promptAnimationSequence = DOTween.Sequence()
            .Append(Prompt.GetComponent<RectTransform>().DOPunchScale(Vector3.one * 0.5f, 0.5f))
            .Append(Prompt.DOFade(0.0f, 0.3f))
            .AppendCallback(() => Prompt.gameObject.SetActive(false));

        _isInside = false;
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
