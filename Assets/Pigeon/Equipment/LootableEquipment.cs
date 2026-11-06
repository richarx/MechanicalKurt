using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class LootableEquipment : MonoBehaviour
{
    [SerializeField] private Transform item;
    [SerializeField] private Transform LightShaft;

    [Space]
    [SerializeField, Tooltip("Facultatif : forcer un pigeon précis. Vide = pigeon du joueur local.")]
    PigeonController _pigeon;

    [SerializeField, Tooltip("Équipement donné au pigeon qui ramasse l'objet (son PigeonEquipment, dans le prefab du joueur).")]
    PigeonEquipment.Equipment _equipment = PigeonEquipment.Equipment.Hat;

    public PigeonEquipment.Equipment Equipment => _equipment;

    [field: SerializeField, MinValue(0.01f), SuffixLabel("unités", true)]
    public float Radius { get; private set; } = 2f;

    [field: SerializeField]
    [field: Tooltip("Reçoit le pigeon qui a ramassé l'objet, après l'équipement.")]
    public UnityEvent<PigeonController> OnPlayerStopped { get; private set; } = new UnityEvent<PigeonController>();

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

    private void LateUpdate()
    {
        PigeonController pigeon = _pigeon != null ? _pigeon : PigeonController.Local;

        // Ramassé au contact de la zone, même en plein vol. Evaluate after the player has moved in Update.
        bool inside = pigeon != null && pigeon.isActiveAndEnabled && !pigeon.IsStun &&
                      (pigeon.transform.position - transform.position).sqrMagnitude <= Radius * Radius;
        if (!inside)
            return;

        glideSequence.Kill();
        spinSequence.Kill();

        PigeonEquipment equipment = pigeon.GetComponentInChildren<PigeonEquipment>();
        if (equipment != null)
            equipment.UnlockItem(_equipment);
        else
            Debug.LogWarning("LootableEquipment : le pigeon n'a pas de PigeonEquipment.", this);

        OnPlayerStopped?.Invoke(pigeon);
        Destroy(gameObject);
    }
}
