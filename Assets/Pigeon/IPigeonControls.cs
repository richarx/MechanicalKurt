using UnityEngine;

public interface IPigeonControls
{
    Vector2 RelativeMovement { get; }
    Vector2 Movement { get; }
    bool ConsumeFlap { get; }
    bool Dive { get; }
}
