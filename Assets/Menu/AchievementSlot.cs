using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image lockedBorder;
    [SerializeField] private Image lockedForeground;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI description;
    [SerializeField] private Sprite defaultIcon;



    public void Setup(AchievementData data, bool isCompleted)
    {
        icon.sprite = data.Icon != null ? data.Icon : defaultIcon;
        lockedBorder.gameObject.SetActive(!isCompleted);
        lockedForeground.gameObject.SetActive(!isCompleted);
        title.text = data.Title;
        description.text = data.Description;
    }
}
