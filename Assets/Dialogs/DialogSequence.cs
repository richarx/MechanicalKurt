using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogLine
{
    [TextArea(2, 5)] public string text;
}

[CreateAssetMenu(menuName = "Dialog/Dialog Sequence", fileName = "Dialogue_New")]
public class DialogSequence : ScriptableObject
{
    public string speaker;
    public List<DialogLine> lines = new List<DialogLine>();
}
