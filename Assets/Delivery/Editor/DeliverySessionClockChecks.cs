using System;

/// <summary>Pure C# checks, also runnable from a standalone console harness.</summary>
public static class DeliverySessionClockChecks
{
    public static void RunAll()
    {
        DeliverySessionClock clock = new DeliverySessionClock();
        Require(clock.State == DeliverySessionState.Ready, "A new clock must be Ready.");
        Require(!clock.Advance(1d), "A Ready clock must not expire.");
        Require(clock.Start(15d), "A valid session must start.");
        Require(!clock.Advance(5d) && clock.RemainingSeconds == 10d, "Real elapsed seconds must be consumed.");
        Require(!clock.Start(99d) && clock.RemainingSeconds == 10d, "Duplicate start must preserve time.");

        clock.SetPaused(true);
        Require(!clock.Advance(100d) && clock.RemainingSeconds == 10d, "Explicit pause must freeze time.");
        clock.SetPaused(false);
        Require(!clock.Advance(100d, true) && clock.RemainingSeconds == 10d, "External pause must freeze time.");
        Require(!clock.Advance(0d) && !clock.Advance(-1d) && !clock.Advance(double.NaN)
            && !clock.Advance(double.PositiveInfinity) && clock.RemainingSeconds == 10d,
            "Invalid elapsed values must preserve time.");

        int expirationCount = 0;
        if (clock.Advance(20d)) expirationCount++;
        if (clock.Advance(20d)) expirationCount++;
        Require(expirationCount == 1 && clock.RemainingSeconds == 0d
            && clock.State == DeliverySessionState.Finished, "Overshoot must expire once and clamp at zero.");
        Require(clock.Start(3d) && clock.RemainingSeconds == 3d, "A finished session must start fresh.");
        clock.SetPaused(true);
        clock.Reset(15d);
        Require(clock.State == DeliverySessionState.Ready && !clock.IsPaused && clock.RemainingSeconds == 15d,
            "Reset must clear state, elapsed time and pause.");
        Require(clock.Start(15d), "Restart after reset must start.");
        Require(clock.Advance(15d) && clock.RemainingSeconds == 0d, "Exact duration must expire.");

        double[] invalidDurations = { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        foreach (double duration in invalidDurations)
        {
            clock.Reset(duration);
            Require(clock.State == DeliverySessionState.Ready && clock.RemainingSeconds == 0d,
                "Invalid reset duration must produce a safe Ready clock.");
            Require(!clock.Start(duration) && clock.State == DeliverySessionState.Ready,
                "Invalid duration must not start a session.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Delivery session check failed: " + message);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/Delivery/Run Session Clock Checks")]
    private static void RunFromMenu()
    {
        RunAll();
        UnityEngine.Debug.Log("Delivery session clock checks passed.");
    }
#endif
}
