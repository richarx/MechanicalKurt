using UnityEngine;

public partial class PigeonController
{
    MovementState ComputeMovementState(in PigeonInput input, in PigeonState s)
    {
        // Déjà au sol : monter une pente en marchant donne une vitesse vers le haut, ce n'est pas un décollage.
        bool wasWalking = (MovementState)s.Mode is MovementState.Grounded or MovementState.Dancing;
        if (s.Grounded && (s.LastVelocity.y <= 0.001f || s.SupportId != 0 || wasWalking))
            return input.Dance ? MovementState.Dancing : MovementState.Grounded;
        if (input.Brake || FinishedRace(s))
            return input.Flap && CanFlap(s, MovementState.HoverFlapping) ? MovementState.HoverFlapping : MovementState.Braking;
        if (input.Flap && CanFlap(s, MovementState.Flapping))
            return MovementState.Flapping;
        if (input.Dive)
            return MovementState.Diving;
        return MovementState.Gliding;
    }

    /// <summary>Arrivé d'une course chronométrée : vol stationnaire jusqu'à la sortie des résultats.</summary>
    static bool FinishedRace(in PigeonState s) => s.Race.Active && s.Race.Place > 0;

    /// <summary>Un battement commencé continue tant qu'il reste de l'endurance ; pour en commencer un, il faut le seuil.</summary>
    bool CanFlap(in PigeonState s, MovementState flappingMode)
    {
        if (!UsesStamina)
            return true;
        bool keepsFlapping = (MovementState)s.Mode == flappingMode && s.Stamina > 0f;
        return keepsFlapping || s.Stamina >= _stamina.FlapThreshold;
    }

    void UpdateStamina(ref PigeonState s, MovementState mode, float dt)
    {
        if (_stamina == null)
            return;
        float rate;
        if (mode == MovementState.Diving)
            rate = _stamina.RecoveryRateDive;
        else if (mode is MovementState.Flapping or MovementState.HoverFlapping)
            rate = -_stamina.DepletionRateFlap;
        else if (mode == MovementState.Braking)
            rate = _stamina.RecoveryRateBrake;
        else if (UnderWater(s.Position))
            rate = _stamina.RecoveryRateUnderWater;
        else
            rate = _stamina.RecoveryRateRegular;
        s.Stamina = Mathf.Clamp(s.Stamina + rate * dt, 0f, _stamina.MaxStamina);
    }

    /// <summary>Regain instantané (tonneau, anneau, atterrissage), signalé à la vue par StaminaBoosts.</summary>
    void AddStaminaBoost(ref PigeonState s, float amount)
    {
        if (_stamina == null || s.Stamina >= _stamina.MaxStamina)
            return;
        s.Stamina = Mathf.Min(s.Stamina + amount, _stamina.MaxStamina);
        s.StaminaBoosts++;
    }
}
