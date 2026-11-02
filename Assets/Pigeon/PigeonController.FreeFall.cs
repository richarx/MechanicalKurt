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
    public float FinishTime;
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
    private double _freeFallInputWindowStarted;

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

    // Lecture locale par image ; seuls la cause et le délai borné de l'appui sont transmis, jamais une hauteur client.
    private void CaptureTimedFreeFallAction(ref PigeonInput input)
    {
        if (!IsFreeFalling(currentState) || input.FreeFallAction != FreeFallChallenge.LossReason.None)
            return;
        var sampled = new PigeonInput
        {
            Movement = _controls != null ? _controls.Movement : Vector2.zero,
            RelativeMovement = _controls != null ? _controls.RelativeMovement : Vector2.zero,
            Flap = _controls != null && _controls.Flap,
            Brake = _controls != null && _controls.Brake,
            Dive = _controls != null && _controls.Dive,
            BarrelLeft = input.BarrelLeft,
            BarrelRight = input.BarrelRight,
            Respawn = input.Respawn || _respawnRequested
        };
        input.FreeFallAction = FreeFallChallenge.GetInputLoss(sampled, currentState.FreeFall.MovementDeadZone);
        if (input.FreeFallAction != FreeFallChallenge.LossReason.None)
            input.FreeFallActionDelay = (float)System.Math.Max(0d, Time.timeAsDouble - _freeFallInputWindowStarted);
    }

    // Simuler la portion de chute précédant l'appui avant de figer sa position et de rétablir le vol habituel.
    private float CaptureFreeFallInput(in PigeonInput input, ref PigeonState state, float dt)
    {
        ref FreeFallProgress progress = ref state.FreeFall;
        if (!IsFreeFalling(state) || state.Frozen || state.MovementFrozen)
            return 0f;

        FreeFallChallenge.LossReason reason = state.Grounded
            ? FreeFallChallenge.LossReason.Ground
            : input.FreeFallAction != FreeFallChallenge.LossReason.None ? input.FreeFallAction
            : FreeFallChallenge.GetInputLoss(input, progress.MovementDeadZone);
        if (reason == FreeFallChallenge.LossReason.None)
            return 0f;

        float fallTime = reason == FreeFallChallenge.LossReason.Ground ? 0f : FreeFallActionTime(input, dt);
        if (fallTime > 0f)
            SimulateFreeFall(ref state, fallTime);
        if (progress.Loss == FreeFallChallenge.LossReason.None)
        {
            progress.Loss = reason;
            progress.InputPosition = state.Position;
        }
        return fallTime;
    }

    private static float FreeFallActionTime(in PigeonInput input, float dt)
        => input.FreeFallAction == FreeFallChallenge.LossReason.None
            || float.IsNaN(input.FreeFallActionDelay) || float.IsInfinity(input.FreeFallActionDelay)
            ? 0f : Mathf.Clamp(input.FreeFallActionDelay, 0f, dt);

    void CaptureFreeFallContact(ref PigeonState state)
    {
        ref FreeFallProgress progress = ref state.FreeFall;
        if (IsFreeFalling(state) && (state.Grounded || UnderWater(state.Position)))
        {
            progress.Loss = FreeFallChallenge.LossReason.Ground;
            progress.InputPosition = state.Position;
        }
    }
}
