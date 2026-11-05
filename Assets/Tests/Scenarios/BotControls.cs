#if UNITY_EDITOR
using UnityEngine;

public class BotControls : IPigeonControls
{
    public Vector2 Movement { get; set; }
    public Vector2 RelativeMovement { get; set; }
    public bool Flap { get; set; }
    public bool Dive { get; set; }
    public bool Brake { get; set; }
    public bool Flex { get; set; }
    public bool PrecisionCamera { get; set; }

    bool _interact;
    bool _predictedInteract;

    /// <summary>Un appui sur Interact (F / Y), vu à la fois par l'input prédit et par les scripts classiques.</summary>
    public void PressInteract()
    {
        _interact = true;
        _predictedInteract = true;
    }

    public void ReleaseAll()
    {
        Movement = RelativeMovement = Vector2.zero;
        Flap = Dive = Brake = Flex = PrecisionCamera = false;
        _interact = _predictedInteract = _cooing = false;
    }

    public bool ConsumeInteract
    {
        get { bool v = _interact; _interact = false; return v; }
    }

    public bool ConsumePredictedInteract
    {
        get
        {
            bool v = _predictedInteract;
            _predictedInteract = false;
            if (v) PredictedInteractConsumed++;
            return v;
        }
    }

    /// <summary>Diagnostic : appuis réellement lus par l'input prédit du pigeon.</summary>
    public int PredictedInteractConsumed { get; private set; }
    public bool HasPendingInteract => _predictedInteract;

    bool _cooing;

    /// <summary>Un appui sur la touche de roucoulement.</summary>
    public void PressCoo() => _cooing = true;

    public bool ConsumeCooing
    {
        get { bool v = _cooing; _cooing = false; return v; }
    }
    public bool ConsumeShit => false;
    public bool ConsumeLeftFlex => false;
    public bool ConsumeRightFlex => false;
}
#endif
