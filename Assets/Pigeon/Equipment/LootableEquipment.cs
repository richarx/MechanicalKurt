using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class LootableEquipment : MonoBehaviour
{
    [SerializeField] private Transform item;
    [SerializeField] private Transform LightShaft;

    [Space]
    [SerializeField] PigeonController _pigeon;

    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 2f;

    [field: SerializeField, MinValue(0f), SuffixLabel("unités/s", true)]
    [field: Tooltip("Vitesse maximale pour considérer le pigeon à l'arrêt.")]
    public float StopSpeedThreshold { get; private set; } = 0.15f;

    [field: SerializeField, MinValue(0f), SuffixLabel("s", true)]
    [field: Tooltip("Durée d'arrêt requise avant de déclencher l'événement. Mettre 0 pour un déclenchement immédiat.")]
    public float StopDuration { get; private set; } = 0.25f;

    [field: SerializeField]
    public UnityEvent OnPlayerStopped { get; private set; } = new UnityEvent();

    private Sequence glideSequence;
    private Sequence spinSequence;

    private void Start()
    {
        glideSequence = DOTween.Sequence();
        glideSequence.Append(item.DOLocalMoveY(1.0f, 3.0f).SetEase(Ease.Linear));
        glideSequence.Join(LightShaft.DOScale(2.5f, 3.0f));
        glideSequence.SetLoops(-1, LoopType.Yoyo);

        spinSequence = DOTween.Sequence();
        spinSequence.Append(item.DORotate(new Vector3(0.0f, 359.99f, 0.0f), 5.0f, RotateMode.LocalAxisAdd).SetEase(Ease.Linear));
        spinSequence.SetLoops(-1, LoopType.Restart);
    }

    private float _stoppedTime;
    private float _nextPigeonSearch;
    private bool _hasTriggered;

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f)
            return;

        if (_pigeon == null && Time.time >= _nextPigeonSearch)
        {
            _pigeon = FindAnyObjectByType<PigeonController>();
            _nextPigeonSearch = Time.time + 1f;
        }

        // Evaluate after the player has moved in Update, using its actual speed.
        bool stoppedInside = _pigeon != null && _pigeon.isActiveAndEnabled &&
                             (_pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius &&
                             _pigeon.Speed <= StopSpeedThreshold;

        if (!stoppedInside)
        {
            _stoppedTime = 0f;
            _hasTriggered = false;
            return;
        }

        if (_hasTriggered)
            return;

        _stoppedTime += Time.deltaTime;
        if (_stoppedTime >= StopDuration)
        {
            _hasTriggered = true;

            glideSequence.Kill();
            spinSequence.Kill();

            OnPlayerStopped?.Invoke();
            Destroy(gameObject);
        }
    }
}
