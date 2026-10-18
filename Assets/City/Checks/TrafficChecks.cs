#if UNITY_EDITOR || TRAFFIC_CHECKS
using System;
using UnityEngine;
using Random = System.Random;

public static class TrafficChecks
{
    public static void RunAll()
    {
        CheckBridge();
        CheckLoopAndProjection();
        CheckInvalidRoutes();
        CheckAccelerationAndBraking();
        CheckTemporaryStops();
        CheckTrafficLights();
        CheckSpawnLayout();
        CheckQueue();
    }

    private static void CheckBridge()
    {
        TrafficRoute bridge = new TrafficRoute(new[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 3f, 4f),
            new Vector3(0f, 3f, 14f), new Vector3(0f, 0f, 18f),
            new Vector3(20f, 0f, 18f), new Vector3(20f, 0f, 0f)
        });
        Require(bridge.IsValid, "Bridge route must be valid.");
        Near(bridge.Length, 78f, "Route length must include slopes in 3D.");
        PositionNear(bridge.GetPosition(2.5f), new Vector3(0f, 1.5f, 2f), "Interpolate the ascent height.");
        PositionNear(bridge.GetPosition(17.5f), new Vector3(0f, 1.5f, 16f), "Interpolate the descent height.");
        Require(bridge.GetDirection(2.5f, 0.5f).y > 0f, "Car must pitch up on the ascent.");
        Require(bridge.GetDirection(17.5f, 0.5f).y < 0f, "Car must pitch down on the descent.");
        Near(bridge.GetDirection(10f, 0.5f).y, 0f, "Deck must remain horizontal.");
    }

    private static void CheckLoopAndProjection()
    {
        TrafficRoute loop = CreateSquare();
        PositionNear(loop.GetPosition(loop.Length + 5f), loop.GetPosition(5f), "Wrap beyond the last point.");
        PositionNear(loop.GetPosition(-5f), loop.GetPosition(loop.Length - 5f), "Wrap behind the first point.");
        Near(loop.WrapDistance(2f - 38f), 4f, "Leader distance must cross the loop seam.");
        Near(loop.GetClosestDistance(new Vector3(5f, 2f, -1f)), 5f, "Project onto the nearest segment, not its endpoint.");

        TrafficRoute duplicatePoint = new TrafficRoute(new[]
        {
            Vector3.zero, Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f)
        });
        Require(duplicatePoint.IsValid, "One duplicate point must not invalidate a usable loop.");
        PositionNear(duplicatePoint.GetPosition(5f), new Vector3(5f, 0f, 0f), "Skip zero-length segments.");
    }

    private static void CheckInvalidRoutes()
    {
        Require(!new TrafficRoute(new Vector3[0]).IsValid, "An empty route must be invalid.");
        Require(!new TrafficRoute(new[] { Vector3.zero }).IsValid, "A single point must be invalid.");
        Require(!new TrafficRoute(new[] { Vector3.one, Vector3.one }).IsValid, "Coincident points must be invalid.");
    }

    private static void CheckAccelerationAndBraking()
    {
        TrafficMotion.PlanStep(0f, 6f, 2f, 5f, float.PositiveInfinity, 0f, 0.5f, out float travel, out float speed);
        Near(speed, 1f, "Acceleration must be gradual.");
        Near(travel, 0.25f, "Travel must use the average speed over the step.");
        TrafficMotion.PlanStep(6f, 0f, 2f, 5f, float.PositiveInfinity, 0f, 0.2f, out _, out speed);
        Near(speed, 5f, "Braking must be gradual when space permits.");
        TrafficMotion.PlanStep(20f, 20f, 2f, 5f, 0.5f, 0f, 2f, out travel, out speed);
        Require(travel <= 0.5f && speed >= 0f, "A long frame must not consume the clearance.");
        TrafficMotion.PlanStep(6f, 6f, 2f, 5f, 0f, 0f, 1f, out travel, out speed);
        Near(travel, 0f, "A blocked car must not move.");
        Near(speed, 0f, "A blocked car must stop.");
        TrafficMotion.PlanStep(3f, 6f, 2f, 5f, 10f, 0f, 0f, out travel, out speed);
        Near(travel, 0f, "Pause must prevent movement.");
        Near(speed, 3f, "Pause must preserve speed for resuming.");
    }

    private static void CheckTemporaryStops()
    {
        TrafficRoute route = CreateSquare();
        Near(route.GetPointDistance(1), 10f, "Stops must use the waypoint's distance on the route.");
        Near(route.GetForwardDistance(38f, 0f, true), 2f, "A stop at the loop seam must remain ahead.");
        Near(route.GetForwardDistance(10f, 10f, true), 0f, "Spawning on a stop must trigger it.");
        Near(route.GetForwardDistance(10f, 10f, false), route.Length, "A served stop must wait for the next lap.");
        Near(route.GetForwardDistance(10f, 20f, false), 10f, "The next different stop must remain reachable.");
        Near(route.GetForwardDistance(10.00001f, 10f, true), 0f,
            "Rounding just past a red point must not let a waiting car escape for a full lap.");

        TrafficMotion.PlanStep(10f, 10f, 2f, 5f, float.PositiveInfinity, 0f, 2f,
            out float travel, out _, 0.5f);
        Require(travel <= 0.5f, "Even a long frame must not cross the stop point.");
        TrafficMotion.PlanStep(6f, 6f, 2f, 5f, 0f, 0f, 1f,
            out travel, out _, 10f);
        Near(travel, 0f, "A queued car must stay behind its leader before reaching the stop.");

        float remaining = 20f;
        float speed = 6f;
        bool didBrake = false;
        for (int frame = 0; frame < 600 && remaining > 0.0001f; frame++)
        {
            float previousSpeed = speed;
            TrafficMotion.PlanStep(speed, 6f, 2f, 5f, float.PositiveInfinity, 0f, 1f / 30f,
                out travel, out speed, remaining);
            Require(travel <= remaining + 0.00001f, "Approaching a stop must preserve its travel limit.");
            remaining = Mathf.Max(0f, remaining - travel);
            didBrake |= speed < previousSpeed;
        }
        Require(remaining <= 0.0001f && didBrake, "A car must brake and reach its stop without getting stuck.");

        // After the wait, the same marker is one full lap away and acceleration resumes.
        TrafficMotion.PlanStep(0f, 6f, 2f, 5f, float.PositiveInfinity, 0f, 0.5f,
            out travel, out speed, route.GetForwardDistance(10f, 10f, false));
        Require(travel > 0f && speed > 0f, "Serving a stop must allow the car to leave it.");
    }

    private static void CheckTrafficLights()
    {
        TrafficLightCycle.Evaluate(0d, 5f, true, out bool isStop, out float elapsed, out float remaining);
        Require(isStop, "A red-start light must begin in stop mode.");
        Near(elapsed, 0f, "A new phase must start at zero elapsed time.");
        Near(remaining, 5f, "A new red phase must have its full duration remaining.");

        TrafficLightCycle.Evaluate(3d, 5f, true, out isStop, out elapsed, out remaining);
        Require(isStop, "The light must remain red before the phase ends.");
        Near(elapsed, 3f, "Elapsed time must be relative to the current phase.");
        Near(remaining, 2f, "An arriving car must wait X minus elapsed, not a fresh X seconds.");

        TrafficLightCycle.Evaluate(5d, 5f, true, out isStop, out elapsed, out remaining);
        Require(!isStop, "The light must switch to pass exactly at X seconds.");
        Near(elapsed, 0f, "The elapsed counter must restart at a phase change.");
        Near(remaining, 5f, "The pass phase must last the same duration as the stop phase.");
        TrafficLightCycle.Evaluate(10d, 5f, true, out isStop, out _, out remaining);
        Require(isStop, "The cycle must return to stop after two phases.");
        Near(remaining, 5f, "The new stop phase must have its full duration.");

        TrafficLightCycle.Evaluate(36.25d, 5f, true, out isStop, out elapsed, out remaining);
        Require(!isStop, "A long elapsed interval must skip all completed phases correctly.");
        Near(elapsed, 1.25f, "Long frames must not drift the phase counter.");
        Near(remaining, 3.75f, "Remaining time must survive multiple completed cycles.");
        TrafficLightCycle.Evaluate(0d, 5f, false, out isStop, out _, out _);
        Require(!isStop, "The light must support starting with pass.");
        TrafficLightCycle.Evaluate(5d, 5f, false, out isStop, out _, out _);
        Require(isStop, "A pass-start light must switch to stop after X seconds.");
        TrafficLightCycle.Evaluate(-1d, 5f, true, out isStop, out elapsed, out _);
        Require(isStop && elapsed == 0f, "A negative elapsed interval must safely use the initial phase.");
        TrafficLightCycle.Evaluate(1d, 0f, true, out _, out _, out remaining);
        Require(remaining > 0f, "An invalid zero phase duration must not cause division by zero.");

        TrafficMotion.PlanStep(6f, 6f, 2f, 5f, float.PositiveInfinity, 0f, 0.5f,
            out float travel, out float speed, float.PositiveInfinity);
        Near(speed, 6f, "A green light must not force a car to brake.");
        Near(travel, 3f, "A green light must allow uninterrupted movement.");
        TrafficMotion.PlanStep(6f, 6f, 2f, 5f, float.PositiveInfinity, 0f, 1f,
            out travel, out _, 0f);
        Near(travel, 0f, "A car at the red point must not advance.");
    }

    private static void CheckSpawnLayout()
    {
        float[] lengths = { 4f, 4f, 4f, 4f };
        float[] gaps = { 2f, 2f, 2f, 2f };
        Require(TrafficSpawnLayout.TryCreateDistances(100f, lengths, gaps, 0f, 1f, new Random(42), out float[] regular),
            "A spacious loop must accept its cars.");
        for (int i = 0; i < regular.Length; i++)
        {
            float distance = (regular[(i + 1) % regular.Length] - regular[i] + 100f) % 100f;
            Near(distance, 25f, "Zero deviation must distribute equal cars regularly, including the seam.");
        }
        Require(!TrafficSpawnLayout.TryCreateDistances(20f, lengths, gaps, 3f, 1f, new Random(42), out _),
            "An overcrowded loop must reject the layout instead of overlapping cars.");
        Require(TrafficSpawnLayout.TryCreateDistances(28f, lengths, gaps, 0f, 1f, new Random(42), out float[] tight),
            "An exactly fitting layout must handle zero free space without dividing by zero.");
        for (int i = 0; i < tight.Length; i++)
            Near((tight[(i + 1) % tight.Length] - tight[i] + 28f) % 28f, 7f, "Preserve minimum spacing on a full loop.");

        float[] mixedLengths = { 3f, 6f, 4f, 5f };
        float[] mixedGaps = { 1f, 3f, 2f, 1f };
        bool varied = false;
        for (int seed = 0; seed < 100; seed++)
        {
            Require(TrafficSpawnLayout.TryCreateDistances(100f, mixedLengths, mixedGaps, 50f, 1f,
                new Random(seed), out float[] distances), "Mixed prefabs must fit when sufficient space exists.");
            float sum = 0f;
            for (int i = 0; i < distances.Length; i++)
            {
                int next = (i + 1) % distances.Length;
                float distance = (distances[next] - distances[i] + 100f) % 100f;
                float minimum = (mixedLengths[i] + mixedLengths[next]) * 0.5f
                    + Math.Max(mixedGaps[i], mixedGaps[next]) + 1f;
                Require(distances[i] >= 0f && distances[i] < 100f, "All spawn distances must lie on the loop.");
                Require(distance >= minimum - 0.001f, "Even extreme deviation must preserve clearance for every prefab pair.");
                varied |= Math.Abs(distance - 25f) > 1f;
                sum += distance;
            }
            Near(sum, 100f, "The spacing must consume one full loop, not leave an unsafe seam.");
        }
        Require(varied, "Nonzero deviation must produce irregular spacings.");
        Require(!TrafficSpawnLayout.TryCreateDistances(100f, new float[0], new float[0], 3f, 1f,
            new Random(42), out _), "An empty selection must fail safely.");
        Require(TrafficSpawnLayout.TryCreateDistances(20f, new[] { 4f }, new[] { 2f }, 3f, 1f,
            new Random(42), out float[] single) && single.Length == 1, "A single car must be supported.");
    }

    private static void CheckQueue()
    {
        TrafficRoute route = CreateSquare();
        float[] distances = { 39f, 12f, 25f };
        float[] speeds = { 0f, 0f, 0f };
        float[] travel = new float[3];
        float[] nextSpeeds = new float[3];
        const float clearance = 6f;
        bool hasStopped = false;
        bool hasResumed = false;

        for (int frame = 0; frame < 900; frame++)
        {
            float deltaTime = frame == 300 ? 1.5f : 1f / 30f;
            for (int car = 0; car < 3; car++)
            {
                // Car order remains fixed on this one-way circuit.
                int leader = (car + 1) % 3;
                float gap = route.WrapDistance(distances[leader] - distances[car]) - clearance;
                Require(gap >= -0.001f, "Cars must preserve clearance, including at the loop seam.");
                float maxSpeed = car == 1 ? (frame < 150 || frame >= 650 ? 3f : 0f) : 8f;
                TrafficMotion.PlanStep(speeds[car], maxSpeed, 2f, 5f, gap, speeds[leader], deltaTime,
                    out travel[car], out nextSpeeds[car]);
            }
            for (int car = 0; car < 3; car++)
            {
                distances[car] = route.WrapDistance(distances[car] + travel[car]);
                speeds[car] = nextSpeeds[car];
            }

            if (frame == 640)
                hasStopped = speeds[0] < 0.01f && speeds[1] < 0.01f && speeds[2] < 0.01f;
            if (frame == 899)
                hasResumed = speeds[0] > 0.5f && speeds[1] > 0.5f && speeds[2] > 0.5f;
        }

        Require(hasStopped, "A queue must stop behind a stationary car.");
        Require(hasResumed, "A queue must resume when its leader starts moving.");
    }

    private static TrafficRoute CreateSquare()
    {
        return new TrafficRoute(new[]
        {
            Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f)
        });
    }

    private static void PositionNear(Vector3 actual, Vector3 expected, string message)
    {
        Require((actual - expected).sqrMagnitude < 0.000001f, message);
    }

    private static void Near(float actual, float expected, string message)
    {
        Require(Math.Abs(actual - expected) < 0.001f, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Traffic check failed: " + message);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/City/Run Traffic Checks")]
    private static void RunFromMenu()
    {
        RunAll();
        Debug.Log("Traffic checks passed: slopes, loop, projection, acceleration, braking, temporary stops, traffic lights, spawn layout and queue.");
    }
#endif
}
#endif
