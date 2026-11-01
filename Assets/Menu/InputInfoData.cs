using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InputInfoData", menuName = "Scriptable Objects/InputInfoData")]
public class InputInfoData : ScriptableObject
{
    public string label;
    public List<Sprite> gamepadSprites;
    public List<Sprite> keyboardSprites;
    public bool isSmallGamepadIcon;
    public bool isSmallKeyboardIcon;
}
