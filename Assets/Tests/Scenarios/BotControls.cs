#if UNITY_EDITOR
using UnityEngine;

// Input scripté pour les scénarios : remplace PlayerControls via PigeonController.SetControlsOverride.
// Le pigeon passe ainsi par exactement le même code que pour un vrai joueur.
public class BotControls : IPigeonControls
{
    public Vector2 Movement { get; set; }          // direction monde au sol (x, z)
    public Vector2 RelativeMovement { get; set; }  // vol : x = virage, y = tangage
    public bool Flap { get; set; }
    public bool Dive { get; set; }
    public bool Brake { get; set; }
    public bool Flex => false;
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
        Flap = Dive = Brake = PrecisionCamera = false;
        _interact = _predictedInteract = false;
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

    public bool ConsumeCooing => false;
    public bool ConsumeShit => false;
    public bool ConsumeLeftFlex => false;
    public bool ConsumeRightFlex => false;
}
#endif
