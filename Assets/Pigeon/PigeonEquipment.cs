using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Équipement porté : stocké dans l'état prédit du pigeon (vu par tous), changé par requête d'input.
/// La cinématique de ramassage reste locale au joueur qui ramasse.
/// </summary>
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

    [Space]
    [SerializeField] private AchievementData fashionAchievement;

    /// <summary>
    /// Valeur = numéro de bit dans l'état réseau, et sérialisée dans les prefabs de loot : n'ajouter qu'à la fin.
    /// </summary>
    public enum Equipment
    {
        Backpack,
        Hat,
        Shoes,
        Sunglasses
    }

    public const int Count = (int)Equipment.Sunglasses + 1;
    public const byte AllBits = (1 << Count) - 1;
    public static byte Bit(Equipment equipment) => (byte)(1 << (int)equipment);

    PigeonController _pigeon;
    PlayerControls playerControls;
    bool _viewApplied;
    byte _shownMask;

    private void Awake()
    {
        _pigeon = GetComponent<PigeonController>();
        playerControls = GetComponent<PlayerControls>();
    }

    /// <summary>Équipements affichés en ce moment (vue).</summary>
    public bool IsWorn(Equipment equipment) => (CurrentWornMask() & Bit(equipment)) != 0;

    /// <summary>Masque de ce que montrent les objets en ce moment (sert d'état initial au spawn).</summary>
    public byte CurrentWornMask()
    {
        byte mask = 0;
        if (backpack != null && backpack.activeSelf) mask |= Bit(Equipment.Backpack);
        if (hat != null && hat.activeSelf) mask |= Bit(Equipment.Hat);
        if (shoeLeft != null && shoeLeft.activeSelf) mask |= Bit(Equipment.Shoes);
        if (sunglasses != null && sunglasses.activeSelf) mask |= Bit(Equipment.Sunglasses);
        return mask;
    }

    [ContextMenu("Toggle BackPack")]
    public void ToggleBackPack()
    {
        PlayUnlock(Equipment.Backpack, toggle: true);
    }

    [ContextMenu("Toggle Hat")]
    public void ToggleHat()
    {
        PlayUnlock(Equipment.Hat, toggle: true);
    }

    [ContextMenu("Toggle Shoes")]
    public void ToggleShoes()
    {
        PlayUnlock(Equipment.Shoes, toggle: true);
    }

    [ContextMenu("Toggle Sunglasses")]
    public void ToggleSunglasses()
    {
        PlayUnlock(Equipment.Sunglasses, toggle: true);
    }

    public void UnlockItem(Equipment equipment)
    {
        PlayUnlock(equipment, toggle: false);
    }

    private void PlayUnlock(Equipment equipment, bool toggle)
    {
        if (_pigeon != null && !_pigeon.isOwner)
        {
            Request(equipment, toggle);
            return;
        }

        // En vol ou en marchant vite : pas de cinématique, le joueur garde ses commandes.
        bool stillOnGround = _pigeon == null || (_pigeon.IsGrounded && _pigeon.Speed <= CinematicMaxSpeed);
        if (!stillOnGround)
        {
            Request(equipment, toggle);
            SFXManager.Instance.PlaySFX(equipClip, 0.5f);
            DOVirtual.DelayedCall(0.2f, FinishUnlock);
            return;
        }

        if (playerControls != null)
            playerControls.SetControlsBlocked(true);
        if (equipmentCamera != null)
            equipmentCamera.Priority = 100;

        Sequence sequence = DOTween.Sequence();

        sequence.AppendInterval(1.5f);
        sequence.AppendCallback(() => Request(equipment, toggle));
        sequence.AppendCallback(() => SFXManager.Instance.PlaySFX(equipClip, 0.5f));
        sequence.AppendInterval(0.5f);

        sequence.AppendCallback(() => { if (playerControls != null) playerControls.SetForceFlex(true); });
        sequence.AppendInterval(3.0f);
        sequence.AppendCallback(() => { if (playerControls != null) playerControls.SetForceFlex(false); });

        sequence.AppendCallback(() => { if (equipmentCamera != null) equipmentCamera.Priority = 0; });
        sequence.AppendCallback(() => { if (playerControls != null) playerControls.SetControlsBlocked(false); });
        sequence.AppendCallback(FinishUnlock);
    }

    const float CinematicMaxSpeed = 0.5f;

    void FinishUnlock()
    {
        if (this == null)
            return;
        if (CurrentWornMask() == AllBits && fashionAchievement != null && AchievementManager.Instance != null)
            AchievementManager.Instance.Unlock(fashionAchievement);
        if (MusicPlayer.instance != null)
            MusicPlayer.instance.PlayMusic(MusicPlayer.Songs.NightDelivery, 5.0f);
    }

    private void Request(Equipment equipment, bool toggle)
    {
        if (_pigeon != null)
            _pigeon.RequestEquipment(equipment, toggle);
        else
            ApplyWorn((byte)(toggle ? CurrentWornMask() ^ Bit(equipment) : CurrentWornMask() | Bit(equipment)));
    }

    public void ApplyWorn(byte worn)
    {
        if (_viewApplied && worn == _shownMask)
            return;

        byte changed = (byte)(worn ^ _shownMask);
        bool animate = _viewApplied;
        _viewApplied = true;
        _shownMask = worn;

        for (int i = 0; i < Count; i++)
        {
            Equipment equipment = (Equipment)i;
            byte bit = Bit(equipment);
            if (animate && (changed & bit) == 0)
                continue;

            bool on = (worn & bit) != 0;
            (GameObject first, GameObject second) = GetEquipmentObjects(equipment);
            ShowEquipmentObject(first, on, animate);
            ShowEquipmentObject(second, on, animate);
        }
    }

    private void ShowEquipmentObject(GameObject equipmentObject, bool on, bool animate)
    {
        if (equipmentObject == null)
            return;
        equipmentObject.SetActive(on);
        if (!animate || !on)
            return;

        equipmentObject.transform.DOComplete();
        equipmentObject.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f).SetLink(equipmentObject);
        if (equipVFX != null)
            Destroy(Instantiate(equipVFX, equipmentObject.transform.position, Quaternion.identity), 3.0f);
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
}
