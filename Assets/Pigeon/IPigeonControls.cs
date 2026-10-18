using UnityEngine;

public interface IPigeonControls
{
    Vector2 RelativeMovement { get; }
    Vector2 Movement { get; }
    bool Flap { get; }
    bool Dive { get; }
}
