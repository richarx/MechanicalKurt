using UnityEngine;
using UnityEngine.UI;

public class InputIcon : MonoBehaviour
{
    [SerializeField] private Image foreground;

    public void Setup(Sprite icon)
    {
        foreground.sprite = icon;
    }
}
