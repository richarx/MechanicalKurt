using System;

public enum DeliverySessionState
{
    Ready,
    Running,
    Finished
}

/// <summary>Deterministic session time; the scene component supplies elapsed real seconds.</summary>
public sealed class DeliverySessionClock
{
    public DeliverySessionState State { get; private set; } = DeliverySessionState.Ready;
    public double DurationSeconds { get; private set; }
    public double RemainingSeconds { get; private set; }
    public bool IsPaused { get; private set; }

    #region Session

    public static bool IsValidDuration(double seconds)
    {
        return !double.IsNaN(seconds) && !double.IsInfinity(seconds) && seconds > 0d;
    }

    public bool Start(double durationSeconds)
    {
        if (State == DeliverySessionState.Running || !IsValidDuration(durationSeconds))
            return false;

        DurationSeconds = durationSeconds;
        RemainingSeconds = durationSeconds;
        IsPaused = false;
        State = DeliverySessionState.Running;
        return true;
    }

    public void Reset(double durationSeconds)
    {
        DurationSeconds = IsValidDuration(durationSeconds) ? durationSeconds : 0d;
        RemainingSeconds = DurationSeconds;
        IsPaused = false;
        State = DeliverySessionState.Ready;
    }

    public void SetPaused(bool paused)
    {
        if (State != DeliverySessionState.Running)
            return;

        IsPaused = paused;
    }

    #endregion

    #region Time

    // Returns true only for the advance that expires this session.
    public bool Advance(double elapsedSeconds, bool externallyPaused = false)
    {
        if (State != DeliverySessionState.Running || IsPaused || externallyPaused
            || !IsValidDuration(elapsedSeconds))
            return false;

        RemainingSeconds = Math.Max(0d, RemainingSeconds - elapsedSeconds);
        if (RemainingSeconds > 0d)
            return false;

        State = DeliverySessionState.Finished;
        IsPaused = false;
        return true;
    }

    #endregion
}
