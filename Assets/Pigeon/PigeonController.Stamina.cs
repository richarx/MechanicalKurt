using UnityEngine;

public partial class PigeonController
{
    MovementState ComputeMovementState(in PigeonInput input, in PigeonState s)
    {
        if (s.Grounded && (s.LastVelocity.y <= 0.001f || s.SupportId != 0))
            return MovementState.Grounded;
        if (input.Brake || FinishedRace(s))
            return MovementState.Braking;
        if (input.Flap && CanFlap(s))
            return MovementState.Flapping;
        if (input.Dive)
            return MovementState.Diving;
        return MovementState.Gliding;
    }

    /// <summary>Arrivé d'une course ou vainqueur de la chute libre : vol stationnaire jusqu'à la sortie des résultats.</summary>
    static bool FinishedRace(in PigeonState s) => (s.Race.Active && s.Race.Place > 0)
        || (s.FreeFall.Active && s.FreeFall.Finished && s.FreeFall.Loss == FreeFallChallenge.LossReason.None);

    /// <summary>Un battement commencé continue tant qu'il reste de l'endurance ; pour en commencer un, il faut le seuil.</summary>
    bool CanFlap(in PigeonState s)
    {
        if (!UsesStamina)
            return true;
        bool keepsFlapping = (MovementState)s.Mode == MovementState.Flapping && s.Stamina > 0f;
        return keepsFlapping || s.Stamina >= _stamina.FlapThreshold;
    }

    void UpdateStamina(ref PigeonState s, MovementState mode, float dt)
    {
        if (_stamina == null)
            return;
        float rate;
        if (mode == MovementState.Diving)
            rate = _stamina.RecoveryRateDive;
        else if (mode == MovementState.Flapping)
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
