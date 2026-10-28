#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;

// Speedway sous l'eau : dans un couloir d'eau dégagé de la vraie scène, deux passages identiques (départ arrêté
// sous la surface, joystick vers l'avant pour piquer du nez) avec puis sans l'effet. Avec l'effet, le pigeon doit
// nettement accélérer (ancienne sensation) ; sans, il reste lent (comportement à 20 ticks/s sans l'effet).
public partial class ScenarioRunner
{
    public const string WaterSpeedScenario = "Eau : speedway";

    const float LaneStartDepth = 1.2f;   // sous la surface
    const float LaneMinFloor = 3.5f;     // profondeur d'eau minimale sous la surface (le pigeon oscille jusqu'à ~2,5 m)
    const float LaneLength = 100f;
    const float RunSeconds = 4f;

    IEnumerator RunWaterSpeedway()
    {
        Water water = FindAnyObjectByType<Water>();
        yield return Check("De l'eau dans la scène", water != null);
        float surface = water.transform.position.y;

        bool found = TryFindWaterLane(water, surface, out Vector3 start, out Vector3 direction, out float length);
        yield return Check("Un couloir d'eau dégagé et profond existe", found && length >= 70f,
            found ? $"départ {start}, direction {direction}, longueur {length:0} m" : "aucun");

        FieldInfo thrustField = typeof(PigeonController).GetField("_underwaterThrust", BindingFlags.Instance | BindingFlags.NonPublic);
        yield return Check("Réglage _underwaterThrust trouvé", thrustField != null);
        float configuredThrust = (float)thrustField.GetValue(Pigeon);
        Defer(() => thrustField.SetValue(Pigeon, configuredThrust));
        yield return Check("Effet activé dans le prefab", configuredThrust > 0f, $"_underwaterThrust {configuredThrust}");

        float withEffect = 0f, withoutEffect = 0f;
        yield return SpeedwayRun(start, direction, "avec l'effet", speed => withEffect = speed);
        thrustField.SetValue(Pigeon, 0f);
        yield return SpeedwayRun(start, direction, "sans l'effet", speed => withoutEffect = speed);
        thrustField.SetValue(Pigeon, configuredThrust);

        yield return Check($"Avec l'effet, le pigeon accélère sous l'eau (≥ 20 m/s après {RunSeconds:0} s)", withEffect >= 20f,
            $"{withEffect:0.0} m/s");
        yield return Check("L'effet fait une vraie différence (≥ 7 m/s)", withEffect - withoutEffect >= 7f,
            $"avec {withEffect:0.0} m/s, sans {withoutEffect:0.0} m/s");
    }

    IEnumerator SpeedwayRun(Vector3 start, Vector3 direction, string label, System.Action<float> result)
    {
        Bot.ReleaseAll();
        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        // Nez à -30°, comme une entrée dans l'eau ; le joystick vers l'avant le fait piquer jusqu'au maximum du plané.
        yield return Teleport(Pigeon, start, Quaternion.Euler(30f, yaw, 0f), $"Départ sous l'eau ({label})");
        Bot.RelativeMovement = new Vector2(0f, 1f);

        float end = Time.realtimeSinceStartup + RunSeconds;
        int samples = 0, underwater = 0;
        bool stunned = false;
        while (Time.realtimeSinceStartup < end)
        {
            samples++;
            if (Pigeon.IsUndeWater) underwater++;
            stunned |= Pigeon.IsStun;
            yield return null;
        }
        float speed = Pigeon.HorizontalSpeed;
        Bot.ReleaseAll();
        result(speed);

        yield return Check($"Passage {label} : vitesse horizontale après {RunSeconds:0} s", true,
            $"{speed:0.0} m/s, sous l'eau {100f * underwater / Mathf.Max(1, samples):0}% du temps, distance {Vector3.Distance(start, Pigeon.transform.position):0} m");
        yield return Check($"Passage {label} : pas de choc", !stunned);
    }

    // Cherche, sur une grille couvrant l'eau, le plus long couloir horizontal sans obstacle, avec assez de fond.
    static bool TryFindWaterLane(Water water, float surface, out Vector3 start, out Vector3 direction, out float length)
    {
        start = direction = Vector3.zero;
        length = 0f;
        Renderer area = water.GetComponentInChildren<Renderer>();
        if (area == null)
            return false;

        Bounds bounds = area.bounds;
        float y = surface - LaneStartDepth;
        const int grid = 12;
        const QueryTriggerInteraction ignoreTriggers = QueryTriggerInteraction.Ignore;
        for (int gx = 0; gx < grid; gx++)
            for (int gz = 0; gz < grid; gz++)
            {
                Vector3 p = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (gx + 0.5f) / grid), y,
                    Mathf.Lerp(bounds.min.z, bounds.max.z, (gz + 0.5f) / grid));
                if (Physics.CheckSphere(p, 0.8f, Physics.AllLayers, ignoreTriggers) || !DeepEnough(p, surface))
                    continue;

                for (int k = 0; k < 8; k++)
                {
                    float angle = k * Mathf.PI / 4f;
                    Vector3 d = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    float clear = Physics.SphereCast(p, 0.8f, d, out RaycastHit hit, LaneLength, Physics.AllLayers, ignoreTriggers)
                        ? hit.distance : LaneLength;
                    // Le fond doit rester assez bas tout le long du couloir.
                    for (float along = 10f; along < clear; along += 10f)
                        if (!DeepEnough(p + d * along, surface))
                        {
                            clear = along - 10f;
                            break;
                        }
                    if (clear > length)
                    {
                        length = clear;
                        start = p;
                        direction = d;
                    }
                }
            }
        return length > 0f;
    }

    static bool DeepEnough(Vector3 point, float surface)
    {
        Vector3 from = new Vector3(point.x, surface - 0.2f, point.z);
        return !Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f, Physics.AllLayers, QueryTriggerInteraction.Ignore)
            || surface - hit.point.y >= LaneMinFloor;
    }
}
#endif
