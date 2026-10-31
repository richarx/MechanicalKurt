using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputInfo : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private RectTransform gamepadIconHolder;
    [SerializeField] private RectTransform keyboardIconHolder;

    [Space]
    [SerializeField] private InputIcon iconPrefab;
    [SerializeField] private InputIcon smallIconPrefab;

    [Space]
    [SerializeField] private Color lightColor;
    [SerializeField] private Color darkColor;


    public void Setup(InputInfoData data, bool isDark)
    {
        background.color = isDark ? darkColor : lightColor;
        label.text = data.label;

        foreach (Sprite sprite in data.gamepadSprites)
        {
            InputIcon inputIcon = Instantiate(data.isSmallGamepadIcon ? smallIconPrefab : iconPrefab, gamepadIconHolder);
            inputIcon.Setup(sprite);
        }

        foreach (Sprite sprite in data.keyboardSprites)
        {
            InputIcon inputIcon = Instantiate(data.isSmallKeyboardIcon ? smallIconPrefab : iconPrefab, keyboardIconHolder);
            inputIcon.Setup(sprite);
        }
    }
}
