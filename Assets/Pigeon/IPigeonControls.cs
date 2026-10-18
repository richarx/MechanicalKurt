using UnityEngine;

public interface IPigeonControls
{
    Vector2 RelativeMovement { get; }
    Vector2 Movement { get; }
    bool Flap { get; }
    bool Dive { get; }
    bool Brake { get; }
    bool Flex { get; }
    bool ConsumeInteract { get; }
    bool ConsumeCooing { get; }
    bool ConsumeShit { get; }
    bool ConsumeLeftFlex { get; }
    bool ConsumeRightFlex { get; }
}
