using UnityEngine;

[DisallowMultipleComponent]
public class CharacterRacePath : CityPath
{
    protected override bool IsLooping => false;

    protected override void Reset()
    {
        base.Reset();
        SetGizmoColor(Color.cyan);
    }
}
