using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;

// Équipement du pigeon (sac, chapeau, chaussures, lunettes).
// - Ce qui est porté vit dans l'état prédit du pigeon (PigeonState.Equipment) : tous les clients le voient,
//   y compris ceux qui arrivent en cours de partie. On ne le change que par une requête d'input (RequestEquipment).
// - La vue (objets affichés, petit « pop » et VFX) se déduit de cet état sur chaque client : ApplyWorn.
// - La petite cinématique de ramassage (caméra, pose, musique, son) reste locale au joueur qui ramasse.
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

    // La valeur sert de numéro de bit dans l'état réseau ET est sérialisée dans les prefabs de loot (_equipment) :
    // n'ajouter qu'à la fin, ne jamais réordonner. Au-delà de 8, passer les masques (état, input) en ushort.
    public enum Equipment
    {
        Backpack,   // bit 0
        Hat,        // bit 1
        Shoes,      // bit 2
        Sunglasses  // bit 3
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

    // Ramassage (LootableEquipment) : porte l'équipement.
    public void UnlockItem(Equipment equipment)
    {
        PlayUnlock(equipment, toggle: false);
    }

    private void PlayUnlock(Equipment equipment, bool toggle)
    {
        // Caméra, commandes, pose et musique appartiennent au joueur local : un bot (ou un pigeon contrôlé
        // ailleurs) s'équipe directement, sans cinématique chez ce client.
        if (_pigeon != null && !_pigeon.isOwner)
        {
            Request(equipment, toggle);
            return;
        }

        if (playerControls != null)
            playerControls.SetControlsBlocked(true);
        if (equipmentCamera != null)
            equipmentCamera.Priority = 100;

        Sequence sequence = DOTween.Sequence();

        sequence.AppendInterval(1.5f);
        // L'objet apparaît quand la simulation a appliqué la requête (ApplyWorn), chez tous les joueurs.
        sequence.AppendCallback(() => Request(equipment, toggle));
        sequence.AppendCallback(() => SFXManager.Instance.PlaySFX(equipClip, 0.5f));
        sequence.AppendInterval(0.5f);

        sequence.AppendCallback(() => { if (playerControls != null) playerControls.SetForceFlex(true); });
        sequence.AppendInterval(3.0f);
        sequence.AppendCallback(() => { if (playerControls != null) playerControls.SetForceFlex(false); });

        sequence.AppendCallback(() => { if (equipmentCamera != null) equipmentCamera.Priority = 0; });
        sequence.AppendCallback(() => { if (playerControls != null) playerControls.SetControlsBlocked(false); });

        sequence.AppendCallback(() => { if (MusicPlayer.instance != null) MusicPlayer.instance.PlayMusic(MusicPlayer.Songs.NightDelivery, 5.0f); });
    }

    private void Request(Equipment equipment, bool toggle)
    {
        if (_pigeon != null)
            _pigeon.RequestEquipment(equipment, toggle);
        else
            ApplyWorn((byte)(toggle ? CurrentWornMask() ^ Bit(equipment) : CurrentWornMask() | Bit(equipment)));
    }

    // Vue, sur chaque client, à partir de l'état du pigeon. Le premier passage (spawn, arrivée en cours de partie)
    // pose l'état sans effet ; ensuite, un objet qui apparaît fait un petit « pop » avec VFX.
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
        // Lié à l'objet : le pigeon peut disparaître pendant le « pop » (joueur qui se déconnecte).
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
