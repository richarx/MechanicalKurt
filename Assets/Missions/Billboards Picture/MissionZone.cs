using System.Collections;
using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

public class MissionZone : MonoBehaviour
{
    [field: SerializeField, Required]
    public BillboardMission Mission { get; private set; }

    [field: SerializeField, Required]
    [field: Tooltip("Texte d'instruction partagé sur le canvas (« Appuyer sur Y / F »).")]
    public GameObject Prompt { get; private set; }

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

    [SerializeField] private AudioClip pictureBillboardSFX;
    [SerializeField] private AudioClip successBillboardSFX;
    [SerializeField] private CinemachineCamera camera;
    [SerializeField] private float cameraSwapDuration = 3f;


    private void LateUpdate()
    {
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
            Prompt.SetActive(inside);
            // Jeter un appui fait hors zone pour ne pas valider dès l'entrée.
            if (inside)
                _ = _controls.ConsumeInteract;
            return;
        }

        if (_isInside && _controls.ConsumeInteract)
            StartCoroutine(Validate());
    }

    private IEnumerator Validate()
    {
        HidePrompt();
        Mission.ReportZoneCompleted(this);
        OnStartValidation.Invoke();

        _controls.SetControlsBlocked(true);
        yield return new WaitForSeconds(cameraSwapDuration);
        _controls.SetControlsBlocked(false);
        
        OnValidated.Invoke();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        HidePrompt();
        _nextPigeonSearch = 0f;
    }

    private void HidePrompt()
    {
        if (_isInside && Prompt != null)
            Prompt.SetActive(false);
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
        SFXManager.Instance.PlaySFX(pictureBillboardSFX);
        float newPitch = 0.95f + Mission.CompletedCount * -0.05f;
        SFXManager.Instance.PlaySFX(successBillboardSFX, delay: 0.2f, pitch: newPitch);
    }
}
