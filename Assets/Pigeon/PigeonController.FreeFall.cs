using PurrNet.Prediction;
using UnityEngine;

/// <summary>Participation à la chute libre, conservée dans l'état prédit du pigeon.</summary>
public struct FreeFallProgress
{
    public PredictedComponentID Activity;
    public int StartCount;
    public bool Active;
    public bool Finished;
    public bool Dismissed;
    public byte Place;
    public FreeFallChallenge.LossReason Loss;
    public Vector3 InputPosition;
    public float CurrentHeight;
    public float MinimumHeight;
    public bool HasHeight;
    public float MovementDeadZone;

    public bool IsIn(PredictedComponentID activity, int startCount)
        => startCount > 0 && StartCount == startCount && Activity.Equals(activity);
}

public partial class PigeonController
{
    public FreeFallProgress FreeFallProgress => _viewState.FreeFall;
    public bool IsInMultiplayerActivity => _viewState.Race.Active || _viewState.FreeFall.Active;

    private static bool IsFreeFalling(in PigeonState state) => state.FreeFall.Active
        && !state.FreeFall.Finished && state.FreeFall.Loss == FreeFallChallenge.LossReason.None;

    /// <summary>Annule l'élan au sommet du lâcher, dans la simulation de l'activité.</summary>
    [SimulationOnly]
    public void ResetFreeFallMotion() => ResetFreeFallMotion(ref currentState);

    private static void ResetFreeFallMotion(ref PigeonState state)
    {
        state.LastVelocity = Vector3.zero;
        state.PendingImpulse = Vector3.zero;
        state.Pitch = 0f;
        state.PreserveTeleportPitch = false;
        state.Grounded = false;
        state.Stunned = false;
        state.StunRemaining = 0f;
        state.StunRotation = Quaternion.identity;
        state.Mode = (byte)MovementState.Falling;
        ClearSupport(ref state);
    }

    private static Vector3 FreeFallVelocity(in PigeonState state, float gravity, float dt)
        => new Vector3(0f, state.LastVelocity.y + gravity * dt, 0f);

    private void SimulateFreeFall(ref PigeonState state, float dt)
    {
        state.Mode = (byte)MovementState.Falling;
        state.PendingImpulse = Vector3.zero;
        _stunPossible = false;
        // Gravité seule : aucune portance, direction automatique, traînée, impulsion ou courant ascendant.
        MoveController(ref state, FreeFallVelocity(state, Physics.gravity.y, dt), dt, allowSupport: false);
        UpdateStamina(ref state, MovementState.Falling, dt);
        if (state.Grounded)
        {
            _landEvent.Invoke();
            if (_stamina != null)
                AddStaminaBoost(ref state, _stamina.MaxStamina);
        }
        CaptureFreeFallContact(ref state);
    }

    // Capturer AVANT le déplacement : un battement ou une réapparition ne doit pas améliorer la hauteur.
    static void CaptureFreeFallInput(in PigeonInput input, ref PigeonState state)
    {
        ref FreeFallProgress progress = ref state.FreeFall;
        if (!progress.Active || progress.Finished || progress.Loss != FreeFallChallenge.LossReason.None)
            return;

        FreeFallChallenge.LossReason reason = state.Grounded
            ? FreeFallChallenge.LossReason.Ground
            : FreeFallChallenge.GetInputLoss(input, progress.MovementDeadZone);
        if (reason == FreeFallChallenge.LossReason.None)
            return;
        progress.Loss = reason;
        progress.InputPosition = state.Position;
    }

    void CaptureFreeFallContact(ref PigeonState state)
    {
        ref FreeFallProgress progress = ref state.FreeFall;
        if (progress.Active && !progress.Finished && (state.Grounded || UnderWater(state.Position)))
        {
            progress.Loss = FreeFallChallenge.LossReason.Ground;
            progress.InputPosition = state.Position;
        }
    }
}
