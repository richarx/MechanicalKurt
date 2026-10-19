using UnityEngine;

public class PigeonEquipment : MonoBehaviour
{
    [SerializeField] private GameObject backpack;
    [SerializeField] private GameObject hat;
    [SerializeField] private GameObject shoeLeft;
    [SerializeField] private GameObject shoeRight;

    [ContextMenu("Toggle BackPack")]
    public void ToggleBackPack()
    {
        backpack.SetActive(!backpack.activeSelf);
    }

    [ContextMenu("Toggle Hat")]
    public void ToggleHat()
    {
        hat.SetActive(!hat.activeSelf);
    }

    [ContextMenu("Toggle Shoes")]
    public void ToggleShoes()
    {
        shoeLeft.SetActive(!shoeLeft.activeSelf);
        shoeRight.SetActive(!shoeRight.activeSelf);
    }
}
