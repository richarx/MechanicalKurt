using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

public class PigeonEquipment : MonoBehaviour
{
    [SerializeField] private GameObject backpack;
    [SerializeField] private GameObject hat;
    [SerializeField] private GameObject shoeLeft;
    [SerializeField] private GameObject shoeRight;
    [SerializeField] private GameObject sunglasses;

    [Space]
    [SerializeField] private CinemachineCamera equipmentCamera;

    [Space]
    [SerializeField] private AudioClip equipClip;
    [SerializeField] private GameObject equipVFX;

    public enum Equipment
    {
        Backpack,
        Hat,
        Shoes,
        Sunglasses
    }

    PlayerControls playerControls;

    private void Start()
    {
        playerControls = GetComponent<PlayerControls>();
    }

    [ContextMenu("Toggle BackPack")]
    public void ToggleBackPack()
    {
        UnlockItem(Equipment.Backpack);
    }

    [ContextMenu("Toggle Hat")]
    public void ToggleHat()
    {
        UnlockItem(Equipment.Hat);
    }

    [ContextMenu("Toggle Shoes")]
    public void ToggleShoes()
    {
        UnlockItem(Equipment.Shoes);
    }

    [ContextMenu("Toggle Sunglasses")]
    public void ToggleSunglasses()
    {
        UnlockItem(Equipment.Sunglasses);
    }

    public void UnlockItem(Equipment equipment)
    {
        playerControls.SetControlsBlocked(true);
        equipmentCamera.Priority = 100;

        Sequence sequence = DOTween.Sequence();

        (GameObject equipment1, GameObject equipment2) = GetEquipmentObjects(equipment);

        sequence.AppendInterval(1.5f);
        sequence.AppendCallback(() => ActivateEquipmentObject(equipment));

        if (equipment1 != null)
        {
            sequence.Append(equipment1.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f));
            sequence.JoinCallback(() => Destroy(Instantiate(equipVFX, equipment1.transform.position, Quaternion.identity), 3.0f));
        }

        if (equipment2 != null)
        {
            sequence.Join(equipment2.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f));
            sequence.JoinCallback(() => Destroy(Instantiate(equipVFX, equipment2.transform.position, Quaternion.identity), 3.0f));
        }

        sequence.JoinCallback(() => SFXManager.Instance.PlaySFX(equipClip, 0.5f));


        sequence.AppendCallback(() => playerControls.SetForceFlex(true));
        sequence.AppendInterval(3.0f);
        sequence.AppendCallback(() => playerControls.SetForceFlex(false));

        sequence.AppendCallback(() => equipmentCamera.Priority = 0);
        sequence.AppendCallback(() => playerControls.SetControlsBlocked(false));

        sequence.AppendCallback(() => MusicPlayer.instance.PlayMusic(MusicPlayer.Songs.NightDelivery, 5.0f));
    }

    private (GameObject, GameObject) GetEquipmentObjects(Equipment equipment)
    {
        switch (equipment)
        {
            case Equipment.Backpack:
                return (backpack, null);
            case Equipment.Hat:
                return (hat, null);
            case Equipment.Shoes:
                return (shoeLeft, shoeRight);
            case Equipment.Sunglasses:
                return (sunglasses, null);
            default:
                return (null, null);
        }
    }

    private void ActivateEquipmentObject(Equipment equipment)
    {
        switch (equipment)
        {
            case Equipment.Backpack:
                backpack.SetActive(!backpack.activeSelf);
                break;
            case Equipment.Hat:
                hat.SetActive(!hat.activeSelf);
                break;
            case Equipment.Shoes:
                shoeLeft.SetActive(!shoeLeft.activeSelf);
                shoeRight.SetActive(!shoeRight.activeSelf);
                break;
            case Equipment.Sunglasses:
                sunglasses.SetActive(!sunglasses.activeSelf);
                break;
        }
    }
}
